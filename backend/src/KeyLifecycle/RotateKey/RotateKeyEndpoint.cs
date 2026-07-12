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
                        : XmlConvert.ToTimeSpan(request.GracePeriod),
                    // MapInboundClaims=false (Program.cs) keeps this the raw JWT claim name —
                    // TestTokenFactory only writes "consumerId" for Consumer tokens, so
                    // TenantAdmin/SecurityAdmin naturally read null here.
                    ActorConsumerId: httpContext.User.FindFirst("consumerId")?.Value);

                var result = await handler.HandleAsync(command, cancel);

                if (result.IsFailure)
                {
                    // RFC 9457 Problem Details; status/title/type mapping lives in ApiProblem (api-spec.md §2.2).
                    return ApiProblem.FromFailure(result.Error, httpContext);
                }

                return Results.Ok(result.Value);
            })
            // api-spec.md §3.2.4 Authorization row: TenantAdmin, Consumer（限自身金鑰）. The role
            // half is enforced here (RequireRole), red-driven by "操作者無輪替權限 — 拒絕輪替"
            // (defect-repro, security review 9e0e432). The ownership half ("限自身金鑰") is
            // enforced in RotateKeyHandler (ActorConsumerId guard), red-driven by
            // "Consumer 輪替非自身金鑰 — 拒絕" (defect-repro companion scenario).
            .RequireAuthorization(policy => policy.RequireRole("TenantAdmin", "Consumer"));
    }
}
