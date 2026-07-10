# Control Plane JWT 認證與 Actor 傳遞：AuthToken 基礎設施最小落地

> Lead-in：repo 至今零 auth 基礎設施（無套件、無 middleware、無 ClaimsPrincipal 使用），而 Wave 3 首場景「成功暫停金鑰」的 Then 斷言 KeySuspended 事件含 `suspendedBy`——該值唯一來源是 JWT claims。本 ADR 固化 Control Plane 的 JWT 認證機制、Actor 型別與傳遞路徑、以及強制範圍；角色授權（403）、IDOR 交叉驗證等明文後置。

---

## Status

Accepted (2026-07-10)

同步項目：`docs/design/api-spec.md` §2.1（System 內部 JWT 條目＋`name` claim 增列）與本 ADR 同 commit；`tasks/todo.md` #25 結案註記、`tasks/bdd-progress.md` 解鎖表「已建立」標記，因描述**落地完成態**，於 Phase 2 實作 commit 同步（ADR-020 Status 欄「後續 commit 落地不在同 commit 義務內」先例）。

---

## Context

### 現況

四處事實並排：

- `03_SuspendResumeKey.feature`「成功暫停金鑰」Then：「系統產生 KeySuspended 事件，包含 keyId、suspendedBy、reason」；api-spec §3.2.5 request body 只有 `reason`，`suspendedBy` 唯一來源是 JWT `sub` claim（§2.1）。
- 實際程式碼：`Host/Program.cs` 無 `AddAuthentication` / `AddAuthorization`；全部 csproj 零 auth 套件；grep `ClaimsPrincipal|HttpContext.User|Bearer` 全 repo production code 零命中。
- 兩處早已預留的錨點：`FunctionalTests/Infrastructure/FunctionalTestContext.cs` 的 `AuthToken` 屬性（佔位、從未被讀寫）；`RevokeKey/RevokeKeyResponse.cs` 註解「revokedBy omitted this pass — repo has no auth/actor infrastructure yet」。
- 契約已定：`docs/design/context-integration-spec.md` §3 定義 `Actor { type: "User"|"System", id, name }`；§6.1 各生命週期事件（KeyRevoked / KeySuspended / KeyResumed / KeyUnlocked）皆含 Actor 欄位。`tasks/bdd-progress.md` 解鎖表明文「Wave 3 開始前：AuthToken 機制（Security Admin / Consumer / System 的 JWT）」。

### 問題嚴重度

- 不建 AuthToken，03 檔 8 場景中至少 5 個（suspendedBy 斷言、System 拒絕、Consumer 權限不足、resumedBy）無法誠實實作——重演 ADR-020 之前 response-body 代理斷言的名不符實模式。
- api-spec §2.1 有兩個缺口：(a) System actor 不在 JWT 角色表（僅存在於事件 Actor 型別），場景 3「System 嘗試暫停」無 token 可發；(b) claims 無顯示名稱欄，Actor.name 無來源。

### 易混淆概念釐清

| 概念 | 是什麼 | 本 ADR 是否規範 |
|---|---|---|
| 認證（401：token 缺失/無效） | JwtBearer middleware 驗簽章與存活期 | ✅ |
| Actor 型別與傳遞路徑 | ClaimsPrincipal → Actor → Command → 事件 | ✅ |
| 角色授權（403：role 不符 endpoint） | RequireAuthorization policy per role | ❌ 後置（場景「權限不足」落地時，屆時需一併勘誤 §2.1 SecurityAdmin 前綴表未含 `/tenants/*/keys/*` 的不一致） |
| IDOR 交叉驗證（URL tenantId ↔ claims） | §2.1 既定規則 | ❌ 後置（尚無場景斷言） |
| 401/403 的 RFC 9457 body 形狀 | api-spec §2.2 UNAUTHORIZED/FORBIDDEN envelope | ❌ 後置（尚無場景斷言 body，本階段裸 status code） |
| Data Plane / 內部端點認證 | mTLS 或 Internal Service Token（§2.1） | ❌ 後置（維持匿名＋債務登記） |
| revokedBy 回補 | Wave 2 既登記債務 | ❌ 獨立小包（2026-07-10 使用者裁決） |

---

## Decision

### 1. 認證機制：`Microsoft.AspNetCore.Authentication.JwtBearer`＋對稱簽章金鑰

- 套件經 Central Package Management 引入；簽章金鑰自 configuration `Jwt:SigningKey` 讀取（Base64，HMAC-SHA256，≥ 32 bytes），測試以環境變數注入，循 `ApiKeyHashing__Pepper` 先例；dev appsettings 帶開發用金鑰。
- TokenValidationParameters：簽章與存活期驗證必開；issuer / audience 本階段**不驗**（單一簽發者、對稱金鑰情境無鑑別價值，多簽發者時另開 ADR）；`NameClaimType = "sub"`、`RoleClaimType = "role"`。

