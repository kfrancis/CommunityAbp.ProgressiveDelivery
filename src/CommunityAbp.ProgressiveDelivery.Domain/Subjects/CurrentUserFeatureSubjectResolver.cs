using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace CommunityAbp.ProgressiveDelivery.Subjects;

/// <summary>
/// Default subject resolver: the authenticated user within the current tenant. Anonymous callers have
/// no subject and always receive the official level. Replace via DI to support clients, sessions, etc.
/// </summary>
public class CurrentUserFeatureSubjectResolver : IFeatureSubjectResolver, ITransientDependency
{
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;

    public CurrentUserFeatureSubjectResolver(ICurrentUser currentUser, ICurrentTenant currentTenant)
    {
        _currentUser = currentUser;
        _currentTenant = currentTenant;
    }

    public ValueTask<FeatureSubject?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.Id is { } userId)
        {
            return ValueTask.FromResult<FeatureSubject?>(FeatureSubject.User(userId, _currentTenant.Id));
        }

        return ValueTask.FromResult<FeatureSubject?>(null);
    }
}
