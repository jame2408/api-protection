namespace ApiKeyManagement.KeyLifecycle.CreateApiKey;

public record CreateApiKeyCommand(
    string TenantId,
    string ConsumerId,
    string Name,
    string Environment,
    IReadOnlyList<string> Scopes,
    DateTimeOffset ExpiresAt
);
