namespace Remittance.Core.Money;

/// <summary>
/// ISO 4217 currency code. Knows how many decimal places the currency uses,
/// which matters a lot in the Gulf: KWD, BHD and OMR have three, not two.
/// </summary>
public readonly record struct CurrencyCode
{
    private static readonly Dictionary<string, int> MinorUnitOverrides = new(StringComparer.Ordinal)
    {
        ["KWD"] = 3,
        ["BHD"] = 3,
        ["OMR"] = 3,
        ["JOD"] = 3,
        ["IQD"] = 3,
        ["TND"] = 3,
        ["JPY"] = 0,
        ["KRW"] = 0,
        ["VND"] = 0,
    };

    public string Value { get; }

    public CurrencyCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 3 || !value.All(char.IsAsciiLetter))
        {
            throw new ArgumentException($"'{value}' is not a valid ISO 4217 currency code.", nameof(value));
        }

        Value = value.ToUpperInvariant();
    }

    public int MinorUnits => MinorUnitOverrides.TryGetValue(Value, out var units) ? units : 2;

    public static CurrencyCode Parse(string value) => new(value);

    public static bool TryParse(string? value, out CurrencyCode code)
    {
        try
        {
            code = new CurrencyCode(value!);
            return true;
        }
        catch (ArgumentException)
        {
            code = default;
            return false;
        }
    }

    public override string ToString() => Value;
}
