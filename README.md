# emission-ledger

A small ASP.NET Core (.NET 8, C#) service for Scope 1 and Scope 2 greenhouse gas accounting.
Sites send activity data (fuel deliveries, metered electricity, district heat), the service
validates it against an emission factor catalogue and produces a yearly report in tonnes CO2e.

Scope 2 electricity is reported both ways, as the GHG Protocol Scope 2 guidance asks:

- **location-based**: grid average factor for the country
- **market-based**: residual mix factor for the share *not* covered by guarantees of origin

So a site that buys certified renewable electricity sees its market-based figure drop while the
location-based figure stays the same.

## API

| Method | Path | What it does |
|---|---|---|
| GET | `/health` | liveness |
| GET | `/factors` | the emission factor catalogue |
| POST | `/activities` | batch ingest; every row is validated and reported back as created, updated, unchanged or rejected (with reasons) |
| GET | `/activities?siteId=&year=` | stored activity rows |
| GET | `/reports/{year}?siteId=` | Scope 1, Scope 2 (location and market), totals and a breakdown by activity type |

Re-sending a row with the same `externalId` is safe: identical rows come back as `unchanged`,
changed ones as `updated`. That makes it easy to re-run an import from an ERP or a meter export.

```bash
docker build -t emission-ledger . && docker run -p 8080:8080 emission-ledger

curl -s localhost:8080/activities -H 'content-type: application/json' -d '{
  "activities": [
    {"externalId": "fuel-2025-03", "siteId": "plant-1", "year": 2025, "type": "fuel",
     "fuel": "diesel", "quantity": 1200, "unit": "l"},
    {"externalId": "meter-2025-q1", "siteId": "plant-1", "year": 2025, "type": "electricity",
     "country": "FI", "quantity": 85, "unit": "MWh", "renewableShare": 0.6},
    {"externalId": "heat-2025-q1", "siteId": "plant-1", "year": 2025, "type": "heat",
     "country": "FI", "quantity": 0.4, "unit": "GJ"}
  ]}'

curl -s localhost:8080/reports/2025?siteId=plant-1
```

Units are converted to the factor's unit (kWh, MWh, MJ, GJ, l, m3, kg, t); a row in a unit that
cannot be converted, like litres of electricity, is rejected with a reason instead of being
silently miscounted.

## Emission factors

`src/EmissionLedger/Data/factors.json` ships **sample values, rounded, for demonstration only**.
For real reporting, replace them with the official factors for your reporting year (for example
national statistics for fuels and grid electricity, and the published European residual mix for
market-based Scope 2) and keep the source on each entry.

## Tests

```bash
dotnet test tests/EmissionLedger.Tests/EmissionLedger.Tests.csproj
```

Unit tests cover unit conversion, both Scope 2 methods, guarantees of origin and validation;
integration tests run the real API through `WebApplicationFactory`.

## Not in scope

Scope 3, persistence beyond memory, authentication and multi-tenant separation. The store sits
behind `IActivityStore`, so a database-backed implementation can replace the in-memory one.
