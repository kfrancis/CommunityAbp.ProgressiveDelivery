using System.Collections;

namespace CommunityAbp.ProgressiveDelivery.Execution;

/// <summary>
/// Route table for <see cref="IProgressiveDelivery.ExecuteAsync{TResult}"/>. Supports collection-initializer syntax:
/// <code>
/// new ProgressiveRoutes&lt;ClaimResult&gt;
/// {
///     [0] = LoadClaimsLegacyAsync,
///     [1] = LoadClaimsOptimizedAsync,
/// }
/// </code>
/// Routes do not need to be defined for every level; the highest route at or below the effective level runs.
/// </summary>
public sealed class ProgressiveRoutes<TResult> : IReadOnlyDictionary<int, Func<CancellationToken, Task<TResult>>>
{
    private readonly SortedDictionary<int, Func<CancellationToken, Task<TResult>>> _routes = [];

    public Func<CancellationToken, Task<TResult>> this[int level]
    {
        get => _routes[level];
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(level);
            ArgumentNullException.ThrowIfNull(value);
            _routes[level] = value;
        }
    }

    public IEnumerable<int> Keys => _routes.Keys;

    public IEnumerable<Func<CancellationToken, Task<TResult>>> Values => _routes.Values;

    public int Count => _routes.Count;

    public ProgressiveRoutes<TResult> Add(int level, Func<CancellationToken, Task<TResult>> route)
    {
        this[level] = route;
        return this;
    }

    /// <summary>Convenience for routes that ignore cancellation.</summary>
    public ProgressiveRoutes<TResult> Add(int level, Func<Task<TResult>> route)
    {
        ArgumentNullException.ThrowIfNull(route);
        this[level] = _ => route();
        return this;
    }

    public bool ContainsKey(int key) => _routes.ContainsKey(key);

    public bool TryGetValue(int key, out Func<CancellationToken, Task<TResult>> value) => _routes.TryGetValue(key, out value!);

    public IEnumerator<KeyValuePair<int, Func<CancellationToken, Task<TResult>>>> GetEnumerator() => _routes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
