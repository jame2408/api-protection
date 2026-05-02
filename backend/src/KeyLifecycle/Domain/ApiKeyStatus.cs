namespace ApiKeyManagement.KeyLifecycle.Domain;

public enum ApiKeyStatus
{
    Active,
    Rotating,
    Locked,
    Suspended,
    Revoked,
    Expired
}
