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

    /// <summary>
    /// POSTs with no request body and captures the response + its body onto the context.
    /// Only for endpoints whose api-spec explicitly defines no request body — endpoints that
    /// take a body (even an empty JSON object, e.g. "未提供原因" scenarios) must keep using
    /// PostAsJsonAsync directly at the call site, since the body there carries scenario intent.
    /// </summary>
    public static async Task PostNoBodyAndCaptureAsync(this FunctionalTestContext ctx, string url)
    {
        ctx.Response = await ctx.Client.PostAsync(url, null);
        ctx.ResponseBody = await ctx.Response.Content.ReadAsStringAsync();
    }
}
