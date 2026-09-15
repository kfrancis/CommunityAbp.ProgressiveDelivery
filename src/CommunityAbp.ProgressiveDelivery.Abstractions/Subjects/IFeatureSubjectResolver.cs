namespace CommunityAbp.ProgressiveDelivery.Subjects;

/// <summary>
/// Resolves the subject of the current execution context (current user, current client, ...).
/// Returns <c>null</c> when there is no identifiable subject; resolution then falls back to the official level.
/// </summary>
public interface IFeatureSubjectResolver
{
    ValueTask<FeatureSubject?> ResolveAsync(CancellationToken cancellationToken = default);
}
