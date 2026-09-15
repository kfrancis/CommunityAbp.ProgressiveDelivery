using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Subjects;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace CommunityAbp.ProgressiveDelivery.Caching;

/// <summary>
/// Cached sticky assignment for one subject on one track. Negative lookups are cached too
/// (<see cref="Level"/> is <c>null</c>). Keys are tenant-prefixed automatically by ABP, and every
/// read/write is performed inside <c>CurrentTenant.Change(subject.TenantId)</c>, so tenants never share entries.
/// </summary>
[CacheName("PdAssignment")]
public class FeatureAssignmentCacheItem
{
    public int? Level { get; set; }

    public string? Reason { get; set; }

    public static readonly FeatureAssignmentCacheItem None = new();
}

public interface IFeatureAssignmentCache
{
    Task<FeatureAssignmentCacheItem> GetAsync(Guid featureTrackId, FeatureSubject subject, CancellationToken cancellationToken = default);

    Task InvalidateAsync(Guid featureTrackId, FeatureSubject subject, CancellationToken cancellationToken = default);

    static string BuildKey(Guid featureTrackId, string subjectType, string subjectId)
        => $"{featureTrackId:N}:{subjectType.ToUpperInvariant()}:{subjectId}";
}

public class FeatureAssignmentCache : IFeatureAssignmentCache, ITransientDependency
{
    private readonly IDistributedCache<FeatureAssignmentCacheItem, string> _cache;
    private readonly IFeatureAssignmentRepository _assignmentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ProgressiveDeliveryOptions _options;

    public FeatureAssignmentCache(
        IDistributedCache<FeatureAssignmentCacheItem, string> cache,
        IFeatureAssignmentRepository assignmentRepository,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<ProgressiveDeliveryOptions> options)
    {
        _cache = cache;
        _assignmentRepository = assignmentRepository;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
        _options = options.Value;
    }

    public virtual async Task<FeatureAssignmentCacheItem> GetAsync(Guid featureTrackId, FeatureSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        using (_currentTenant.Change(subject.TenantId))
        {
            var key = IFeatureAssignmentCache.BuildKey(featureTrackId, subject.Type, subject.Id);

            return await _cache.GetOrAddAsync(
                key,
                async () =>
                {
                    var assignment = await _assignmentRepository.FindAsync(featureTrackId, subject.Type, subject.Id, cancellationToken);
                    return assignment is null
                        ? new FeatureAssignmentCacheItem()
                        : new FeatureAssignmentCacheItem { Level = assignment.AssignedLevel, Reason = assignment.AssignmentReason };
                },
                () => new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _options.AssignmentCacheDuration
                },
                token: cancellationToken) ?? FeatureAssignmentCacheItem.None;
        }
    }

    /// <summary>
    /// Removes the entry now and again after the ambient unit of work commits, so a concurrent reader cannot
    /// re-populate the cache with pre-commit data. The tenant scope is re-established for the deferred removal
    /// because ABP normalises cache keys (tenant prefix) at removal time, not at scheduling time.
    /// </summary>
    public virtual async Task InvalidateAsync(Guid featureTrackId, FeatureSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var key = IFeatureAssignmentCache.BuildKey(featureTrackId, subject.Type, subject.Id);
        var tenantId = subject.TenantId;

        using (_currentTenant.Change(tenantId))
        {
            await _cache.RemoveAsync(key, token: cancellationToken);
        }

        _unitOfWorkManager.Current?.OnCompleted(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _cache.RemoveAsync(key);
            }
        });
    }
}
