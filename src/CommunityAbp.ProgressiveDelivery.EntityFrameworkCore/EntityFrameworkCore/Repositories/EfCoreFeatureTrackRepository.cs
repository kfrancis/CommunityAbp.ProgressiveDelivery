using System.Linq.Dynamic.Core;
using CommunityAbp.ProgressiveDelivery.Tracks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace CommunityAbp.ProgressiveDelivery.EntityFrameworkCore.Repositories;

public class EfCoreFeatureTrackRepository : EfCoreRepository<IProgressiveDeliveryDbContext, FeatureTrack, Guid>, IFeatureTrackRepository
{
    public EfCoreFeatureTrackRepository(IDbContextProvider<IProgressiveDeliveryDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public override async Task<IQueryable<FeatureTrack>> WithDetailsAsync()
    {
        return (await GetQueryableAsync())
            .Include(t => t.Levels)
            .Include(t => t.Rollouts);
    }

    public virtual async Task<FeatureTrack?> FindByNameAsync(string name, bool includeDetails = true, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToUpperInvariant();
        var query = includeDetails ? await WithDetailsAsync() : await GetQueryableAsync();

        return await query
            .FirstOrDefaultAsync(t => t.Name.ToUpper() == normalized, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<FeatureTrack>> GetListAsync(
        string? filter = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        var query = includeDetails ? await WithDetailsAsync() : await GetQueryableAsync();

        return await ApplyFilter(query, filter)
            .OrderBy(string.IsNullOrWhiteSpace(sorting) ? nameof(FeatureTrack.Name) : sorting)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<long> GetCountAsync(string? filter = null, CancellationToken cancellationToken = default)
    {
        return await ApplyFilter(await GetQueryableAsync(), filter).LongCountAsync(GetCancellationToken(cancellationToken));
    }

    private static IQueryable<FeatureTrack> ApplyFilter(IQueryable<FeatureTrack> query, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return query;
        }

        var pattern = $"%{filter.Trim()}%";
        return query.Where(t => EF.Functions.Like(t.Name, pattern) || (t.DisplayName != null && EF.Functions.Like(t.DisplayName, pattern)));
    }
}
