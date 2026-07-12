using ApiKeyManagement.KeyLifecycle.CreateApiKey;
using ApiKeyManagement.KeyLifecycle.RevokeKey;
using ApiKeyManagement.KeyLifecycle.RotateKey;
using ApiKeyManagement.KeyLifecycle.SuspendKey;
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

    // Generic authorization-denial code (api-spec.md §2.2 FORBIDDEN row). Not a per-BC
    // *FailureCodes constant because the authorization policy runs in Program.cs / the
    // authorization middleware — before any BC Handler executes — so no BC "owns" this code.
    public const string ForbiddenCode = "FORBIDDEN";

    // Failure.Code -> (HTTP status, human-readable title). VALIDATION_ERROR:* is handled by prefix.
    private static readonly Dictionary<string, (int Status, string Title)> Map =
        new()
        {
            [ForbiddenCode] = (StatusCodes.Status403Forbidden, "Forbidden"),
            [ConsumerValidationFailureCodes.TenantNotFound] = (StatusCodes.Status404NotFound, "Tenant Not Found"),
            [ConsumerValidationFailureCodes.ConsumerNotFound] = (StatusCodes.Status404NotFound, "Consumer Not Found"),
            [ConsumerValidationFailureCodes.TenantSuspended] = (StatusCodes.Status403Forbidden, "Tenant Suspended"),
            [CreateApiKeyFailureCodes.KeyLimitExceeded] = (StatusCodes.Status409Conflict, "Key Limit Exceeded"),
            [CreateApiKeyFailureCodes.KeyNameDuplicate] = (StatusCodes.Status409Conflict, "Key Name Duplicate"),
            [CreateApiKeyFailureCodes.ScopeNotFound] = (StatusCodes.Status422UnprocessableEntity, "Scope Not Found"),
            [CreateApiKeyFailureCodes.ExpiresAtExceedsMax] = (StatusCodes.Status422UnprocessableEntity, "Expiry Exceeds Maximum"),
            [RevokeKeyFailureCodes.KeyNotFound] = (StatusCodes.Status404NotFound, "Key Not Found"),
            [RevokeKeyFailureCodes.KeyInTerminalState] = (StatusCodes.Status409Conflict, "Key In Terminal State"),
            [SuspendKeyFailureCodes.InvalidStateTransition] = (StatusCodes.Status409Conflict, "Invalid State Transition"),
            [SuspendKeyFailureCodes.HumanActorRequired] = (StatusCodes.Status422UnprocessableEntity, "Human Actor Required"),
            [RotateKeyFailureCodes.RotationInProgress] = (StatusCodes.Status409Conflict, "Rotation In Progress"),
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
