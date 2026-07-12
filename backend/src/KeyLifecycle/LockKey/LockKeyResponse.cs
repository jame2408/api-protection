using ApiKeyManagement.KeyLifecycle.Domain;

namespace ApiKeyManagement.KeyLifecycle.LockKey;

// Faithful I6 output (context-integration-spec.md §4.6) — no lifecycleStatus field, unlike
// other endpoints' responses; shape is deliberately different.
public record LockKeyResponse(
    Guid KeyId,
    ApiKeyStatus PreviousStatus,
    DateTimeOffset LockedAt
);
