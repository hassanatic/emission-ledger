using EmissionLedger.Domain;

namespace EmissionLedger.Tests;

public class EmissionCalculatorTests
{
    // Round numbers so the expected values are easy to check by hand.
    private static readonly EmissionCalculator Calculator = new(new FactorCatalog(new[]
    {
        new EmissionFactor("fuel.diesel", "Diesel", Scope.Scope1, "l", 2.5m, "test"),
        new EmissionFactor("electricity.location.fi", "Grid FI", Scope.Scope2, "kWh", 0.1m, "test"),
        new EmissionFactor("electricity.residual.fi", "Residual FI", Scope.Scope2, "kWh", 0.4m, "test"),
        new EmissionFactor("heat.location.fi", "Heat FI", Scope.Scope2, "kWh", 0.2m, "test"),
    }));

    private static ActivityInput Diesel(string id = "d1", decimal litres = 1000) =>
        new(id, "plant-1", 2025, "fuel", "diesel", null, litres, "l", null);

    private static ActivityInput Electricity(string id = "e1", decimal mwh = 10, decimal? renewable = null) =>
        new(id, "plant-1", 2025, "electricity", null, "FI", mwh, "MWh", renewable);

    private static ActivityInput Heat(string id = "h1") =>
        new(id, "plant-1", 2025, "heat", null, "FI", 5000, "kWh", null);

    [Fact]
    public void Fuel_is_scope1_and_the_same_under_both_methods()
    {
        var result = Calculator.Calculate(Diesel());

        Assert.Equal(Scope.Scope1, result.Scope);
        Assert.Equal(2500m, result.LocationKg);
        Assert.Equal(2500m, result.MarketKg);
    }

    [Fact]
    public void Electricity_without_certificates_uses_the_residual_mix_for_market_based()
    {
        var result = Calculator.Calculate(Electricity());

        Assert.Equal(Scope.Scope2, result.Scope);
        Assert.Equal(1000m, result.LocationKg);
        Assert.Equal(4000m, result.MarketKg);
    }

    [Fact]
    public void Guarantees_of_origin_reduce_only_the_market_based_figure()
    {
        var result = Calculator.Calculate(Electricity(renewable: 0.75m));

        Assert.Equal(1000m, result.LocationKg);
        Assert.Equal(1000m, result.MarketKg);
    }

    [Fact]
    public void Fully_certified_electricity_is_zero_market_based()
    {
        var result = Calculator.Calculate(Electricity(renewable: 1m));

        Assert.Equal(0m, result.MarketKg);
    }

    [Fact]
    public void Report_splits_scopes_and_types()
    {
        var report = Calculator.Report(2025, null, new[] { Diesel(), Electricity(), Heat() });

        Assert.Equal(3, report.Activities);
        Assert.Equal(2.5m, report.Scope1Tonnes);
        Assert.Equal(2.0m, report.Scope2LocationTonnes);
        Assert.Equal(5.0m, report.Scope2MarketTonnes);
        Assert.Equal(4.5m, report.TotalLocationTonnes);
        Assert.Equal(7.5m, report.TotalMarketTonnes);
        Assert.Equal(new[] { "electricity", "fuel", "heat" }, report.ByType.Select(t => t.Type));
    }

    [Fact]
    public void Report_filters_by_year_and_site()
    {
        var otherSite = Diesel("d2") with { SiteId = "office" };
        var otherYear = Diesel("d3") with { Year = 2024 };

        var report = Calculator.Report(2025, "plant-1", new[] { Diesel(), otherSite, otherYear });

        Assert.Equal(1, report.Activities);
        Assert.Equal(2.5m, report.Scope1Tonnes);
    }

    [Fact]
    public void Valid_activity_has_no_errors()
    {
        Assert.Empty(Calculator.Validate(Diesel()));
        Assert.Empty(Calculator.Validate(Electricity(renewable: 0.5m)));
    }

    [Fact]
    public void Unknown_fuel_is_rejected()
    {
        var errors = Calculator.Validate(Diesel() with { Fuel = "kerosene" });

        Assert.Contains("no emission factor 'fuel.kerosene'", errors);
    }

    [Fact]
    public void Wrong_unit_for_the_factor_is_rejected()
    {
        var errors = Calculator.Validate(Electricity() with { Unit = "l" });

        Assert.Contains("unit 'l' cannot be converted to 'kWh'", errors);
        Assert.Single(errors);
    }

    [Fact]
    public void Renewable_share_is_only_for_electricity_and_within_range()
    {
        Assert.Contains("renewableShare only applies to electricity", Calculator.Validate(Diesel() with { RenewableShare = 0.5m }));
        Assert.Contains("renewableShare must be between 0 and 1", Calculator.Validate(Electricity(renewable: 1.2m)));
    }

    [Fact]
    public void Missing_fields_are_all_reported()
    {
        var errors = Calculator.Validate(new ActivityInput(null, null, 0, "fuel", null, null, 0, null, null));

        Assert.Contains("externalId is required", errors);
        Assert.Contains("siteId is required", errors);
        Assert.Contains("year must be between 1990 and 2100", errors);
        Assert.Contains("quantity must be positive", errors);
        Assert.Contains("unknown unit ''", errors);
        Assert.Contains("fuel is required for this type", errors);
    }
}
