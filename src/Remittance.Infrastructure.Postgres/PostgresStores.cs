using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Remittance.Core.Abstractions;
using Remittance.Core.Money;
using Remittance.Core.Quotes;
using Remittance.Core.Transfers;

namespace Remittance.Infrastructure.Postgres;

public sealed class PostgresRateStore(NpgsqlDataSource db) : IRateStore
{
    public async Task<ExchangeRate?> GetLatestAsync(CurrencyCode from, CurrencyCode to, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            """
            select rate, as_of from exchange_rates
            where from_ccy = $1 and to_ccy = $2
            order by as_of desc
            limit 1
            """);
        cmd.Parameters.AddWithValue(from.Value);
        cmd.Parameters.AddWithValue(to.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new ExchangeRate(from, to, reader.GetDecimal(0), reader.GetFieldValue<DateTimeOffset>(1));
    }

    public async Task SaveAsync(ExchangeRate rate, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            """
            insert into exchange_rates (from_ccy, to_ccy, rate, as_of)
            values ($1, $2, $3, $4)
            on conflict (from_ccy, to_ccy, as_of) do update set rate = excluded.rate
            """);
        cmd.Parameters.AddWithValue(rate.From.Value);
        cmd.Parameters.AddWithValue(rate.To.Value);
        cmd.Parameters.AddWithValue(rate.Rate);
        cmd.Parameters.AddWithValue(rate.AsOf.ToUniversalTime());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ExchangeRate>> GetAllLatestAsync(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            """
            select distinct on (from_ccy, to_ccy) from_ccy, to_ccy, rate, as_of
            from exchange_rates
            order by from_ccy, to_ccy, as_of desc
            """);

        var result = new List<ExchangeRate>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ExchangeRate(
                CurrencyCode.Parse(reader.GetString(0)),
                CurrencyCode.Parse(reader.GetString(1)),
                reader.GetDecimal(2),
                reader.GetFieldValue<DateTimeOffset>(3)));
        }

        return result;
    }
}

public sealed class PostgresQuoteStore(NpgsqlDataSource db) : IQuoteStore
{
    public async Task SaveAsync(Quote quote, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            """
            insert into quotes (id, corridor, send_amount, send_ccy, fee, receive_amount, receive_ccy,
                                mid_rate, customer_rate, created_at, expires_at)
            values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
            """);
        cmd.Parameters.AddWithValue(quote.Id);
        cmd.Parameters.AddWithValue(quote.Corridor);
        cmd.Parameters.AddWithValue(quote.SendAmount.Amount);
        cmd.Parameters.AddWithValue(quote.SendAmount.Currency.Value);
        cmd.Parameters.AddWithValue(quote.Fee.Amount);
        cmd.Parameters.AddWithValue(quote.ReceiveAmount.Amount);
        cmd.Parameters.AddWithValue(quote.ReceiveAmount.Currency.Value);
        cmd.Parameters.AddWithValue(quote.MidRate);
        cmd.Parameters.AddWithValue(quote.CustomerRate);
        cmd.Parameters.AddWithValue(quote.CreatedAt.ToUniversalTime());
        cmd.Parameters.AddWithValue(quote.ExpiresAt.ToUniversalTime());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Quote?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            """
            select id, corridor, send_amount, send_ccy, fee, receive_amount, receive_ccy,
                   mid_rate, customer_rate, created_at, expires_at
            from quotes where id = $1
            """);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? QuoteMapper.Read(reader, 0) : null;
    }
}

