using ApiKeyManagement.KeyLifecycle.Domain;

namespace ApiKeyManagement.KeyLifecycle.UnlockKey;

public record UnlockKeyResponse(
    Guid KeyId,
    ApiKeyStatus LifecycleStatus,
    string UnlockedBy
);
