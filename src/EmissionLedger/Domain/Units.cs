namespace EmissionLedger.Domain;

/// <summary>
/// Converts activity quantities into the unit an emission factor is expressed in.
/// Energy is normalised to MJ and volume to litres so conversions stay exact in decimal.
/// </summary>
public static class Units
{
    private static readonly Dictionary<string, (string Dimension, decimal ToBase)> Table =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["kWh"] = ("energy", 3.6m),
            ["MWh"] = ("energy", 3600m),
            ["GJ"] = ("energy", 1000m),
            ["MJ"] = ("energy", 1m),
            ["l"] = ("volume", 1m),
            ["m3"] = ("volume", 1000m),
            ["kg"] = ("mass", 1m),
            ["t"] = ("mass", 1000m),
        };

    public static bool IsKnown(string? unit) => unit is not null && Table.ContainsKey(unit);

    public static bool SameDimension(string a, string b) =>
        Table.TryGetValue(a, out var x) && Table.TryGetValue(b, out var y) && x.Dimension == y.Dimension;

    public static decimal Convert(decimal quantity, string from, string to)
    {
        if (!Table.TryGetValue(from, out var source))
            throw new ArgumentException($"unknown unit '{from}'");
        if (!Table.TryGetValue(to, out var target))
            throw new ArgumentException($"unknown unit '{to}'");
        if (source.Dimension != target.Dimension)
            throw new ArgumentException($"cannot convert {from} ({source.Dimension}) to {to} ({target.Dimension})");

        return quantity * source.ToBase / target.ToBase;
    }
}
