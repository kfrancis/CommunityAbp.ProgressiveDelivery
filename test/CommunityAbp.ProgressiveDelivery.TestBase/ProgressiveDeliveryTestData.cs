namespace CommunityAbp.ProgressiveDelivery;

/// <summary>Deterministic identifiers shared by all test projects.</summary>
public static class ProgressiveDeliveryTestData
{
    public static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OtherUserId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid TenantA = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid TenantB = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    /// <summary>Levels 0..3, official 1. Level 1 = None, level 2 = SafeRead, level 3 = SafeRead.</summary>
    public const string ClaimsTrack = "Test.Claims.Loading";

    /// <summary>Levels 0..2, official 0. Level 1 = Idempotent, level 2 = DemoteOnly.</summary>
    public const string SearchTrack = "Test.PatientSearch";

    /// <summary>Levels 0..1, official 0, disabled.</summary>
    public const string DisabledTrack = "Test.Disabled";
}
