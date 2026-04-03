using ApiKeyManagement.SharedKernel.Contracts;
using ApiKeyManagement.TenantManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApiKeyManagement.TenantManagement.Application;

public class ConsumerValidatorService(ITenantQueryContext db) : IConsumerValidator
{
    public async Task<ConsumerValidationResult> ValidateAsync(
        string tenantId, string consumerId, CancellationToken cancel = default)
    {
        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancel);

        if (tenant is null)
            return new ConsumerValidationResult(false, "TENANT_NOT_FOUND");

        if (tenant.Status == TenantStatus.Suspended)
            return new ConsumerValidationResult(false, "TENANT_SUSPENDED");

        var consumer = await db.Consumers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == consumerId && c.TenantId == tenantId, cancel);

        if (consumer is null)
            return new ConsumerValidationResult(false, "CONSUMER_NOT_FOUND");

        return new ConsumerValidationResult(true);
    }
}
