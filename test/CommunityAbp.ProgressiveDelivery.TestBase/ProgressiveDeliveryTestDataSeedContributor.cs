using CommunityAbp.ProgressiveDelivery.Tracks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace CommunityAbp.ProgressiveDelivery;

public class ProgressiveDeliveryTestDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly FeatureTrackManager _trackManager;
    private readonly IFeatureTrackRepository _trackRepository;

    public ProgressiveDeliveryTestDataSeedContributor(FeatureTrackManager trackManager, IFeatureTrackRepository trackRepository)
    {
        _trackManager = trackManager;
        _trackRepository = trackRepository;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (context.TenantId is not null || await _trackRepository.FindByNameAsync(ProgressiveDeliveryTestData.ClaimsTrack, includeDetails: false) is not null)
        {
            return;
        }

        var claims = await _trackManager.CreateAsync(ProgressiveDeliveryTestData.ClaimsTrack, "Claims loading", "Test track", levelZeroDescription: "Original claims loading");
        await _trackManager.AddLevelAsync(claims, 1, "Optimized SQL", "Uses optimized SQL for claims loading.", fallbackPolicy: FallbackPolicy.None);
        await _trackManager.AddLevelAsync(claims, 2, "Caching", "Adds a read-through cache in front of claims loading.", fallbackPolicy: FallbackPolicy.SafeRead);
        await _trackManager.AddLevelAsync(claims, 3, "Parallel validation", "Validates claims in parallel.", isPerformanceSensitive: true, fallbackPolicy: FallbackPolicy.SafeRead);
        await _trackManager.SetOfficialLevelAsync(claims, 1, "Seed");

        var search = await _trackManager.CreateAsync(ProgressiveDeliveryTestData.SearchTrack, "Patient search");
        await _trackManager.AddLevelAsync(search, 1, "Full-text search", "Uses full-text index.", fallbackPolicy: FallbackPolicy.Idempotent);
        await _trackManager.AddLevelAsync(search, 2, "Ranked results", "Adds ranking.", fallbackPolicy: FallbackPolicy.DemoteOnly);

        var disabled = await _trackManager.CreateAsync(ProgressiveDeliveryTestData.DisabledTrack, "Disabled", isEnabled: false);
        await _trackManager.AddLevelAsync(disabled, 1, "Experimental");
    }
}
