using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>
    /// Resolves <typeparamref name="THandler"/> from a fresh DI scope and invokes it via
    /// <paramref name="invoke"/>. A fresh scope (rather than reusing ctx.ServiceScope) mirrors how
    /// the production job resolves the handler each run — these System Agent Job slices have no
    /// HostedService/timer wrapper this round, so DI direct invocation from the step IS the
    /// trigger surface, and each invocation should get its own scope just like a real scheduled
    /// run would. Takes a delegate rather than resolving-and-returning the handler: the scope must
    /// stay alive until the handler call completes. A resolve-and-return shape would let the
    /// `using var scope` above dispose the moment the helper returns — before the caller's own
    /// `await handler.HandleAsync(...)` even runs — leaving that call to execute against an
    /// already-disposed scope's services.
    /// </summary>
    public static async Task<TResult> InScopedHandlerAsync<THandler, TResult>(
        this FunctionalTestContext ctx, Func<THandler, Task<TResult>> invoke) where THandler : notnull
    {
        using var scope = ctx.ServiceScope!.ServiceProvider
            .GetRequiredService<IServiceScopeFactory>().CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<THandler>();

        return await invoke(handler);
    }
}
