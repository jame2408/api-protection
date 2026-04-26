namespace ApiKeyManagement.KeyLifecycle.CreateApiKey;

public static class CreateApiKeyFailureCodes
{
    public const string KeyLimitExceeded = "KEY_LIMIT_EXCEEDED";
    public const string KeyNameDuplicate = "KEY_NAME_DUPLICATE";
    public const string ScopeNotFound = "SCOPE_NOT_FOUND";
    public const string ExpiresAtExceedsMax = "EXPIRES_AT_EXCEEDS_MAX";
    public const string ValidationErrorScopesEmpty = "VALIDATION_ERROR:scopes_empty";
    public const string ValidationErrorExpiresAtPast = "VALIDATION_ERROR:expires_at_past";
    public const string ValidationErrorPrefix = "VALIDATION_ERROR";
}
