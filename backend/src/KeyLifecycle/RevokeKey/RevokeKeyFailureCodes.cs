namespace ApiKeyManagement.KeyLifecycle.RevokeKey;

public static class RevokeKeyFailureCodes
{
    // Wire value is the shared generic code (api-spec.md §2.2) — this scenario has no
    // BC-specific "key not found" variant, unlike TENANT_NOT_FOUND / CONSUMER_NOT_FOUND.
    public const string KeyNotFound = "NOT_FOUND";
    public const string KeyInTerminalState = "KEY_IN_TERMINAL_STATE";
    public const string ValidationErrorReasonEmpty = "VALIDATION_ERROR:reason_empty";
    public const string ValidationErrorPrefix = "VALIDATION_ERROR";
}
