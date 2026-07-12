using System.Text.Json;
using ApiKeyManagement.KeyLifecycle.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ApiKeyManagement.KeyLifecycle.LockKey;

public static class LockKeyEndpoint
{
    public const string Route = "/internal/keys/{keyId}/lock";

    public record Request(
        string TenantId,
        string RuleId,
        string Severity,
        string Reason,
        DateTimeOffset DetectedAt,
        JsonElement Evidence);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(
            Route,
            async (
                Guid keyId,
                Request request,
                ILockKeyHandler handler,
                HttpContext httpContext,
                CancellationToken cancel) =>
            {
                var command = new LockKeyCommand(
                    TenantId: request.TenantId,
                    KeyId: keyId,
                    RuleId: request.RuleId,
                    Severity: request.Severity,
                    Reason: request.Reason,
                    DetectedAt: request.DetectedAt,
                    Evidence: request.Evidence);

                var result = await handler.HandleAsync(command, cancel);

                if (result.IsFailure)
                {
                    // RFC 9457 Problem Details; status/title/type mapping lives in ApiProblem (api-spec.md §2.2).
                    return ApiProblem.FromFailure(result.Error, httpContext);
                }

                return Results.Ok(result.Value);
            })
            // Internal endpoint (Monitoring BC service-to-service call, I6) — not exposed
            // externally, unlike Scanner's AllowAnonymous precedent
            // (RevokeLeakedKeysEndpoint.Map). Decided 2026-07-12 ("非 System 角色嘗試鎖定"
            // scenario): the System-only restriction is expressed as an endpoint-level role
            // policy (403 FORBIDDEN via ProblemAuthorizationResultHandler), not a handler actor
            // guard — unlike SuspendKey, there is no in-handler "System rejected" contract this
            // endpoint needs to protect, so the rejection can live entirely at the auth layer.
            // mTLS / Internal Service Token hardening is tracked by ADR-024 and out of scope here.
            .RequireAuthorization(policy => policy.RequireRole("System"));
    }
}
