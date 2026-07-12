namespace ApiKeyManagement.KeyLifecycle.CompleteGracePeriod;

// Always Ok — a scan finding zero Rotating keys past their deadline is a successful run of zero
// completions, not a failure (no negative-scan scenario exists to route through Failure).
public record CompleteGracePeriodScanResponse(
    int CompletedCount
);
