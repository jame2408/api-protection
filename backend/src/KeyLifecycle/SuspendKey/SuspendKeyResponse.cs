using ApiKeyManagement.KeyLifecycle.Domain;

namespace ApiKeyManagement.KeyLifecycle.SuspendKey;

public record SuspendKeyResponse(
    Guid KeyId,
    ApiKeyStatus LifecycleStatus,
    string SuspendedBy,
    string Reason
);
