using System.Linq.Dynamic.Core;
using CommunityAbp.ProgressiveDelivery.Assignments;
using CommunityAbp.ProgressiveDelivery.Subjects;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace CommunityAbp.ProgressiveDelivery.EntityFrameworkCore.Repositories;

public class EfCoreFeatureAssignmentRepository : EfCoreRepository<IProgressiveDeliveryDbContext, FeatureAssignment, Guid>, IFeatureAssignmentRepository
{
    public EfCoreFeatureAssignmentRepository(IDbContextProvider<IProgressiveDeliveryDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<FeatureAssignment?> FindAsync(Guid featureTrackId, string subjectType, string subjectId, CancellationToken cancellationToken = default)
    {
        subjectType = FeatureSubject.NormalizeType(subjectType);
        subjectId = FeatureSubject.NormalizeId(subjectId);

        return await (await GetQueryableAsync())
            .FirstOrDefaultAsync(
                a => a.FeatureTrackId == featureTrackId && a.SubjectType == subjectType && a.SubjectId == subjectId,
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<FeatureAssignment>> GetListAsync(
        Guid? featureTrackId = null,
        string? subjectType = null,
        string? subjectId = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFilter(await GetQueryableAsync(), featureTrackId, subjectType, subjectId)
            .OrderBy(string.IsNullOrWhiteSpace(sorting) ? $"{nameof(FeatureAssignment.LastModificationTime)} desc, {nameof(FeatureAssignment.CreationTime)} desc" : sorting)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<long> GetCountAsync(
        Guid? featureTrackId = null,
        string? subjectType = null,
        string? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        return await ApplyFilter(await GetQueryableAsync(), featureTrackId, subjectType, subjectId)
            .LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<string>> GetSubjectIdsAsync(
        string subjectType,
        string? filter = null,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        subjectType = FeatureSubject.NormalizeType(subjectType);
        var query = (await GetQueryableAsync()).Where(a => a.SubjectType == subjectType);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filter = filter.Trim();
            query = query.Where(a => a.SubjectId.Contains(filter));
        }

        return await query
            .Select(a => a.SubjectId)
            .Distinct()
            .OrderBy(id => id)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    private static IQueryable<FeatureAssignment> ApplyFilter(IQueryable<FeatureAssignment> query, Guid? featureTrackId, string? subjectType, string? subjectId)
    {
        if (featureTrackId is { } trackId)
        {
            query = query.Where(a => a.FeatureTrackId == trackId);
        }

        if (!string.IsNullOrWhiteSpace(subjectType))
        {
            subjectType = FeatureSubject.NormalizeType(subjectType);
            query = query.Where(a => a.SubjectType == subjectType);
        }

        if (!string.IsNullOrWhiteSpace(subjectId))
        {
            subjectId = FeatureSubject.NormalizeId(subjectId);
            query = query.Where(a => a.SubjectId == subjectId);
        }

        return query;
    }
}
