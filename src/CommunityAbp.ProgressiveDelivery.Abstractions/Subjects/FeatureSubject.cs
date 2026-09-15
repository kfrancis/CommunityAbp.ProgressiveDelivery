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

        Type = type;
        Id = id;
        TenantId = tenantId;
    }

    /// <summary>One of <see cref="FeatureSubjectTypes"/> or a custom value.</summary>
    public string Type { get; }

    /// <summary>Stable identifier within <see cref="Type"/>.</summary>
    public string Id { get; }

    /// <summary>Tenant the subject belongs to; <c>null</c> for host-level subjects.</summary>
    public Guid? TenantId { get; }

    public static FeatureSubject User(Guid userId, Guid? tenantId = null) => new(FeatureSubjectTypes.User, userId.ToString("D"), tenantId);

    public static FeatureSubject Tenant(Guid tenantId) => new(FeatureSubjectTypes.Tenant, tenantId.ToString("D"), tenantId);

    public static FeatureSubject Client(string clientId, Guid? tenantId = null) => new(FeatureSubjectTypes.Client, clientId, tenantId);

    public static FeatureSubject Anonymous(string sessionId, Guid? tenantId = null) => new(FeatureSubjectTypes.Anonymous, sessionId, tenantId);

    public static FeatureSubject Custom(string type, string id, Guid? tenantId = null) => new(type, id, tenantId);

    public override string ToString() => TenantId is null ? $"{Type}:{Id}" : $"{Type}:{Id}@{TenantId:D}";
}
