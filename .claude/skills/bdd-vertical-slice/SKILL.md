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

---

## 實作節奏（BDD Red-Green 循環）

```
1. 選定下一個場景（從 FunctionalTests/Features/ 的 .feature 檔）
2. 執行 dotnet test → 確認場景 Pending（step 尚未實作）
3. 識別場景涉及哪些 BC（分析 Given/When/Then 步驟）
4. 逐 BC 實作最小切片（Domain → Handler → Endpoint）
5. 補齊 step definitions（實際 HTTP 呼叫）
6. 執行 dotnet test → 場景 Green ✅
7. 回到步驟 1，選下一個場景
```

---

## 專案結構速查

### BDD 測試位置

```
backend/tests/FunctionalTests/
├── Features/KeyLifecycle/     ← .feature 檔（44 scenarios）
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
| ACTIVE 金鑰數 / 上限 | KeyLifecycle（guard） |
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
[Then(@"金鑰狀態為 ACTIVE")]
public async Task ThenKeyStatusIsActive()
{
    _ctx.Response.StatusCode.Should().Be(HttpStatusCode.Created);
    var body = JsonSerializer.Deserialize<CreateApiKeyResponse>(_ctx.ResponseBody!);
    body!.Status.Should().Be("ACTIVE");
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
dotnet test tests/FunctionalTests/

# 只執行指定 feature 的場景
dotnet test tests/FunctionalTests/ --filter "FullyQualifiedName~建立API金鑰"

# 建置確認（0 error 才繼續）
dotnet build ApiKeyManagement.slnx
```

---

## 目前進度

**已通過場景：** 0 / 44（全部 Pending）

**下一個目標場景：**
```gherkin
# Features/KeyLifecycle/CreateApiKey.feature
Scenario: 成功建立金鑰（第一個場景）
```

涉及 BC：TenantManagement（租戶驗證）、KeyLifecycle（CreateApiKey）、AccessPolicy（預設 policy）
