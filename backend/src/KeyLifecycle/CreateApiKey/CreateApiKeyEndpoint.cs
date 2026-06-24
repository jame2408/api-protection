using ApiKeyManagement.KeyLifecycle.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ApiKeyManagement.KeyLifecycle.CreateApiKey;

public static class CreateApiKeyEndpoint
{
    public record Request(
        string Name,
        string Environment,
        IReadOnlyList<string> Scopes,
        DateTimeOffset ExpiresAt
    );

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/v1/tenants/{tenantId}/consumers/{consumerId}/keys",
            async (
                string tenantId,
                string consumerId,
                Request request,
                ICreateApiKeyHandler handler,
                HttpContext httpContext,
                CancellationToken cancel) =>
            {
                var command = new CreateApiKeyCommand(
                    TenantId: tenantId,
                    ConsumerId: consumerId,
                    Name: request.Name,
                    Environment: request.Environment,
                    Scopes: request.Scopes,
                    ExpiresAt: request.ExpiresAt);

                var result = await handler.HandleAsync(command, cancel);

                if (result.IsFailure)
                {
                    // RFC 9457 Problem Details; status/title/type mapping lives in ApiProblem (api-spec.md §2.2).
                    return ApiProblem.FromFailure(result.Error, httpContext);
                }

                return Results.Created(
                    $"/api/v1/tenants/{tenantId}/consumers/{consumerId}/keys/{result.Value.KeyId}",
                    result.Value);
            });
    }
}
