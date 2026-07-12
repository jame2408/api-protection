namespace ApiKeyManagement.KeyLifecycle.RotateKey;

public static class RotateKeyFailureCodes
{
    // Wire values are the shared generic codes (api-spec.md §2.2 / §3.2.4 Errors table) — reuse
    // the same literals as RevokeKeyFailureCodes.KeyNotFound / SuspendKeyFailureCodes
    // .InvalidStateTransition; ApiProblem.Map must not register these keys twice.
    public const string KeyNotFound = "NOT_FOUND";
    public const string InvalidStateTransition = "INVALID_STATE_TRANSITION";

    // Consumer（限自身金鑰）ownership guard (api-spec.md §3.2.4 Errors 表), red-driven by
    // "Consumer 輪替非自身金鑰 — 拒絕". Same wire literal as ApiProblem.ForbiddenCode — reuse,
    // do not add a second ApiProblem.Map entry for "FORBIDDEN" (duplicate dictionary key throws
    // at static init; mirrors ResumeKeyFailureCodes' reuse-not-redeclare pattern).
    public const string Forbidden = "FORBIDDEN";

    // api-spec.md §3.2.4 Errors table also lists ROTATION_IN_PROGRESS (INV-4) and
    // KEY_ALREADY_EXPIRED — deferred to scenarios 36/37 (no red to drive them yet in this
    // scenario's guard chain; detailed-design §6.2 guard order: status → 未到期 → 無其他
    // Rotating, to be inserted at that position when their scenarios land).
}
