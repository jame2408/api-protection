using ApiKeyManagement.KeyLifecycle.Domain;

namespace ApiKeyManagement.KeyLifecycle.RevokeKey;

// revokedBy omitted this pass — repo has no auth/actor infrastructure yet (debt tracked separately).
public record RevokeKeyResponse(
    Guid KeyId,
    ApiKeyStatus PreviousStatus,
    ApiKeyStatus LifecycleStatus,
    string Reason
);
