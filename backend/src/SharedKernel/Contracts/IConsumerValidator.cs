namespace ApiKeyManagement.SharedKernel.Contracts;

public record ConsumerValidationResult(bool IsValid, string? ErrorCode = null);

public interface IConsumerValidator
{
    Task<ConsumerValidationResult> ValidateAsync(string tenantId, string consumerId, CancellationToken ct = default);
}