```csharp
// before：Program.cs 無任何 auth 註冊
// after（示意）：
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = /* §1 參數 */);
builder.Services.AddAuthorization();
// pipeline：UseAuthentication() / UseAuthorization() 置於
// UseMiddleware<UnhandledExceptionMiddleware>() 之後、Map*Endpoints() 之前
```

### 2. Claims schema：api-spec §2.1 為準，補兩缺口（同步項目）

沿用 `sub` / `tenantId` / `role` / `consumerId`，並於 §2.1 增列：

- **System actor**＝`role=System` 的內部 JWT（`sub` 為服務名稱，如 `monitoring-service`）。System token 能**通過認證**；「暫停僅限人為」（design-doc 不變量「Suspended 僅限人為」）由 domain/handler guard 以 `Actor.Type` 拒絕，走業務 Failure，不是 403。
- **`name` claim**（選填，顯示名稱）：Actor.name 的來源；缺省 fallback 為 `sub`。

### 3. Actor 型別落 SharedKernel，傳遞走顯式參數鏈

```csharp
// SharedKernel/Domain/Actor.cs（新）
public enum ActorType { User, System }
public record Actor(ActorType Type, string Id, string Name);
// 映射：靜態方法 Actor.FromClaims(ClaimsPrincipal)（System.Security.Claims 是 BCL，
// SharedKernel 不因此引入 ASP.NET 依賴）
```

傳遞路徑（每環顯式，不走 ambient context）：endpoint 讀 `httpContext.User` → `Actor.FromClaims` → 放入 Command record 欄位 → handler → domain 方法參數 → domain event 欄位 → outbox payload（ADR-020 收割自動序列化，wire 為 camelCase 巢狀物件，enum 依 ADR-006 PascalCase 字串）。

### 4. 強制範圍：control plane 全面 `RequireAuthorization()`，內部端點明文匿名

- 既有與未來所有 `/api/v1/tenants/*` control-plane endpoints（CreateApiKey、RevokeKey 起）一律 `.RequireAuthorization()`（2026-07-10 使用者裁決：同步強制，不留免認證窗口）。
- 內部批次端點（RevokeLeakedKeys，`/api/internal/*`）依 §2.1 屬 Internal Service Token / mTLS 範圍，本階段顯式 `AllowAnonymous`＋債務註解，不掛 JWT。

### 5. 測試基礎設施契約

- `TestInfrastructure` 新增 `TestTokenFactory`：以測試簽章金鑰簽發 SecurityAdmin / TenantAdmin / Consumer / System 四種 token（claims 依 §2）。
- `TestHooks` BeforeScenario 預設 `FunctionalTestContext.AuthToken` = SecurityAdmin token 並掛上 `Client.DefaultRequestHeaders.Authorization`——既有場景零改動保綠；場景措辭需要不同 actor 時由 step 換發 token 覆寫。

### 6. 本 ADR 接受時的同步項目

| 檔案 | 改動 | 時點 |
|---|---|---|
| `docs/design/api-spec.md` §2.1 | System 內部 JWT 條目＋`name` claim 增列 | 與本 ADR 同 commit |
| `tasks/todo.md` #25 | 結案註記（auth middleware 已落地） | Phase 2 實作 commit |
| `tasks/bdd-progress.md` 解鎖表 | 「Wave 3 開始前」條目標記已建立 | Phase 2 實作 commit |
| `docs/verification-matrix.md` | 本階段無新常設機械檢驗（見 Rationale），不登記 | — |

---

## Rationale

### 為什麼用真 JwtBearer 而不是測試專用假認證 scheme

api-spec §2.1 明文 `Bearer {JWT}`。假 scheme 讓 production 認證路徑永遠沒被任何測試走過——「測試綠」與「部署後能動」脫鉤。真 JWT 的增量成本只是測試端一個 token factory。

### 為什麼 Actor 走顯式參數鏈而不是 `ICurrentActor` DI service

endpoints 既有模式已注入 `HttpContext`（RevokeKeyEndpoint 先例）；Command record 加欄位使依賴可見、handler 測試無需 mock ambient service；scoped ICurrentActor＋middleware 填值是多一層註冊面與隱式耦合，本 repo 單一入口情境無對應收益。

### 為什麼角色授權（403）與 IDOR 後置

目前無任何場景斷言 403 / 跨租戶拒絕——先建 = 投機性防線（制度凍結啟發式同款論證，ADR-020 Relay 後置同構）。且 §2.1 SecurityAdmin 前綴表與各 endpoint Authorization 行存在不一致，403 policy 落地前必須先勘誤 spec，屬該場景 slice 的前置裁決，不在本 ADR 搶答。

### 為什麼不登記新 verification-matrix 常設檢驗

「control-plane 必認證」的常設守門會在「權限不足」場景（403）落地時以 BDD 場景形式自然到位；本階段以一次性故意紅（剝 token → 401）取證即可，避免為過渡態建常設機制。

