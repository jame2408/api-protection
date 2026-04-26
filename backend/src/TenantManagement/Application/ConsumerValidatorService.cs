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
            return new ConsumerValidationResult(false, ConsumerValidationFailureCodes.TenantNotFound);

        if (tenant.Status == TenantStatus.Suspended)
            return new ConsumerValidationResult(false, ConsumerValidationFailureCodes.TenantSuspended);

        var consumer = await db.Consumers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == consumerId && c.TenantId == tenantId, cancel);

        if (consumer is null)
            return new ConsumerValidationResult(false, ConsumerValidationFailureCodes.ConsumerNotFound);

        return new ConsumerValidationResult(true);
    }
}
