using System.Text.Json.Serialization;
using Remittance.Api;
using Remittance.Api.Endpoints;
using Remittance.Core.Abstractions;
using Remittance.Core.Quotes;
using Remittance.Infrastructure;
using Remittance.Infrastructure.Postgres;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new CurrencyCodeJsonConverter());
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var pricing = builder.Configuration.GetSection("Pricing").Get<PricingOptions>() ?? new PricingOptions();
builder.Services.AddSingleton(pricing);
builder.Services.AddSingleton<CorridorCatalog>();
builder.Services.AddSingleton(new QuotePolicy(
    TimeSpan.FromMinutes(pricing.QuoteLifetimeMinutes),
    TimeSpan.FromMinutes(pricing.MaxRateAgeMinutes)));
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<QuoteCalculator>();

var connectionString = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddSingleton<IRateStore, InMemoryRateStore>();
    builder.Services.AddSingleton<IQuoteStore, InMemoryQuoteStore>();
    builder.Services.AddSingleton<ITransferStore, InMemoryTransferStore>();
    builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
}
else
{
    builder.Services.AddPostgresStores(connectionString);
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHostedService<RateSeeder>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapOpenApi();
app.MapHealthChecks("/health");

app.MapCorridorEndpoints();
app.MapRateEndpoints();
app.MapQuoteEndpoints();
app.MapTransferEndpoints();

app.Run();

// Exposed for WebApplicationFactory in the API tests.
public partial class Program;
