using ApiKeyManagement.KeyLifecycle.Http;
using ApiKeyManagement.SharedKernel.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ApiKeyManagement.KeyLifecycle.UnlockKey;

public static class UnlockKeyEndpoint
{
    public const string Route = "/api/v1/tenants/{tenantId}/keys/{keyId}/unlock";

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(
            Route,
            async (
                string tenantId,
                Guid keyId,
                IUnlockKeyHandler handler,
                HttpContext httpContext,
                CancellationToken cancel) =>
            {
                var command = new UnlockKeyCommand(
                    TenantId: tenantId,
                    KeyId: keyId,
                    UnlockedBy: Actor.FromClaims(httpContext.User));

                var result = await handler.HandleAsync(command, cancel);

                if (result.IsFailure)
                {
                    // RFC 9457 Problem Details; status/title/type mapping lives in ApiProblem (api-spec.md §2.2).
                    return ApiProblem.FromFailure(result.Error, httpContext);
                }

                return Results.Ok(result.Value);
            })
            // api-spec.md §3.2.7 Authorization row: SecurityAdmin-only. This feature has a
            // not-yet-enabled "操作者權限不足 — 拒絕解鎖" scenario (feature line 37-41) requiring
            // a non-SecurityAdmin authenticated request to reach the handler and be rejected
            // there, so bare RequireAuthorization() (any authenticated actor) is used here rather
            // than RequireRole — mirrors ResumeKey (90feb44) / LockKey (789e562) precedent. The
            // SecurityAdmin-only restriction is deliberately deferred to that scenario's red.
            .RequireAuthorization();
    }
}
