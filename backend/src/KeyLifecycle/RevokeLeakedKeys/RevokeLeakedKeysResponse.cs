using ApiKeyManagement.KeyLifecycle.Domain;

namespace ApiKeyManagement.KeyLifecycle.RevokeLeakedKeys;

public record RevokeLeakedKeysResponse(IReadOnlyList<RevokedKeySummary> RevokedKeys);

public record RevokedKeySummary(Guid KeyId, ApiKeyStatus LifecycleStatus);
