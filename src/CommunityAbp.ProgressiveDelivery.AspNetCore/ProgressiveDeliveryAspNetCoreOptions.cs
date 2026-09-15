namespace CommunityAbp.ProgressiveDelivery;

public class ProgressiveDeliveryAspNetCoreOptions
{
    /// <summary>
    /// Request header carrying the caller's maximum supported level(s). Format:
    /// <c>*=3</c> (all tracks) or <c>Cab.Claims.Loading=2;Cab.Web=4</c>. Entries combine with the minimum winning.
    /// </summary>
    public string CapabilityHeaderName { get; set; } = ProgressiveDeliveryHttpHeaders.Capabilities;

    /// <summary>Set <c>false</c> to ignore the header entirely.</summary>
    public bool HonourCapabilityHeader { get; set; } = true;
}

public static class ProgressiveDeliveryHttpHeaders
{
    public const string Capabilities = "X-ProgressiveDelivery-Capabilities";
}
