# 需求分析與設計方法論

本文件定義從需求到可執行規格的展開流程，確保團隊在分析與設計階段有一致的作法。

---

## 1. 文件層級與展開順序

| Step | 文件 | 目的 | 產出 |
|------|------|------|------|
| 1 | PRD | 定義「做什麼」與「為什麼」 | 功能範圍、使用者角色、業務規則 |
| 2 | 高階 Design Doc | 定義系統邊界與核心結構 | Bounded Context、Context Map、狀態機、核心流程、ADR |
| 3 | Context Integration Spec | 定義 BC 之間如何對話 | 通訊方式（同步/非同步）、API 契約、Domain Event Payload、錯誤處理 |
| 4 | Per-BC Detailed Design | 定義每個 BC 內部怎麼運作 | Aggregate 行為規格、Domain Service、Repository 介面、設計模式 |
| 5 | Specification by Example | 定義怎麼驗收、怎麼測試 | BDD 場景（Given/When/Then） |

### 展開原則

- **逐層展開，不跳級**：每一層的輸入是上一層的產出。
- **技術無關**：Step 1-5 不綁定特定技術框架，所有文件應能適用於任何技術堆疊。
- **一致性檢查**：展開下一層時，回頭檢查上一層是否需要修正（雙向回饋）。

---

## 2. Step 3 — Context Integration Spec

### 目的

定義 BC 之間的契約，使不同 BC 可以**平行開發**。

### 內容結構

每對有交互的 BC，描述以下項目：

```
BC-A → BC-B
├── 觸發場景：什麼業務事件引發通訊
├── 通訊方式：同步 API / 非同步 Event / 混合
├── 契約規格：
│   ├── 同步：Command 名稱、Input、Output、錯誤碼
│   └── 非同步：Event 名稱、Payload Schema、順序保證
├── 失敗處理：重試策略、降級行為、冪等性要求
└── 資料一致性：最終一致 / 強一致、補償機制
```

### 完成標準

- 所有 Context Map 上的關係都有對應的整合規格。
- 每個非同步 Event 都有明確的 Payload Schema。
- 失敗場景都有定義處理策略。

---

## 3. Step 4 — Per-BC Detailed Design

### 目的

定義每個 BC 的 Aggregate 行為，使開發者可以直接實作。

### 執行順序

按 BC 重要性排序，Core 先做：

1. **Key Lifecycle**（Core）
2. **Access Policy**（Supporting，與 Core 緊密耦合）
3. **Monitoring & Detection**（Supporting）
4. **Audit & Compliance**（Supporting）
5. **Tenant Management**（Generic）

### Aggregate 行為規格格式（必須遵循）

每個 Aggregate 的每個行為，一律使用以下結構：

```
Command:  [命令名稱]
Guard:    [前置條件 / 不變條件檢查，用 AND/OR 連接]
State:    [狀態變更：從 → 到]
Event:    [產生的 Domain Event 名稱 + 關鍵欄位]
```

範例：

```
Command:  CreateApiKey
Guard:    租戶金鑰數 < 上限 AND 名稱在租戶內不重複
State:    → PendingActivation
Event:    ApiKeyCreated { keyId, tenantId, name, scope, createdAt }

Command:  RevokeApiKey
Guard:    狀態 ∈ { Active, Suspended } AND 操作者有權限
State:    Active/Suspended → Revoked
Event:    ApiKeyRevoked { keyId, revokedBy, reason, revokedAt }
```

**這個格式的目的**：讓 Step 4 的產出可以機械式推導出 Step 5 的 BDD 場景。

- `Guard` → Given 的變體（通過 vs 不通過）
- `Command` → When
- `State` + `Event` → Then

### 每個 BC 的內容結構

```
BC: [名稱]
├── Aggregate Root
│   ├── 行為規格（Command → Guard → State → Event）
│   ├── 不變條件彙整
│   └── 狀態轉換圖（如有）
├── Domain Service（跨 Aggregate 的邏輯）
├── Repository 介面（持久化契約）
└── 適用的設計模式與選擇理由
```

---

## 4. Step 4 → Step 5 的銜接：Example Mapping

### 問題

Step 4（內部設計）是**由內而外**的視角，Step 5（BDD 場景）是**由外而內**的視角。直接從 Step 4 跳到 Step 5，團隊容易卡住。

### 解法：Example Mapping Workshop

**Example Mapping** 是 Matt Wynne 提出的協作技巧，用四種卡片把設計和行為串起來：

- 🟡 **Story**：一個 Command（來自 Step 4）
- 🔵 **Rule**：Guard / 不變條件（來自 Step 4）
- 🟢 **Example**：具體場景 → 這就是 BDD 場景的草稿
- 🔴 **Question**：團隊發現的疑問 → 回到設計或 PRD 釐清

### 執行方式

1. 從 Step 4 取出一個 Command（🟡），放在最上方。
2. 把該 Command 的所有 Guard 列出來（🔵），放在 Story 下方。
3. 每個 Guard，團隊一起想出「通過的例子」和「不通過的例子」（🟢）。
4. 過程中發現的疑問記在紅色卡片上（🔴）。
5. 每個 Command 約 20-30 分鐘。

