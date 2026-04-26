using ApiKeyManagement.SharedKernel.Contracts;
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
                        ConsumerValidationFailureCodes.TenantNotFound    => Results.NotFound(new { error = result.Error.Code }),
                        ConsumerValidationFailureCodes.ConsumerNotFound  => Results.NotFound(new { error = result.Error.Code }),
                        ConsumerValidationFailureCodes.TenantSuspended   => Results.Json(new { error = result.Error.Code }, statusCode: 403),
                        CreateApiKeyFailureCodes.KeyLimitExceeded        => Results.Conflict(new { error = result.Error.Code }),
                        CreateApiKeyFailureCodes.KeyNameDuplicate        => Results.Conflict(new { error = result.Error.Code }),
                        CreateApiKeyFailureCodes.ScopeNotFound           => Results.UnprocessableEntity(new { error = result.Error.Code }),
                        CreateApiKeyFailureCodes.ExpiresAtExceedsMax     => Results.UnprocessableEntity(new { error = result.Error.Code }),
                        _ when result.Error.Code.StartsWith(CreateApiKeyFailureCodes.ValidationErrorPrefix) =>
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
