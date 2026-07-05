using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiKeyManagement.TestInfrastructure;

/// <summary>
/// Custom WebApplicationFactory that wires the Host to test containers.
/// Consumed by all BC test projects via Reqnroll [BeforeTestRun] hooks.
/// </summary>
public class ApiKeyManagementWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _rabbitMqConnectionString;

    public ApiKeyManagementWebApplicationFactory(
        string postgresConnectionString,
        string rabbitMqConnectionString)
    {
        _postgresConnectionString = postgresConnectionString;
        _rabbitMqConnectionString = rabbitMqConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set env vars before the host reads configuration.
        // ASP.NET Core env vars (format: Key__NestedKey) override all appsettings files.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _postgresConnectionString);
        Environment.SetEnvironmentVariable("RabbitMq__Host", _rabbitMqConnectionString);
        // ADR-017: fake pepper for functional tests (not a secret — same pattern as connection strings above).
        Environment.SetEnvironmentVariable(
            "ApiKeyHashing__Pepper",
            Convert.ToBase64String("functional-test-pepper-32bytes!!"u8));

        builder.ConfigureServices(services =>
        {
            // Freeze TimeProvider so expiry-boundary scenarios share one deterministic
            // "now" between the test's When step and the handler's guard (see FrozenTimeProvider).
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FrozenTimeProvider());
        });
    }
}
