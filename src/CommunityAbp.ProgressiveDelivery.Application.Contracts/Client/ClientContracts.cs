using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Services;

namespace CommunityAbp.ProgressiveDelivery.Client;

/// <summary>
/// Batch resolution request from a lightweight client (mobile, SPA, worker) for the current subject.
/// </summary>
public class ResolveFeatureLevelsInput
{
    [Required]
    [MinLength(1)]
    public List<string> Tracks { get; set; } = [];

    /// <summary>
    /// Highest level the client can run, per track. Use <c>*</c> as the key for a global maximum.
    /// Applied as constraints; the server never returns a level above what the client declares.
    /// </summary>
    public Dictionary<string, int>? Capabilities { get; set; }
}

public class ResolvedFeatureLevelDto
{
    public string Track { get; set; } = default!;

    public bool TrackExists { get; set; }

    public int OfficialLevel { get; set; }

    public int HighestAvailableLevel { get; set; }

    public int EffectiveLevel { get; set; }

    public bool Experimental { get; set; }
}

/// <summary>
/// Resolution endpoint for the calling subject. Requires authentication only; no permission, because every
/// subject is entitled to know its own effective levels.
/// </summary>
public interface IProgressiveDeliveryClientAppService : IApplicationService
{
    Task<List<ResolvedFeatureLevelDto>> ResolveAsync(ResolveFeatureLevelsInput input);
}
