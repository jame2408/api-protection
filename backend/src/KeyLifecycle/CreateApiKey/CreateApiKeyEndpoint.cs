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
                CancellationToken ct) =>
            {
                var command = new CreateApiKeyCommand(
                    TenantId: tenantId,
                    ConsumerId: consumerId,
                    Name: request.Name,
                    Environment: request.Environment,
                    Scopes: request.Scopes,
                    ExpiresAt: request.ExpiresAt);

                try
                {
                    var response = await handler.HandleAsync(command, ct);
                    return Results.Created(
                        $"/api/v1/tenants/{tenantId}/consumers/{consumerId}/keys/{response.KeyId}",
                        response);
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message switch
                    {
                        "TENANT_NOT_FOUND" => Results.NotFound(new { error = ex.Message }),
                        "CONSUMER_NOT_FOUND" => Results.NotFound(new { error = ex.Message }),
                        "TENANT_SUSPENDED" => Results.Json(new { error = ex.Message }, statusCode: 403),
                        "KEY_LIMIT_EXCEEDED" => Results.Conflict(new { error = ex.Message }),
                        "KEY_NAME_DUPLICATE" => Results.Conflict(new { error = ex.Message }),
                        "SCOPE_NOT_FOUND" => Results.UnprocessableEntity(new { error = ex.Message }),
                        "EXPIRES_AT_EXCEEDS_MAX" => Results.UnprocessableEntity(new { error = ex.Message }),
                        _ when ex.Message.StartsWith("VALIDATION_ERROR") =>
                            Results.BadRequest(new { error = ex.Message }),
                        _ => Results.Problem(ex.Message)
                    };
                }
            });
    }
}
