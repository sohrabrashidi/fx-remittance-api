using Remittance.Core.Money;
using Remittance.Core.Quotes;

namespace Remittance.Infrastructure;

/// <summary>Shape of the "Pricing" section in appsettings.json.</summary>
public sealed class PricingOptions
{
    public int QuoteLifetimeMinutes { get; set; } = 10;

    public int MaxRateAgeMinutes { get; set; } = 30;

    public List<CorridorOptions> Corridors { get; set; } = [];
}

public sealed class CorridorOptions
{
    public string Source { get; set; } = "";

    public string Target { get; set; } = "";

    public decimal MarginBps { get; set; }

    public decimal MinSendAmount { get; set; }

    public decimal MaxSendAmount { get; set; }

    public List<FeeTierOptions> Fees { get; set; } = [];
}

public sealed class FeeTierOptions
{
    public decimal UpTo { get; set; }

    public decimal FlatFee { get; set; }

    public decimal PercentBps { get; set; }
}

public sealed class CorridorCatalog
{
    private readonly Dictionary<string, Corridor> _corridors;

    public CorridorCatalog(PricingOptions options)
    {
        _corridors = options.Corridors
            .Select(c => new Corridor(
                CurrencyCode.Parse(c.Source),
                CurrencyCode.Parse(c.Target),
                c.MarginBps,
                c.MinSendAmount,
                c.MaxSendAmount,
                new FeeSchedule(c.Fees.Select(f => new FeeTier(f.UpTo, f.FlatFee, f.PercentBps)))))
            .ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<Corridor> All => _corridors.Values;

    public Corridor? Find(CurrencyCode source, CurrencyCode target) =>
        _corridors.GetValueOrDefault($"{source}-{target}");
}
