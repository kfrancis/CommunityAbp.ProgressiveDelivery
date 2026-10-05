namespace CommunityAbp.ProgressiveDelivery.Subjects.Lookup;

/// <summary>
/// Finds subjects of one <see cref="SubjectType"/> by something a human knows (name, user name, email, ...) so
/// administrators don't have to paste ids. Register implementations in DI (e.g. <c>ITransientDependency</c>);
/// several providers may serve the same subject type and their results are merged.
/// </summary>
/// <remarks>
/// Tenant scope comes from the ambient <c>ICurrentTenant</c>: the caller switches to the subject's tenant
/// before calling. Returned <see cref="FeatureSubjectLookupItem.SubjectId"/> values must be the ids the runtime
/// uses for the subject (for users: the user id).
/// </remarks>
public interface IFeatureSubjectLookupProvider
{
    /// <summary>One of <see cref="FeatureSubjectTypes"/> or a custom type.</summary>
    string SubjectType { get; }

    /// <summary>Subjects matching <paramref name="filter"/> (all subjects, up to <paramref name="maxResultCount"/>, when empty).</summary>
    Task<IReadOnlyList<FeatureSubjectLookupItem>> SearchAsync(string? filter, int maxResultCount, CancellationToken cancellationToken = default);

    /// <summary>The subject with this id, or <c>null</c> when unknown to this provider.</summary>
    Task<FeatureSubjectLookupItem?> FindAsync(string subjectId, CancellationToken cancellationToken = default);
}

/// <summary>A human-readable view of a subject.</summary>
/// <param name="SubjectId">Id as used by the runtime.</param>
/// <param name="DisplayName">Primary label, e.g. a user name or tenant name.</param>
/// <param name="Detail">Optional secondary text, e.g. full name and email.</param>
public sealed record FeatureSubjectLookupItem(string SubjectId, string DisplayName, string? Detail = null);