### 參與角色

- **必須**：開發者（熟悉 Step 4 設計）
- **必須**：PO 或 Domain Expert（確認業務正確性）
- **建議**：QA（擅長想邊界案例）

### 產出

🟢 卡片整理後，直接轉成 Step 5 的 Given/When/Then 格式。

---

## 5. Step 5 — Specification by Example（BDD 場景）

### 目的

三合一：

1. **使用情境**：使用者怎麼用
2. **驗收條件**：怎麼算做完
3. **可執行規格**：直接變成 BDD 測試

### 場景格式

```gherkin
Feature: [功能名稱]

  Scenario: [場景描述]
    Given [前置條件 — 來自 Guard 的正向/反向設定]
    When  [操作 — 來自 Command]
    Then  [預期結果 — 來自 State 變更 + Event]
```

### 從 Step 4 推導的範例

Step 4 Aggregate 行為規格：

```
Command:  CreateApiKey
Guard:    租戶金鑰數 < 上限 AND 名稱在租戶內不重複
State:    → PendingActivation
Event:    ApiKeyCreated { keyId, tenantId, name, scope, createdAt }
```

推導出的 BDD 場景：

```gherkin
Feature: 建立 API Key

  Scenario: 成功建立金鑰
    Given 租戶目前有 5 把金鑰，上限為 10
    And   租戶內沒有名為 "my-service-key" 的金鑰
    When  租戶建立名為 "my-service-key" 的金鑰
    Then  金鑰狀態為 PendingActivation
    And   產生 ApiKeyCreated 事件

  Scenario: 超過金鑰數量上限
    Given 租戶目前有 10 把金鑰，上限為 10
    When  租戶建立新金鑰
    Then  建立失敗，錯誤原因為「超過金鑰上限」

  Scenario: 金鑰名稱重複
    Given 租戶內已有名為 "my-service-key" 的金鑰
    When  租戶建立名為 "my-service-key" 的金鑰
    Then  建立失敗，錯誤原因為「金鑰名稱重複」
```

---

## 6. 執行節奏

### 設計階段：逐 BC 走完 Step 4 → Example Mapping → Step 5

```
Key Lifecycle:  Step 4 設計 → Example Mapping → Step 5 BDD
Access Policy:  Step 4 設計 → Example Mapping → Step 5 BDD
Monitoring:     Step 4 設計 → Example Mapping → Step 5 BDD
Audit:          Step 4 設計 → Example Mapping → Step 5 BDD
Tenant:         Step 4 設計 → Example Mapping → Step 5 BDD
```

理由：做完一個 BC 的 Step 4 後，設計上下文還在腦中，馬上做 Example Mapping 最順暢。

### 實作階段：逐場景垂直切片（⚠️ 不是逐 BC）

設計文件完成後，**實作必須以場景（scenario）為單位推進**，而非以 BC 為單位。

```
場景 1：成功建立金鑰
  → 實作 TenantManagement（只實作場景需要的部分）
  → 實作 KeyLifecycle CreateApiKey slice
  → 實作 AccessPolicy CreatePolicy slice（金鑰建立後的預設 policy）
  → 場景通過 ✅

場景 2：租戶不存在，拒絕建立
  → 在現有 TenantManagement 上擴充 guard 邏輯
  → 場景通過 ✅

場景 N：下一個場景 ...
```

### 為什麼實作階段要逐場景而非逐 BC

- **持續交付**：每個場景通過就是一個可交付的功能切片
- **避免過度設計**：只實作場景需要的最小 BC 切片，不預先建整個 BC
- **跨 BC 場景是常態**：「成功建立金鑰」就同時涉及 TenantManagement、KeyLifecycle、AccessPolicy
- **BDD 的測試單位是場景**，不是 BC；測試 assembly 也因此應統一為一個 `FunctionalTests`

### 錯誤做法：全部 BC 逐一做完再整合

```
KeyLifecycle 全部做完 → AccessPolicy 全部做完 → TenantManagement 全部做完 → 整合測試
↑ 整合晚、風險高、無法持續交付
```

---

## 7. 品質檢查點

每個 Step 完成後，在進入下一步之前確認：

### Step 3 完成後

- [ ] 所有 Context Map 關係都有整合規格
- [ ] 非同步 Event 都有 Payload Schema
- [ ] 失敗場景都有處理策略

### Step 4 完成後（每個 BC）

- [ ] 所有 Aggregate 行為都用 Command → Guard → State → Event 格式描述
- [ ] 不變條件完整且無矛盾
- [ ] 與 Step 3 的契約一致

### Example Mapping 完成後

- [ ] 每個 Command 都做過 Example Mapping
- [ ] 🔴 Question 卡片都已釐清或記錄到 Open Questions
- [ ] 🟢 Example 卡片已轉成 Given/When/Then 草稿

### Step 5 完成後（每個 BC）

- [ ] 每個 Guard 條件都有正向和反向場景
- [ ] 場景使用領域語言，非技術人員可讀
- [ ] 場景可直接用於 BDD 測試框架
