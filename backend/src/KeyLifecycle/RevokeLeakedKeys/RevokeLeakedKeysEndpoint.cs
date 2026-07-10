using ApiKeyManagement.KeyLifecycle.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ApiKeyManagement.KeyLifecycle.RevokeLeakedKeys;

public static class RevokeLeakedKeysEndpoint
{
    public const string Route = "/internal/security/leaked-keys";

    public record Request(string Prefix);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(
            Route,
            async (
                Request request,
                IRevokeLeakedKeysHandler handler,
                HttpContext httpContext,
                CancellationToken cancel) =>
            {
                var command = new RevokeLeakedKeysCommand(KeyPrefix: request.Prefix);

                var result = await handler.HandleAsync(command, cancel);

                if (result.IsFailure)
                {
                    // RFC 9457 Problem Details; status/title/type mapping lives in ApiProblem (api-spec.md §2.2).
                    return ApiProblem.FromFailure(result.Error, httpContext);
                }

                return Results.Ok(result.Value);
            })
            // ADR-024 §4: internal batch endpoint — belongs to Internal Service Token / mTLS
            // scope per api-spec.md §2.1, not yet implemented. Explicit AllowAnonymous is the
            // debt marker until that lands (ADR-024 easily-confused-concepts table).
            .AllowAnonymous();
    }
}
