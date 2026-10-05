using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace CommunityAbp.ProgressiveDelivery.Subjects.Lookup;

/// <summary>
/// Looks users up through ABP's <see cref="IExternalUserLookupServiceProvider"/>, which the Identity module
/// (in process) or the Identity HTTP API client (tiered) registers. Without one, user lookup is unavailable
/// and ids have to be typed in.
/// </summary>
public class UserFeatureSubjectLookupProvider : IFeatureSubjectLookupProvider, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public UserFeatureSubjectLookupProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public string SubjectType => FeatureSubjectTypes.User;

    protected IExternalUserLookupServiceProvider? UserLookup => _serviceProvider.GetService<IExternalUserLookupServiceProvider>();

    public virtual async Task<IReadOnlyList<FeatureSubjectLookupItem>> SearchAsync(string? filter, int maxResultCount, CancellationToken cancellationToken = default)
    {
        if (UserLookup is not { } lookup)
        {
            return [];
        }

        var results = new List<FeatureSubjectLookupItem>();

        // Identity's text filter doesn't match ids; a pasted id should still resolve to its user.
        if (Guid.TryParse(filter?.Trim(), out var id) && await lookup.FindByIdAsync(id, cancellationToken) is { } byId)
        {
            results.Add(ToItem(byId));
        }

        var users = await lookup.SearchAsync(
            sorting: "UserName",
            filter: string.IsNullOrWhiteSpace(filter) ? null : filter.Trim(),
            maxResultCount: maxResultCount,
            cancellationToken: cancellationToken);

        results.AddRange(users.Where(u => results.All(r => r.SubjectId != u.Id.ToString("D"))).Select(ToItem));
        return results.Take(maxResultCount).ToList();
    }

    public virtual async Task<FeatureSubjectLookupItem?> FindAsync(string subjectId, CancellationToken cancellationToken = default)
    {
        if (UserLookup is not { } lookup || !Guid.TryParse(subjectId, out var id))
        {
            return null;
        }

        return await lookup.FindByIdAsync(id, cancellationToken) is { } user ? ToItem(user) : null;
    }

    protected virtual FeatureSubjectLookupItem ToItem(IUserData user)
    {
        var fullName = string.Join(' ', new[] { user.Name, user.Surname }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var detail = string.Join(" · ", new[] { fullName, user.Email }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new FeatureSubjectLookupItem(user.Id.ToString("D"), user.UserName, detail.Length == 0 ? null : detail);
    }
}
