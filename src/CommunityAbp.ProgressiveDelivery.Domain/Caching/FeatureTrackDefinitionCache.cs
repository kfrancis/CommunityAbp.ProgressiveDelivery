using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace CommunityAbp.ProgressiveDelivery.Caching;

/// <summary>
/// Read-through cache for track definitions keyed by normalised track name.
/// </summary>
public interface IFeatureTrackDefinitionCache
{
    /// <summary>Returns the cached definition, or <c>null</c> when the track does not exist.</summary>
    Task<FeatureTrackDefinitionCacheItem?> GetAsync(string trackName, CancellationToken cancellationToken = default);

    Task InvalidateAsync(string trackName, CancellationToken cancellationToken = default);

    static string NormalizeKey(string trackName) => trackName.Trim().ToUpperInvariant();
}

public class FeatureTrackDefinitionCache : IFeatureTrackDefinitionCache, ITransientDependency
{
    private readonly IDistributedCache<FeatureTrackDefinitionCacheItem, string> _cache;
    private readonly IFeatureTrackRepository _trackRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ProgressiveDeliveryOptions _options;

    public FeatureTrackDefinitionCache(
        IDistributedCache<FeatureTrackDefinitionCacheItem, string> cache,
        IFeatureTrackRepository trackRepository,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<ProgressiveDeliveryOptions> options)
    {
        _cache = cache;
        _trackRepository = trackRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _options = options.Value;
    }

    public virtual async Task<FeatureTrackDefinitionCacheItem?> GetAsync(string trackName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackName);

        var key = IFeatureTrackDefinitionCache.NormalizeKey(trackName);
        FeatureTrackDefinitionCacheItem? loaded = null;

        var item = await _cache.GetOrAddAsync(
            key,
            async () =>
            {
                var track = await _trackRepository.FindByNameAsync(trackName, includeDetails: true, cancellationToken);
                loaded = track is null
                    ? FeatureTrackDefinitionCacheItem.Missing(trackName)
                    : FeatureTrackDefinitionCacheItem.FromEntity(track);
                return loaded;
            },
            () => new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = loaded is { Exists: false }
                    ? _options.MissingTrackCacheDuration
                    : _options.TrackDefinitionCacheDuration
            },
            token: cancellationToken);

        return item is { Exists: true } ? item : null;
    }

    /// <summary>Removes the entry now and again after the ambient unit of work commits (see <see cref="FeatureAssignmentCache.InvalidateAsync"/>).</summary>
    public virtual async Task InvalidateAsync(string trackName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackName);

        var key = IFeatureTrackDefinitionCache.NormalizeKey(trackName);
        await _cache.RemoveAsync(key, token: cancellationToken);
        _unitOfWorkManager.Current?.OnCompleted(() => _cache.RemoveAsync(key));
    }
}
