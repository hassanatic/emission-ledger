using EmissionLedger.Domain;
using EmissionLedger.Storage;

namespace EmissionLedger.Api;

public sealed record IngestRequest(List<ActivityInput>? Activities);

public sealed record IngestOutcome(string? ExternalId, string Status, IReadOnlyList<string> Errors);

public sealed record IngestResult(int Created, int Updated, int Unchanged, int Rejected, IReadOnlyList<IngestOutcome> Outcomes);

public static class Endpoints
{
    public static IEndpointRouteBuilder MapLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapGet("/factors", (FactorCatalog catalog) =>
            Results.Ok(catalog.All.OrderBy(f => f.Key, StringComparer.Ordinal)));

        app.MapPost("/activities", (IngestRequest request, IActivityStore store, EmissionCalculator calculator) =>
        {
            if (request.Activities is null || request.Activities.Count == 0)
                return Results.BadRequest(new { error = "activities must be a non-empty array" });

            var outcomes = new List<IngestOutcome>(request.Activities.Count);
            foreach (var activity in request.Activities)
            {
                var errors = calculator.Validate(activity);
                if (errors.Count > 0)
                {
                    outcomes.Add(new IngestOutcome(activity.ExternalId, "rejected", errors));
                    continue;
                }

                var status = store.Upsert(activity);
                outcomes.Add(new IngestOutcome(activity.ExternalId, status.ToString().ToLowerInvariant(), Array.Empty<string>()));
            }

            return Results.Ok(new IngestResult(
                outcomes.Count(o => o.Status == "created"),
                outcomes.Count(o => o.Status == "updated"),
                outcomes.Count(o => o.Status == "unchanged"),
                outcomes.Count(o => o.Status == "rejected"),
                outcomes));
        });

        app.MapGet("/activities", (string? siteId, int? year, IActivityStore store) =>
            Results.Ok(store.Query(siteId, year)));

        app.MapGet("/reports/{year:int}", (int year, string? siteId, IActivityStore store, EmissionCalculator calculator) =>
            Results.Ok(calculator.Report(year, siteId, store.Query(siteId, year))));

        return app;
    }
}
