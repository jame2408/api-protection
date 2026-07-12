using System.Security.Cryptography;
using System.Text.Json;
using ApiKeyManagement.KeyLifecycle.Domain.Events;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.Domain;

public class ApiKey : AggregateRoot<Guid>
{
    private const int MaxActiveKeysPerConsumerEnv = 10;
    private const int MaxKeyValidityDays = 365;

    public const string LeakedInPublicRepositoryReason = "Key leaked in public repository";

    public string ConsumerId { get; private set; } = string.Empty;
    public string TenantId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Environment { get; private set; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; private set; } = [];
    public ApiKeyStatus Status { get; private set; }
    public string KeyPrefix { get; private set; } = string.Empty;
    public string KeyHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid PolicyId { get; private set; }
    public Guid? SuccessorKeyId { get; private set; }
    public Guid? PredecessorKeyId { get; private set; }

    // EF Core
    private ApiKey() { }

    public static int GetMaxActiveKeys() => MaxActiveKeysPerConsumerEnv;
    public static int GetMaxValidityDays() => MaxKeyValidityDays;

    /// <summary>
    /// Creates a new ApiKey. Returns the aggregate and the raw key (display once).
    /// </summary>
    public static (ApiKey key, string rawKey) Create(
        string consumerId,
        string tenantId,
        string name,
        string environment,
        IReadOnlyList<string> scopes,
        DateTimeOffset expiresAt,
        Guid policyId,
        IApiKeyHasher hasher)
    {
        var keyId = Guid.NewGuid();
        var (prefix, rawKey, keyHash) = GenerateKeyMaterial(tenantId, environment, hasher);

        var key = new ApiKey
        {
            Id = keyId,
            ConsumerId = consumerId,
            TenantId = tenantId,
            Name = name,
            Environment = environment,
            Scopes = scopes,
            Status = ApiKeyStatus.Active,
            KeyPrefix = prefix,
            KeyHash = keyHash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            PolicyId = policyId,
        };

        key.AddDomainEvent(new KeyCreated(
            EventId: Guid.NewGuid(),
            OccurredAt: key.CreatedAt,
            KeyId: keyId,
            ConsumerId: consumerId,
            TenantId: tenantId,
            Name: name,
            Environment: environment,
            Scopes: scopes,
            KeyPrefix: prefix,
            ExpiresAt: expiresAt,
            PolicyId: policyId));

        return (key, rawKey);
    }

    /// <summary>
    /// Revokes the key. Captures the previous status for the KeyRevoked event; guards
    /// (not-found / empty reason / terminal state) are the handler's responsibility.
    /// </summary>
    public void Revoke(string reason, Actor revokedBy)
    {
        var previousStatus = Status;
        Status = ApiKeyStatus.Revoked;
        SuccessorKeyId = null;

        AddDomainEvent(new KeyRevoked(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            KeyId: Id,
            PreviousStatus: previousStatus.ToString(),
            Reason: reason,
            RevokedBy: revokedBy));
    }

    /// <summary>
    /// Suspends the key. Guards (not-found / non-Active status) are the handler's
    /// responsibility; mirrors <see cref="Revoke"/>.
    /// </summary>
    public void Suspend(string reason, Actor suspendedBy)
    {
        Status = ApiKeyStatus.Suspended;

        AddDomainEvent(new KeySuspended(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            KeyId: Id,
            SuspendedBy: suspendedBy,
            Reason: reason));
    }

    /// <summary>
    /// Locks the key following an automated anomaly-detection rule hit. Guards (not-found /
    /// non-Active status) are the handler's responsibility; mirrors <see cref="Suspend"/>.
    /// </summary>
    public void Lock(string ruleId, string reason, JsonElement evidence)
    {
        Status = ApiKeyStatus.Locked;

        AddDomainEvent(new KeyLocked(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            KeyId: Id,
            RuleId: ruleId,
            Reason: reason,
            Evidence: evidence));
    }

    /// <summary>
    /// Resumes the key. Guards (not-found / non-Suspended status) are the handler's
    /// responsibility; mirrors <see cref="Suspend"/>. No actor-type restriction — invariant 6
    /// ("human-only") only constrains Suspend, not Resume.
    /// </summary>
    public void Resume(Actor resumedBy)
    {
        Status = ApiKeyStatus.Active;

        AddDomainEvent(new KeyResumed(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            KeyId: Id,
            ResumedBy: resumedBy));
    }

    /// <summary>
    /// Clears the predecessor link on a rotation successor (used when the predecessor is
    /// revoked while Rotating — design-doc.md T6).
    /// </summary>
    public void ClearPredecessorLink() => PredecessorKeyId = null;

    /// <summary>
    /// Records that this key must be flagged to Security Admin and Consumer following a
    /// Secret Scanner leak detection (RevokeLeakedKeys slice) — separate from the KeyRevoked
    /// event so the revoke path itself stays reason-agnostic.
    /// </summary>
    public void NotifyLeakDetected() =>
        AddDomainEvent(new KeyLeakNotificationRequested(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            KeyId: Id,
            Audiences: ["SecurityAdmin", "Consumer"]));

    private static (string prefix, string rawKey, string keyHash) GenerateKeyMaterial(
        string tenantId, string environment, IApiKeyHasher hasher)
    {
        // prefix: apk_{first4ofTenant}_{envAbbr}
        var tenantAbbr = (tenantId.Length >= 4 ? tenantId[..4] : tenantId).ToLowerInvariant();
        var envAbbr = environment.ToLowerInvariant() switch
        {
            "production" => "prod",
            "sandbox" => "sbx",
            _ => environment.ToLowerInvariant()[..Math.Min(4, environment.Length)]
        };
        var prefix = $"apk_{tenantAbbr}_{envAbbr}";

        // random body: 32 hex chars (128-bit entropy — ADR-017 Implementation Rule 1)
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        var randomHex = Convert.ToHexString(randomBytes).ToLowerInvariant();

        // checksum: first 4 hex chars of SHA256 of the combined
        var combined = $"{prefix}_{randomHex}";
        var checksumBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(combined));
        var checksum = Convert.ToHexString(checksumBytes)[..4].ToLowerInvariant();

        var rawKey = $"{combined}_{checksum}";

        // hash: HMAC-SHA256 + server-side pepper (ADR-017 Implementation Rule 3)
        var keyHash = hasher.ComputeHash(rawKey);

        return (prefix, rawKey, keyHash);
    }
}
