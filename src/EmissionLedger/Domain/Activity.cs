namespace EmissionLedger.Domain;

/// <summary>
/// One line of activity data, e.g. a fuel delivery or a month of metered electricity.
/// </summary>
/// <param name="ExternalId">Id from the source system; re-sending the same id updates the record.</param>
/// <param name="Type">fuel, electricity or heat.</param>
/// <param name="Fuel">Fuel key for type=fuel, e.g. diesel or natural_gas.</param>
/// <param name="Country">Grid or network country for electricity and heat, e.g. FI.</param>
/// <param name="RenewableShare">Share (0..1) of the electricity covered by guarantees of origin.</param>
public sealed record ActivityInput(
    string? ExternalId,
    string? SiteId,
    int Year,
    string? Type,
    string? Fuel,
    string? Country,
    decimal Quantity,
    string? Unit,
    decimal? RenewableShare);

public sealed record ActivityEmissions(
    string ExternalId,
    string SiteId,
    int Year,
    Scope Scope,
    string Type,
    decimal LocationKg,
    decimal MarketKg);

public sealed record TypeTotal(string Type, decimal LocationTonnes, decimal MarketTonnes);

public sealed record YearReport(
    int Year,
    string? SiteId,
    int Activities,
    decimal Scope1Tonnes,
    decimal Scope2LocationTonnes,
    decimal Scope2MarketTonnes,
    decimal TotalLocationTonnes,
    decimal TotalMarketTonnes,
    IReadOnlyList<TypeTotal> ByType);
