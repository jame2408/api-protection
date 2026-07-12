namespace ApiKeyManagement.KeyLifecycle.LockKey;

public static class LockKeyFailureCodes
{
    // Wire values are the shared generic codes (api-spec.md §2.2) — same literals as
    // RevokeKeyFailureCodes.KeyNotFound / SuspendKeyFailureCodes.InvalidStateTransition /
    // ResumeKeyFailureCodes.KeyNotFound / ResumeKeyFailureCodes.InvalidStateTransition.
    // ApiProblem.Map already registers these two string keys; do NOT add another dictionary
    // entry for them here (duplicate key throws at static-init time).
    public const string KeyNotFound = "NOT_FOUND";
    public const string InvalidStateTransition = "INVALID_STATE_TRANSITION";
}
