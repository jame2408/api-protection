using ApiKeyManagement.KeyLifecycle.CreateApiKey;
using ApiKeyManagement.SharedKernel.Contracts;
using ApiKeyManagement.SharedKernel.Domain;
using Microsoft.AspNetCore.Http;

namespace ApiKeyManagement.KeyLifecycle.Http;

/// <summary>
/// Maps a <see cref="Failure"/> to an RFC 9457 Problem Details response (api-spec.md §2.2 錯誤).
/// Single source for the error wire envelope so endpoints don't hand-roll bodies. Mapping a failure
/// code to an HTTP status is an HTTP-boundary decision and lives here, not in the Service/Handler.
/// Lives in the KeyLifecycle BC (the only BC exposing endpoints today); extract to a shared web
/// helper if a second BC needs it.
/// </summary>
public static class ApiProblem
{
    private const string TypeBaseUri = "https://api.example.com/errors/";

    // Failure.Code -> (HTTP status, human-readable title). VALIDATION_ERROR:* is handled by prefix.
    private static readonly Dictionary<string, (int Status, string Title)> Map =
        new()
        {
            [ConsumerValidationFailureCodes.TenantNotFound] = (StatusCodes.Status404NotFound, "Tenant Not Found"),
            [ConsumerValidationFailureCodes.ConsumerNotFound] = (StatusCodes.Status404NotFound, "Consumer Not Found"),
            [ConsumerValidationFailureCodes.TenantSuspended] = (StatusCodes.Status403Forbidden, "Tenant Suspended"),
            [CreateApiKeyFailureCodes.KeyLimitExceeded] = (StatusCodes.Status409Conflict, "Key Limit Exceeded"),
            [CreateApiKeyFailureCodes.KeyNameDuplicate] = (StatusCodes.Status409Conflict, "Key Name Duplicate"),
            [CreateApiKeyFailureCodes.ScopeNotFound] = (StatusCodes.Status422UnprocessableEntity, "Scope Not Found"),
            [CreateApiKeyFailureCodes.ExpiresAtExceedsMax] = (StatusCodes.Status422UnprocessableEntity, "Expiry Exceeds Maximum"),
        };

    /// <summary>Builds the RFC 9457 response for a failure. <c>errorCode</c> carries the stable
    /// <see cref="Failure.Code"/>; <c>traceId</c> ties the response to server logs.</summary>
    public static IResult FromFailure(Failure failure, HttpContext http)
    {
        var code = failure.Code;
        var (status, title) = Resolve(code);

        return Results.Problem(
            type: TypeBaseUri + ToKebab(code),
            title: title,
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = code,
                ["traceId"] = http.TraceIdentifier,
            });
    }

    private static (int Status, string Title) Resolve(string code)
    {
        if (Map.TryGetValue(code, out var mapped))
        {
            return mapped;
        }

        if (code.StartsWith(CreateApiKeyFailureCodes.ValidationErrorPrefix, StringComparison.Ordinal))
        {
            return (StatusCodes.Status400BadRequest, "Validation Error");
        }

        // Unknown code: still a well-formed RFC 9457 body, surfaced as a 500 with the code preserved.
        return (StatusCodes.Status500InternalServerError, "Internal Error");
    }

    // KEY_LIMIT_EXCEEDED -> key-limit-exceeded; VALIDATION_ERROR:scopes_empty -> validation-error-scopes-empty
    private static string ToKebab(string code)
        => code.ToLowerInvariant().Replace('_', '-').Replace(':', '-');
}
