using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ApiKeyManagement.KeyLifecycle.ValidateKey;

/// <summary>
/// Data Plane validation trigger — api-spec.md §4.1 <c>POST /api/internal/v1/validate-key</c>.
/// Lives in KeyLifecycle (not a new Validation BC): 2026-07-26 題 3 裁決第一刀直查 KeyLifecycle
/// 主表，即 context-integration-spec.md §4.7 I7 明文的 L3 回源路徑（「回源查詢 KL」「Fail-through
/// to L3」）。一個獨立的 Validation BC 與 <c>KeyValidationView</c> 投影留待投影實作到位時再搬——
/// 屆時再抽 endpoint／handler。I7 的事件投影至今未實作，本端點不是該投影的消費者。
/// </summary>
public static class ValidateKeyEndpoint
{
    public const string Route = "/api/internal/v1/validate-key";

    public record Request(string ApiKey, string SourceIp, string RequestedScope, string? RequestId);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost(
            Route,
            async (
                Request request,
                IValidateKeyHandler handler,
                CancellationToken cancel) =>
            {
                var command = new ValidateKeyCommand(
                    ApiKey: request.ApiKey,
                    SourceIp: request.SourceIp,
                    RequestedScope: request.RequestedScope,
                    RequestId: request.RequestId);

                var result = await handler.HandleAsync(command, cancel);

                if (result.IsFailure)
                {
                    // 2026-07-26 裁決：失敗回應為 HTTP 200 ＋扁平 body（不是本 BC 其餘端點共用的
                    // RFC 9457 ApiProblem 信封），也不是把 httpStatusHint 當傳輸層狀態碼。
                    // `httpStatusHint` 這個欄位存在的意義就是「讓 Gateway 決定回 401 還是 403」
                    // （api-spec.md §4.1「Gateway 根據 httpStatusHint 直接回傳對應的 HTTP Status
                    // 給客戶端」）；若傳輸層狀態已經帶了它，該欄位即為冗餘。本端點是內部 RPC，
                    // 「呼叫成功、答案是否定」即 200 —— Gateway 才是真正面向外部客戶端回應狀態
                    // 碼的一方。
                    var httpStatusHint = ValidateKeyFailureMapping.Resolve(result.Error.Code);

                    return Results.Ok(new ValidateKeyFailureResponse(
                        Valid: false,
                        ErrorCode: result.Error.Code,
                        HttpStatusHint: httpStatusHint));
                }

                return Results.Ok(result.Value);
            })
            // Internal Data Plane endpoint（Gateway → 系統）。api-spec.md §4 寫的是
            // mTLS／Internal Service Token；本 repo 目前以 System role JWT 作為內部呼叫者的既有
            // 替代（ADR-024），鏡射 LockKeyEndpoint.Map 先例。本輪無任何場景覆蓋「非 System 被
            // 拒」——屬已登記的覆蓋缺口，不因此省略此授權設定。
            .RequireAuthorization(policy => policy.RequireRole("System"));
    }
}
