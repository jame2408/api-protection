---
name: bdd-vertical-slice
description: >-
  Guide implementing BDD scenarios as vertical slices across Bounded Contexts.
  Use when: (1) starting a new BDD scenario implementation, (2) deciding which
  BC code to write for a given feature step, (3) user says "implement scenario",
  "make scenario pass", "next scenario", or "BDD slice".
metadata:
  trigger: '"/bdd-vertical-slice", "implement scenario", "make the scenario pass", or "next scenario"'
---

# BDD Vertical Slice Implementation Guide

本專案的 BDD 開發節奏是**以場景（scenario）為單位推進**，而非以 Bounded Context 為單位。

## 核心原則

> **一個場景 = 一個垂直切片 = 可交付的最小功能**

一個 Gherkin scenario 可能橫跨多個 BC。實作時，每個 BC **只實作該場景需要的最小切片**，不預先建整個 BC 的完整功能。

> **協作模式**：在多模型協調模式下，本流程由 orchestrator 以 `tasks/_templates/executor-spec.md` 派工、executor 依規格執行、orchestrator 驗證後放行 commit（詳見 `docs/orchestration.md`）。

---

## 實作節奏（BDD Red-Green-Refactor 循環）

> 前置：本 skill 只處理已在 `tasks/bdd-progress.md` 的場景；backlog → progress 的晉升是使用者專屬動作，不得自行執行（見 CLAUDE.md「BDD Scenario Development Cycle」段）。

```
1. 讀取 tasks/bdd-progress.md，找到「下一個」欄位確認目標場景。
   若需驗證與 .feature 檔一致，可執行：
   grep -rn "@ignore" backend/tests/FunctionalTests/Features/ | sort | head -1

2. 移除該場景的 @ignore tag（只移除一個；例外：多個 scenarios 共用完全相同的新
   step definitions 時可一併移除 — 見 CLAUDE.md「BDD Scenario Development Cycle」段）

3. 執行 dotnet test → 確認 Red（場景 Pending）
   → 若移除 @ignore 後直接 Green（啟用型場景），適用故意紅義務：不得視為完成，
     依 tasks/_templates/executor-spec.md「故意紅」欄位補驗證，做法見對應 lesson
     （tasks/lessons.md，不在此重複其步驟）。

4. Pre-implementation check — 對照 CLAUDE.md §4 Verification Standards 確認：
   - Result pattern（Result<T, Failure>，禁止 throw 控制業務邏輯）
   - cancel 命名，CancellationToken 傳遞至所有 I/O
   - DI lifetime（Scoped 服務禁用 IServiceScopeFactory）
   - 禁止在 Service/Domain 注入 ILogger

5. 識別場景涉及哪些 BC（分析 Given/When/Then 步驟）

6. 撰寫 step definitions → 執行 dotnet test 確認失敗為「not implemented」

7. 先診斷失敗點：若 guard/slice 邏輯已存在、失敗純因 step 缺 seed 資料，走「純
   test-side 修正」路徑（production 零異動，先例見 commit 9101bff、26a1160），
   回到步驟 6 修正 step definitions 即可；只有切片邏輯真的缺少時才逐 BC 實作
   （Domain → Repository 介面 → Handler → Repository 實作 → Endpoint → DI 註冊）

8. 執行 dotnet test → 場景 Green ✅（其他場景維持 pass/skip）

9. Refactor（見 CLAUDE.md「Refactor discipline」段及下方 checklist）
   → 執行 dotnet test → 確認仍 Green ✅

10. 更新 tasks/bdd-progress.md：標記 ✅，遞增「已通過」數
    （此更新須與本場景實作同一個 commit — 見 CLAUDE.md「BDD Scenario Development
    Cycle」段）

11. Commit，回到步驟 1
```

### Refactor Checklist（步驟 9）

隔離規則見 CLAUDE.md「Refactor discipline」段。兩種 Refactor 絕不混用，各自獨立執行後必須重新確認 Green：

> **留痕義務**：判斷結果必須留痕 — enablement commit 的 message 須含 `Refactor-assessment:` trailer（`scripts/git-hooks/commit-msg` 機械化強制，staged net `@ignore` 移除 ≥ 1 時觸發）；判斷「不重構」也要寫明理由，不得省略。以 spec 派工執行本流程時（見上方「協作模式」），spec 必須鏡射本步驟並要求 executor 回報，見 `tasks/_templates/executor-spec.md`「重構評估」欄。

| 類型 | 允許修改 | 禁止碰觸 |
|------|---------|---------|
| **Production Refactor** | `backend/src/` | `backend/tests/` |
| **Test Refactor** | `backend/tests/` | `backend/src/` |

