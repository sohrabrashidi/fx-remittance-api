using Microsoft.AspNetCore.Diagnostics;
using Remittance.Core.Quotes;
using Remittance.Core.Transfers;

namespace Remittance.Api;

/// <summary>
/// Turns business rule violations into RFC 7807 problem responses with a
/// stable machine-readable "code", so API clients don't have to parse messages.
/// </summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, code) = exception switch
        {
            QuoteRejectedException q => (StatusCodes.Status422UnprocessableEntity, q.Code),
            TransferRuleException t => (StatusCodes.Status409Conflict, t.Code),
            ArgumentException => (StatusCodes.Status400BadRequest, "invalid_request"),
            _ => (0, ""),
        };

        if (status == 0)
        {
            return false;
        }

        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = status,
                Title = exception.Message,
                Extensions = { ["code"] = code },
            },
        });
    }
}
