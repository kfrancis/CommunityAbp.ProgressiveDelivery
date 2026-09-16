using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;
using Volo.Abp.Tracing;
using Volo.Abp.Uow;

namespace CommunityAbp.ProgressiveDelivery.Execution;

/// <summary>
/// Runs side-effect writes (automatic demotions, cohort assignments) in their own unit of work without
/// competing with the caller's transaction. When an ambient unit of work exists the write is deferred until
/// that unit of work is disposed, because writing from a second connection while the first still holds its
/// transaction blocks on SQLite and contends for locks elsewhere. Without an ambient unit of work the write
/// runs immediately. Principal, correlation id and trace context are carried over so history stays attributable.
/// </summary>
public interface IProgressiveDeliveryWriteScheduler
{
    Task ScheduleAsync(string description, Func<IServiceProvider, CancellationToken, Task> work, CancellationToken cancellationToken = default);
}

public class ProgressiveDeliveryWriteScheduler : IProgressiveDeliveryWriteScheduler, ITransientDependency
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly ILogger<ProgressiveDeliveryWriteScheduler> _logger;

    public ProgressiveDeliveryWriteScheduler(
        IUnitOfWorkManager unitOfWorkManager,
        IServiceScopeFactory scopeFactory,
        ICurrentPrincipalAccessor principalAccessor,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<ProgressiveDeliveryWriteScheduler> logger)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _scopeFactory = scopeFactory;
        _principalAccessor = principalAccessor;
        _correlationIdProvider = correlationIdProvider;
        _logger = logger;
    }

    public virtual Task ScheduleAsync(string description, Func<IServiceProvider, CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        var principal = _principalAccessor.Principal;
        var correlationId = _correlationIdProvider.Get();
        var parent = Activity.Current?.Context;

        var ambient = _unitOfWorkManager.Current;
        if (ambient is null)
        {
            return RunAsync(description, work, principal, correlationId, parent, cancellationToken);
        }

        while (ambient.Outer is not null)
        {
            ambient = ambient.Outer;
        }

        // Deferred work must not observe the request's cancellation: by the time it runs the request is over.
        ambient.Disposed += (_, _) =>
        {
            _ = Task.Run(() => RunAsync(description, work, principal, correlationId, parent, CancellationToken.None));
        };

        _logger.LogDebug("Deferred '{Description}' until the ambient unit of work completes.", description);
        return Task.CompletedTask;
    }

    protected virtual async Task RunAsync(
        string description,
        Func<IServiceProvider, CancellationToken, Task> work,
        System.Security.Claims.ClaimsPrincipal? principal,
        string? correlationId,
        ActivityContext? parent,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider;

            using var activity = parent is { } parentContext
                ? new Activity("progressive_delivery.persist").SetParentId(parentContext.TraceId, parentContext.SpanId, parentContext.TraceFlags).Start()
                : null;

            using (principal is null ? null : provider.GetRequiredService<ICurrentPrincipalAccessor>().Change(principal))
            using (correlationId is null ? null : provider.GetRequiredService<ICorrelationIdProvider>().Change(correlationId))
            {
                var unitOfWorkManager = provider.GetRequiredService<IUnitOfWorkManager>();
                using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = false }, requiresNew: true);
                await work(provider, cancellationToken);
                await uow.CompleteAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Progressive delivery write '{Description}' failed.", description);
        }
    }
}
