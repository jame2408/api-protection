namespace ApiKeyManagement.KeyLifecycle.ValidateKey;

/// <summary>
/// errorCode → httpStatusHint for validate-key's flat failure wire shape (api-spec.md §4.1
/// 驗證失敗錯誤碼表). Not the same concept as <c>ApiProblem.Map</c>: that one resolves an
/// RFC 9457 envelope (HTTP transport status + title) for this BC's other endpoints;
/// this one resolves a bare int carried *inside* a 200 OK body (2026-07-26 裁決, decision 1 —
/// validate-key is an internal RPC, the transport status is always 200, and Gateway alone
/// decides what to return to its own caller using this hint). Two failure wire shapes
/// coexisting in one BC is intentional, not duplication to collapse.
/// </summary>
public static class ValidateKeyFailureMapping
{
    // 「宣告即映射」：ValidateKeyFailureCodes 目前宣告的每一個碼，都必須在此有一列——已宣告卻
    // 未映射的碼會靜默落到通用 500（Wave 8 第二條場景的 mutation 已實測過這個空洞，見
    // tasks/checkpoint.md commit 4387033 的登記）。新碼隨其自己的場景一起加一列，不預先鋪陳
    // 尚不存在的碼（IP_NOT_ALLOWED／SCOPE_INSUFFICIENT／KEY_INACTIVE／KEY_EXPIRED／KEY_REVOKED
    // 各有自己的場景）。忘記加列時，Resolve 的字典查找本身會拋出，交由
    // UnhandledExceptionMiddleware 轉成通用 500 —— 與過去的行為一致，不需要我們自己寫 throw。
    private static readonly Dictionary<string, int> HttpStatusHints = new()
    {
        [ValidateKeyFailureCodes.KeyNotFound] = 401,
        [ValidateKeyFailureCodes.KeyFormatInvalid] = 401,
    };

    public static int Resolve(string code) => HttpStatusHints[code];
}
