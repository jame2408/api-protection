using System.Security.Cryptography;
using System.Text;
using ApiKeyManagement.KeyLifecycle.Domain;
using ApiKeyManagement.SharedKernel.Domain;

namespace ApiKeyManagement.KeyLifecycle.ValidateKey;

/// <summary>
/// Executes the validation funnel's Layer 1 (format) and Layer 4 (hash-lookup) checks (ADR-029
/// §1: all five layers run inside this system process, not the Gateway — this handler backs the
/// funnel's sole trigger, <c>POST /api/internal/v1/validate-key</c>). The remaining funnel layers
/// (status, IP, scope-insufficient) are separate Wave 8 scenarios still under @ignore — see the
/// DEFERRAL comments below for each guard's future insertion point. No tenantId scoping here
/// (unlike most KeyLifecycle handlers): the request carries none (api-spec.md §4.1 請求欄位表),
/// so the hash lookup is necessarily cross-tenant; tenantId is read off the matched key and
/// returned.
/// </summary>
public class ValidateKeyHandler(
    IApiKeyRepository keyRepository,
    IApiKeyHasher hasher
) : IValidateKeyHandler
{
    public async Task<Result<ValidateKeyResponse, Failure>> HandleAsync(
        ValidateKeyCommand command, CancellationToken cancel = default)
    {
        // Layer 1（格式檢查）置於方法最前面，早於 Layer 4 的 ComputeHash 與資料庫查找——漏斗
        // 層序即成本序（design-doc.md §6.2.1「每一層的成本遞增，先用便宜的檢查過濾明顯無效的
        // 請求」）：字串前綴比對是 O(1) 且零 I/O，能在雜湊運算與 DB 往返之前擋掉明顯不合法的
        // 輸入。本輪只實作前綴檢查（rawKey 必須以 "apk_" 起頭，ApiKey.GenerateKeyMaterial 的
        // 格式規格）。
        //
        // DEFERRAL：長度與 checksum 檢查目前無任何場景覆蓋（api-spec.md §4.1 的
        // KEY_FORMAT_INVALID 列雖寫「前綴/長度/checksum 不合法」，但本輪只有前綴有場景驅動）。
        // 若要補齊需依 ADR-030 另行產出對應場景（使用者裁決）——不得自行補建無場景的檢查。
        if (!command.ApiKey.StartsWith("apk_", StringComparison.Ordinal))
        {
            return FailureProvider.CreateFailure(ValidateKeyFailureCodes.KeyFormatInvalid);
        }

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
            // 401，故意不區分「不存在」與「錯誤」以防列舉）尚未啟用——本分支已可產出正確的
            // errorCode/httpStatusHint wire 形狀（ValidateKeyFailureMapping 已涵蓋 KeyNotFound），
            // 缺的只是該場景本身的紅綠驗證。
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
