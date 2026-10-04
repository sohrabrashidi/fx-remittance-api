using Remittance.Infrastructure;

namespace Remittance.Api.Endpoints;

public static class CorridorEndpoints
{
    public static void MapCorridorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/corridors", (CorridorCatalog catalog) =>
            catalog.All.Select(c => new
            {
                corridor = c.Key,
                source = c.Source.Value,
                target = c.Target.Value,
                c.MinSendAmount,
                c.MaxSendAmount,
                fees = c.Fees.Tiers,
            }))
            .WithTags("Corridors");
    }
}
