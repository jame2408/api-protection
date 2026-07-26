namespace ApiKeyManagement.KeyLifecycle.Domain;

public interface IApiKeyRepository
{
    Task SaveAsync(ApiKey apiKey, CancellationToken cancel = default);
    Task<int> CountActiveAsync(string consumerId, string environment, string tenantId, CancellationToken cancel = default);
    Task<bool> ExistsNameAsync(string name, string consumerId, string environment, string tenantId, CancellationToken cancel = default);
    Task<bool> ExistsRotatingAsync(string consumerId, string environment, string tenantId, CancellationToken cancel = default);
    Task<ApiKey?> GetByIdAsync(Guid keyId, string tenantId, CancellationToken cancel = default);
    Task UpdateAsync(ApiKey apiKey, CancellationToken cancel = default);
    Task<IReadOnlyList<ApiKey>> GetNonTerminalByPrefixAsync(string keyPrefix, CancellationToken cancel = default);

    // System Agent scan (C9 CompleteGracePeriod) — cross-tenant on purpose: the grace-period
    // sweep is a system-wide job, not a per-tenant request, so unlike GetByIdAsync there is no
    // tenantId filter here.
    Task<IReadOnlyList<ApiKey>> GetRotatingAsync(CancellationToken cancel = default);

    // System Agent scan (C8 ExpireKey) — cross-tenant on purpose, same reasoning as
    // GetRotatingAsync above. Filters both the date predicate and terminal states here (rather
    // than in a per-key guard) — see ExpireKeyScanHandler's class comment for why this BC has no
    // per-key command handler this round.
    Task<IReadOnlyList<ApiKey>> GetExpiredNonTerminalAsync(DateTimeOffset now, CancellationToken cancel = default);
}
