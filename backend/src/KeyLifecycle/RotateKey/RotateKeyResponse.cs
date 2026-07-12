using ApiKeyManagement.KeyLifecycle.Domain;

namespace ApiKeyManagement.KeyLifecycle.RotateKey;

// Nested oldKey/newKey shape per api-spec.md §3.2.4 response example.
public record RotateKeyResponse(
    RotateKeyResponse.OldKeyInfo OldKey,
    RotateKeyResponse.NewKeyInfo NewKey)
{
    public record OldKeyInfo(
        Guid KeyId,
        ApiKeyStatus LifecycleStatus,
        DateTimeOffset GraceDeadline,
        Guid SuccessorKeyId
    );

    public record NewKeyInfo(
        Guid KeyId,
        string Name,
        string KeyPrefix,
        string TruncatedKey,
        string Environment,
        IReadOnlyList<string> Scopes,
        ApiKeyStatus LifecycleStatus,
        Guid PolicyId,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt,
        Guid PredecessorKeyId,
        // Display-safe once — rawKey never appears in any other response (api-spec.md §3.2.4).
        string RawKey
    );
}
