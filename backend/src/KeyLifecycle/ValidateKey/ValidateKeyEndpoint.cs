using ApiKeyManagement.KeyLifecycle.Http;
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
                HttpContext httpContext,
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
                    // DEFERRAL: api-spec.md §4.1 的失敗回應形狀不是 RFC 9457 —— 是扁平的
                    // `{ valid:false, errorCode, httpStatusHint, detail }`，與本 BC 其餘端點共用
                    // 的 ApiProblem 信封不同。本輪唯一啟用的場景（成功驗證）不會走到這條分支；
                    // ApiProblem.FromFailure 是佔位，待對應失敗場景的紅驅動該 wire 形狀落地時
                    // 於此處替換。
                    return ApiProblem.FromFailure(result.Error, httpContext);
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
