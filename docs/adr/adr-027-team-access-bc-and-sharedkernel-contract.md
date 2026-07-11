# Team Access BC 落地形態與 SharedKernel 契約

> Lead-in：role-management discovery（`docs/design/discovery/role-management/`）定案了 `Team`＋`AccessGrant` 兩個新 aggregate 與新 BC，但「新 BC 以什麼形態落地、金鑰建立時如何跨 BC 驗證 grant、失敗語意與存量金鑰如何處置」未固化。本 ADR 終結這組落地形態問題，是 discovery `adr-topics.md` 四題中的題 1。

---

## Status

Accepted (2026-07-11)

同步項目：`docs/design/design-doc.md` §3.1 Context 總覽表新增 Team Access 列（同 commit）。

---

## Context

### 現況

discovery 已由使用者裁決接受（`docs/design/discovery/role-management/model.md` 的「BC 邊界與分類建議」段）：

```
（★新）Team Access ｜ Team＋AccessGrant＋申請→核准工作流 ｜ Supporting
KeyLifecycle（既有）｜ ApiKey 微擴（grantId 引用＋快照 guard＋級聯撤銷訂閱）｜ Core（不變）
```

金鑰建立時需要新 guard「grant 須 Active、申請的 scope ⊆ grant 的 scope 集合」，但架構鐵律禁止 BC 直接互引（`BoundedContextIsolationTests` 的 `BoundedContext_ShouldNot_DependOn_OtherBoundedContexts`）。既有的跨 BC 查詢先例是 `docs/adr/adr-003-error-handling-and-cross-bc-contracts.md` §3「跨 BC contract 可以回傳 contract DTO」：

```csharp
// SharedKernel/Contracts/IConsumerValidator.cs（既有先例）
public record ConsumerValidationResult(bool IsValid, string? ErrorCode = null) { … }

public interface IConsumerValidator
{
    Task<ConsumerValidationResult> ValidateAsync(string tenantId, string consumerId, CancellationToken cancel = default);
}
```

`CreateApiKeyHandler` 現況的 scope guard 只查 Registry 存在性，尚無「誰有權用這個 scope」的概念：

```csharp
// CreateApiKeyHandler guard 4（現況）
var allScopesExist = await scopeRegistry.AllExistAsync(command.Scopes, cancel);
if (!allScopesExist)
    return FailureProvider.CreateFailure(CreateApiKeyFailureCodes.ScopeNotFound);
```

另有存量現實（discovery `open-questions.md` OQ4）：既有 3 個團隊的金鑰在 grant 概念出現前已發放，無 grant 可引用。

### 易混淆概念釐清

| 概念 | 是什麼 | 本 ADR 是否規範 |
|---|---|---|
| Team Access BC 落地形態（目錄、隔離登記） | 新 BC 的工程落點 | ✅ |
| 金鑰建立時的 grant 驗證契約（介面、DTO、失敗碼） | 跨 BC 查詢形狀 | ✅ |
| scope 快照的儲存落點與存量金鑰處置 | `ApiKey` 資料形狀 | ✅ |
| 團隊管理者的授權判斷落點 | Control Plane 授權骨架 | ❌ adr-topics 題 2 |
| Scope Registry 歸屬遷移 | 資料歸屬 | ❌ adr-topics 題 3 |
| `AccessGrantRevoked` 級聯撤銷的一致性語意 | 事件驅動時序 | ❌ adr-topics 題 4 |
| Team Access 的 endpoint 形狀與 BDD 場景 | API 細節與規格 | ❌ api-spec 與 requirements-analysis-design 於 slice 時定；場景產出另受 ADR-022 Discovery 凍結管轄 |

---

## Decision

### 1. Team Access 為獨立 BC，目錄 `backend/src/TeamAccess/`

循 `KeyLifecycle`／`AccessPolicy` 既有版型（Domain／各 use-case 目錄）。assembly 命名 `ApiKeyManagement.TeamAccess`，並登記進 `BoundedContextIsolationTests` 的 `KnownMinimumBoundedContexts`。

### 2. grant 驗證走 SharedKernel 同步查詢契約（ADR-003 §3 模式）