public sealed class PostgresTransferStore(NpgsqlDataSource db) : ITransferStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task AddAsync(Transfer transfer, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            """
            insert into transfers (id, quote_id, reference, sender, recipient, status, history, created_at)
            values ($1, $2, $3, $4, $5, $6, $7, $8)
            """);
        cmd.Parameters.AddWithValue(transfer.Id);
        cmd.Parameters.AddWithValue(transfer.Quote.Id);
        cmd.Parameters.AddWithValue(transfer.Reference);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(transfer.Sender, Json));
        cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(transfer.Recipient, Json));
        cmd.Parameters.AddWithValue(transfer.Status.ToString());
        cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(transfer.History, Json));
        cmd.Parameters.AddWithValue(transfer.CreatedAt.ToUniversalTime());

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new TransferRuleException("quote_already_used", "This quote was already used for another transfer.");
        }
    }

    public async Task UpdateAsync(Transfer transfer, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("update transfers set status = $2, history = $3 where id = $1");
        cmd.Parameters.AddWithValue(transfer.Id);
        cmd.Parameters.AddWithValue(transfer.Status.ToString());
        cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(transfer.History, Json));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Transfer?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            """
            select q.id, q.corridor, q.send_amount, q.send_ccy, q.fee, q.receive_amount, q.receive_ccy,
                   q.mid_rate, q.customer_rate, q.created_at, q.expires_at,
                   t.id, t.reference, t.sender, t.recipient, t.status, t.history, t.created_at
            from transfers t
            join quotes q on q.id = t.quote_id
            where t.id = $1
            """);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var quote = QuoteMapper.Read(reader, 0);
        return Transfer.Restore(
            reader.GetGuid(11),
            quote,
            JsonSerializer.Deserialize<Party>(reader.GetString(13), Json)!,
            JsonSerializer.Deserialize<Party>(reader.GetString(14), Json)!,
            reader.GetString(12),
            reader.GetFieldValue<DateTimeOffset>(17),
            Enum.Parse<TransferStatus>(reader.GetString(15)),
            JsonSerializer.Deserialize<List<TransferEvent>>(reader.GetString(16), Json) ?? []);
    }

    public async Task<bool> QuoteAlreadyUsedAsync(Guid quoteId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("select exists (select 1 from transfers where quote_id = $1)");
        cmd.Parameters.AddWithValue(quoteId);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }
}

public sealed class PostgresIdempotencyStore(NpgsqlDataSource db) : IIdempotencyStore
{
    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            "select request_hash, status_code, response_body from idempotency_keys where key = $1");
        cmd.Parameters.AddWithValue(key);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new IdempotencyRecord(
            key,
            reader.GetString(0).Trim(),
            reader.IsDBNull(1) ? null : reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    public async Task<bool> TryClaimAsync(string key, string requestHash, CancellationToken ct)
    {
        // The primary key does the locking for us: only one insert can win.
        await using var cmd = db.CreateCommand(
            "insert into idempotency_keys (key, request_hash) values ($1, $2) on conflict (key) do nothing");
        cmd.Parameters.AddWithValue(key);
        cmd.Parameters.AddWithValue(requestHash);
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task CompleteAsync(string key, int statusCode, string responseBody, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand(
            "update idempotency_keys set status_code = $2, response_body = $3 where key = $1");
        cmd.Parameters.AddWithValue(key);
        cmd.Parameters.AddWithValue(statusCode);
        cmd.Parameters.AddWithValue(responseBody);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

internal static class QuoteMapper
{
    public static Quote Read(NpgsqlDataReader r, int o)
    {
        var sendCcy = CurrencyCode.Parse(r.GetString(o + 3).Trim());
        var receiveCcy = CurrencyCode.Parse(r.GetString(o + 6).Trim());
        var send = new Money(r.GetDecimal(o + 2), sendCcy);
        var fee = new Money(r.GetDecimal(o + 4), sendCcy);

        return new Quote(
            r.GetGuid(o),
            r.GetString(o + 1),
            send,
            fee,
            send + fee,
            r.GetDecimal(o + 7),
            r.GetDecimal(o + 8),
            new Money(r.GetDecimal(o + 5), receiveCcy),
            r.GetFieldValue<DateTimeOffset>(o + 9),
            r.GetFieldValue<DateTimeOffset>(o + 10));
    }
}
