using Remittance.Core.Money;

namespace Remittance.Core.Quotes;

/// <summary>
/// A send/receive currency pair the business is licensed to serve, with its own
/// pricing. Margin is applied on top of the mid-market rate, in basis points.
/// </summary>
public sealed record Corridor(
    CurrencyCode Source,
    CurrencyCode Target,
    decimal MarginBps,
    decimal MinSendAmount,
    decimal MaxSendAmount,
    FeeSchedule Fees)
{
    public string Key => $"{Source}-{Target}";

    public decimal CustomerRate(ExchangeRate mid)
    {
        if (mid.From != Source || mid.To != Target)
        {
            throw new ArgumentException($"Rate {mid.From}/{mid.To} does not match corridor {Key}.");
        }

        // Customer gets slightly fewer target units per source unit than mid-market.
        // Keep 8 decimals: enough precision for any realistic pair, still readable.
        return Math.Round(mid.Rate * (1m - MarginBps / 10_000m), 8, MidpointRounding.ToZero);
    }
}
