using ApiKeyManagement.KeyLifecycle.Domain;

namespace ApiKeyManagement.KeyLifecycle.ResumeKey;

public record ResumeKeyResponse(
    Guid KeyId,
    ApiKeyStatus LifecycleStatus,
    string ResumedBy
);
