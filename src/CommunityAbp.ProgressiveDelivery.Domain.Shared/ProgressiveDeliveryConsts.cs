namespace CommunityAbp.ProgressiveDelivery;

public static class ProgressiveDeliveryConsts
{
    public const int MaxTrackNameLength = 128;
    public const int MaxDisplayNameLength = 256;
    public const int MaxDescriptionLength = 1024;
    public const int MaxSupportDescriptionLength = 2048;
    public const int MaxSubjectTypeLength = 64;
    public const int MaxSubjectIdLength = 256;
    public const int MaxReasonLength = 512;
    public const int MaxCorrelationIdLength = 64;
    public const int MaxTraceIdLength = 64;
    public const int MaxMetadataLength = 4096;

    /// <summary>Rollout percentages are stored in basis points: 10000 = 100%.</summary>
    public const int RolloutBasisPointsMax = 10_000;
}
