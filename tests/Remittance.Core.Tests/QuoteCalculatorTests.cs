using Remittance.Core.Money;
using Remittance.Core.Quotes;

namespace Remittance.Core.Tests;

public class QuoteCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly CurrencyCode Kwd = CurrencyCode.Parse("KWD");
    private static readonly CurrencyCode Inr = CurrencyCode.Parse("INR");

    private readonly TestClock _clock = new(Now);

    private static Corridor KwdToInr() => new(
        Kwd,
        Inr,
        MarginBps: 50,
        MinSendAmount: 5,
        MaxSendAmount: 3000,
        new FeeSchedule([
            new FeeTier(100, 1.000m, 0),
            new FeeTier(3000, 2.000m, 10),
        ]));

    private QuoteCalculator Calculator() => new(_clock, QuotePolicy.Default);

    private static ExchangeRate Mid(decimal rate, DateTimeOffset? asOf = null) => new(Kwd, Inr, rate, asOf ?? Now);

    [Fact]
    public void Send_mode_applies_margin_and_fee()
    {
        var quote = Calculator().Calculate(KwdToInr(), Mid(280m), 100m, QuoteMode.Send);

        // 280 * (1 - 0.005) = 278.6
        Assert.Equal(278.6m, quote.CustomerRate);
        Assert.Equal(27860.00m, quote.ReceiveAmount.Amount);
        Assert.Equal(1.000m, quote.Fee.Amount);
        Assert.Equal(101.000m, quote.TotalToPay.Amount);
        Assert.Equal(Now.AddMinutes(10), quote.ExpiresAt);
    }

    [Fact]
    public void Higher_tier_adds_percentage_fee()
    {
        var quote = Calculator().Calculate(KwdToInr(), Mid(280m), 1000m, QuoteMode.Send);

        // 2.000 flat + 0.10% of 1000 = 3.000
        Assert.Equal(3.000m, quote.Fee.Amount);
    }

    [Fact]
    public void Receive_mode_guarantees_the_recipient_amount()
    {
        var quote = Calculator().Calculate(KwdToInr(), Mid(280m), 50_000m, QuoteMode.Receive);

        Assert.Equal(50_000m, quote.ReceiveAmount.Amount);
        Assert.True(quote.SendAmount.Amount * quote.CustomerRate >= 50_000m);
    }

    [Fact]
    public void Below_minimum_is_rejected()
    {
        var ex = Assert.Throws<QuoteRejectedException>(
            () => Calculator().Calculate(KwdToInr(), Mid(280m), 2m, QuoteMode.Send));

        Assert.Equal("amount_below_minimum", ex.Code);
    }

    [Fact]
    public void Above_maximum_is_rejected()
    {
        var ex = Assert.Throws<QuoteRejectedException>(
            () => Calculator().Calculate(KwdToInr(), Mid(280m), 5000m, QuoteMode.Send));

        Assert.Equal("amount_above_limit", ex.Code);
    }

    [Fact]
    public void Stale_rate_is_refused()
    {
        var oldRate = Mid(280m, Now.AddHours(-2));

        var ex = Assert.Throws<QuoteRejectedException>(
            () => Calculator().Calculate(KwdToInr(), oldRate, 100m, QuoteMode.Send));

        Assert.Equal("rate_stale", ex.Code);
    }

    [Fact]
    public void Rate_for_wrong_pair_is_refused()
    {
        var wrong = new ExchangeRate(Kwd, CurrencyCode.Parse("PHP"), 190m, Now);

        Assert.Throws<ArgumentException>(() => Calculator().Calculate(KwdToInr(), wrong, 100m, QuoteMode.Send));
    }

    [Fact]
    public void Zero_amount_is_rejected()
    {
        var ex = Assert.Throws<QuoteRejectedException>(
            () => Calculator().Calculate(KwdToInr(), Mid(280m), 0m, QuoteMode.Send));

        Assert.Equal("invalid_amount", ex.Code);
    }
}
