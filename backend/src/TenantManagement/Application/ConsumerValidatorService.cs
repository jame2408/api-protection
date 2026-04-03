using ApiKeyManagement.SharedKernel.Contracts;
using ApiKeyManagement.TenantManagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApiKeyManagement.TenantManagement.Application;

public class ConsumerValidatorService(IServiceScopeFactory scopeFactory) : IConsumerValidator
{
    public async Task<ConsumerValidationResult> ValidateAsync(
        string tenantId, string consumerId, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ITenantQueryContext>();

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        if (tenant is null)
            return new ConsumerValidationResult(false, "TENANT_NOT_FOUND");

        if (tenant.Status == TenantStatus.Suspended)
            return new ConsumerValidationResult(false, "TENANT_SUSPENDED");

        var consumer = await db.Consumers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == consumerId && c.TenantId == tenantId, ct);

        if (consumer is null)
            return new ConsumerValidationResult(false, "CONSUMER_NOT_FOUND");

        return new ConsumerValidationResult(true);
    }
}
