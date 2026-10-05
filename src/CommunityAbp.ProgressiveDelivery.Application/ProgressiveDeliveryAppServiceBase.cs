using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Localization;
using CommunityAbp.ProgressiveDelivery.Subjects;
using CommunityAbp.ProgressiveDelivery.Subjects.Lookup;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application.Services;

namespace CommunityAbp.ProgressiveDelivery;

public abstract class ProgressiveDeliveryAppServiceBase : ApplicationService
{
    protected ProgressiveDeliveryAppServiceBase()
    {
        LocalizationResource = typeof(ProgressiveDeliveryResource);
        ObjectMapperContext = typeof(ProgressiveDeliveryApplicationModule);
    }

    /// <summary>
    /// Builds the subject for a request. A tenant caller is always confined to its own tenant; only the host
    /// may target other tenants (or host-level subjects) explicitly.
    /// </summary>
    protected virtual FeatureSubject ToSubject(SubjectRefDto input)
    {
        var tenantId = CurrentTenant.Id is { } currentTenantId
            ? currentTenantId
            : input.UseCurrentTenant ? null : input.TenantId;

        return new FeatureSubject(input.SubjectType, input.SubjectId, tenantId);
    }

    /// <summary>Fills a display name on each item whose subject a lookup provider knows.</summary>
    protected virtual async Task FillSubjectDisplayNamesAsync<T>(
        IReadOnlyCollection<T> items,
        Func<T, FeatureSubject?> getSubject,
        Action<T, string> setDisplayName)
    {
        var subjects = items.Select(getSubject).OfType<FeatureSubject>().ToList();
        if (subjects.Count == 0)
        {
            return;
        }

        var names = await LazyServiceProvider.GetRequiredService<FeatureSubjectLookupManager>().FindManyAsync(subjects);
        foreach (var item in items)
        {
            if (getSubject(item) is { } subject && names.TryGetValue(subject, out var found))
            {
                setDisplayName(item, found.DisplayName);
            }
        }
    }
}
