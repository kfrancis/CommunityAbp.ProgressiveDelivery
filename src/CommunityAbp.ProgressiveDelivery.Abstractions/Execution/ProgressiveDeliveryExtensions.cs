namespace CommunityAbp.ProgressiveDelivery.Execution;

public static class ProgressiveDeliveryExtensions
{
    /// <summary>Executes routes that return no value.</summary>
    public static async Task ExecuteAsync(
        this IProgressiveDelivery progressiveDelivery,
        string trackName,
        IReadOnlyDictionary<int, Func<CancellationToken, Task>> routes,
        ProgressiveExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progressiveDelivery);
        ArgumentNullException.ThrowIfNull(routes);

        var wrapped = new ProgressiveRoutes<bool>();
        foreach (var (level, route) in routes)
        {
            wrapped[level] = async ct =>
            {
                await route(ct).ConfigureAwait(false);
                return true;
            };
        }

        await progressiveDelivery.ExecuteAsync(trackName, wrapped, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Convenience overload accepting a route-table builder.</summary>
    public static Task<TResult> ExecuteAsync<TResult>(
        this IProgressiveDelivery progressiveDelivery,
        string trackName,
        Action<ProgressiveRoutes<TResult>> configureRoutes,
        ProgressiveExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progressiveDelivery);
        ArgumentNullException.ThrowIfNull(configureRoutes);

        var routes = new ProgressiveRoutes<TResult>();
        configureRoutes(routes);
        return progressiveDelivery.ExecuteAsync(trackName, routes, options, cancellationToken);
    }

    /// <summary>Returns <c>true</c> when the current subject is at or above <paramref name="level"/> for the track.</summary>
    public static async Task<bool> IsAtLeastAsync(
        this IProgressiveDelivery progressiveDelivery,
        string trackName,
        int level,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progressiveDelivery);
        return await progressiveDelivery.GetEffectiveLevelAsync(trackName, cancellationToken).ConfigureAwait(false) >= level;
    }
}
