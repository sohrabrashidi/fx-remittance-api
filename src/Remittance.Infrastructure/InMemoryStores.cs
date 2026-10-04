using System.Collections.Concurrent;
using Remittance.Core.Abstractions;
using Remittance.Core.Money;
using Remittance.Core.Quotes;
using Remittance.Core.Transfers;

namespace Remittance.Infrastructure;

// In-memory implementations. Good for local runs and tests; swap for the
// Postgres ones by setting ConnectionStrings:Postgres.

public sealed class InMemoryRateStore : IRateStore
{
    private readonly ConcurrentDictionary<(CurrencyCode, CurrencyCode), ExchangeRate> _latest = new();

    public Task<ExchangeRate?> GetLatestAsync(CurrencyCode from, CurrencyCode to, CancellationToken ct) =>
        Task.FromResult(_latest.TryGetValue((from, to), out var rate) ? rate : null);

    public Task SaveAsync(ExchangeRate rate, CancellationToken ct)
    {
        _latest.AddOrUpdate((rate.From, rate.To), rate, (_, existing) => rate.AsOf >= existing.AsOf ? rate : existing);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ExchangeRate>> GetAllLatestAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ExchangeRate>>(_latest.Values.OrderBy(r => r.From.Value).ThenBy(r => r.To.Value).ToList());
}

public sealed class InMemoryQuoteStore : IQuoteStore
{
    private readonly ConcurrentDictionary<Guid, Quote> _quotes = new();

    public Task SaveAsync(Quote quote, CancellationToken ct)
    {
        _quotes[quote.Id] = quote;
        return Task.CompletedTask;
    }

    public Task<Quote?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_quotes.TryGetValue(id, out var quote) ? quote : null);
}

public sealed class InMemoryTransferStore : ITransferStore
{
    private readonly ConcurrentDictionary<Guid, Transfer> _transfers = new();
    private readonly ConcurrentDictionary<Guid, Guid> _quoteToTransfer = new();

    public Task AddAsync(Transfer transfer, CancellationToken ct)
    {
        if (!_quoteToTransfer.TryAdd(transfer.Quote.Id, transfer.Id))
        {
            throw new TransferRuleException("quote_already_used", "This quote was already used for another transfer.");
        }

        _transfers[transfer.Id] = transfer;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Transfer transfer, CancellationToken ct)
    {
        _transfers[transfer.Id] = transfer;
        return Task.CompletedTask;
    }

    public Task<Transfer?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_transfers.TryGetValue(id, out var transfer) ? transfer : null);

    public Task<bool> QuoteAlreadyUsedAsync(Guid quoteId, CancellationToken ct) =>
        Task.FromResult(_quoteToTransfer.ContainsKey(quoteId));
}

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _records = new(StringComparer.Ordinal);

    public Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct) =>
        Task.FromResult(_records.TryGetValue(key, out var record) ? record : null);

    public Task<bool> TryClaimAsync(string key, string requestHash, CancellationToken ct) =>
        Task.FromResult(_records.TryAdd(key, new IdempotencyRecord(key, requestHash, null, null)));

    public Task CompleteAsync(string key, int statusCode, string responseBody, CancellationToken ct)
    {
        _records.AddOrUpdate(
            key,
            _ => throw new InvalidOperationException($"Idempotency key '{key}' was never claimed."),
            (_, existing) => existing with { StatusCode = statusCode, ResponseBody = responseBody });
        return Task.CompletedTask;
    }
}
