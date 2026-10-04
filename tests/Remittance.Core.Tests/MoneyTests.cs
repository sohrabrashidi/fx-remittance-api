using Remittance.Core.Money;

namespace Remittance.Core.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData("KWD", 3)]
    [InlineData("BHD", 3)]
    [InlineData("USD", 2)]
    [InlineData("INR", 2)]
    [InlineData("JPY", 0)]
    public void Minor_units_follow_iso_4217(string code, int expected)
    {
        Assert.Equal(expected, CurrencyCode.Parse(code).MinorUnits);
    }

    [Fact]
    public void Kuwaiti_dinar_rounds_to_three_decimals()
    {
        var amount = new Money.Money(10.12345m, CurrencyCode.Parse("KWD")).Rounded();

        Assert.Equal(10.123m, amount.Amount);
    }

    [Fact]
    public void Midpoint_rounds_away_from_zero()
    {
        var amount = new Money.Money(2.125m, CurrencyCode.Parse("USD")).Rounded();

        Assert.Equal(2.13m, amount.Amount);
    }

    [Fact]
    public void Rounding_up_never_goes_below_the_original()
    {
        var amount = new Money.Money(1.0001m, CurrencyCode.Parse("KWD")).RoundedUp();

        Assert.Equal(1.001m, amount.Amount);
    }

    [Fact]
    public void Adding_different_currencies_is_an_error()
    {
        var kwd = new Money.Money(1m, CurrencyCode.Parse("KWD"));
        var usd = new Money.Money(1m, CurrencyCode.Parse("USD"));

        Assert.Throws<InvalidOperationException>(() => kwd + usd);
    }

    [Theory]
    [InlineData("")]
    [InlineData("KW")]
    [InlineData("KWDX")]
    [InlineData("K1D")]
    public void Invalid_codes_are_rejected(string code)
    {
        Assert.False(CurrencyCode.TryParse(code, out _));
    }

    [Fact]
    public void Codes_are_normalised_to_upper_case()
    {
        Assert.Equal("KWD", CurrencyCode.Parse("kwd").Value);
    }
}
