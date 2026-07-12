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
            // api-spec.md §3.2.7 Authorization row: SecurityAdmin-only — deliberately narrower
            // than ResumeKey's RequireRole("SecurityAdmin", "TenantAdmin"). ResumeKey's TenantAdmin
            // inclusion is a plain state-transition reversal; Unlock is spec-framed as a security
            // confirmation act ("Locked 狀態僅由系統（Monitoring）觸發，解鎖是人工確認安全後的操作" —
            // api-spec.md §3.2.7 note), so the spec restricts it to SecurityAdmin alone. UnlockKeyHandler
            // has no actor-type guard (unlike SuspendKey's System-only lock counterpart), so there is
            // no 422 contract to preserve by admitting extra roles past this policy — rejection at the
            // role gate is correct. Precedent: ResumeKey (90feb44) / LockKey (7f02530).
            .RequireAuthorization(policy => policy.RequireRole("SecurityAdmin"));
    }
}