**Production Refactor 檢查項目：**
- 對照 `.claude/references/dotnet/*.rule.md` 確認合規（Result pattern、`cancel` 命名、DI lifetime）
- 消除本輪實作引入的重複邏輯
- 命名規範（PascalCase 方法、`_camelCase` 欄位、`Async` suffix）

**Test Refactor 檢查項目：**
- Step definitions 可跨場景重用（避免 Given/When/Then body 複製貼上）
- NEVER access DB directly in When steps — 動作必須透過 API 或 domain 邊界觸發
- In Then steps, prefer API/observable state assertions. 只有在預期狀態為內部改變且未暴露於任何公開 API 時，才允許直接存取 DB 進行驗證
- 每個 step 職責單一清晰

---

## 專案結構速查

### BDD 測試位置

```
backend/tests/FunctionalTests/
├── Features/KeyLifecycle/     ← .feature 檔（場景總數以 tasks/bdd-progress.md 帳面為準）
├── Steps/                     ← Step Definitions（HTTP 呼叫）
├── Infrastructure/
│   ├── TestHooks.cs           ← Reqnroll hooks（容器生命週期）
│   └── FunctionalTestContext.cs  ← 共用狀態（Client / Response / AuthToken）
└── reqnroll.json
```

### 來源 BC 位置

```
backend/src/
├── KeyLifecycle/    ← Domain/ + CreateApiKey/ + RotateKey/ ...
├── AccessPolicy/
├── TenantManagement/
├── Monitoring/
├── Audit/
├── SharedKernel/    ← BC 間介面契約（IConsumerValidator 等）
└── Host/            ← Program.cs（純接線）
```

---

## Step 1：識別場景涉及的 BC

閱讀 scenario 的 Given/When/Then，對應到 BC：

| 步驟關鍵字 | 對應 BC |
|-----------|---------|
| 租戶 / Tenant / Consumer | TenantManagement |
| 金鑰建立 / CreateApiKey | KeyLifecycle |
| AccessPolicy / 預設 policy | AccessPolicy |
| Active 金鑰數 / 上限 | KeyLifecycle（guard） |
| Scope Registry | KeyLifecycle（ScopeRegistry） |

---

## Step 2：BC 最小切片實作順序

每個 BC 按以下順序實作（只做場景需要的部分）：

```
1. Domain（Aggregate / ValueObject / Domain Event）
2. Repository 介面（IXxxRepository in Domain/）
3. Handler（Application layer）
4. Repository 實作（Infrastructure/）
5. Endpoint（若場景是 HTTP 觸發）
6. DI 註冊（{BC}Module.cs）
```

---

## Step 3：補齊 Step Definitions

Step definitions 在 `tests/FunctionalTests/Steps/`。

**Given steps**：透過 API 或直接在 DB 建立前置資料
**When steps**：呼叫 `_ctx.Client.PostAsync(...)` 等 HTTP 方法，回應存入 `_ctx.Response`
**Then steps**：斷言 `_ctx.Response.StatusCode`、反序列化 body 檢查欄位

```csharp
// When 步驟範例
[When(@"""(.*)"" 在 Production 環境建立金鑰，...")]
public async Task WhenCreateApiKey(...)
{
    var request = new CreateApiKeyRequest { ... };
    _ctx.Response = await _ctx.Client.PostAsJsonAsync("/api/keys", request);
    _ctx.ResponseBody = await _ctx.Response.Content.ReadAsStringAsync();
}

// Then 步驟範例
// 依 ADR-006：必須以 raw JSON literal 鎖定 wire format，
// 不可只 deserialize 後比 enum value。
[Then(@"金鑰狀態為 Active")]
public Task ThenKeyStatusIsActive()
{
    _ctx.Response.StatusCode.Should().Be(HttpStatusCode.Created);
    using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
    doc.RootElement.GetProperty("lifecycleStatus").GetString().Should().Be("Active");
    return Task.CompletedTask;
}
```

---

## BC 間介面（SharedKernel/Contracts/）

BC 不直接參照彼此，透過 SharedKernel 定義介面，Host 負責接線：

| 介面 | 定義者 | 實作者 | 使用者 |
|------|--------|--------|--------|
| `IConsumerValidator` | SharedKernel | TenantManagement | KeyLifecycle |
| `IAccessPolicyService` | SharedKernel | AccessPolicy | KeyLifecycle |
| `IKeyLockService` | SharedKernel | KeyLifecycle | Monitoring |

---

## 常用指令

```bash
# 執行所有 BDD 場景
dotnet test backend/tests/FunctionalTests/

# 只執行指定 feature 的場景
dotnet test backend/tests/FunctionalTests/ --filter "FullyQualifiedName~建立API金鑰"

# 建置確認（0 error 才繼續）
dotnet build backend/ApiKeyManagement.slnx
```

