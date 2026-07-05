namespace ApiKeyManagement.KeyLifecycle.RevokeKey;

public record RevokeKeyCommand(
    string TenantId,
    Guid KeyId,
    string Reason
);
