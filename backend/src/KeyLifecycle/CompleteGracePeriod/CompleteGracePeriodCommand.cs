namespace ApiKeyManagement.KeyLifecycle.CompleteGracePeriod;

public record CompleteGracePeriodCommand(
    Guid KeyId,
    string TenantId
);
