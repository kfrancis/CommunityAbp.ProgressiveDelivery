using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Testing;
using Volo.Abp.Uow;

namespace CommunityAbp.ProgressiveDelivery;

/// <summary>
/// Builds a fresh ABP application (and in-memory database) per test instance. TUnit creates one instance per test.
/// </summary>
public abstract class ProgressiveDeliveryTestBase<TStartupModule> : AbpIntegratedTest<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected virtual Task WithUnitOfWorkAsync(Func<Task> func) => WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(), func);

    protected virtual async Task WithUnitOfWorkAsync(AbpUnitOfWorkOptions options, Func<Task> action)
    {
        using var scope = ServiceProvider.CreateScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        using var uow = uowManager.Begin(options);
        await action();
        await uow.CompleteAsync();
    }

    protected virtual Task<TResult> WithUnitOfWorkAsync<TResult>(Func<Task<TResult>> func) => WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(), func);

    protected virtual async Task<TResult> WithUnitOfWorkAsync<TResult>(AbpUnitOfWorkOptions options, Func<Task<TResult>> func)
    {
        using var scope = ServiceProvider.CreateScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        using var uow = uowManager.Begin(options);
        var result = await func();
        await uow.CompleteAsync();
        return result;
    }

    /// <summary>Switches the current principal (user + tenant) for the duration of the returned scope.</summary>
    protected IDisposable ChangePrincipal(Guid? userId, Guid? tenantId = null)
    {
        var accessor = GetRequiredService<ICurrentPrincipalAccessor>();
        return accessor.Change(FakeCurrentPrincipalAccessor.GetPrincipal(userId, tenantId));
    }

    protected IDisposable ChangePrincipal(ClaimsPrincipal principal)
    {
        var accessor = GetRequiredService<ICurrentPrincipalAccessor>();
        return accessor.Change(principal);
    }
}
