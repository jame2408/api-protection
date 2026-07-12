namespace ApiKeyManagement.KeyLifecycle.CompleteGracePeriod;

public static class CompleteGracePeriodFailureCodes
{
    // Wire values are the shared generic codes (api-spec.md §2.2) — reuse the same literals as
    // RotateKeyFailureCodes.KeyNotFound / .InvalidStateTransition. This slice has no HTTP
    // endpoint (api-spec.md §3.1 does not expose it, §3.4 matrix: System Agent Job), so there is
    // no ApiProblem.Map entry to worry about duplicating — these codes only ever surface via the
    // Result<T, Failure> returned to the (future) per-key command caller.
    public const string KeyNotFound = "NOT_FOUND";
    public const string InvalidStateTransition = "INVALID_STATE_TRANSITION";
}
