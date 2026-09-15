namespace CommunityAbp.ProgressiveDelivery;

public static class ProgressiveDeliveryErrorCodes
{
    private const string Prefix = "ProgressiveDelivery:";

    public const string TrackNameAlreadyExists = Prefix + "TrackNameAlreadyExists";
    public const string TrackNotFound = Prefix + "TrackNotFound";
    public const string LevelAlreadyExists = Prefix + "LevelAlreadyExists";
    public const string LevelNotContiguous = Prefix + "LevelNotContiguous";
    public const string LevelNotFound = Prefix + "LevelNotFound";
    public const string LevelOutOfRange = Prefix + "LevelOutOfRange";
    public const string OfficialLevelOutOfRange = Prefix + "OfficialLevelOutOfRange";
    public const string RolloutAlreadyExists = Prefix + "RolloutAlreadyExists";
    public const string RolloutNotFound = Prefix + "RolloutNotFound";
    public const string RolloutTargetMustBeAboveOfficial = Prefix + "RolloutTargetMustBeAboveOfficial";
    public const string PercentageOutOfRange = Prefix + "PercentageOutOfRange";
}
