using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace CommunityAbp.ProgressiveDelivery.Rollouts;

/// <summary>
/// Percentage-based, sticky rollout of a candidate level. Cohort membership is deterministic
/// (see <see cref="IRolloutCohortAllocator"/>), so growing <see cref="PercentageBasisPoints"/> keeps
/// already-included subjects included.
/// </summary>
public class FeatureRollout : Entity<Guid>
{
    public Guid FeatureTrackId { get; private set; }

    public int TargetLevel { get; private set; }

    /// <summary>0..10000. 100 = 1%, 10000 = 100%.</summary>
    public int PercentageBasisPoints { get; private set; }

    public FeatureRolloutStatus Status { get; private set; }

    /// <summary>
    /// When <c>false</c> (default) a subject must also be in the cohort of every level between the
    /// official level and <see cref="TargetLevel"/>; intermediate levels are never skipped.
    /// </summary>
    public bool AllowSkippingIntermediateLevels { get; private set; }

    public DateTime CreationTime { get; private set; }

    public DateTime? LastModificationTime { get; private set; }

    protected FeatureRollout()
    {
    }

    internal FeatureRollout(Guid id, Guid featureTrackId, int targetLevel, int percentageBasisPoints, bool allowSkippingIntermediateLevels)
        : base(id)
    {
        FeatureTrackId = featureTrackId;
        TargetLevel = Check.Range(targetLevel, nameof(targetLevel), 1);
        AllowSkippingIntermediateLevels = allowSkippingIntermediateLevels;
        Status = FeatureRolloutStatus.Active;
        SetPercentage(percentageBasisPoints);
    }

    public FeatureRollout SetPercentage(int percentageBasisPoints)
    {
        if (percentageBasisPoints < 0 || percentageBasisPoints > ProgressiveDeliveryConsts.RolloutBasisPointsMax)
        {
            throw new BusinessException(ProgressiveDeliveryErrorCodes.PercentageOutOfRange);
        }

        PercentageBasisPoints = percentageBasisPoints;
        return this;
    }

    public FeatureRollout SetStatus(FeatureRolloutStatus status)
    {
        Status = status;
        return this;
    }

    public FeatureRollout SetAllowSkippingIntermediateLevels(bool allow)
    {
        AllowSkippingIntermediateLevels = allow;
        return this;
    }

    public FeatureRollout Pause() => SetStatus(FeatureRolloutStatus.Paused);

    public FeatureRollout Resume() => SetStatus(FeatureRolloutStatus.Active);

    public FeatureRollout Suspend()
    {
        PercentageBasisPoints = 0;
        return SetStatus(FeatureRolloutStatus.Suspended);
    }

    public FeatureRollout Complete() => SetStatus(FeatureRolloutStatus.Completed);
}
