using Remittance.Core.Abstractions;
using Remittance.Core.Money;

namespace Remittance.Core.Quotes;

public sealed class QuoteCalculator(IClock clock, QuotePolicy policy)
{
    public Quote Calculate(Corridor corridor, ExchangeRate mid, decimal amount, QuoteMode mode)
    {
        mid.Validate();

        if (amount <= 0)
        {
            throw new QuoteRejectedException("invalid_amount", "Amount must be greater than zero.");
        }

        var now = clock.UtcNow;
        if (mid.IsStale(now, policy.MaxRateAge))
        {
            throw new QuoteRejectedException(
                "rate_stale",
                $"Latest {corridor.Key} rate is from {mid.AsOf:u}. Refusing to quote on stale data.");
        }

        var customerRate = corridor.CustomerRate(mid);

        Money.Money send;
        Money.Money receive;

        if (mode == QuoteMode.Send)
        {
            send = new Money.Money(amount, corridor.Source).Rounded();
            receive = new Money.Money(send.Amount * customerRate, corridor.Target).Rounded();
        }
        else
        {
            receive = new Money.Money(amount, corridor.Target).Rounded();
            // Round the send side up so the recipient never gets less than requested.
            send = new Money.Money(receive.Amount / customerRate, corridor.Source).RoundedUp();
        }

        if (send.Amount < corridor.MinSendAmount)
        {
            throw new QuoteRejectedException(
                "amount_below_minimum",
                $"Minimum send amount for {corridor.Key} is {corridor.MinSendAmount} {corridor.Source}.");
        }

        if (send.Amount > corridor.MaxSendAmount)
        {
            throw new QuoteRejectedException(
                "amount_above_limit",
                $"Maximum send amount for {corridor.Key} is {corridor.MaxSendAmount} {corridor.Source}.");
        }

        var fee = corridor.Fees.FeeFor(send);

        return new Quote(
            Id: Guid.NewGuid(),
            Corridor: corridor.Key,
            SendAmount: send,
            Fee: fee,
            TotalToPay: send + fee,
            MidRate: mid.Rate,
            CustomerRate: customerRate,
            ReceiveAmount: receive,
            CreatedAt: now,
            ExpiresAt: now + policy.QuoteLifetime);
    }
}

public sealed record QuotePolicy(TimeSpan QuoteLifetime, TimeSpan MaxRateAge)
{
    public static QuotePolicy Default { get; } = new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30));
}
