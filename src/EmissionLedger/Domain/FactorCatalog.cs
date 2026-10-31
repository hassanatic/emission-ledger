using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmissionLedger.Domain;

public sealed class FactorCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, EmissionFactor> _byKey;

    public FactorCatalog(IEnumerable<EmissionFactor> factors)
    {
        _byKey = factors.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<EmissionFactor> All => _byKey.Values;

    public EmissionFactor? Find(string key) => _byKey.TryGetValue(key, out var factor) ? factor : null;

    /// <summary>Loads the sample factor set shipped with the service (Data/factors.json).</summary>
    public static FactorCatalog LoadDefault()
    {
        var assembly = typeof(FactorCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream("EmissionLedger.Data.factors.json")
            ?? throw new InvalidOperationException("embedded factors.json not found");
        var factors = JsonSerializer.Deserialize<List<EmissionFactor>>(stream, JsonOptions)
            ?? throw new InvalidOperationException("factors.json is empty");
        return new FactorCatalog(factors);
    }
}
