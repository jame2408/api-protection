using Microsoft.Extensions.DependencyInjection;

namespace ApiKeyManagement.FunctionalTests.Infrastructure;

/// <summary>
/// Shared state passed between Given / When / Then step definitions within a scenario.
/// Reqnroll injects this via its built-in DI container (new instance per scenario).
/// </summary>
public class FunctionalTestContext
{
    /// <summary>HTTP client pointed at the test host. Set by [BeforeScenario].</summary>
    public HttpClient Client { get; set; } = null!;

    /// <summary>The raw HTTP response from the most recent When step.</summary>
    public HttpResponseMessage? Response { get; set; }

    /// <summary>Lazily-read response body string.</summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// Bearer token for the current actor role.
    /// Step definitions swap this out to simulate Consumer / Security Admin / System.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>The current tenant ID set by Given steps.</summary>
    public string CurrentTenantId { get; set; } = string.Empty;

    /// <summary>Expiry days set by a Given step; consumed by the When step.</summary>
    public int ExpiresInDays { get; set; }

    /// <summary>DI scope opened for the scenario; used by step definitions to resolve DbContext.</summary>
    public IServiceScope? ServiceScope { get; set; }

    /// <summary>Maps a Gherkin key alias (e.g. "key-A") to its seeded Id, for steps that need to reference a previously-seeded key by name.</summary>
    public Dictionary<string, Guid> SeededKeys { get; } = new();
}
