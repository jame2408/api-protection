using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.RevokeLeakedKeys;

public record RevokeLeakedKeysCommand(string KeyPrefix, Actor RevokedBy);
