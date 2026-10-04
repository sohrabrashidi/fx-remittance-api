using Remittance.Core.Money;
using Remittance.Core.Quotes;

namespace Remittance.Core.Tests;

public class FeeScheduleTests
{
    private static readonly CurrencyCode Kwd = CurrencyCode.Parse("KWD");

    [Fact]
    public void Picks_the_first_tier_that_covers_the_amount()
    {
        var fees = new FeeSchedule([
            new FeeTier(3000, 2m, 0),
            new FeeTier(100, 1m, 0),
        ]);

        Assert.Equal(1m, fees.FeeFor(new Money.Money(100m, Kwd)).Amount);
        Assert.Equal(2m, fees.FeeFor(new Money.Money(100.001m, Kwd)).Amount);
    }

    [Fact]
    public void Percentage_fee_is_rounded_to_currency_precision()
    {
        var fees = new FeeSchedule([new FeeTier(5000, 0m, 15)]);

        // 0.15% of 333.333 = 0.4999995 -> 0.500
        Assert.Equal(0.500m, fees.FeeFor(new Money.Money(333.333m, Kwd)).Amount);
    }

    [Fact]
    public void Empty_schedule_is_invalid()
    {
        Assert.Throws<ArgumentException>(() => new FeeSchedule([]));
    }
}
