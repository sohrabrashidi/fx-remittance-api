using Remittance.Core.Abstractions;
using Remittance.Core.Money;

namespace Remittance.Api;

/// <summary>
/// Loads starting rates from configuration ("SeedRates") so the API is usable
/// right after startup. In production rates come from the treasury feed via PUT /rates.
/// </summary>
public sealed class RateSeeder(IConfiguration config, IRateStore rates, IClock clock, ILogger<RateSeeder> log)
    : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var seeds = config.GetSection("SeedRates").Get<Dictionary<string, decimal>>() ?? [];
        foreach (var (pair, value) in seeds)
        {
            var parts = pair.Split('-');
            if (parts.Length != 2)
            {
                log.LogWarning("Skipping seed rate with bad key {Pair}", pair);
                continue;
            }

            await rates.SaveAsync(
                new ExchangeRate(CurrencyCode.Parse(parts[0]), CurrencyCode.Parse(parts[1]), value, clock.UtcNow),
                ct);
        }

        log.LogInformation("Seeded {Count} exchange rates", seeds.Count);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
