using Remittance.Core.Money;
using Remittance.Core.Quotes;
using Remittance.Core.Transfers;

namespace Remittance.Api;

public sealed record MoneyDto(decimal Amount, CurrencyCode Currency)
{
    public static MoneyDto From(Money m) => new(m.Amount, m.Currency);
}

public sealed record QuoteResponse(
    Guid Id,
    string Corridor,
    MoneyDto SendAmount,
    MoneyDto Fee,
    MoneyDto TotalToPay,
    decimal CustomerRate,
    MoneyDto ReceiveAmount,
    DateTimeOffset ExpiresAt)
{
    public static QuoteResponse From(Quote q) => new(
        q.Id,
        q.Corridor,
        MoneyDto.From(q.SendAmount),
        MoneyDto.From(q.Fee),
        MoneyDto.From(q.TotalToPay),
        q.CustomerRate,
        MoneyDto.From(q.ReceiveAmount),
        q.ExpiresAt);
}

public sealed record CreateTransferRequest(Guid QuoteId, Party Sender, Party Recipient);

public sealed record TransferResponse(
    Guid Id,
    string Reference,
    TransferStatus Status,
    QuoteResponse Quote,
    Party Sender,
    Party Recipient,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TransferEvent> History)
{
    public static TransferResponse From(Transfer t) => new(
        t.Id,
        t.Reference,
        t.Status,
        QuoteResponse.From(t.Quote),
        t.Sender,
        t.Recipient,
        t.CreatedAt,
        t.History);
}

public sealed record StatusChangeRequest(TransferStatus Status, string? Note);

public sealed record RateUpdateRequest(decimal Rate, DateTimeOffset? AsOf);
