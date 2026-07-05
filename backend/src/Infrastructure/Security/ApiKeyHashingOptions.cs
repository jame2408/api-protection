namespace ApiKeyManagement.Infrastructure.Security;

/// <summary>
/// Binds the `ApiKeyHashing` configuration section. See docs/adr/adr-017-key-hash-hmac-and-hotpath-contract.md.
/// </summary>
public sealed class ApiKeyHashingOptions
{
    public const string SectionName = "ApiKeyHashing";

    public string Pepper { get; set; } = string.Empty;
}
