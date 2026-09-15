using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace CommunityAbp.ProgressiveDelivery.Seeding;

/// <summary>
/// Seeds <see cref="ProgressiveDeliveryOptions.Tracks"/>. Idempotent: creates missing tracks, appends missing
/// higher levels, never lowers the official level and never deletes anything. Tracks are host-level, so this
/// runs only for the host (tenant seeding is a no-op).
/// </summary>
public class ProgressiveDeliveryDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly ProgressiveDeliveryOptions _options;
    private readonly FeatureTrackManager _trackManager;
    private readonly IFeatureTrackRepository _trackRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<ProgressiveDeliveryDataSeedContributor> _logger;

    public ProgressiveDeliveryDataSeedContributor(
        IOptions<ProgressiveDeliveryOptions> options,
        FeatureTrackManager trackManager,
        IFeatureTrackRepository trackRepository,
        ICurrentTenant currentTenant,
        ILogger<ProgressiveDeliveryDataSeedContributor> logger)
    {
        _options = options.Value;
        _trackManager = trackManager;
        _trackRepository = trackRepository;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (context.TenantId is not null || _options.Tracks.Count == 0)
        {
            return;
        }

        using (_currentTenant.Change(null))
        {
            foreach (var definition in _options.Tracks)
            {
                await SeedTrackAsync(definition);
            }
        }
    }

    protected virtual async Task SeedTrackAsync(FeatureTrackDefinition definition)
    {
        var levels = definition.Levels.OrderBy(l => l.Level).ToList();
        var track = await _trackRepository.FindByNameAsync(definition.Name, includeDetails: true);

        if (track is null)
        {
            var levelZero = levels.FirstOrDefault(l => l.Level == 0);
            track = await _trackManager.CreateAsync(definition.Name, definition.DisplayName, definition.Description, definition.IsEnabled, levelZero?.Description);
            if (levelZero is not null)
            {
                track.GetLevel(0)
                    .SetSupportDescription(levelZero.SupportDescription)
                    .SetFallbackPolicy(levelZero.FallbackPolicy)
                    .SetPerformanceSensitive(levelZero.IsPerformanceSensitive);
            }

            _logger.LogInformation("Seeded feature track {Track}.", definition.Name);
        }

        foreach (var level in levels.Where(l => l.Level > track.HighestAvailableLevel))
        {
            await _trackManager.AddLevelAsync(track, level.Level, level.Description, level.SupportDescription, level.IsPerformanceSensitive, level.FallbackPolicy);
            _logger.LogInformation("Seeded level {Level} on feature track {Track}.", level.Level, definition.Name);
        }

        if (track.OfficialLevel < definition.InitialOfficialLevel && definition.InitialOfficialLevel <= track.HighestAvailableLevel && track.OfficialLevel == 0)
        {
            await _trackManager.SetOfficialLevelAsync(track, definition.InitialOfficialLevel, "Seeded initial official level");
        }
    }
}
