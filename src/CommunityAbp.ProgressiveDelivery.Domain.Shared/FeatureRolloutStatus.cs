namespace CommunityAbp.ProgressiveDelivery;

public enum FeatureRolloutStatus
{
    /// <summary>Cohort evaluation is active for this rollout.</summary>
    Active = 0,

    /// <summary>Cohort evaluation is paused; existing assignments remain.</summary>
    Paused = 1,

    /// <summary>The rollout finished (typically because the level became official).</summary>
    Completed = 2,

    /// <summary>The rollout was stopped by a policy or operator due to problems.</summary>
    Suspended = 3
}
