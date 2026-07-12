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

    // INV-4 guard (RotateKeyHandler, status guard 之後) — api-spec.md §3.2.4 Errors 表既有列，
    // rotate 首個專屬碼（非共用字面值，ApiProblem.Map 首度註冊）。
    public const string RotationInProgress = "ROTATION_IN_PROGRESS";

    // api-spec.md §3.2.4 Errors table also lists KEY_ALREADY_EXPIRED — deferred to its scenario
    // (no red to drive it yet in this scenario's guard chain; detailed-design §6.2 guard order:
    // status → 未到期 → 無其他 Rotating, to be inserted between status and the INV-4 guard above
    // when that scenario lands).
}
