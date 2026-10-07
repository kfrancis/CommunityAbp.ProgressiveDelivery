using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Permissions;
using CommunityAbp.ProgressiveDelivery.Subjects.Lookup;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;

namespace CommunityAbp.ProgressiveDelivery.Subjects;

/// <remarks>Open to anyone who can view or override assignments: the override modal needs the picker.</remarks>
[Authorize]
public class FeatureSubjectLookupAppService : ProgressiveDeliveryAppServiceBase, IFeatureSubjectLookupAppService
{
    private static readonly string[] BuiltInTypes = [FeatureSubjectTypes.User, FeatureSubjectTypes.Tenant, FeatureSubjectTypes.Client, FeatureSubjectTypes.Anonymous];

    private readonly FeatureSubjectLookupManager _lookupManager;

    public FeatureSubjectLookupAppService(FeatureSubjectLookupManager lookupManager)
    {
        _lookupManager = lookupManager;
    }

    public virtual async Task<ListResultDto<FeatureSubjectTypeDto>> GetTypesAsync()
    {
        await CheckLookupPermissionAsync();

        var searchable = _lookupManager.GetSearchableSubjectTypes();
        var types = BuiltInTypes.Concat(searchable).Distinct(StringComparer.Ordinal)
            .Select(t => new FeatureSubjectTypeDto { Name = t, IsSearchable = searchable.Contains(t) })
            .ToList();

        return new ListResultDto<FeatureSubjectTypeDto>(types);
    }

    public virtual async Task<ListResultDto<FeatureSubjectLookupDto>> SearchAsync(SearchFeatureSubjectsInput input)
    {
        await CheckLookupPermissionAsync();

        var subjectType = FeatureSubject.NormalizeType(input.SubjectType);

        // Tenants are looked up from the caller's own context: the tenant provider confines tenant callers itself.
        var tenantId = subjectType == FeatureSubjectTypes.Tenant ? CurrentTenant.Id : ResolveTenantId(input.TenantId);
        using (CurrentTenant.Change(tenantId))
        {
            var results = await _lookupManager.SearchAsync(subjectType, input.Filter, input.MaxResultCount);
            return new ListResultDto<FeatureSubjectLookupDto>(results.Select(r => new FeatureSubjectLookupDto
            {
                SubjectType = subjectType,
                SubjectId = r.SubjectId,
                DisplayName = r.DisplayName,
                Detail = r.Detail,
                HasAssignments = r.HasAssignments
            }).ToList());
        }
    }

    public virtual async Task<FeatureSubjectLookupDto?> FindAsync(SubjectRefDto input)
    {
        await CheckLookupPermissionAsync();

        var subject = ToSubject(input);
        var found = await _lookupManager.FindManyAsync([subject]);

        return found.TryGetValue(subject, out var item)
            ? new FeatureSubjectLookupDto { SubjectType = subject.Type, SubjectId = subject.Id, DisplayName = item.DisplayName, Detail = item.Detail }
            : null;
    }

    protected virtual async Task CheckLookupPermissionAsync()
    {
        if (!await AuthorizationService.IsGrantedAnyAsync(ProgressiveDeliveryPermissions.Assignments.View, ProgressiveDeliveryPermissions.Assignments.Override))
        {
            throw new AbpAuthorizationException(code: AbpAuthorizationErrorCodes.GivenPolicyHasNotGranted);
        }
    }

    /// <summary>A tenant caller is always confined to its own tenant; the host may pick one.</summary>
    protected virtual Guid? ResolveTenantId(Guid? requested) => CurrentTenant.Id ?? requested;
}
