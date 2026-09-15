using Volo.Abp.Domain.Repositories;

namespace CommunityAbp.ProgressiveDelivery.Assignments;

public interface IFeatureAssignmentRepository : IRepository<FeatureAssignment, Guid>
{
    /// <summary>Tenant scope comes from the ambient <c>ICurrentTenant</c> (data filter).</summary>
    Task<FeatureAssignment?> FindAsync(Guid featureTrackId, string subjectType, string subjectId, CancellationToken cancellationToken = default);

    Task<List<FeatureAssignment>> GetListAsync(
        Guid? featureTrackId = null,
        string? subjectType = null,
        string? subjectId = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default);

    Task<long> GetCountAsync(
        Guid? featureTrackId = null,
        string? subjectType = null,
        string? subjectId = null,
        CancellationToken cancellationToken = default);
}
