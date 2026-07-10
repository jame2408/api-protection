using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.KeyLifecycle.RevokeKey;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.RevokeLeakedKeys;

// Composition over reimplementation: delegates each key's revocation to IRevokeKeyHandler so
// the batch path inherits its guards, successor cleanup, and KeyRevoked event for free
// (orchestrator design decision, tasks/checkpoint.md 19/46).
public class RevokeLeakedKeysHandler(
    IApiKeyRepository keyRepository,
    IRevokeKeyHandler revokeKeyHandler
) : IRevokeLeakedKeysHandler
{
    public async Task<Result<RevokeLeakedKeysResponse, Failure>> HandleAsync(
        RevokeLeakedKeysCommand command, CancellationToken cancel = default)
    {
        // 1. Global scan by prefix (no tenantId — Secret Scanner doesn't know the tenant).
        var matchingKeys = await keyRepository.GetNonTerminalByPrefixAsync(command.KeyPrefix, cancel);

        var revokedKeys = new List<RevokedKeySummary>(matchingKeys.Count);

        // 2. Sequential, not parallel: all keys share this request's single DbContext, which
        //    is not safe for concurrent operations.
        foreach (var key in matchingKeys)
        {
            var revokeResult = await revokeKeyHandler.HandleAsync(
                new RevokeKeyCommand(key.TenantId, key.Id, ApiKey.LeakedInPublicRepositoryReason),
                cancel);

            if (revokeResult.IsFailure)
                return revokeResult.Error;

            // 3. `key` and the instance RevokeKeyHandler just mutated are the same tracked
            //    entity (EF Core identity map, same scoped DbContext) — safe to layer the
            //    notification event on it and persist here.
            key.NotifyLeakDetected();
            await keyRepository.UpdateAsync(key, cancel);

            revokedKeys.Add(new RevokedKeySummary(key.Id, key.Status));
        }

        return new RevokeLeakedKeysResponse(revokedKeys);
    }
}
