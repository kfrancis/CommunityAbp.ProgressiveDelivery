using CommunityAbp.ProgressiveDelivery.Assignments;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;

namespace CommunityAbp.ProgressiveDelivery.Subjects.Lookup;

/// <summary>
/// Turns what an administrator knows about a subject (a user name, an email, a tenant name) into the subject id
/// the runtime uses, and back again for display. Merges every <see cref="IFeatureSubjectLookupProvider"/> for a
/// type, then tops results up with ids that already have assignments, so client and custom subject types are
/// still pickable without a provider.
/// </summary>
/// <remarks>Tenant scope comes from the ambient <c>ICurrentTenant</c>.</remarks>
public class FeatureSubjectLookupManager : DomainService
{
    private readonly IEnumerable<IFeatureSubjectLookupProvider> _providers;
    private readonly IFeatureAssignmentRepository _assignmentRepository;

    public FeatureSubjectLookupManager(IEnumerable<IFeatureSubjectLookupProvider> providers, IFeatureAssignmentRepository assignmentRepository)
    {
        _providers = providers;
        _assignmentRepository = assignmentRepository;
    }

    /// <summary>Subject types with at least one lookup provider.</summary>
    public virtual IReadOnlyList<string> GetSearchableSubjectTypes()
        => _providers.Select(p => FeatureSubject.NormalizeType(p.SubjectType)).Distinct(StringComparer.Ordinal).ToList();

    public virtual async Task<List<FeatureSubjectLookupResult>> SearchAsync(
        string subjectType,
        string? filter,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        subjectType = FeatureSubject.NormalizeType(subjectType);
        var results = new List<FeatureSubjectLookupResult>();

        foreach (var provider in GetProviders(subjectType))
        {
            foreach (var item in await provider.SearchAsync(filter, maxResultCount, cancellationToken))
            {
                Add(results, item, hasAssignments: false);
            }
        }

        var assigned = await _assignmentRepository.GetSubjectIdsAsync(
            subjectType, FeatureSubject.NormalizeId(filter), maxResultCount, cancellationToken);
        var assignedSet = assigned.ToHashSet(StringComparer.Ordinal);

        for (var i = 0; i < results.Count; i++)
        {
            if (assignedSet.Contains(results[i].SubjectId))
            {
                results[i] = results[i] with { HasAssignments = true };
            }
        }

        foreach (var id in assigned)
        {
            if (results.Count >= maxResultCount)
            {
                break;
            }

            // Ids the providers didn't return (deleted users, client ids, custom types): try to name them anyway.
            if (results.All(r => r.SubjectId != id))
            {
                var found = await FindAsync(subjectType, id, cancellationToken);
                Add(results, found ?? new FeatureSubjectLookupItem(id, id), hasAssignments: true);
            }
        }

        return results.Take(maxResultCount).ToList();
    }

    /// <summary>Human-readable view of one subject, or <c>null</c> when no provider knows it.</summary>
    public virtual async Task<FeatureSubjectLookupItem?> FindAsync(string subjectType, string subjectId, CancellationToken cancellationToken = default)
    {
        subjectType = FeatureSubject.NormalizeType(subjectType);
        subjectId = FeatureSubject.NormalizeId(subjectId);

        foreach (var provider in GetProviders(subjectType))
        {
            try
            {
                if (await provider.FindAsync(subjectId, cancellationToken) is { } item)
                {
                    return item with { SubjectId = FeatureSubject.NormalizeId(item.SubjectId) };
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Display names are a convenience; a broken lookup (e.g. remote Identity down) must not break listings.
                Logger.LogWarning(ex, "Subject lookup provider {Provider} failed to find {SubjectType} {SubjectId}.", provider.GetType().Name, subjectType, subjectId);
            }
        }

        return null;
    }

    /// <summary>
    /// Display names for many subjects at once, each looked up inside its own tenant. Subjects no provider knows
    /// are left out of the result.
    /// </summary>
    public virtual async Task<Dictionary<FeatureSubject, FeatureSubjectLookupItem>> FindManyAsync(
        IEnumerable<FeatureSubject> subjects,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<FeatureSubject, FeatureSubjectLookupItem>();
        var searchable = GetSearchableSubjectTypes();

        foreach (var group in subjects.Distinct().Where(s => searchable.Contains(s.Type)).GroupBy(s => s.TenantId))
        {
            using (CurrentTenant.Change(group.Key))
            {
                foreach (var subject in group)
                {
                    if (await FindAsync(subject.Type, subject.Id, cancellationToken) is { } item)
                    {
                        result[subject] = item;
                    }
                }
            }
        }

        return result;
    }

    protected virtual IEnumerable<IFeatureSubjectLookupProvider> GetProviders(string subjectType)
        => _providers.Where(p => string.Equals(FeatureSubject.NormalizeType(p.SubjectType), subjectType, StringComparison.Ordinal));

    private static void Add(List<FeatureSubjectLookupResult> results, FeatureSubjectLookupItem item, bool hasAssignments)
    {
        var id = FeatureSubject.NormalizeId(item.SubjectId);
        if (string.IsNullOrWhiteSpace(id) || results.Any(r => r.SubjectId == id))
        {
            return;
        }

        results.Add(new FeatureSubjectLookupResult(id, item.DisplayName, item.Detail, hasAssignments));
    }
}

/// <summary>A search hit: the subject plus whether it already has any assignment.</summary>
public sealed record FeatureSubjectLookupResult(string SubjectId, string DisplayName, string? Detail, bool HasAssignments);