---

## Consequences

### Positive

- Wave 3 全檔（含 System 拒絕、Consumer 權限不足的前置身分機制）解鎖；`FunctionalTestContext.AuthToken` 佔位欄啟用。
- 既有 control-plane endpoints 同步關閉免認證窗口，todo #25 主體解消。
- Actor 型別一次落 SharedKernel，後續 revokedBy / resumedBy / unlockedBy 回補為純機械性欄位增量。

### Negative / Trade-offs

- 對稱金鑰單一 secret，洩漏即可偽造任意角色 token。
  - Mitigation: 金鑰只經 configuration 注入（不落 repo，`Jwt:SigningKey` 缺失時啟動 fail-loud）；非對稱簽章／外部 IdP 屬未來多簽發者 ADR。
- issuer / audience 不驗，token 可跨環境重放（dev token 打 staging）。
  - Mitigation: 各環境簽章金鑰不同，簽章驗證即隔離；明文記錄於 §1，多簽發者時補驗。
- 401 為裸 status code，與 api-spec §2.2 UNAUTHORIZED envelope 暫不一致。
  - Mitigation: 易混淆表明文登記為後置項；有場景斷言 body 時補 RFC 9457 shaping，非默默偏離。
- 既有全部功能場景隱式依賴預設 SecurityAdmin token，場景讀者看不到認證前提。
  - Mitigation: 預設 token 佈署在 TestHooks 單點＋註解；需區分 actor 的場景一律顯式 Given 換 token（03 檔起即為範例）。

---

## Alternatives Considered

### Alternative A: 測試專用假認證 handler（`AuthenticationHandler` stub），production 不裝 JWT

Rejected. production 認證路徑零測試覆蓋，spec §2.1 的 Bearer JWT 契約名存實亡；與「測試綠＝可交付」的驗證紀律直接矛盾。

### Alternative B: 自建最小 JWT 解析 middleware，不引 JwtBearer 套件

Rejected. 手寫 token 驗證是 OWASP 級自傷（alg confusion、時序、過期邊界），違反 Security First；JwtBearer 是框架第一方套件，依賴預算成本近零（NU1903/1904 弱點 gate 自動納管）。

### Alternative C: `ICurrentActor` scoped service＋middleware 填值

Rejected. 隱式 ambient 依賴，handler 單測需 mock 註冊；endpoints 既有 `HttpContext` 注入模式已提供讀取點，顯式參數鏈更短且可審計（Rationale 詳述）。

### Alternative D: 被動基礎建設——只裝管線，既有 endpoints 維持匿名

Rejected. 2026-07-10 使用者裁決同步強制：同一服務內「部分 endpoint 免認證」是不一致態，且 todo #25 繼續懸掛；既有場景以預設 token 保綠，改動成本一次付清。

### Alternative E: 本波順手回補 revokedBy（Command／事件／Response 欄位）

Rejected. 2026-07-10 使用者裁決獨立小包：基礎設施 slice 混入行為面改動違反單一職責拆包紀律；Actor 型別落地後回補為純機械性增量，遲付無息。

---

## Implementation Rules

1. JWT 驗證參數依 §1：簽章＋存活期必驗、issuer/audience 不驗、`NameClaimType="sub"`、`RoleClaimType="role"`；簽章金鑰唯一來源為 configuration `Jwt:SigningKey`，缺失時啟動 fail-loud，禁止硬編碼 fallback。
2. `Actor` record 與 `Actor.FromClaims` 唯一落點為 `SharedKernel/Domain`；BC 內不得自建 actor/claims 解析。
3. Actor 一律走顯式參數鏈（endpoint → Command 欄位 → handler → domain → event）；`backend/src/` 禁止出現 `IHttpContextAccessor`。
4. 所有 `/api/v1/` control-plane endpoints 必掛 `.RequireAuthorization()`；`/api/internal/` 端點顯式 `AllowAnonymous`＋債務註解，二者擇一必居其一，不得無標記。
5. 測試 token 簽發唯一落點為 `TestInfrastructure` 的 `TestTokenFactory`；步驟類別不得自行組 JWT。
6. 後置項（403 role policy、IDOR、RFC 9457 401/403 body、internal token、revokedBy 回補）落地前，`backend/src/` 不得出現對應的半成品實作；各項落地時若涉 §2.1 前綴表勘誤，先補 spec 再寫碼。
7. **驗收**：

   ```bash
   git --no-pager grep -n 'IHttpContextAccessor' -- backend/src/
   # 預期 0 命中（Rule 3）
   git --no-pager grep -Ln 'RequireAuthorization|AllowAnonymous' -- backend/src/*/[A-Z]*Endpoint.cs
   # 預期 0 命中（Rule 4：每個 endpoint 檔必有其一）
   ```

8. 任何提案修改 1–7，必須先開新 ADR。
