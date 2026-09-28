using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace CommunityAbp.ProgressiveDelivery.Seeding;

/// <summary>
/// Upserts <see cref="ProgressiveDeliveryOptions.Tracks"/> on every <c>IDataSeeder.SeedAsync()</c>. A track missing
/// from the store is created; an existing track with the same name is adopted (<see cref="FeatureTrack.IsDefinedInCode"/>
/// set); the definition (display name, description, level fields) then follows code and missing higher levels are
/// appended, while operational state (official level, enabled, rollouts, assignments) is set only at creation and
/// afterwards belongs to admins. Tracks previously defined in code but no longer present are retired
/// (<see cref="FeatureTrack.IsDefinedInCode"/> cleared), never deleted. Tracks are host-level, so this runs only
/// for the host (tenant seeding is a no-op).
/// </summary>
public class ProgressiveDeliveryDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private const string DefaultLevelZeroDescription = "Original implementation"; // same default as FeatureTrackManager.CreateAsync

    private readonly ProgressiveDeliveryOptions _options;
    private readonly FeatureTrackManager _trackManager;
    private readonly IFeatureTrackRepository _trackRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<ProgressiveDeliveryDataSeedContributor> _logger;

    public ProgressiveDeliveryDataSeedContributor(
        IOptions<ProgressiveDeliveryOptions> options,
        FeatureTrackManager trackManager,
        IFeatureTrackRepository trackRepository,
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator,
        ILogger<ProgressiveDeliveryDataSeedContributor> logger)
    {
        _options = options.Value;
        _trackManager = trackManager;
        _trackRepository = trackRepository;
        _currentTenant = currentTenant;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (context.TenantId is not null || _options.Tracks.Count == 0)
        {
            return; // a process that configures no tracks must not retire every track
        }

        // Validate everything before writing anything: invalid definitions are programmer errors.
        foreach (var definition in _options.Tracks)
        {
            ValidateDefinition(definition);
        }

        using (_currentTenant.Change(null))
        {
            foreach (var definition in _options.Tracks)
            {
                await SeedTrackAsync(definition);
            }

            await RetireRemovedTracksAsync();
        }
    }

    protected virtual void ValidateDefinition(FeatureTrackDefinition definition)
    {
        var levels = definition.Levels.OrderBy(l => l.Level).ToList();

        if (levels.Any(l => l.Level < 0))
        {
            throw new InvalidOperationException($"Feature track '{definition.Name}' has a level with a negative number.");
        }

        if (levels.Select(l => l.Level).Distinct().Count() != levels.Count)
        {
            throw new InvalidOperationException($"Feature track '{definition.Name}' has duplicate level numbers.");
        }

        for (var i = 0; i < levels.Count; i++)
        {
            if (levels[i].Level != i)
            {
                throw new InvalidOperationException($"Feature track '{definition.Name}' must define levels 0..{levels.Count - 1} contiguously.");
            }
        }

        var maxLevel = levels.Count == 0 ? 0 : levels[^1].Level;
        if (definition.InitialOfficialLevel < 0 || definition.InitialOfficialLevel > maxLevel)
        {
            throw new InvalidOperationException($"Feature track '{definition.Name}' has an InitialOfficialLevel outside the range 0..{maxLevel}.");
        }
    }

    protected virtual IReadOnlyList<FeatureLevelDefinition> GetDesiredLevels(FeatureTrackDefinition definition)
    {
        var levels = definition.Levels.OrderBy(l => l.Level).ToList();

        if (levels.Count == 0)
        {
            return [new FeatureLevelDefinition(0, null, null, FallbackPolicy.None, false)];
        }

        var levelZeroIndex = levels.FindIndex(l => l.Level == 0);
        if (levelZeroIndex >= 0 && levels[levelZeroIndex].Description is null)
        {
            levels[levelZeroIndex] = levels[levelZeroIndex] with { Description = DefaultLevelZeroDescription };
        }

        return levels;
    }

    protected virtual async Task SeedTrackAsync(FeatureTrackDefinition definition)
    {
        var desiredLevels = GetDesiredLevels(definition);
        var track = await _trackRepository.FindByNameAsync(definition.Name, includeDetails: true);
        var created = false;

        if (track is null)
        {
            track = await _trackManager.CreateAsync(
                definition.Name, definition.DisplayName, definition.Description, definition.IsEnabled, desiredLevels[0].Description);
            created = true;
            _logger.LogInformation("Seeded feature track {Track}.", definition.Name);
        }

        var changed = false;

        if (!track.IsDefinedInCode)
        {
            track.SetDefinedInCode(true);
            changed = true;
            if (!created)
            {
                _logger.LogInformation("Feature track {Track} already existed and is now defined in code; its definition will follow code.", track.Name);
            }
        }

        changed |= ApplyDefinition(track, definition, desiredLevels);

        if (changed)
        {
            await _trackManager.UpdateAsync(track); // persists once, invalidates the definition cache, publishes FeatureTrackChangedEto
        }

        if (created && definition.InitialOfficialLevel > 0)
        {
            await _trackManager.SetOfficialLevelAsync(track, definition.InitialOfficialLevel, "Seeded initial official level");
        }
    }

    protected virtual bool ApplyDefinition(FeatureTrack track, FeatureTrackDefinition definition, IReadOnlyList<FeatureLevelDefinition> desiredLevels)
    {
        var changed = false;

        if (!string.Equals(track.DisplayName, definition.DisplayName, StringComparison.Ordinal))
        {
            _logger.LogInformation("Track {Track} {Field} {Old} -> {New} (from code)", track.Name, "DisplayName", track.DisplayName, definition.DisplayName);
            track.SetDisplayName(definition.DisplayName);
            changed = true;
        }

        if (!string.Equals(track.Description, definition.Description, StringComparison.Ordinal))
        {
            _logger.LogInformation("Track {Track} {Field} {Old} -> {New} (from code)", track.Name, "Description", track.Description, definition.Description);
            track.SetDescription(definition.Description);
            changed = true;
        }

        foreach (var level in desiredLevels)
        {
            var existing = track.FindLevel(level.Level);

            if (existing is null)
            {
                if (level.Level > track.HighestAvailableLevel)
                {
                    track.AddLevel(_guidGenerator.Create(), level.Level, level.Description, level.SupportDescription, level.IsPerformanceSensitive, level.FallbackPolicy);
                    changed = true;
                    _logger.LogInformation("Seeded level {Level} on feature track {Track}.", level.Level, track.Name);
                }

                continue;
            }

            if (!string.Equals(existing.Description, level.Description, StringComparison.Ordinal))
            {
                _logger.LogInformation("Track {Track} level {Level} {Field} {Old} -> {New} (from code)", track.Name, level.Level, "Description", existing.Description, level.Description);
                existing.SetDescription(level.Description);
                changed = true;
            }

            if (!string.Equals(existing.SupportDescription, level.SupportDescription, StringComparison.Ordinal))
            {
                _logger.LogInformation("Track {Track} level {Level} {Field} {Old} -> {New} (from code)", track.Name, level.Level, "SupportDescription", existing.SupportDescription, level.SupportDescription);
                existing.SetSupportDescription(level.SupportDescription);
                changed = true;
            }

            if (existing.FallbackPolicy != level.FallbackPolicy)
            {
                _logger.LogInformation("Track {Track} level {Level} {Field} {Old} -> {New} (from code)", track.Name, level.Level, "FallbackPolicy", existing.FallbackPolicy, level.FallbackPolicy);
                existing.SetFallbackPolicy(level.FallbackPolicy);
                changed = true;
            }

            if (existing.IsPerformanceSensitive != level.IsPerformanceSensitive)
            {
                _logger.LogInformation("Track {Track} level {Level} {Field} {Old} -> {New} (from code)", track.Name, level.Level, "IsPerformanceSensitive", existing.IsPerformanceSensitive, level.IsPerformanceSensitive);
                existing.SetPerformanceSensitive(level.IsPerformanceSensitive);
                changed = true;
            }
        }

        var desiredLevelNumbers = desiredLevels.Select(l => l.Level).ToHashSet();
        foreach (var level in track.Levels.Where(l => !desiredLevelNumbers.Contains(l.Level)))
        {
            _logger.LogWarning("Track {Track} level {Level} exists in the database but is not defined in code; it is kept because levels are append-only.", track.Name, level.Level);
        }

        return changed;
    }

    protected virtual async Task RetireRemovedTracksAsync()
    {
        var codeNames = new HashSet<string>(_options.Tracks.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
        var marked = await _trackRepository.GetListAsync(t => t.IsDefinedInCode, includeDetails: true);

        foreach (var track in marked.Where(t => !codeNames.Contains(t.Name)))
        {
            track.SetDefinedInCode(false);
            await _trackManager.UpdateAsync(track);
            _logger.LogWarning("Track {Track} is no longer defined in code; it can be deleted from the admin UI.", track.Name);
        }
    }
}
