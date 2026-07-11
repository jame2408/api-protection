using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace ApiKeyManagement.FunctionalTests.Infrastructure;

/// <summary>
/// Shared RFC 9457 Problem Details assertion helper for failure Then steps (api-spec.md §2.2).
/// Callers keep their own "scenario text -> (HttpStatusCode, errorCode)" map next to their step
/// (wire-lock intent: production uses *FailureCodes.* constants, the map re-states the literal
/// strings so a constant value drift surfaces as a test failure) and pass the resolved pair here
/// for the shared envelope assertions (status / content-type / body shape / type suffix / traceId).
/// </summary>
public static class ProblemAssertions
{
    public static void RequireProblem(
        HttpResponseMessage response, string body, HttpStatusCode expectedStatus, string expectedErrorCode)
    {
        response.StatusCode.Should().Be(expectedStatus);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/problem+json");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        root.GetProperty("status").GetInt32().Should().Be((int)expectedStatus);
        root.GetProperty("errorCode").GetString().Should().Be(expectedErrorCode);
        root.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("type").GetString().Should().EndWith(Kebab(expectedErrorCode));
        root.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrEmpty();
    }

    // Mirror of ApiProblem.ToKebab — the wire `type` suffix derives from the error code.
    private static string Kebab(string code)
        => code.ToLowerInvariant().Replace('_', '-').Replace(':', '-');
}
