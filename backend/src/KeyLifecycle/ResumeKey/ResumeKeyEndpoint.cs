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
            // ADR-024 §4: control-plane endpoint, must be authenticated. Role policy (api-spec.md
            // §3.2.6 Authorization: SecurityAdmin, TenantAdmin) is deliberately not enforced yet —
            // left as a bare authentication gate until the "操作者無恢復權限 — 拒絕恢復" scenario
            // drives it in with a real 403 red, per the SuspendKey precedent for the human-actor
            // guard (this scenario has no actor-type restriction, only a role restriction).
            .RequireAuthorization();
    }
}
