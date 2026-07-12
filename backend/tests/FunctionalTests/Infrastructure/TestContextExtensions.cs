using System.Net.Http.Headers;

namespace ApiKeyManagement.FunctionalTests.Infrastructure;

/// <summary>
/// Shared step-definition helpers built on top of FunctionalTestContext. Kept as extension
/// methods, not instance methods on FunctionalTestContext itself — that class is a pure shared
/// state bag (see its doc comment), so behavior lives here instead (mirrors OutboxAssertions /
/// ProblemAssertions).
/// </summary>
public static class TestContextExtensions
{
    /// <summary>
    /// Sets the bearer token for the current actor and applies it to the shared HttpClient.
    /// Callers pick which TestTokenFactory.CreateXxxToken(...) to issue — "which actor" is the
    /// calling step's own semantics, not this helper's.
    /// </summary>
    public static void AuthenticateAs(this FunctionalTestContext ctx, string token)
    {
        ctx.AuthToken = token;
        ctx.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
