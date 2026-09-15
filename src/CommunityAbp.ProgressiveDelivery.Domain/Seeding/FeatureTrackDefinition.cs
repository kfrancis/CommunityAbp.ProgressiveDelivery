using System.Collections.ObjectModel;

namespace CommunityAbp.ProgressiveDelivery.Seeding;

/// <summary>
/// Code-first description of a track, seeded into the store on <c>IDataSeeder.SeedAsync</c>.
/// </summary>
public sealed class FeatureTrackDefinition
{
    public FeatureTrackDefinition(string name, string? displayName = null, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        DisplayName = displayName;
        Description = description;
    }

    public string Name { get; }

    public string? DisplayName { get; set; }

    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Applied only when the track is first created.</summary>
    public int InitialOfficialLevel { get; set; }

    public List<FeatureLevelDefinition> Levels { get; } = [];

    public FeatureTrackDefinition WithLevel(
        int level,
        string? description = null,
        string? supportDescription = null,
        FallbackPolicy fallbackPolicy = FallbackPolicy.None,
        bool isPerformanceSensitive = false)
    {
        Levels.Add(new FeatureLevelDefinition(level, description, supportDescription, fallbackPolicy, isPerformanceSensitive));
        return this;
    }

    public FeatureTrackDefinition WithInitialOfficialLevel(int level)
    {
        InitialOfficialLevel = level;
        return this;
    }
}

public sealed record FeatureLevelDefinition(
    int Level,
    string? Description,
    string? SupportDescription,
    FallbackPolicy FallbackPolicy,
    bool IsPerformanceSensitive);

public sealed class FeatureTrackDefinitionCollection : KeyedCollection<string, FeatureTrackDefinition>
{
    public FeatureTrackDefinitionCollection()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    protected override string GetKeyForItem(FeatureTrackDefinition item) => item.Name;

    public FeatureTrackDefinition Add(string name, string? displayName = null, string? description = null)
    {
        var definition = new FeatureTrackDefinition(name, displayName, description);
        Add(definition);
        return definition;
    }
}
