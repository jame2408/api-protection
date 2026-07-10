using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.SuspendKey;

public record SuspendKeyCommand(
    string TenantId,
    Guid KeyId,
    string Reason,
    Actor SuspendedBy
);
