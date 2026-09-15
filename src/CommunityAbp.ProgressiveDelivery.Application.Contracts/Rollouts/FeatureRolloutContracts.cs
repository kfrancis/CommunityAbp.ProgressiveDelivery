using System.ComponentModel.DataAnnotations;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Volo.Abp.Application.Services;

namespace CommunityAbp.ProgressiveDelivery.Rollouts;

public class StartFeatureRolloutDto
{
    [Range(1, int.MaxValue)]
    public int TargetLevel { get; set; }

    /// <summary>0..10000 basis points (100 = 1%).</summary>
    [Range(0, ProgressiveDeliveryConsts.RolloutBasisPointsMax)]
    public int PercentageBasisPoints { get; set; }

    public bool AllowSkippingIntermediateLevels { get; set; }
}

public class UpdateFeatureRolloutDto
{
    [Range(0, ProgressiveDeliveryConsts.RolloutBasisPointsMax)]
    public int? PercentageBasisPoints { get; set; }

    public FeatureRolloutStatus? Status { get; set; }

    public bool? AllowSkippingIntermediateLevels { get; set; }
}

/// <summary>
/// Rollout management. Reading requires <c>ProgressiveDelivery.Rollouts</c>; writing requires <c>ProgressiveDelivery.Rollouts.Manage</c>.
/// </summary>
public interface IFeatureRolloutAppService : IApplicationService
{
    Task<List<FeatureRolloutDto>> GetListAsync(Guid trackId);

    Task<FeatureRolloutDto> StartAsync(Guid trackId, StartFeatureRolloutDto input);

    Task<FeatureRolloutDto> UpdateAsync(Guid trackId, int targetLevel, UpdateFeatureRolloutDto input);

    Task DeleteAsync(Guid trackId, int targetLevel);
}
