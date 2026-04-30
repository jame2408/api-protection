using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.Api.Middleware;

public class UnhandledExceptionMiddleware(
    RequestDelegate next,
    ILogger<UnhandledExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            logger.LogError(
                ex,
                "Unhandled exception while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            var failure = FailureProvider.CreateFailure(HostMiddlewareFailureCodes.UnhandledException);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(
                new { error = failure.Code },
                context.RequestAborted);
        }
    }
}
