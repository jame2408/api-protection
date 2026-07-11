using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.ResumeKey;

public record ResumeKeyCommand(
    string TenantId,
    Guid KeyId,
    Actor ResumedBy
);
