namespace Remittance.Core.Quotes;

public enum QuoteMode
{
    /// <summary>Customer says how much they want to send.</summary>
    Send,

    /// <summary>Customer says how much the recipient must receive.</summary>
    Receive,
}

public sealed record QuoteRequest(string Source, string Target, decimal Amount, QuoteMode Mode = QuoteMode.Send);
