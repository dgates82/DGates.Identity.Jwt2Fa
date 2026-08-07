using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DGates.Identity.Jwt2Fa.Extensions;

/// <summary>
/// Applied once per module at <c>MapGroup</c> level, so every endpoint in that group is
/// covered automatically with no cooperation required from the consuming app. Catches
/// unhandled exceptions, logs the real one, and returns a generic message instead —
/// mirrors the source app's own per-action <c>catch (Exception e) { LogError(e, ...);
/// return 500 "An unexpected error occurred."; }</c> pattern, which this package had no
/// equivalent for anywhere until now.
/// </summary>
internal sealed class ExceptionHandlingEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception ex)
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("DGates.Identity.Jwt2Fa");
            logger.LogError(ex, "Unhandled exception handling {Method} {Path}",
                context.HttpContext.Request.Method, context.HttpContext.Request.Path);

            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "An unexpected error occurred.");
        }
    }
}
