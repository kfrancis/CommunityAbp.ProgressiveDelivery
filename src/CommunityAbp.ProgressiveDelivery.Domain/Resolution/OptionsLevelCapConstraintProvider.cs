using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace CommunityAbp.ProgressiveDelivery.Resolution;

/// <summary>
/// Emergency cap from <see cref="ProgressiveDeliveryOptions.LevelCaps"/>. Bind the options from configuration
/// to get a restart-free (with options reload) kill switch per track.
/// </summary>
[ExposeServices(typeof(IFeatureLevelConstraintProvider))]
public class OptionsLevelCapConstraintProvider : IFeatureLevelConstraintProvider, ITransientDependency
{
    public const string SourceName = "options-cap";

    private readonly IOptionsMonitor<ProgressiveDeliveryOptions> _options;

    public OptionsLevelCapConstraintProvider(IOptionsMonitor<ProgressiveDeliveryOptions> options)
    {
        _options = options;
    }

    public ValueTask<FeatureLevelConstraint?> GetConstraintAsync(FeatureLevelConstraintContext context, CancellationToken cancellationToken = default)
    {
        if (_options.CurrentValue.LevelCaps.TryGetValue(context.TrackName, out var cap))
        {
            return ValueTask.FromResult<FeatureLevelConstraint?>(new FeatureLevelConstraint(Math.Max(0, cap), SourceName, "Configured level cap"));
        }

        return ValueTask.FromResult<FeatureLevelConstraint?>(null);
    }
}
