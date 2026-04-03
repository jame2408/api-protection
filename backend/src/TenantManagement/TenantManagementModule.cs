using ApiKeyManagement.SharedKernel.Contracts;
using ApiKeyManagement.TenantManagement.Application;
using Microsoft.Extensions.DependencyInjection;

namespace ApiKeyManagement.TenantManagement;

public static class TenantManagementModule
{
    public static IServiceCollection AddTenantManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IConsumerValidator, ConsumerValidatorService>();
        return services;
    }
}
