using ApiKeyManagement.KeyLifecycle.Http;
using ApiKeyManagement.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ApiKeyManagement.KeyLifecycle.SuspendKey;

public static class SuspendKeyEndpoint
{
    public const string Route = "/api/v1/tenants/{tenantId}/keys/{keyId}/suspend";

    public record Request(string Reason);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(
            Route,
            async (
                string tenantId,
                Guid keyId,
                Request request,
                ISuspendKeyHandler handler,
                HttpContext httpContext,
                CancellationToken cancel) =>
            {
                var command = new SuspendKeyCommand(
                    TenantId: tenantId,
                    KeyId: keyId,
                    Reason: request.Reason,
                    SuspendedBy: Actor.FromClaims(httpContext.User));

                var result = await handler.HandleAsync(command, cancel);

                if (result.IsFailure)
                {
                    // RFC 9457 Problem Details; status/title/type mapping lives in ApiProblem (api-spec.md §2.2).
                    return ApiProblem.FromFailure(result.Error, httpContext);
                }

                return Results.Ok(result.Value);
            })
            .RequireAuthorization(); // ADR-024 §4: control-plane endpoint, must be authenticated.
    }
}
