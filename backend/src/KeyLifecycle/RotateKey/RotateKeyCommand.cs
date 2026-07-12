namespace ApiKeyManagement.KeyLifecycle.RotateKey;

public record RotateKeyCommand(
    string TenantId,
    Guid KeyId,
    TimeSpan? GracePeriod,
    // api-spec.md §3.2.4 Authorization row: Consumer（限自身金鑰）. Null for TenantAdmin /
    // SecurityAdmin tokens (no consumerId claim) — those roles are not held to the "own key"
    // restriction, only Consumer is (RotateKeyHandler guard).
    string? ActorConsumerId
);
