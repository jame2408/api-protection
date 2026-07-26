namespace ApiKeyManagement.KeyLifecycle.ValidateKey;

// api-spec.md §4.1「Response — 驗證失敗」：扁平 body，非 RFC 9457（2026-07-26 裁決，
// ValidateKeyEndpoint 註解）。`detail` 欄位本輪不實作 — 比照 ValidateKeyResponse 的
// rateLimitConfig 處置：它只出現在 api-spec 的範例 JSON，失敗回應沒有欄位表定義它（不像成功
// 回應有），且無場景斷言；登記後留待有場景時再加。
public record ValidateKeyFailureResponse(
    bool Valid,
    string ErrorCode,
    int HttpStatusHint
);
