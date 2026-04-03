using ApiKeyManagement.KeyLifecycle.CreateApiKey;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ApiKeyManagement.KeyLifecycle;

public static class KeyLifecycleModule
{
    public static IServiceCollection AddKeyLifecycleModule(this IServiceCollection services)
    {
        services.AddScoped<ICreateApiKeyHandler, CreateApiKeyHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapKeyLifecycleEndpoints(this IEndpointRouteBuilder app)
    {
        CreateApiKeyEndpoint.Map(app);
        return app;
    }
}
