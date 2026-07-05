using ApiKeyManagement.AccessPolicy.Domain;
using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.Infrastructure.Persistence.Repositories;
using ApiKeyManagement.Infrastructure.Security;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.TenantManagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiKeyManagement.Infrastructure;

public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

        services.AddDbContextPool<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Register ITenantQueryContext (AppDbContext implements it)
        services.AddScoped<ITenantQueryContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Repositories
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IAccessPolicyRepository, AccessPolicyRepository>();
        services.AddScoped<IScopeRegistry, ScopeRegistryService>();

        // ADR-017: HMAC-SHA256 + pepper key hasher; options validation runs at first resolution (fail-fast).
        services.AddOptions<ApiKeyHashingOptions>().Bind(configuration.GetSection(ApiKeyHashingOptions.SectionName));
        services.AddSingleton<IApiKeyHasher, HmacApiKeyHasher>();

        // Clock injection for deterministic expiry-boundary tests.
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
