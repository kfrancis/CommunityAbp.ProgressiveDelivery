using Volo.Abp.Domain.Repositories;

namespace CommunityAbp.ProgressiveDelivery.Tracks;

public interface IFeatureTrackRepository : IRepository<FeatureTrack, Guid>
{
    Task<FeatureTrack?> FindByNameAsync(string name, bool includeDetails = true, CancellationToken cancellationToken = default);

    Task<List<FeatureTrack>> GetListAsync(
        string? filter = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        bool includeDetails = false,
        CancellationToken cancellationToken = default);

    Task<long> GetCountAsync(string? filter = null, CancellationToken cancellationToken = default);
}
