using Volo.Abp.Domain.Repositories;

namespace CommunityAbp.ProgressiveDelivery.Transitions;

public interface IFeatureTransitionRepository : IRepository<FeatureTransition, Guid>
{
    Task<List<FeatureTransition>> GetListAsync(
        Guid? featureTrackId = null,
        string? subjectType = null,
        string? subjectId = null,
        FeatureTransitionType? transitionType = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default);

    Task<long> GetCountAsync(
        Guid? featureTrackId = null,
        string? subjectType = null,
        string? subjectId = null,
        FeatureTransitionType? transitionType = null,
        CancellationToken cancellationToken = default);
}
