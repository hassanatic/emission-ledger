using System.Collections.Concurrent;
using EmissionLedger.Domain;

namespace EmissionLedger.Storage;

public enum UpsertStatus
{
    Created,
    Updated,
    Unchanged,
}

public interface IActivityStore
{
    UpsertStatus Upsert(ActivityInput activity);

    IReadOnlyList<ActivityInput> Query(string? siteId, int? year);
}

/// <summary>
/// Keeps activities keyed by their external id, so re-sending a batch is safe:
/// identical records are reported as unchanged, changed ones as updated.
/// </summary>
public sealed class InMemoryActivityStore : IActivityStore
{
    private readonly ConcurrentDictionary<string, ActivityInput> _items = new(StringComparer.Ordinal);

    public UpsertStatus Upsert(ActivityInput activity)
    {
        var id = activity.ExternalId ?? throw new ArgumentException("externalId is required");
        var status = UpsertStatus.Created;

        _items.AddOrUpdate(id, activity, (_, existing) =>
        {
            status = existing == activity ? UpsertStatus.Unchanged : UpsertStatus.Updated;
            return activity;
        });

        return status;
    }

    public IReadOnlyList<ActivityInput> Query(string? siteId, int? year) =>
        _items.Values
            .Where(a => siteId is null || string.Equals(a.SiteId, siteId, StringComparison.OrdinalIgnoreCase))
            .Where(a => year is null || a.Year == year)
            .OrderBy(a => a.Year)
            .ThenBy(a => a.SiteId, StringComparer.Ordinal)
            .ThenBy(a => a.ExternalId, StringComparer.Ordinal)
            .ToList();
}
