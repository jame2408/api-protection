namespace ApiKeyManagement.KeyLifecycle.ValidateKey;

// api-spec.md §4.1 請求欄位表：no tenantId — the hash lookup is cross-tenant by design
// (ValidateKeyHandler class comment), tenantId comes back out in the response instead.
public record ValidateKeyCommand(
    string ApiKey,
    string SourceIp,
    string RequestedScope,
    string? RequestId
);
