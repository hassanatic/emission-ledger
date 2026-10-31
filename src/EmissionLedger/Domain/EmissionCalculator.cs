namespace EmissionLedger.Domain;

/// <summary>
/// GHG Protocol style Scope 1 and Scope 2 calculation.
/// Scope 2 electricity is reported both location-based (grid average) and market-based
/// (residual mix for the share not covered by guarantees of origin).
/// </summary>
public sealed class EmissionCalculator
{
    private readonly FactorCatalog _factors;

    public EmissionCalculator(FactorCatalog factors) => _factors = factors;

    public IReadOnlyList<string> Validate(ActivityInput a)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(a.ExternalId)) errors.Add("externalId is required");
        if (string.IsNullOrWhiteSpace(a.SiteId)) errors.Add("siteId is required");
        if (a.Year < 1990 || a.Year > 2100) errors.Add("year must be between 1990 and 2100");
        if (a.Quantity <= 0) errors.Add("quantity must be positive");
        if (!Units.IsKnown(a.Unit)) errors.Add($"unknown unit '{a.Unit}'");

        if (a.RenewableShare is { } share)
        {
            if (share < 0 || share > 1) errors.Add("renewableShare must be between 0 and 1");
            if (!string.Equals(a.Type, "electricity", StringComparison.OrdinalIgnoreCase))
                errors.Add("renewableShare only applies to electricity");
        }

        string[] keys;
        try
        {
            keys = FactorKeys(a);
        }
        catch (ArgumentException e)
        {
            errors.Add(e.Message);
            return errors;
        }

        foreach (var key in keys)
        {
            var factor = _factors.Find(key);
            if (factor is null)
            {
                errors.Add($"no emission factor '{key}'");
                continue;
            }

            if (Units.IsKnown(a.Unit) && !Units.SameDimension(a.Unit!, factor.Unit))
                errors.Add($"unit '{a.Unit}' cannot be converted to '{factor.Unit}'");
        }

        return errors.Distinct().ToList();
    }

    /// <summary>Calculates one activity. Call <see cref="Validate"/> first.</summary>
    public ActivityEmissions Calculate(ActivityInput a)
    {
        var keys = FactorKeys(a);
        var location = Require(keys[0]);
        var locationKg = Units.Convert(a.Quantity, a.Unit!, location.Unit) * location.KgCo2ePerUnit;
        var marketKg = locationKg;

        if (keys.Length > 1)
        {
            var residual = Require(keys[1]);
            var uncovered = 1m - (a.RenewableShare ?? 0m);
            marketKg = Units.Convert(a.Quantity, a.Unit!, residual.Unit) * uncovered * residual.KgCo2ePerUnit;
        }

        return new ActivityEmissions(
            a.ExternalId!, a.SiteId!, a.Year, location.Scope, a.Type!.ToLowerInvariant(), locationKg, marketKg);
    }

    public YearReport Report(int year, string? siteId, IEnumerable<ActivityInput> activities)
    {
        var rows = activities
            .Where(a => a.Year == year)
            .Where(a => siteId is null || string.Equals(a.SiteId, siteId, StringComparison.OrdinalIgnoreCase))
            .Select(Calculate)
            .ToList();

        static decimal Tonnes(decimal kg) => Math.Round(kg / 1000m, 3);

        var scope1 = rows.Where(r => r.Scope == Scope.Scope1).Sum(r => r.LocationKg);
        var scope2Location = rows.Where(r => r.Scope == Scope.Scope2).Sum(r => r.LocationKg);
        var scope2Market = rows.Where(r => r.Scope == Scope.Scope2).Sum(r => r.MarketKg);

        var byType = rows
            .GroupBy(r => r.Type)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new TypeTotal(g.Key, Tonnes(g.Sum(r => r.LocationKg)), Tonnes(g.Sum(r => r.MarketKg))))
            .ToList();

        return new YearReport(
            year,
            siteId,
            rows.Count,
            Tonnes(scope1),
            Tonnes(scope2Location),
            Tonnes(scope2Market),
            Tonnes(scope1 + scope2Location),
            Tonnes(scope1 + scope2Market),
            byType);
    }

    private EmissionFactor Require(string key) =>
        _factors.Find(key) ?? throw new InvalidOperationException($"no emission factor '{key}'");

    private static string[] FactorKeys(ActivityInput a) => a.Type?.Trim().ToLowerInvariant() switch
    {
        "fuel" => new[] { $"fuel.{Required(a.Fuel, "fuel")}" },
        "electricity" => new[]
        {
            $"electricity.location.{Required(a.Country, "country")}",
            $"electricity.residual.{Required(a.Country, "country")}",
        },
        "heat" => new[] { $"heat.location.{Required(a.Country, "country")}" },
        _ => throw new ArgumentException($"unknown type '{a.Type}' (expected fuel, electricity or heat)"),
    };

    private static string Required(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{name} is required for this type")
            : value.Trim().ToLowerInvariant();
}
