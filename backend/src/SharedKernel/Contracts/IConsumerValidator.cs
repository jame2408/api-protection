namespace ApiKeyManagement.SharedKernel.Contracts;

public record ConsumerValidationResult(bool IsValid, string? ErrorCode = null)
{
    public static ConsumerValidationResult Valid() => new(true);

    public static ConsumerValidationResult Invalid(string errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("Error code is required.", nameof(errorCode));

        return new ConsumerValidationResult(false, errorCode);
    }
}

public interface IConsumerValidator
{
    Task<ConsumerValidationResult> ValidateAsync(string tenantId, string consumerId, CancellationToken cancel = default);
}
