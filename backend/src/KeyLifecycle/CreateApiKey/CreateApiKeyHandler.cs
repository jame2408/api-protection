using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Contracts;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.CreateApiKey;

public class CreateApiKeyHandler(
    IConsumerValidator consumerValidator,
    IApiKeyRepository keyRepository,
    IScopeRegistry scopeRegistry,
    IAccessPolicyService accessPolicyService,
    IApiKeyHasher keyHasher
) : ICreateApiKeyHandler
{
    public async Task<Result<CreateApiKeyResponse, Failure>> HandleAsync(
        CreateApiKeyCommand command, CancellationToken cancel = default)
    {
        // 1. Validate tenant + consumer (I1)
        var validation = await consumerValidator.ValidateAsync(command.TenantId, command.ConsumerId, cancel);
        if (!validation.IsValid)
            return FailureProvider.CreateFailure(validation.ErrorCode!);

        // 2. Guard: active key count
        var activeCount = await keyRepository.CountActiveAsync(
            command.ConsumerId, command.Environment, command.TenantId, cancel);
        if (activeCount >= ApiKey.GetMaxActiveKeys())
            return FailureProvider.CreateFailure(CreateApiKeyFailureCodes.KeyLimitExceeded);

        // 3. Guard: name uniqueness
        var nameExists = await keyRepository.ExistsNameAsync(
            command.Name, command.ConsumerId, command.Environment, command.TenantId, cancel);
        if (nameExists)
            return FailureProvider.CreateFailure(CreateApiKeyFailureCodes.KeyNameDuplicate);

        // 4. Guard: scopes
        if (!command.Scopes.Any())
            return FailureProvider.CreateFailure(CreateApiKeyFailureCodes.ValidationErrorScopesEmpty);

        var allScopesExist = await scopeRegistry.AllExistAsync(command.Scopes, cancel);
        if (!allScopesExist)
            return FailureProvider.CreateFailure(CreateApiKeyFailureCodes.ScopeNotFound);

        // 5. Guard: expiry
        var now = DateTimeOffset.UtcNow;
        if (command.ExpiresAt <= now)
            return FailureProvider.CreateFailure(CreateApiKeyFailureCodes.ValidationErrorExpiresAtPast);
        if (command.ExpiresAt > now.AddDays(ApiKey.GetMaxValidityDays()))
            return FailureProvider.CreateFailure(CreateApiKeyFailureCodes.ExpiresAtExceedsMax);

        // 6. Create AccessPolicy (I2) — gets policyId before creating key
        var policyId = await accessPolicyService.CreateDefaultPolicyAsync(
            Guid.NewGuid(), command.TenantId, cancel);

        // 7. Create ApiKey aggregate
        var (apiKey, rawKey) = ApiKey.Create(
            command.ConsumerId,
            command.TenantId,
            command.Name,
            command.Environment,
            command.Scopes,
            command.ExpiresAt,
            policyId,
            keyHasher);

        // 8. Persist
        await keyRepository.SaveAsync(apiKey, cancel);

        return new CreateApiKeyResponse(
            KeyId: apiKey.Id,
            ConsumerId: apiKey.ConsumerId,
            TenantId: apiKey.TenantId,
            Name: apiKey.Name,
            KeyPrefix: apiKey.KeyPrefix,
            // Display-safe suffix for identifying the key after rawKey is shown once (api-spec.md §2.2).
            TruncatedKey: "..." + rawKey[^4..],
            RawKey: rawKey,
            Environment: apiKey.Environment,
            Scopes: apiKey.Scopes,
            LifecycleStatus: apiKey.Status,
            PolicyId: apiKey.PolicyId,
            CreatedAt: apiKey.CreatedAt,
            ExpiresAt: apiKey.ExpiresAt);
    }
}
