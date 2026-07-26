namespace ApiKeyManagement.KeyLifecycle.ValidateKey;

// api-spec.md §4.1 成功回應欄位表. `rateLimitConfig` deliberately omitted this round — 2026-07-26
// 裁決（07_ValidateKey.feature 場景註解）：AccessPolicy 聚合尚無該欄位，預設值也無規格定義；
// 收攏至 Wave 9。`keyHash` never appears here — ADR-029 §3 / Implementation Rule 3.
public record ValidateKeyResponse(
    bool Valid,
    Guid KeyId,
    string TenantId,
    string ConsumerId,
    string Environment,
    IReadOnlyList<string> Scopes
);
