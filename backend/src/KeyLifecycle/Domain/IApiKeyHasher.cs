namespace ApiKeyManagement.KeyLifecycle.Domain;

/// <summary>
/// Computes the storage hash for a raw API key. Algorithm choice (HMAC-SHA256 + server-side
/// pepper) is an Infrastructure concern — see docs/adr/adr-017-key-hash-hmac-and-hotpath-contract.md.
/// </summary>
public interface IApiKeyHasher
{
    string ComputeHash(string rawKey);
}
