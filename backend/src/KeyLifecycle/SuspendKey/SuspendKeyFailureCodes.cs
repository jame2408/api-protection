namespace ApiKeyManagement.KeyLifecycle.SuspendKey;

public static class SuspendKeyFailureCodes
{
    // Wire value is the shared generic code (api-spec.md §2.2) — reuses the same literal as
    // RevokeKeyFailureCodes.KeyNotFound; ApiProblem.Map must not register this key twice.
    public const string KeyNotFound = "NOT_FOUND";
    public const string InvalidStateTransition = "INVALID_STATE_TRANSITION";
    public const string ValidationErrorReasonEmpty = "VALIDATION_ERROR:reason_empty";
    public const string ValidationErrorPrefix = "VALIDATION_ERROR";
}
