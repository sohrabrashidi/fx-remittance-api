namespace Remittance.Core.Money;

public readonly record struct Money(decimal Amount, CurrencyCode Currency)
{
    public static Money Zero(CurrencyCode currency) => new(0m, currency);

    /// <summary>Rounds half away from zero to the currency's minor unit.</summary>
    public Money Rounded() =>
        this with { Amount = Math.Round(Amount, Currency.MinorUnits, MidpointRounding.AwayFromZero) };

    /// <summary>Rounds up to the next minor unit. Used when the recipient amount must not fall short.</summary>
    public Money RoundedUp()
    {
        var factor = Pow10(Currency.MinorUnits);
        return this with { Amount = Math.Ceiling(Amount * factor) / factor };
    }

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    public static bool operator >(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a.Amount > b.Amount;
    }

    public static bool operator <(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a.Amount < b.Amount;
    }

    public static bool operator >=(Money a, Money b) => !(a < b);

    public static bool operator <=(Money a, Money b) => !(a > b);

    public override string ToString() =>
        $"{Amount.ToString($"F{Currency.MinorUnits}", System.Globalization.CultureInfo.InvariantCulture)} {Currency}";

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (a.Currency != b.Currency)
        {
            throw new InvalidOperationException($"Cannot combine {a.Currency} with {b.Currency}.");
        }
    }

    private static decimal Pow10(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }
}
