namespace ApiKeyManagement.KeyLifecycle.CreateApiKey;

public record CreateApiKeyResponse(
    Guid KeyId,
    string ConsumerId,
    string TenantId,
    string Name,
    string KeyPrefix,
    string RawKey,
    string Environment,
    IReadOnlyList<string> Scopes,
    string LifecycleStatus,
    Guid PolicyId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt
);
