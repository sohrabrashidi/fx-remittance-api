namespace Remittance.Core.Quotes;

public sealed class QuoteRejectedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
