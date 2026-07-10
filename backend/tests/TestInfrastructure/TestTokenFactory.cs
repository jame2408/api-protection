using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApiKeyManagement.TestInfrastructure;

/// <summary>
/// Single signing point for functional-test JWTs (ADR-024 §5 Rule 5: step definitions must
/// not assemble tokens themselves). Signs with the same symmetric key
/// <see cref="ApiKeyManagementWebApplicationFactory"/> injects into the test host via
/// "Jwt__SigningKey" — same pattern as the ApiKeyHashing__Pepper test constant.
/// </summary>
public static class TestTokenFactory
{
    // ADR-024 §5: the one place this Base64 (32-byte) test signing key is declared;
    // ApiKeyManagementWebApplicationFactory reads this same constant for env var injection.
    // Decodes to the 32-byte ASCII string "functional-test-jwt-signing-32b!" (same
    // fake-secret pattern as ApiKeyHashing__Pepper — not a real secret).
    public const string SigningKeyBase64 = "ZnVuY3Rpb25hbC10ZXN0LWp3dC1zaWduaW5nLTMyYiE=";

    private static readonly JsonWebTokenHandler Handler = new();

    private static readonly SigningCredentials Credentials = new(
        new SymmetricSecurityKey(Convert.FromBase64String(SigningKeyBase64)),
        SecurityAlgorithms.HmacSha256);

    /// <summary>SecurityAdmin token — api-spec.md §2.1 role with cross-tenant alert/audit-log access.</summary>
    public static string CreateSecurityAdminToken(
        string sub = "security-admin-1", string? name = null) =>
        CreateToken(sub, role: "SecurityAdmin", tenantId: null, consumerId: null, name);

    /// <summary>TenantAdmin token — scoped to a single tenant's own resources.</summary>
    public static string CreateTenantAdminToken(
        string sub = "tenant-admin-1", string tenantId = "tenant-1", string? name = null) =>
        CreateToken(sub, role: "TenantAdmin", tenantId, consumerId: null, name);

    /// <summary>Consumer token — scoped to a single tenant + consumer's own keys.</summary>
    public static string CreateConsumerToken(
        string sub = "consumer-1",
        string tenantId = "tenant-1",
        string consumerId = "consumer-1",
        string? name = null) =>
        CreateToken(sub, role: "Consumer", tenantId, consumerId, name);

    /// <summary>
    /// System (non-human) actor token — ADR-024 §2: passes authentication; "human-only" business
    /// guards (e.g. Suspend) reject it as a business Failure, not a 403.
    /// </summary>
    public static string CreateSystemToken(
        string sub = "monitoring-service", string? name = null) =>
        CreateToken(sub, role: "System", tenantId: null, consumerId: null, name);

    private static string CreateToken(
        string sub, string role, string? tenantId, string? consumerId, string? name)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = sub,
            ["role"] = role
        };

        if (tenantId is not null)
        {
            claims["tenantId"] = tenantId;
        }

        if (consumerId is not null)
        {
            claims["consumerId"] = consumerId;
        }

        if (name is not null)
        {
            claims["name"] = name;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = Credentials
        };

        return Handler.CreateToken(descriptor);
    }
}
