namespace ApiKeyManagement.KeyLifecycle.ValidateKey;

public static class ValidateKeyFailureCodes
{
    // Wire value matches api-spec.md §4.1 錯誤碼表 KEY_NOT_FOUND row (Layer 4: 雜湊驗證) — hash
    // miss and hash mismatch share this code on purpose (不區分「不存在」與「錯誤」以防列舉).
    public const string KeyNotFound = "KEY_NOT_FOUND";

    // Wire value matches api-spec.md §4.1 錯誤碼表 KEY_FORMAT_INVALID row (Layer 1: 格式檢查).
    // 本輪只實作前綴檢查（見 ValidateKeyHandler 開頭的 Layer 1 guard）；長度／checksum 檢查
    // 尚無場景覆蓋，deferred — 同一處註解記載。
    public const string KeyFormatInvalid = "KEY_FORMAT_INVALID";

    // errorCode → httpStatusHint 的映射見 ValidateKeyFailureMapping，不在此類——「宣告即映射」：
    // 本類新增一個碼時，該映射須在同一 commit 補上對應一列（理由見該檔註解），此類本身只負責
    // 常數宣告，避免裸字串散落（exceptions.rule.md）。
}
