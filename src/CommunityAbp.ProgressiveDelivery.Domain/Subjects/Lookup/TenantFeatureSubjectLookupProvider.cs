using CommunityAbp.ProgressiveDelivery.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace CommunityAbp.ProgressiveDelivery.Subjects.Lookup;

/// <summary>
/// Looks tenants up through <see cref="ITenantStore"/> (Tenant Management, or the configuration-based default
/// store). A tenant caller only ever sees its own tenant.
/// </summary>
public class TenantFeatureSubjectLookupProvider : IFeatureSubjectLookupProvider, ITransientDependency
{
    private readonly ITenantStore _tenantStore;
    private readonly ICurrentTenant _currentTenant;
    private readonly IStringLocalizer<ProgressiveDeliveryResource> _localizer;

    public TenantFeatureSubjectLookupProvider(ITenantStore tenantStore, ICurrentTenant currentTenant, IStringLocalizer<ProgressiveDeliveryResource> localizer)
    {
        _tenantStore = tenantStore;
        _currentTenant = currentTenant;
        _localizer = localizer;
    }

    public string SubjectType => FeatureSubjectTypes.Tenant;

    public virtual async Task<IReadOnlyList<FeatureSubjectLookupItem>> SearchAsync(string? filter, int maxResultCount, CancellationToken cancellationToken = default)
    {
        IEnumerable<TenantConfiguration> tenants = _currentTenant.Id is { } ownTenantId
            ? await _tenantStore.FindAsync(ownTenantId) is { } own ? [own] : []
            : await _tenantStore.GetListAsync();

        filter = filter?.Trim();
        if (!string.IsNullOrEmpty(filter))
        {
            tenants = tenants.Where(t =>
                t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || t.Id.ToString("D").StartsWith(filter, StringComparison.OrdinalIgnoreCase)
                || (Guid.TryParse(filter, out var id) && t.Id == id));
        }

        return tenants
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResultCount)
            .Select(ToItem)
            .ToList();
    }

    public virtual async Task<FeatureSubjectLookupItem?> FindAsync(string subjectId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(subjectId, out var id) || (_currentTenant.Id is { } ownTenantId && ownTenantId != id))
        {
            return null;
        }

        return await _tenantStore.FindAsync(id) is { } tenant ? ToItem(tenant) : null;
    }

    protected virtual FeatureSubjectLookupItem ToItem(TenantConfiguration tenant)
        => new(tenant.Id.ToString("D"), tenant.Name, tenant.IsActive ? null : _localizer["TenantInactive"].Value);
}
