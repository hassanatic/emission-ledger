using System.Text.Json.Serialization;
using EmissionLedger.Api;
using EmissionLedger.Domain;
using EmissionLedger.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton(FactorCatalog.LoadDefault());
builder.Services.AddSingleton<EmissionCalculator>();
builder.Services.AddSingleton<IActivityStore, InMemoryActivityStore>();

var app = builder.Build();

app.MapLedgerEndpoints();

app.Run();

// Exposed for WebApplicationFactory in the integration tests.
public partial class Program
{
}
