using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.RevokeLeakedKeys;

public interface IRevokeLeakedKeysHandler
{
    Task<Result<RevokeLeakedKeysResponse, Failure>> HandleAsync(
        RevokeLeakedKeysCommand command, CancellationToken cancel = default);
}
