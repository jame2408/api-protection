using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.CompleteGracePeriod;

public interface ICompleteGracePeriodHandler
{
    Task<Result<CompleteGracePeriodResponse, Failure>> HandleAsync(
        CompleteGracePeriodCommand command, CancellationToken cancel = default);
}
