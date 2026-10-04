using System.Security.Cryptography;
using System.Text;

namespace Remittance.Api;

/// <summary>
/// Minimal shared-secret check for back-office endpoints (rate updates, status changes).
/// Real deployments would put these behind proper auth; this keeps the demo self-contained.
/// </summary>
public sealed class ApiKeyFilter(IConfiguration config) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = config["BackOffice:ApiKey"];
        var provided = context.HttpContext.Request.Headers["X-Api-Key"].ToString();

        if (string.IsNullOrEmpty(expected) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided)))
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }
}
