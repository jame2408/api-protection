using System.Text.Json;

namespace ApiKeyManagement.KeyLifecycle.LockKey;

public record LockKeyCommand(
    string TenantId,
    Guid KeyId,
    string RuleId,
    string Severity,
    string Reason,
    DateTimeOffset DetectedAt,
    JsonElement Evidence);
