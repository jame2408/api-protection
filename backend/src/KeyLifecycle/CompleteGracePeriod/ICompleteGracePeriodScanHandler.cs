using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.CompleteGracePeriod;

public interface ICompleteGracePeriodScanHandler
{
    Task<Result<CompleteGracePeriodScanResponse, Failure>> HandleAsync(CancellationToken cancel = default);
}
