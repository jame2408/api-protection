using ApiKeyManagement.Infrastructure.Persistence;
using ApiKeyManagement.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reqnroll;
using Respawn;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace ApiKeyManagement.FunctionalTests.Infrastructure;

/// <summary>
/// Reqnroll lifecycle hooks for the FunctionalTests assembly.
///
/// Container lifecycle:
///   [BeforeTestRun]  — start PostgreSQL + RabbitMQ containers once, build WebApplicationFactory
///   [AfterTestRun]   — stop and dispose containers
///
/// Scenario lifecycle:
///   [BeforeScenario] — create HttpClient + DI scope, set on ScenarioContext
///   [AfterScenario]  — reset DB via Respawn, dispose HttpClient + DI scope
/// </summary>
[Binding]
public class TestHooks
{
    // --- Assembly-scoped (static) resources ---
    private static PostgreSqlContainer _postgres = null!;
    private static RabbitMqContainer _rabbitmq = null!;
    private static ApiKeyManagementWebApplicationFactory _factory = null!;
    private static Respawner _respawner = null!;

    public static ApiKeyManagementWebApplicationFactory Factory => _factory;
    public static string PostgresConnectionString => _postgres.GetConnectionString();

    // --- Scenario-scoped (injected) ---
    private readonly FunctionalTestContext _context;

    public TestHooks(FunctionalTestContext context)
    {
        _context = context;
    }

    // -------------------------------------------------------------------------
    // Test Run lifecycle
    // -------------------------------------------------------------------------

    [BeforeTestRun]
    public static async Task StartInfrastructureAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .Build();

        _rabbitmq = new RabbitMqBuilder("rabbitmq:4-alpine")
            .Build();

        await Task.WhenAll(_postgres.StartAsync(), _rabbitmq.StartAsync());

        _factory = new ApiKeyManagementWebApplicationFactory(
            _postgres.GetConnectionString(),
            _rabbitmq.GetConnectionString());

        // Run EF Core migrations
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Initialise Respawn checkpoint
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres
        });
    }

    [AfterTestRun]
    public static async Task StopInfrastructureAsync()
    {
        await _factory.DisposeAsync();
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _rabbitmq.DisposeAsync().AsTask());
    }

    // -------------------------------------------------------------------------
    // Scenario lifecycle
    // -------------------------------------------------------------------------

    [BeforeScenario]
    public void SetupScenario()
    {
        _context.Client = _factory.CreateClient();
        _context.ServiceScope = _factory.Services.CreateScope();
    }

    [AfterScenario]
    public async Task TeardownScenarioAsync()
    {
        _context.ServiceScope?.Dispose();

        // Reset DB to clean state between scenarios
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);

        _context.Client.Dispose();
        if (_context.Response is not null)
            _context.Response.Dispose();
    }
}
