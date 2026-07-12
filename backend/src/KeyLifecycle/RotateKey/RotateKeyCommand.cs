namespace ApiKeyManagement.KeyLifecycle.RotateKey;

public record RotateKeyCommand(
    string TenantId,
    Guid KeyId,
    TimeSpan? GracePeriod
);
