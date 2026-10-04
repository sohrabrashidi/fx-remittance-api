namespace Remittance.Core.Quotes;

public sealed record Quote(
    Guid Id,
    string Corridor,
    Money.Money SendAmount,
    Money.Money Fee,
    Money.Money TotalToPay,
    decimal MidRate,
    decimal CustomerRate,
    Money.Money ReceiveAmount,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
{
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
