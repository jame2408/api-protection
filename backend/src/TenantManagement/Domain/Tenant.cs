using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.TenantManagement.Domain;

public enum TenantStatus { Active, Suspended }

public class Tenant : AggregateRoot<string>
{
    public TenantStatus Status { get; private set; }

    private Tenant() { }

    public Tenant(string tenantId, TenantStatus status) : base(tenantId)
    {
        Status = status;
    }
}
