namespace ApiKeyManagement.KeyLifecycle.ExpireKey;

// Always Ok — a scan finding zero due keys is a successful run of zero transitions, not a
// failure (mirrors CompleteGracePeriodScanResponse's reasoning).
//
// Named "Processed" rather than "Expired": scenario 46 ("Locked 金鑰到期 — 轉為 Revoked 以保留安全
// 上下文", deferred — see ApiKey.Expire's doc comment) will make this scan's Locked branch
// produce a KeyRevoked event, not KeyExpired, so a count named "ExpiredCount" would misdescribe
// that branch once it lands.
public record ExpireKeyScanResponse(
    int ProcessedCount
);
