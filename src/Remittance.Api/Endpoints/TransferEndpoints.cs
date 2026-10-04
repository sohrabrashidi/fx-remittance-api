using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Remittance.Core.Abstractions;
using Remittance.Core.Transfers;

namespace Remittance.Api.Endpoints;

public static class TransferEndpoints
{
    private const string IdempotencyHeader = "Idempotency-Key";

    public static void MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/transfers").WithTags("Transfers");

        group.MapPost("/", CreateTransfer);

        group.MapGet("/{id:guid}", async (Guid id, ITransferStore transfers, CancellationToken ct) =>
            await transfers.GetAsync(id, ct) is { } transfer
                ? Results.Ok(TransferResponse.From(transfer))
                : Results.NotFound());

        // Back-office / partner callback. Protected with the same API key as rate updates.
        group.MapPost("/{id:guid}/status", async (
                Guid id,
                StatusChangeRequest body,
                ITransferStore transfers,
                IClock clock,
                CancellationToken ct) =>
            {
                var transfer = await transfers.GetAsync(id, ct);
                if (transfer is null)
                {
                    return Results.NotFound();
                }

                transfer.MoveTo(body.Status, clock.UtcNow, body.Note);
                await transfers.UpdateAsync(transfer, ct);
                return Results.Ok(TransferResponse.From(transfer));
            })
            .AddEndpointFilter<ApiKeyFilter>();
    }

    private static async Task<IResult> CreateTransfer(
        HttpContext http,
        CreateTransferRequest request,
        IQuoteStore quotes,
        ITransferStore transfers,
        IIdempotencyStore idempotency,
        IClock clock,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken ct)
    {
        var key = http.Request.Headers[IdempotencyHeader].ToString();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Send a unique {IdempotencyHeader} header (max 100 chars) with every new transfer.",
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency_key_required" });
        }

        var json = jsonOptions.Value.SerializerOptions;
        var requestHash = Hash(JsonSerializer.Serialize(request, json));

        if (!await idempotency.TryClaimAsync(key, requestHash, ct))
        {
            var existing = await idempotency.GetAsync(key, ct);
            if (existing is null || existing.RequestHash != requestHash)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "This Idempotency-Key was already used with a different request body.",
                    extensions: new Dictionary<string, object?> { ["code"] = "idempotency_key_reused" });
            }

            if (!existing.IsCompleted)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The original request with this key is still being processed. Retry shortly.",
                    extensions: new Dictionary<string, object?> { ["code"] = "request_in_progress" });
            }

            http.Response.Headers["Idempotent-Replayed"] = "true";
            return Results.Content(existing.ResponseBody, "application/json", Encoding.UTF8, existing.StatusCode);
        }

        var quote = await quotes.GetAsync(request.QuoteId, ct);
        if (quote is null)
        {
            return await Finish(StatusCodes.Status404NotFound, new { code = "quote_not_found" });
        }

        try
        {
            var transfer = Transfer.Create(quote, request.Sender, request.Recipient, clock.UtcNow);
            await transfers.AddAsync(transfer, ct);

            http.Response.Headers.Location = $"/transfers/{transfer.Id}";
            return await Finish(StatusCodes.Status201Created, TransferResponse.From(transfer));
        }
        catch (TransferRuleException ex)
        {
            return await Finish(StatusCodes.Status409Conflict, new { code = ex.Code, title = ex.Message });
        }

        async Task<IResult> Finish(int status, object body)
        {
            var payload = JsonSerializer.Serialize(body, json);
            await idempotency.CompleteAsync(key, status, payload, ct);
            return Results.Content(payload, "application/json", Encoding.UTF8, status);
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
