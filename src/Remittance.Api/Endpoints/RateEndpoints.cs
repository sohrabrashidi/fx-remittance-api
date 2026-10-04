using Remittance.Core.Abstractions;
using Remittance.Core.Money;

namespace Remittance.Api.Endpoints;

public static class RateEndpoints
{
    public static void MapRateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/rates").WithTags("Rates");

        group.MapGet("/", async (IRateStore rates, CancellationToken ct) => await rates.GetAllLatestAsync(ct));

        group.MapPut("/{from}/{to}", async (
                string from,
                string to,
                RateUpdateRequest body,
                IRateStore rates,
                IClock clock,
                CancellationToken ct) =>
            {
                var rate = new ExchangeRate(
                    CurrencyCode.Parse(from),
                    CurrencyCode.Parse(to),
                    body.Rate,
                    (body.AsOf ?? clock.UtcNow).ToUniversalTime()).Validate();

                await rates.SaveAsync(rate, ct);
                return Results.Ok(rate);
            })
            .AddEndpointFilter<ApiKeyFilter>();
    }
}
