namespace ApiKeyManagement.KeyLifecycle.ValidateKey;

public static class ValidateKeyFailureCodes
{
    // Wire value matches api-spec.md §4.1 錯誤碼表 KEY_NOT_FOUND row (Layer 4: 雜湊驗證) — hash
    // miss and hash mismatch share this code on purpose (不區分「不存在」與「錯誤」以防列舉).
    // The errorCode/httpStatusHint wire mapping itself is deferred — see ValidateKeyHandler's
    // DEFERRAL comment; this constant only exists so the handler never passes a bare string.
    public const string KeyNotFound = "KEY_NOT_FOUND";
}
