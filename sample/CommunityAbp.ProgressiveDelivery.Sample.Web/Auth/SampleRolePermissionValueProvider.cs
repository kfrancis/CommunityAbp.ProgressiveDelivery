using CommunityAbp.ProgressiveDelivery.Permissions;
using Volo.Abp.Authorization.Permissions;

namespace CommunityAbp.ProgressiveDelivery.Sample.Web.Auth;

/// <summary>
/// Grants permissions from the <c>pd-role</c> claim instead of a permission store: "ops" gets everything,
/// "support" gets read-only inspection. Demonstrates how the UI hides mutating controls per permission.
/// </summary>
public class SampleRolePermissionValueProvider : PermissionValueProvider
{
    public const string ProviderName = "SampleRole";

    private static readonly HashSet<string> SupportPermissions =
    [
        ProgressiveDeliveryPermissions.Tracks.Default,
        ProgressiveDeliveryPermissions.Levels.Default,
        ProgressiveDeliveryPermissions.Rollouts.Default,
        ProgressiveDeliveryPermissions.Assignments.Default,
        ProgressiveDeliveryPermissions.Assignments.View,
        ProgressiveDeliveryPermissions.Telemetry
    ];

    public SampleRolePermissionValueProvider(IPermissionStore permissionStore)
        : base(permissionStore)
    {
    }

    public override string Name => ProviderName;

    public override Task<PermissionGrantResult> CheckAsync(PermissionValueCheckContext context)
    {
        var role = context.Principal?.FindFirst(DevAuthenticationHandler.RoleClaim)?.Value;
        return Task.FromResult(IsGranted(role, context.Permission.Name) ? PermissionGrantResult.Granted : PermissionGrantResult.Undefined);
    }

    public override Task<MultiplePermissionGrantResult> CheckAsync(PermissionValuesCheckContext context)
    {
        var role = context.Principal?.FindFirst(DevAuthenticationHandler.RoleClaim)?.Value;
        var result = new MultiplePermissionGrantResult();
        foreach (var permission in context.Permissions)
        {
            result.Result[permission.Name] = IsGranted(role, permission.Name) ? PermissionGrantResult.Granted : PermissionGrantResult.Undefined;
        }

        return Task.FromResult(result);
    }

    private static bool IsGranted(string? role, string permission)
    {
        return role switch
        {
            SamplePersonas.Ops => permission.StartsWith(ProgressiveDeliveryPermissions.GroupName, StringComparison.Ordinal),
            SamplePersonas.Support => SupportPermissions.Contains(permission),
            _ => false
        };
    }
}
