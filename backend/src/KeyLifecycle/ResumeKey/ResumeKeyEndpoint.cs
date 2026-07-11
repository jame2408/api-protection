using ApiKeyManagement.KeyLifecycle.Http;
using ApiKeyManagement.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ApiKeyManagement.KeyLifecycle.ResumeKey;

public static class ResumeKeyEndpoint
{
    public const string Route = "/api/v1/tenants/{tenantId}/keys/{keyId}/resume";

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(
            Route,
            async (
                string tenantId,
                Guid keyId,
                IResumeKeyHandler handler,
                HttpContext httpContext,
                CancellationToken cancel) =>
            {
                var command = new ResumeKeyCommand(
                    TenantId: tenantId,
                    KeyId: keyId,
                    ResumedBy: Actor.FromClaims(httpContext.User));

                var result = await handler.HandleAsync(command, cancel);

                if (result.IsFailure)
                {
                    // RFC 9457 Problem Details; status/title/type mapping lives in ApiProblem (api-spec.md §2.2).
                    return ApiProblem.FromFailure(result.Error, httpContext);
                }

                return Results.Ok(result.Value);
            })
            // api-spec.md §3.2.6 Authorization row: SecurityAdmin, TenantAdmin. Unlike
            // SuspendKeyEndpoint's role gate, "System" is deliberately NOT included here.
            // SuspendKey admits System into its role gate so the request reaches the handler's
            // actor-type guard, which rejects System with a 422 HUMAN_ACTOR_REQUIRED — preserving
            // that 422 contract requires letting System past the role check first. ResumeKey has no
            // such actor-type guard (ResumeKeyHandler.cs guard 1: invariant 6 constrains Suspend
            // only, there is no System-resume scenario to protect a 422 for), so there is nothing to
            // preserve — System being rejected here with 403 by the role policy is correct.
            .RequireAuthorization(policy => policy.RequireRole("SecurityAdmin", "TenantAdmin"));
    }
}
