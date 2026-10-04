using Remittance.Core.Money;
using Remittance.Core.Quotes;
using Remittance.Core.Transfers;

namespace Remittance.Core.Abstractions;

public interface IRateStore
{
    Task<ExchangeRate?> GetLatestAsync(CurrencyCode from, CurrencyCode to, CancellationToken ct);

    Task SaveAsync(ExchangeRate rate, CancellationToken ct);

    Task<IReadOnlyList<ExchangeRate>> GetAllLatestAsync(CancellationToken ct);
}

public interface IQuoteStore
{
    Task SaveAsync(Quote quote, CancellationToken ct);

    Task<Quote?> GetAsync(Guid id, CancellationToken ct);
}

public interface ITransferStore
{
    Task AddAsync(Transfer transfer, CancellationToken ct);

    Task UpdateAsync(Transfer transfer, CancellationToken ct);

    Task<Transfer?> GetAsync(Guid id, CancellationToken ct);

    Task<bool> QuoteAlreadyUsedAsync(Guid quoteId, CancellationToken ct);
}

/// <summary>
/// Remembers responses for Idempotency-Key headers so a retried POST
/// (flaky mobile network, double click) doesn't create a second transfer.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct);

    /// <summary>Returns false if another request already claimed the key.</summary>
    Task<bool> TryClaimAsync(string key, string requestHash, CancellationToken ct);

    Task CompleteAsync(string key, int statusCode, string responseBody, CancellationToken ct);
}

public sealed record IdempotencyRecord(string Key, string RequestHash, int? StatusCode, string? ResponseBody)
{
    public bool IsCompleted => StatusCode is not null;
}
