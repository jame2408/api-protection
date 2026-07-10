using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.SuspendKey;

public interface ISuspendKeyHandler
{
    Task<Result<SuspendKeyResponse, Failure>> HandleAsync(
        SuspendKeyCommand command, CancellationToken cancel = default);
}
