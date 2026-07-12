using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.KeyLifecycle.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ApiKeyManagement.FunctionalTests.Infrastructure;

/// <summary>
/// Shared aliased-seed-key creation for Given steps that need a bare ApiKey row as a starting
/// point. Handlers under test in this slice validate neither tenant nor consumer existence
/// (RevokeKeyHandler.cs et al.), so no Tenant/Consumer row is seeded here — CurrentTenantId is
/// only needed as a valid URL segment.
/// </summary>
public static class ApiKeySeeding
{
    /// <summary>
    /// Creates and adds an ApiKey (environment "Production", scopes ["seed:read"]) to the
    /// scenario's DbContext and, unless <paramref name="register"/> is false, registers it under
    /// <paramref name="keyAlias"/> in ctx.SeededKeys. Does not call SaveChangesAsync — the caller
    /// owns the save (and any post-seed CurrentValue overrides needed to bypass ApiKey's private
    /// setters, e.g. to force a non-Active status).
    /// </summary>
    public static ApiKey AddSeedKey(
        this FunctionalTestContext ctx,
        string keyAlias,
        string consumerId = "any-consumer",
        DateTimeOffset? expiresAt = null,
        bool register = true)
    {
        var db = ctx.ServiceScope!.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = ctx.ServiceScope!.ServiceProvider.GetRequiredService<IApiKeyHasher>();

        var (key, _) = ApiKey.Create(
            consumerId: consumerId,
            tenantId: ctx.CurrentTenantId,
            name: keyAlias,
            environment: "Production",
            scopes: ["seed:read"],
            expiresAt: expiresAt ?? DateTimeOffset.UtcNow.AddDays(30),
            policyId: Guid.NewGuid(),
            hasher: hasher);

        db.ApiKeys.Add(key);

        if (register)
        {
            ctx.SeededKeys[keyAlias] = key.Id;
        }

        return key;
    }
}
