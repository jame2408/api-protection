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
                    return result.Error.Code switch
                    {
                        "TENANT_NOT_FOUND"       => Results.NotFound(new { error = result.Error.Code }),
                        "CONSUMER_NOT_FOUND"     => Results.NotFound(new { error = result.Error.Code }),
                        "TENANT_SUSPENDED"       => Results.Json(new { error = result.Error.Code }, statusCode: 403),
                        "KEY_LIMIT_EXCEEDED"     => Results.Conflict(new { error = result.Error.Code }),
                        "KEY_NAME_DUPLICATE"     => Results.Conflict(new { error = result.Error.Code }),
                        "SCOPE_NOT_FOUND"        => Results.UnprocessableEntity(new { error = result.Error.Code }),
                        "EXPIRES_AT_EXCEEDS_MAX" => Results.UnprocessableEntity(new { error = result.Error.Code }),
                        _ when result.Error.Code.StartsWith("VALIDATION_ERROR") =>
                            Results.BadRequest(new { error = result.Error.Code }),
                        _ => Results.Problem(result.Error.Code)
                    };
                }

                return Results.Created(
                    $"/api/v1/tenants/{tenantId}/consumers/{consumerId}/keys/{result.Value.KeyId}",
                    result.Value);
            });
    }
}
