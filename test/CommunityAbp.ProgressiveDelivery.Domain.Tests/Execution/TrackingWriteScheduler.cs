using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.Security.Claims;
using Volo.Abp.Tracing;
using Volo.Abp.Uow;

namespace CommunityAbp.ProgressiveDelivery.Execution;

/// <summary>
/// <see cref="ProgressiveDeliveryWriteScheduler"/> that reports to <see cref="DeferredWriteTracker"/>. Behaviour is
/// otherwise unchanged, so tests still exercise the real deferral path.
/// </summary>
public sealed class TrackingWriteScheduler : ProgressiveDeliveryWriteScheduler
{
    private readonly DeferredWriteTracker _tracker;

    public TrackingWriteScheduler(
        DeferredWriteTracker tracker,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceScopeFactory scopeFactory,
        ICurrentPrincipalAccessor principalAccessor,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<ProgressiveDeliveryWriteScheduler> logger)
        : base(unitOfWorkManager, scopeFactory, principalAccessor, correlationIdProvider, logger)
    {
        _tracker = tracker;
    }

    public override Task ScheduleAsync(string description, Func<IServiceProvider, CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        _tracker.OnScheduled();
        return base.ScheduleAsync(description, work, cancellationToken);
    }

    protected override async Task RunAsync(
        string description,
        Func<IServiceProvider, CancellationToken, Task> work,
        ClaimsPrincipal? principal,
        string? correlationId,
        ActivityContext? parent,
        CancellationToken cancellationToken)
    {
        try
        {
            await base.RunAsync(description, work, principal, correlationId, parent, cancellationToken);
        }
        finally
        {
            _tracker.OnCompleted();
        }
    }
}
