using System.Linq.Dynamic.Core;
using CommunityAbp.ProgressiveDelivery.Transitions;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace CommunityAbp.ProgressiveDelivery.EntityFrameworkCore.Repositories;

public class EfCoreFeatureTransitionRepository : EfCoreRepository<IProgressiveDeliveryDbContext, FeatureTransition, Guid>, IFeatureTransitionRepository
{
    public EfCoreFeatureTransitionRepository(IDbContextProvider<IProgressiveDeliveryDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<List<FeatureTransition>> GetListAsync(
        Guid? featureTrackId = null,
        string? subjectType = null,
        string? subjectId = null,
        FeatureTransitionType? transitionType = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFilter(await GetQueryableAsync(), featureTrackId, subjectType, subjectId, transitionType)
            .OrderBy(string.IsNullOrWhiteSpace(sorting) ? $"{nameof(FeatureTransition.CreationTime)} desc" : sorting)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<long> GetCountAsync(
        Guid? featureTrackId = null,
        string? subjectType = null,
        string? subjectId = null,
        FeatureTransitionType? transitionType = null,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFilter(await GetQueryableAsync(), featureTrackId, subjectType, subjectId, transitionType)
            .LongCountAsync(GetCancellationToken(cancellationToken));
    }

    private static IQueryable<FeatureTransition> ApplyFilter(
        IQueryable<FeatureTransition> query,
        Guid? featureTrackId,
        string? subjectType,
        string? subjectId,
        FeatureTransitionType? transitionType)
    {
        if (featureTrackId is { } trackId)
        {
            query = query.Where(t => t.FeatureTrackId == trackId);
        }

        if (!string.IsNullOrWhiteSpace(subjectType))
        {
            query = query.Where(t => t.SubjectType == subjectType);
        }

        if (!string.IsNullOrWhiteSpace(subjectId))
        {
            query = query.Where(t => t.SubjectId == subjectId);
        }

        if (transitionType is { } type)
        {
            query = query.Where(t => t.TransitionType == type);
        }

        return query;
    }
}
