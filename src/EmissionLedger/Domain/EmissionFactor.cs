namespace EmissionLedger.Domain;

public enum Scope
{
    Scope1 = 1,
    Scope2 = 2,
}

/// <summary>kg CO2e emitted per unit of activity.</summary>
public sealed record EmissionFactor(
    string Key,
    string Label,
    Scope Scope,
    string Unit,
    decimal KgCo2ePerUnit,
    string Source);
