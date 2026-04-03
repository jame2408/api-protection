using ApiKeyManagement.TenantManagement.Domain;

namespace ApiKeyManagement.TenantManagement.Domain;

/// <summary>
/// Minimal read-only view of TenantManagement data.
/// Implemented by AppDbContext in Infrastructure.
/// </summary>
public interface ITenantQueryContext
{
    IQueryable<Tenant> Tenants { get; }
    IQueryable<Consumer> Consumers { get; }
}
