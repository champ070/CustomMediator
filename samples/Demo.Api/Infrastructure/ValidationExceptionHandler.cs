using CustomMediator;
using Microsoft.AspNetCore.Diagnostics;

namespace Demo.Api.Infrastructure;

/// <summary>Turns a validation failure raised by the pipeline into an HTTP 400 with the error list.</summary>
public sealed class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not RequestValidationException validation) return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(new { errors = validation.Errors }, cancellationToken);
        return true;
    }
}
