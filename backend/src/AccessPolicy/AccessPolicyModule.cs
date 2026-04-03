using ApiKeyManagement.AccessPolicy.Application;
using ApiKeyManagement.SharedKernel.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ApiKeyManagement.AccessPolicy;

public static class AccessPolicyModule
{
    public static IServiceCollection AddAccessPolicyModule(this IServiceCollection services)
    {
        services.AddScoped<IAccessPolicyService, AccessPolicyService>();
        return services;
    }
}
