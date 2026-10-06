using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Subjects.Lookup;
using Volo.Abp.DependencyInjection;
using Volo.Abp.OpenIddict.Applications;

namespace CommunityAbp.ProgressiveDelivery.OpenIddict;

/// <summary>
/// Finds <see cref="FeatureSubjectTypes.Client"/> subjects among the OpenIddict applications by client id or display
/// name. The subject id is the OAuth <c>client_id</c>, as used by <see cref="FeatureSubject.Client"/>.
/// </summary>
/// <remarks>
/// OpenIddict applications are host-wide, so results don't depend on the current tenant. The repository only filters
/// on client id, so display names are matched in memory over at most <see cref="MaxScannedApplications"/> applications.
/// </remarks>
[ExposeServices(typeof(IFeatureSubjectLookupProvider))]
public class OpenIddictClientSubjectLookupProvider : IFeatureSubjectLookupProvider, ITransientDependency
{
    private readonly IOpenIddictApplicationRepository _applicationRepository;

    public OpenIddictClientSubjectLookupProvider(IOpenIddictApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public string SubjectType => FeatureSubjectTypes.Client;

    /// <summary>Upper bound on applications read per search.</summary>
    protected virtual int MaxScannedApplications => 1000;

    public virtual async Task<IReadOnlyList<FeatureSubjectLookupItem>> SearchAsync(string? filter, int maxResultCount, CancellationToken cancellationToken = default)
    {
        var applications = await _applicationRepository.GetListAsync(
            nameof(OpenIddictApplication.ClientId), 0, MaxScannedApplications, filter: null, cancellationToken);

        filter = filter?.Trim();
        return applications
            .Where(a => !string.IsNullOrWhiteSpace(a.ClientId))
            .Where(a => string.IsNullOrEmpty(filter)
                || a.ClientId!.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || a.DisplayName?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true)
            .Take(maxResultCount)
            .Select(ToItem)
            .ToList();
    }

    public virtual async Task<FeatureSubjectLookupItem?> FindAsync(string subjectId, CancellationToken cancellationToken = default)
        => await _applicationRepository.FindByClientIdAsync(subjectId, cancellationToken) is { ClientId: not null } application
            ? ToItem(application)
            : null;

    protected virtual FeatureSubjectLookupItem ToItem(OpenIddictApplication application)
    {
        var hasDisplayName = !string.IsNullOrWhiteSpace(application.DisplayName);
        var kind = string.Join(" · ", new[] { application.ClientType, application.ApplicationType }.Where(s => !string.IsNullOrWhiteSpace(s)));

        return new FeatureSubjectLookupItem(
            application.ClientId!,
            hasDisplayName ? application.DisplayName! : application.ClientId!,
            kind.Length == 0 ? null : kind);
    }
}