新增三檔於 `SharedKernel/Contracts/`，形狀循 `IConsumerValidator` 先例：

```csharp
public record AccessGrantValidationResult(bool IsValid, string? ErrorCode = null)
{
    public static AccessGrantValidationResult Valid() => new(true);
    public static AccessGrantValidationResult Invalid(string errorCode) => …; // errorCode 必填檢查同 ConsumerValidationResult
}

public interface IAccessGrantValidator
{
    Task<AccessGrantValidationResult> ValidateAsync(
        Guid grantId,
        string tenantId,
        string consumerId,
        IReadOnlyCollection<string> requestedScopes,
        CancellationToken cancel = default);
}
```

Team Access BC 提供實作；KeyLifecycle 的 handler 在 BC 邊界將 `ErrorCode` 轉 `Failure`（ADR-003 §3 責任分工不變）：

```csharp
// CreateApiKeyHandler（Team Access slice 落地後新增 guard）
var grantResult = await grantValidator.ValidateAsync(
    command.GrantId!.Value, command.TenantId, command.ConsumerId, command.Scopes, cancel);
if (!grantResult.IsValid)
    return FailureProvider.CreateFailure(grantResult.ErrorCode!);
```

### 3. 失敗碼固定三個，HTTP 映射延後到 api-spec 端點章節

`SharedKernel/Contracts/AccessGrantValidationFailureCodes.cs`：

```csharp
public static class AccessGrantValidationFailureCodes
{
    public const string GrantNotFound = "GRANT_NOT_FOUND";
    public const string GrantNotActive = "GRANT_NOT_ACTIVE";
    public const string ScopeNotGranted = "SCOPE_NOT_GRANTED";
}
```

HTTP status 映射是 HTTP 邊界決策（ADR-003 §4），於 api-spec 撰寫該 endpoint 章節時定案並登記 `ApiProblem.Map`（注意既有 duplicate-key 陷阱：`ResumeKeyFailureCodes` 檔頭註解的先例）。

### 4. scope 快照落點＝既有 `ApiKey.Scopes` 欄位；`ApiKey` 新增 nullable `GrantId`

- 快照語意（discovery R6／R11）由既有欄位天然承載：建立時已複製 `command.Scopes`，grant 後續變動不回寫。不新增任何快照儲存。
- `ApiKey` 新增 `GrantId`（nullable `Guid?`）；`null` ＝ grant 概念前發放的存量金鑰。
- `AccessGrant` 不持有金鑰資料；`AccessPolicy` BC 職責（scope／IP／rate 策略）不變。

### 5. 存量金鑰不回填，於重發時自然掛接

- 存量金鑰（`GrantId == null`）維持有效，不做資料回填、不做一次性遷移。
- Team Access slice 上線後，**新建**金鑰必須帶 `GrantId`；存量金鑰於下一次輪替（discovery 的「重發」路徑）時由新生金鑰掛上 grant，自然汰換。

---

## Rationale

### 為什麼同步查詢、不做投影複本

金鑰建立是低頻 Control Plane 操作（discovery R3–R5：3 團隊、季變動一兩次），無驗證路徑的效能壓力；同步查詢先例（`IConsumerValidator`）已在並有架構測試護欄。投影複本引入最終一致性，會出現「核准後立刻建 key 卻失敗」的窗口，為不存在的規模問題付基建成本。

### 為什麼新 BC、不併入既有 BC

`AccessGrant` 的變更理由是「隊對隊授權工作流」，與 `AccessPolicy`（金鑰附帶的策略資料）、`TenantManagement`（Generic 身分與隔離）的變更理由都不同；併入任一者都讓該 BC 服務兩個變更理由。且 discovery Phase C 已由使用者裁決新 BC，本 ADR 只固化工程落點。

### 為什麼存量不回填

3 團隊×2–3 把金鑰的存量，回填需要人工判斷「這把 key 對應哪個 grant」（grant 尚不存在，得先補建），一次性遷移的錯誤風險高於價值；重發路徑（R6 裁決的變更生效機制）本來就會汰換全部存量，借力即可。

---

## Consequences

### Positive

