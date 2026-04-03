using ApiKeyManagement.AccessPolicy.Domain;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.TenantManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApiKeyManagement.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), ITenantQueryContext
{
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<AccessPolicy.Domain.AccessPolicy> AccessPolicies => Set<AccessPolicy.Domain.AccessPolicy>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Consumer> Consumers => Set<Consumer>();
    public DbSet<ScopeRegistryEntry> ScopeRegistryEntries => Set<ScopeRegistryEntry>();

    // ITenantQueryContext implementation
    IQueryable<Tenant> ITenantQueryContext.Tenants => Tenants;
    IQueryable<Consumer> ITenantQueryContext.Consumers => Consumers;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

/// <summary>Reference data entry for the Scope Registry.</summary>
public record ScopeRegistryEntry(string ScopeName);
