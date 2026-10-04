using Remittance.Core.Money;

namespace Remittance.Core.Quotes;

/// <summary>
/// One band of a fee schedule. A transfer falls into the first tier whose
/// <see cref="UpTo"/> is greater than or equal to the send amount.
/// </summary>
public sealed record FeeTier(decimal UpTo, decimal FlatFee, decimal PercentBps);

public sealed class FeeSchedule
{
    private readonly IReadOnlyList<FeeTier> _tiers;

    public FeeSchedule(IEnumerable<FeeTier> tiers)
    {
        _tiers = tiers.OrderBy(t => t.UpTo).ToList();

        if (_tiers.Count == 0)
        {
            throw new ArgumentException("A fee schedule needs at least one tier.", nameof(tiers));
        }

        if (_tiers.Any(t => t.FlatFee < 0 || t.PercentBps < 0))
        {
            throw new ArgumentException("Fees cannot be negative.", nameof(tiers));
        }
    }

    public IReadOnlyList<FeeTier> Tiers => _tiers;

    public Money.Money FeeFor(Money.Money sendAmount)
    {
        var tier = _tiers.FirstOrDefault(t => sendAmount.Amount <= t.UpTo)
            ?? throw new QuoteRejectedException(
                "amount_above_limit",
                $"{sendAmount} is above the highest fee tier ({_tiers[^1].UpTo} {sendAmount.Currency}).");

        var fee = tier.FlatFee + sendAmount.Amount * tier.PercentBps / 10_000m;
        return new Money.Money(fee, sendAmount.Currency).Rounded();
    }
}
