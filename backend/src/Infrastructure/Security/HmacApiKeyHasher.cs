using System.Security.Cryptography;
using System.Text;
using ApiKeyManagement.KeyLifecycle.Domain;
using Microsoft.Extensions.Options;

namespace ApiKeyManagement.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 + server-side pepper key hasher. See docs/adr/adr-017-key-hash-hmac-and-hotpath-contract.md
/// Implementation Rule 3. Pepper is validated at construction time (fail-fast on missing/short config).
/// </summary>
public sealed class HmacApiKeyHasher : IApiKeyHasher
{
    private readonly byte[] _pepper;

    public HmacApiKeyHasher(IOptions<ApiKeyHashingOptions> options)
    {
        var pepperBase64 = options.Value.Pepper;

        byte[] pepper;
        try
        {
            pepper = Convert.FromBase64String(pepperBase64);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "ApiKeyHashing:Pepper must be Base64 of at least 32 bytes (ADR-017).");
        }

        if (pepper.Length < 32)
        {
            throw new InvalidOperationException(
                "ApiKeyHashing:Pepper must be Base64 of at least 32 bytes (ADR-017).");
        }

        _pepper = pepper;
    }

    public string ComputeHash(string rawKey)
        => Convert.ToBase64String(HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(rawKey)));
}
