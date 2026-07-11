using ApiKeyManagement.KeyLifecycle.Http;
using ApiKeyManagement.SharedKernel.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace ApiKeyManagement.Api.Middleware;

/// <summary>
/// Overrides the ASP.NET Core default authorization failure response so a policy rejection
/// (role check failed on an authenticated request) surfaces as an RFC 9457 Problem Details body
/// (api-spec.md §2.2 FORBIDDEN row), consistent with every other Failure response in this API.
/// Only <see cref="PolicyAuthorizationResult.Forbidden"/> is intercepted; the "not yet
/// authenticated" case (401 challenge) keeps the framework default behavior — that's a later,
/// separately-tracked concern (ADR-024 post-hoc debt), not this scenario's scope.
/// </summary>
public class ProblemAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var failure = FailureProvider.CreateFailure(ApiProblem.ForbiddenCode);
            return ApiProblem.FromFailure(failure, context).ExecuteAsync(context);
        }

        return DefaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
