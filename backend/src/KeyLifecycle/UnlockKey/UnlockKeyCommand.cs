using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.UnlockKey;

public record UnlockKeyCommand(
    string TenantId,
    Guid KeyId,
    Actor UnlockedBy
);
