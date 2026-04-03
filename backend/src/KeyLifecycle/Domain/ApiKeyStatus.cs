namespace ApiKeyManagement.KeyLifecycle.Domain;

public enum ApiKeyStatus
{
    ACTIVE,
    ROTATING,
    LOCKED,
    SUSPENDED,
    REVOKED,
    EXPIRED
}
