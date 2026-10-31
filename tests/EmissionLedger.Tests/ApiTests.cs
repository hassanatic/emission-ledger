using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EmissionLedger.Api;
using EmissionLedger.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EmissionLedger.Tests;

public class ApiTests
{
    private static object Batch(params object[] activities) => new { activities };

    private static readonly object CertifiedPower = new
    {
        externalId = "meter-2025-01",
        siteId = "hq",
        year = 2025,
        type = "electricity",
        country = "FI",
        quantity = 12.5,
        unit = "MWh",
        renewableShare = 1.0,
    };

    private static readonly object UnknownFuel = new
    {
        externalId = "delivery-7",
        siteId = "hq",
        year = 2025,
        type = "fuel",
        fuel = "kerosene",
        quantity = 300,
        unit = "l",
    };

    [Fact]
    public async Task Health_is_ok()
    {
        using var factory = new WebApplicationFactory<Program>();
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Factors_are_listed_from_the_embedded_catalog()
    {
        using var factory = new WebApplicationFactory<Program>();
        var json = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/factors");

        var keys = json.EnumerateArray().Select(f => f.GetProperty("key").GetString()).ToList();
        Assert.Contains("fuel.diesel", keys);
        Assert.Contains("electricity.residual.fi", keys);
        Assert.Equal("Scope1", json.EnumerateArray().First(f => f.GetProperty("key").GetString() == "fuel.diesel").GetProperty("scope").GetString());
    }

    [Fact]
    public async Task Batch_ingest_accepts_valid_rows_and_explains_rejected_ones()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/activities", Batch(CertifiedPower, UnknownFuel));
        var result = await response.Content.ReadFromJsonAsync<IngestResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Rejected);
        Assert.Contains("no emission factor 'fuel.kerosene'", result.Outcomes.Single(o => o.Status == "rejected").Errors);
    }

    [Fact]
    public async Task Resending_the_same_batch_is_idempotent()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/activities", Batch(CertifiedPower));
        var second = await (await client.PostAsJsonAsync("/activities", Batch(CertifiedPower))).Content.ReadFromJsonAsync<IngestResult>();
        var stored = await client.GetFromJsonAsync<List<ActivityInput>>("/activities?siteId=hq");

        Assert.Equal(1, second!.Unchanged);
        Assert.Single(stored!);
    }

    [Fact]
    public async Task Empty_batch_is_a_bad_request()
    {
        using var factory = new WebApplicationFactory<Program>();
        var response = await factory.CreateClient().PostAsJsonAsync("/activities", Batch());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Report_shows_certified_electricity_as_zero_market_based()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/activities", Batch(CertifiedPower));
        var report = await client.GetFromJsonAsync<YearReport>("/reports/2025?siteId=hq");

        Assert.NotNull(report);
        Assert.Equal(1, report.Activities);
        Assert.Equal(0m, report.Scope2MarketTonnes);
        Assert.True(report.Scope2LocationTonnes > 0m);
    }
}
