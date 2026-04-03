using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Contracts;

namespace ApiKeyManagement.KeyLifecycle.CreateApiKey;

public class CreateApiKeyHandler(
    IConsumerValidator consumerValidator,
    IApiKeyRepository keyRepository,
    IScopeRegistry scopeRegistry,
    IAccessPolicyService accessPolicyService
) : ICreateApiKeyHandler
{
    public async Task<CreateApiKeyResponse> HandleAsync(
        CreateApiKeyCommand command, CancellationToken ct = default)
    {
        // 1. Validate tenant + consumer (I1)
        var validation = await consumerValidator.ValidateAsync(command.TenantId, command.ConsumerId, ct);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.ErrorCode);
        }

        // 2. Guard: active key count
        var activeCount = await keyRepository.CountActiveAsync(
            command.ConsumerId, command.Environment, command.TenantId, ct);
        if (activeCount >= ApiKey.GetMaxActiveKeys())
            throw new InvalidOperationException("KEY_LIMIT_EXCEEDED");

        // 3. Guard: name uniqueness
        var nameExists = await keyRepository.ExistsNameAsync(
            command.Name, command.ConsumerId, command.Environment, command.TenantId, ct);
        if (nameExists)
            throw new InvalidOperationException("KEY_NAME_DUPLICATE");

        // 4. Guard: scopes
        if (!command.Scopes.Any())
            throw new InvalidOperationException("VALIDATION_ERROR:scopes_empty");

        var allScopesExist = await scopeRegistry.AllExistAsync(command.Scopes, ct);
        if (!allScopesExist)
            throw new InvalidOperationException("SCOPE_NOT_FOUND");

        // 5. Guard: expiry
        var now = DateTimeOffset.UtcNow;
        if (command.ExpiresAt <= now)
            throw new InvalidOperationException("VALIDATION_ERROR:expires_at_past");
        if (command.ExpiresAt > now.AddDays(ApiKey.GetMaxValidityDays()))
            throw new InvalidOperationException("EXPIRES_AT_EXCEEDS_MAX");

        // 6. Create AccessPolicy (I2) — gets policyId before creating key
        var policyId = await accessPolicyService.CreateDefaultPolicyAsync(
            Guid.NewGuid(), command.TenantId, ct);

        // 7. Create ApiKey aggregate
        var (apiKey, rawKey) = ApiKey.Create(
            command.ConsumerId,
            command.TenantId,
            command.Name,
            command.Environment,
            command.Scopes,
            command.ExpiresAt,
            policyId);

        // 8. Persist
        await keyRepository.SaveAsync(apiKey, ct);

        return new CreateApiKeyResponse(
            KeyId: apiKey.Id,
            ConsumerId: apiKey.ConsumerId,
            TenantId: apiKey.TenantId,
            Name: apiKey.Name,
            KeyPrefix: apiKey.KeyPrefix,
            RawKey: rawKey,
            Environment: apiKey.Environment,
            Scopes: apiKey.Scopes,
            LifecycleStatus: apiKey.Status.ToString(),
            PolicyId: apiKey.PolicyId,
            CreatedAt: apiKey.CreatedAt,
            ExpiresAt: apiKey.ExpiresAt);
    }
}
