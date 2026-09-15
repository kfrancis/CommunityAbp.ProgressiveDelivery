namespace CommunityAbp.ProgressiveDelivery.Permissions;

public static class ProgressiveDeliveryPermissions
{
    public const string GroupName = "ProgressiveDelivery";

    public static class Tracks
    {
        public const string Default = GroupName + ".Tracks";
        public const string Manage = Default + ".Manage";
    }

    public static class Levels
    {
        public const string Default = GroupName + ".Levels";
        public const string Manage = Default + ".Manage";
    }

    public static class Assignments
    {
        public const string Default = GroupName + ".Assignments";
        public const string View = Default + ".View";
        public const string Override = Default + ".Override";
    }

    public static class Rollouts
    {
        public const string Default = GroupName + ".Rollouts";
        public const string Manage = Default + ".Manage";
    }

    /// <summary>Read access to transition history.</summary>
    public const string Telemetry = GroupName + ".Telemetry";
}
