namespace CommunityAbp.ProgressiveDelivery;

/// <summary>
/// Declares what the framework is allowed to do when a route at a given level throws.
/// Automatic fallback re-executes a lower route; that is only safe when the failed route
/// did not leave partial side effects behind. The safe default is <see cref="None"/>.
/// </summary>
public enum FallbackPolicy
{
    /// <summary>
    /// No automatic behaviour. The exception surfaces to the caller unchanged.
    /// Use for writes without idempotency guarantees.
    /// </summary>
    None = 0,

    /// <summary>
    /// Failure demotes the subject's sticky assignment for future requests but does not re-execute
    /// anything in the current request. The exception surfaces to the caller.
    /// Use for non-idempotent writes where you still want automatic rollback of the cohort.
    /// </summary>
    DemoteOnly = 1,

    /// <summary>
    /// The route is read-only. Failure demotes the subject and immediately re-executes the next lower route.
    /// </summary>
    SafeRead = 2,

    /// <summary>
    /// The route may write, but is idempotent or fully transactional (nothing persisted when it throws).
    /// Failure demotes the subject and immediately re-executes the next lower route.
    /// </summary>
    Idempotent = 3
}
