namespace ApiKeyManagement.KeyLifecycle.CompleteGracePeriod;

public record CompleteGracePeriodResponse(
    Guid KeyId,
    Guid SuccessorKeyId
);
