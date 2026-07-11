using ApiKeyManagement.KeyLifecycle.Domain;

namespace ApiKeyManagement.KeyLifecycle.RevokeKey;

public record RevokeKeyResponse(
    Guid KeyId,
    ApiKeyStatus PreviousStatus,
    ApiKeyStatus LifecycleStatus,
    string RevokedBy,
    string Reason
);
