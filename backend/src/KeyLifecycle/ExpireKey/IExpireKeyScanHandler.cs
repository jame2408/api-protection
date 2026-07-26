using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.ExpireKey;

public interface IExpireKeyScanHandler
{
    Task<Result<ExpireKeyScanResponse, Failure>> HandleAsync(CancellationToken cancel = default);
}
