using ApiKeyManagement.KeyLifecycle.CreateApiKey;
using ApiKeyManagement.KeyLifecycle.LockKey;
using ApiKeyManagement.KeyLifecycle.ResumeKey;
using ApiKeyManagement.KeyLifecycle.RevokeKey;
using ApiKeyManagement.KeyLifecycle.RevokeLeakedKeys;
using ApiKeyManagement.KeyLifecycle.SuspendKey;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ApiKeyManagement.KeyLifecycle;

public static class KeyLifecycleModule
{
    public static IServiceCollection AddKeyLifecycleModule(this IServiceCollection services)
    {
        services.AddScoped<ICreateApiKeyHandler, CreateApiKeyHandler>();
        services.AddScoped<IRevokeKeyHandler, RevokeKeyHandler>();
        services.AddScoped<IRevokeLeakedKeysHandler, RevokeLeakedKeysHandler>();
        services.AddScoped<ISuspendKeyHandler, SuspendKeyHandler>();
        services.AddScoped<IResumeKeyHandler, ResumeKeyHandler>();
        services.AddScoped<ILockKeyHandler, LockKeyHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapKeyLifecycleEndpoints(this IEndpointRouteBuilder app)
    {
        CreateApiKeyEndpoint.Map(app);
        RevokeKeyEndpoint.Map(app);
        RevokeLeakedKeysEndpoint.Map(app);
        SuspendKeyEndpoint.Map(app);
        ResumeKeyEndpoint.Map(app);
        LockKeyEndpoint.Map(app);
        return app;
    }
}
