using CommunityAbp.ProgressiveDelivery.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace CommunityAbp.ProgressiveDelivery.Permissions;

public class ProgressiveDeliveryPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(ProgressiveDeliveryPermissions.GroupName, L("Permission:ProgressiveDelivery"));

        var tracks = group.AddPermission(ProgressiveDeliveryPermissions.Tracks.Default, L("Permission:Tracks"));
        tracks.AddChild(ProgressiveDeliveryPermissions.Tracks.Manage, L("Permission:Tracks.Manage"));

        var levels = group.AddPermission(ProgressiveDeliveryPermissions.Levels.Default, L("Permission:Levels"));
        levels.AddChild(ProgressiveDeliveryPermissions.Levels.Manage, L("Permission:Levels.Manage"));

        var assignments = group.AddPermission(ProgressiveDeliveryPermissions.Assignments.Default, L("Permission:Assignments"));
        assignments.AddChild(ProgressiveDeliveryPermissions.Assignments.View, L("Permission:Assignments.View"));
        assignments.AddChild(ProgressiveDeliveryPermissions.Assignments.Override, L("Permission:Assignments.Override"));

        var rollouts = group.AddPermission(ProgressiveDeliveryPermissions.Rollouts.Default, L("Permission:Rollouts"));
        rollouts.AddChild(ProgressiveDeliveryPermissions.Rollouts.Manage, L("Permission:Rollouts.Manage"));

        group.AddPermission(ProgressiveDeliveryPermissions.Telemetry, L("Permission:Telemetry"));
    }

    private static LocalizableString L(string name) => LocalizableString.Create<ProgressiveDeliveryResource>(name);
}
