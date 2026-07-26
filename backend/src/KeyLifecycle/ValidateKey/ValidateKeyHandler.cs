using System.Security.Cryptography;
using System.Text;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.ValidateKey;

/// <summary>
/// Executes the validation funnel's hash-lookup layer (ADR-029 §1: all five layers run inside
/// this system process, not the Gateway — this handler backs the funnel's sole trigger,
/// <c>POST /api/internal/v1/validate-key</c>). This round only wires the all-guards-pass path
/// (07_ValidateKey.feature "成功驗證 Active 金鑰"); the remaining funnel layers (format, status,
/// IP, scope-insufficient) are separate Wave 8 scenarios still under @ignore — see the DEFERRAL
/// comments below for each guard's future insertion point. No tenantId scoping here (unlike
/// most KeyLifecycle handlers): the request carries none (api-spec.md §4.1 請求欄位表), so the
/// hash lookup is necessarily cross-tenant; tenantId is read off the matched key and returned.
/// </summary>
public class ValidateKeyHandler(
    IApiKeyRepository keyRepository,
    IApiKeyHasher hasher
) : IValidateKeyHandler
{
    public async Task<Result<ValidateKeyResponse, Failure>> HandleAsync(
        ValidateKeyCommand command, CancellationToken cancel = default)
    {
        // ADR-017 Rule 6(a) 的 KeyHash 唯一索引把等值查找收斂到 DB 層（至多一筆候選），
        // Rule 6(b) 又要求 FixedTimeEquals 恆定時間比較 —— 兩者不是二選一：索引負責「查找
        // 效率」，FixedTimeEquals 負責「相符判定的字面滿足」與防禦深度（即使未來出現繞過索引
        // 查找的呼叫路徑，比對本身仍是恆定時間）。禁止任何地方用 string == 比對雜湊。
        var computedHash = hasher.ComputeHash(command.ApiKey);
        var apiKey = await keyRepository.GetByKeyHashAsync(computedHash, cancel);

        if (apiKey is null ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(apiKey.KeyHash),
                Encoding.UTF8.GetBytes(computedHash)))
        {
            // DEFERRAL: 07_ValidateKey.feature「金鑰雜湊不匹配 — 拒絕驗證」場景（KEY_NOT_FOUND /
            // 401，故意不區分「不存在」與「錯誤」以防列舉）尚未啟用；本分支目前只需回傳
            // Result 失敗，errorCode/httpStatusHint 的 wire 形狀由該場景的紅驅動落地。
            return FailureProvider.CreateFailure(ValidateKeyFailureCodes.KeyNotFound);
        }

        // DEFERRAL: Layer 2 狀態檢查（Suspended/Expired/Revoked 各自場景 → KEY_INACTIVE /
        // KEY_EXPIRED / KEY_REVOKED）與 Layer 5 權限檢查（SCOPE_INSUFFICIENT 場景）本輪不實作
        // ——本場景的種子金鑰恰為 Active 且 scope 涵蓋 requestedScope，兩層守衛尚未被任何紅
        // 驅動。插入點：本行與下方 return 之間，依 api-spec.md §4.1 錯誤碼表對應的 Layer 順序。

        return new ValidateKeyResponse(
            Valid: true,
            KeyId: apiKey.Id,
            TenantId: apiKey.TenantId,
            ConsumerId: apiKey.ConsumerId,
            Environment: apiKey.Environment,
            Scopes: apiKey.Scopes);
    }
}
