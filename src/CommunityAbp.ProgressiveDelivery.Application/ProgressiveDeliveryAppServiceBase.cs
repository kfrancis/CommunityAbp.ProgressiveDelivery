using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Localization;
using CommunityAbp.ProgressiveDelivery.Subjects;
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
}
