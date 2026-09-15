namespace CommunityAbp.ProgressiveDelivery;

public static class ProgressiveDeliveryDbProperties
{
    public static string DbTablePrefix { get; set; } = "Pd";

    public static string? DbSchema { get; set; }

    public const string ConnectionStringName = "ProgressiveDelivery";
}