- 跨 BC 契約複用既有模式，`BoundedContextIsolationTests` 自動護欄新 BC。
- 快照語意零新增儲存；`GrantId` nullable 使存量零擾動。
- 失敗碼先固化，避免各 slice 自造字面值（ADR-003／004 的 bare-string 禁令延續）。

### Negative / Trade-offs

- 存量金鑰長期存在 `GrantId == null` 的雙態，查詢與報表要處理兩種形狀。
  - Mitigation: 重發路徑自然收斂；若需加速收斂，屆時以「限期輪替」營運手段處理，不動 schema。
- 同步查詢使 KeyLifecycle 建鍵路徑對 Team Access 產生執行期依賴（服務故障＝建鍵失敗）。
  - Mitigation: 同進程 modular monolith 下為同生共死，無跨網路故障面；未來拆服務時再依 ADR 治理條款重議。
- `ValidateAsync` 參數帶 `requestedScopes` 使契約偏寬（不只查存在，還做子集判斷）。
  - Mitigation: 子集判斷屬 grant 的不變量（scope ⊆ granted），放提供方單一實作，避免每個消費方各自重寫集合邏輯。

---

## Alternatives Considered

### Alternative A: 投影複本（Team Access 發事件、KeyLifecycle 維護本地 grant 讀模型）

Rejected. 最終一致性在「核准→立刻建 key」的主流程上開時間窗；量級（3 隊、10 API、季變動）無效能理由；投影基建（訂閱、補償、重建）成本遠超同步查詢。

### Alternative B: 併入 AccessPolicy BC

Rejected. AccessPolicy 管金鑰附帶的策略資料，AccessGrant 管隊對隊關係與核准工作流——兩個變更理由；併入後「改工作流」與「改策略形狀」互相牽動。discovery Phase C 使用者裁決亦為獨立 BC。

### Alternative C: 併入 Tenant Management BC（Team 視為身分概念）

Rejected. Tenant Management 是 Generic（design-doc §3.1），承載申請→核准業務工作流會讓 Generic BC 長出 Supporting 行為；且 Context Map 方向（Team Access → Tenant 為 Conformist）會被顛倒。

### Alternative D: 契約直接回傳 `Result<T, Failure>`

Rejected. ADR-003 §3 已裁決跨 BC 契約回傳 contract DTO、`Failure` 轉換屬 consuming BC 邊界責任；跨 BC 共享 `Failure` 構造會把 HTTP 語意滲進 SharedKernel。

---

## Implementation Rules

1. Team Access BC 落 `backend/src/TeamAccess/`，assembly `ApiKeyManagement.TeamAccess`；同 slice commit 將其加入 `BoundedContextIsolationTests.KnownMinimumBoundedContexts`，並以故意紅（暫時引用他 BC namespace）證明隔離測試對新 BC 生效。
2. 跨 BC 契約三檔落 `SharedKernel/Contracts/`：`IAccessGrantValidator`、`AccessGrantValidationResult`、`AccessGrantValidationFailureCodes`；介面帶 `CancellationToken cancel` 且逐呼叫傳遞。
3. 失敗碼字面值以 §3 為準；任何 slice 不得另造 grant 相關 bare-string 失敗碼。
4. `ApiKey.GrantId` 為 nullable；存量金鑰不回填；Team Access slice 上線後新建金鑰（含輪替新生）必須帶 `GrantId`。
5. HTTP 映射與 endpoint 形狀於 api-spec 對應章節撰寫時定案，`ApiProblem.Map` 屆時登記且不得與既有字面值 key 重複註冊。
6. **驗收**（Team Access 首 slice 落地時執行）：

   ```bash
   git --no-pager grep -n -E 'using ApiKeyManagement\.(KeyLifecycle|AccessPolicy|TenantManagement)' -- backend/src/TeamAccess/
   # 預期 0 命中（SharedKernel 除外的跨 BC 引用歸零）
   git --no-pager grep -n -E '"GRANT_NOT_FOUND"|"GRANT_NOT_ACTIVE"|"SCOPE_NOT_GRANTED"' -- backend/src/ ':!backend/src/SharedKernel/Contracts/AccessGrantValidationFailureCodes.cs'
   # 預期 0 命中（字面值單一來源）
   ```

7. 任何提案修改 1–6，必須先開新 ADR。
