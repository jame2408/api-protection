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

    // Expired guard (RotateKeyHandler, status guard 之後、INV-4 guard 之前，detailed-design §6.2
    // guard order) — api-spec.md §3.2.4 Errors 表既有列，rotate 第二個專屬碼
    // （ApiProblem.Map 註冊 409）。
    public const string KeyAlreadyExpired = "KEY_ALREADY_EXPIRED";
}
