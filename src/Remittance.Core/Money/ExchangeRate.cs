namespace Remittance.Core.Money;

/// <summary>Mid-market rate: 1 unit of <see cref="From"/> buys <see cref="Rate"/> units of <see cref="To"/>.</summary>
public sealed record ExchangeRate(CurrencyCode From, CurrencyCode To, decimal Rate, DateTimeOffset AsOf)
{
    public ExchangeRate Validate()
    {
        if (Rate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Rate), "Rate must be positive.");
        }

        if (From == To)
        {
            throw new ArgumentException("Source and target currency must differ.");
        }

        return this;
    }

    public bool IsStale(DateTimeOffset now, TimeSpan maxAge) => now - AsOf > maxAge;
}
