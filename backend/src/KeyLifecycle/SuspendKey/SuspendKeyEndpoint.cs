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
            // api-spec.md §3.2.5 Authorization row: SecurityAdmin, TenantAdmin. "System" is also
            // allowed through this role gate (not listed in that row) so the request reaches the
            // handler's actor-type guard, which rejects System with a 422 HUMAN_ACTOR_REQUIRED —
            // api-spec.md §2.1 System actor 段:「僅限人為操作」類業務限制由 domain guard 以 Actor
            // 型別拒絕（業務 Failure），不以 403 表達；若 policy 排除 System，該場景會從 422 退化成 403。
            .RequireAuthorization(policy => policy.RequireRole("SecurityAdmin", "TenantAdmin", "System"));
    }
}
