using System.Diagnostics.CodeAnalysis;

namespace CommunityAbp.ProgressiveDelivery.Subjects;

/// <summary>
/// The thing a sticky level assignment is attached to: a user, a tenant, a client application,
/// an anonymous session or anything custom. <see cref="TenantId"/> scopes the subject so that
/// assignments and caches never leak between tenants.
/// </summary>
public sealed record FeatureSubject
{
    public FeatureSubject(string type, string id, Guid? tenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Type = NormalizeType(type);
        Id = NormalizeId(id);
        TenantId = tenantId;
    }

    /// <summary>One of <see cref="FeatureSubjectTypes"/> or a custom value. Always in canonical form (see <see cref="NormalizeType"/>).</summary>
    public string Type { get; }

    /// <summary>Stable identifier within <see cref="Type"/>. Always in canonical form (see <see cref="NormalizeId"/>).</summary>
    public string Id { get; }

    /// <summary>Tenant the subject belongs to; <c>null</c> for host-level subjects.</summary>
    public Guid? TenantId { get; }

    public static FeatureSubject User(Guid userId, Guid? tenantId = null) => new(FeatureSubjectTypes.User, userId.ToString("D"), tenantId);

    public static FeatureSubject Tenant(Guid tenantId) => new(FeatureSubjectTypes.Tenant, tenantId.ToString("D"), tenantId);

    public static FeatureSubject Client(string clientId, Guid? tenantId = null) => new(FeatureSubjectTypes.Client, clientId, tenantId);

    public static FeatureSubject Anonymous(string sessionId, Guid? tenantId = null) => new(FeatureSubjectTypes.Anonymous, sessionId, tenantId);

    public static FeatureSubject Custom(string type, string id, Guid? tenantId = null) => new(type, id, tenantId);

    /// <summary>
    /// Canonical form of a subject type: trimmed, and well-known <see cref="FeatureSubjectTypes"/> matched
    /// case-insensitively onto their constant (<c>"user"</c> becomes <c>"User"</c>). Custom types are kept as given.
    /// </summary>
    /// <remarks>
    /// Subject types and ids are compared ordinally everywhere (database, cache keys, rollout hashing), so every
    /// value that crosses into the library must go through this and <see cref="NormalizeId"/>. Returns <c>null</c>
    /// for <c>null</c> so it can be applied to optional filters.
    /// </remarks>
    [return: NotNullIfNotNull(nameof(type))]
    public static string? NormalizeType(string? type)
    {
        if (type is null)
        {
            return null;
        }

        var trimmed = type.Trim();
        foreach (var known in WellKnownTypes)
        {
            if (string.Equals(trimmed, known, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return trimmed;
    }

    /// <summary>
    /// Canonical form of a subject id: trimmed, and GUIDs (<c>D</c>, <c>B</c> or <c>P</c> format, any casing)
    /// rewritten as lower-case <c>D</c> format, matching <see cref="User(Guid, Guid?)"/> and <see cref="Tenant(Guid)"/>.
    /// Other ids are opaque and kept as given apart from trimming.
    /// </summary>
    [return: NotNullIfNotNull(nameof(id))]
    public static string? NormalizeId(string? id)
    {
        if (id is null)
        {
            return null;
        }

        var trimmed = id.Trim();
        return Guid.TryParseExact(trimmed, "D", out var guid)
            || Guid.TryParseExact(trimmed, "B", out guid)
            || Guid.TryParseExact(trimmed, "P", out guid)
            ? guid.ToString("D")
            : trimmed;
    }

    private static readonly string[] WellKnownTypes =
    [
        FeatureSubjectTypes.User,
        FeatureSubjectTypes.Tenant,
        FeatureSubjectTypes.Client,
        FeatureSubjectTypes.Anonymous
    ];

    public override string ToString() => TenantId is null ? $"{Type}:{Id}" : $"{Type}:{Id}@{TenantId:D}";
}
