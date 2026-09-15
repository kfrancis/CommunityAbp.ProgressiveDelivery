using CommunityAbp.ProgressiveDelivery.Resolution;
using Microsoft.AspNetCore.Authorization;

namespace CommunityAbp.ProgressiveDelivery.Client;

[Authorize]
public class ProgressiveDeliveryClientAppService : ProgressiveDeliveryAppServiceBase, IProgressiveDeliveryClientAppService
{
    private readonly IFeatureLevelResolver _resolver;

    public ProgressiveDeliveryClientAppService(IFeatureLevelResolver resolver)
    {
        _resolver = resolver;
    }

    public virtual async Task<List<ResolvedFeatureLevelDto>> ResolveAsync(ResolveFeatureLevelsInput input)
    {
        var result = new List<ResolvedFeatureLevelDto>(input.Tracks.Count);

        foreach (var trackName in input.Tracks.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var options = ResolveCapability(input.Capabilities, trackName) is { } max
                ? new FeatureLevelResolutionOptions { MaxLevel = max, MaxLevelSource = "client-capability" }
                : null;

            var resolution = await _resolver.ResolveAsync(trackName, options);
            result.Add(new ResolvedFeatureLevelDto
            {
                Track = resolution.TrackName,
                TrackExists = resolution.TrackExists,
                OfficialLevel = resolution.OfficialLevel,
                HighestAvailableLevel = resolution.HighestAvailableLevel,
                EffectiveLevel = resolution.EffectiveLevel,
                Experimental = resolution.IsExperimental
            });
        }

        return result;
    }

    protected static int? ResolveCapability(Dictionary<string, int>? capabilities, string trackName)
    {
        if (capabilities is null || capabilities.Count == 0)
        {
            return null;
        }

        int? result = null;
        foreach (var (key, value) in capabilities)
        {
            if (key == "*" || string.Equals(key, trackName, StringComparison.OrdinalIgnoreCase))
            {
                result = result is null ? value : Math.Min(result.Value, value);
            }
        }

        return result;
    }
}
