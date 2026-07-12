using System.Xml;
using ApiKeyManagement.KeyLifecycle.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ApiKeyManagement.KeyLifecycle.RotateKey;

public static class RotateKeyEndpoint
{
    public const string Route = "/api/v1/tenants/{tenantId}/keys/{keyId}/rotate";

    // gracePeriod is an ISO 8601 duration string (e.g. "PT24H") per api-spec.md §3.2.4 —
    // System.Text.Json has no built-in Duration converter, so it's parsed at the boundary via
    // XmlConvert.ToTimeSpan(). No scenario in this wave covers a malformed value, so there is no
    // try-catch / VALIDATION_ERROR wrapping here (scope discipline).
    public record Request(string? GracePeriod);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(
            Route,
            async (
                string tenantId,
                Guid keyId,
                Request request,
                IRotateKeyHandler handler,
                HttpContext httpContext,
                CancellationToken cancel) =>
            {
                var command = new RotateKeyCommand(
                    TenantId: tenantId,
                    KeyId: keyId,
                    GracePeriod: request.GracePeriod is null
                        ? null
                        : XmlConvert.ToTimeSpan(request.GracePeriod));

                var result = await handler.HandleAsync(command, cancel);

                if (result.IsFailure)
                {
                    // RFC 9457 Problem Details; status/title/type mapping lives in ApiProblem (api-spec.md §2.2).
                    return ApiProblem.FromFailure(result.Error, httpContext);
                }

                return Results.Ok(result.Value);
            })
            // api-spec.md §3.2.4 Authorization row: TenantAdmin, Consumer（限自身金鑰）. This
            // scenario set (05_RotateKey.feature) has no authorization-rejection scenario to
            // red-drive either the role policy or the ownership ("限自身金鑰") guard — mirrors
            // LockKey/UnlockKey's opening-scenario precedent (LockKeyEndpoint.Map, commit
            // 789e562): bare RequireAuthorization() (any authenticated actor) is used here, and
            // both the role policy and the ownership guard are deliberately deferred to a future
            // red-driven scenario. Tracked as a checkpoint gap by the orchestrator (2026-07-12).
            .RequireAuthorization();
    }
}
