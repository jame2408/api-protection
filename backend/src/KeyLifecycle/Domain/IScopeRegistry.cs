namespace ApiKeyManagement.KeyLifecycle.Domain;

public interface IScopeRegistry
{
    Task<bool> AllExistAsync(IEnumerable<string> scopes, CancellationToken ct = default);
}
