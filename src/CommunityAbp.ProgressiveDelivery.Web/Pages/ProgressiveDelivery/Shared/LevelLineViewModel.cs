namespace CommunityAbp.ProgressiveDelivery.Web.Pages.ProgressiveDelivery.Shared;

/// <summary>
/// Data for the horizontal level line partial: level 0..Highest with official / experimental / effective /
/// capped states and optional rollout labels.
/// </summary>
public class LevelLineViewModel
{
    public int OfficialLevel { get; init; }

    public int HighestLevel { get; init; }

    public int? EffectiveLevel { get; init; }

    public int? AssignedLevel { get; init; }

    /// <summary>Levels above this value are rendered as capped (a constraint prevents them).</summary>
    public int? CapAt { get; init; }

    /// <summary>Level number → short label (e.g. "25.00 %").</summary>
    public IReadOnlyDictionary<int, string> RolloutLabels { get; init; } = new Dictionary<int, string>();

    /// <summary>Level number → title used as tooltip.</summary>
    public IReadOnlyDictionary<int, string?> Titles { get; init; } = new Dictionary<int, string?>();

    public bool Compact { get; init; }

    public string StateOf(int level)
    {
        if (CapAt is { } cap && level > cap)
        {
            return "capped";
        }

        if (EffectiveLevel == level && level != OfficialLevel)
        {
            return "effective";
        }

        if (level == OfficialLevel)
        {
            return "official";
        }

        return level > OfficialLevel ? "experimental" : "included";
    }
}
