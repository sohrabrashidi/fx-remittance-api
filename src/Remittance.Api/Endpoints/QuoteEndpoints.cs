using Remittance.Core.Abstractions;
using Remittance.Core.Money;
using Remittance.Core.Quotes;
using Remittance.Infrastructure;

namespace Remittance.Api.Endpoints;

public static class QuoteEndpoints
{
    public static void MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/quotes").WithTags("Quotes");

        group.MapPost("/", async (
            QuoteRequest request,
            CorridorCatalog corridors,
            IRateStore rates,
            IQuoteStore quotes,
            QuoteCalculator calculator,
            CancellationToken ct) =>
        {
            var source = CurrencyCode.Parse(request.Source);
            var target = CurrencyCode.Parse(request.Target);

            var corridor = corridors.Find(source, target)
                ?? throw new QuoteRejectedException("corridor_not_supported", $"We don't send {source} to {target}.");

            var mid = await rates.GetLatestAsync(source, target, ct)
                ?? throw new QuoteRejectedException("rate_unavailable", $"No rate loaded for {corridor.Key}.");

            var quote = calculator.Calculate(corridor, mid, request.Amount, request.Mode);
            await quotes.SaveAsync(quote, ct);

            return Results.Created($"/quotes/{quote.Id}", QuoteResponse.From(quote));
        });

        group.MapGet("/{id:guid}", async (Guid id, IQuoteStore quotes, CancellationToken ct) =>
            await quotes.GetAsync(id, ct) is { } quote
                ? Results.Ok(QuoteResponse.From(quote))
                : Results.NotFound());
    }
}
