# BDD Scenario Backlog

新場景從 Discovery（`requirements-analysis-design` skill）產出後，先進此處等待排程。（Discovery 新場景產出目前凍結 — 見 CLAUDE.md「BDD Scenario Development Cycle」段；凍結解除前 `requirements-analysis-design` skill 的 Step 5 不得產出新場景檔。**範圍化解除逐案走 `docs/adr/adr-030-discovery-freeze-scoped-lift.md`**：判準為「API 契約已在 api-spec 定案但 `.feature` 從未產出」，目前已解除 Data Plane（api-spec §4），場景須可指回具體規格條目、不得發明規格外行為。既有場景修訂／缺陷再現／行為移除不受此凍結限制，走 `docs/adr/adr-022-bdd-requirement-type-routing.md` 分流）
只有用戶決定順序後，才將項目移入 `tasks/bdd-progress.md`。
Claude **不得自主將項目從 backlog 升格到 progress**。

## 看板流程

```
Discovery → bdd-backlog.md → (用戶決定順序) → bdd-progress.md → ✅ Done
```

## 升格步驟（由用戶執行）

1. 決定新場景在 `bdd-progress.md` 中的插入位置
2. 將項目從 backlog 移至 `bdd-progress.md` 對應位置
3. 在對應的 `.feature` 檔案加上 `@ignore` tag
4. 若插入位置在現有場景中間，確認 `.feature` 檔案命名前綴是否需要調整

## 格式

```
- [ ] **Scenario 名稱** (`FeatureFile.feature`)
      來源：[discovery session / 需求變更 / 其他]
      說明：簡要描述場景意圖
```

---

## 待排程項目

## 已產出但未展開的場景（`docs/bdd/` → `.feature`）

> **2026-07-26 登記**：`docs/bdd/` 的五份 Discovery Step 5 規格中，**只有 key-lifecycle 被展開進 `.feature`**；其餘四個 BC 合計 **77 條 Gherkin 場景從未展開**，這條管線至今未被啟用。
>
> **這些不受 Discovery 凍結限制** —— 凍結管的是「產出新場景」（`requirements-analysis-design` Step 5），而這 77 條早在 2026-05-02 就已產出。待辦是**展開**：`docs/bdd/` → 本檔 → 使用者決定順序 → `tasks/bdd-progress.md` → `.feature`（帶 `@ignore`）。
>
> **以 Feature 為登記粒度而非逐條列 77 個標題**：場景全文的單一事實來源是 `docs/bdd/`，把標題複製到本檔會製造兩份需同步的清單（drift 面）。晉升時再逐條展開該 Feature 的場景即可。
>
> **前置條件是各 BC 的實作深度，不是場景本身**——目前 TenantManagement 只有被 KL 呼叫的最小面（`ConsumerValidatorService`）、AccessPolicy 是佔位聚合（無 `ipAllowlist`／`rateLimitConfig`）、Monitoring 與 Audit 只有空 csproj、outbox 有寫入端無消費端。各 Feature 的具體前置見下列。

### Tenant Management（19 條，`docs/bdd/tenant-management.md`）

- [ ] **Feature 1: 管理租戶**（8 條）— C1 CreateTenant／C2 SuspendTenant／C3 ReactivateTenant。前置：TM 目前無任何命令與端點，屬全新切片。
- [ ] **Feature 2: 管理 Consumer**（7 條）— C4 RegisterConsumer／C5 UpdateConsumer。同上。
- [ ] **Feature 3: 驗證 Consumer 身份**（4 條）— I1 查詢；**實作已存在**（`ConsumerValidatorService`，CreateApiKey 場景已間接覆蓋部分路徑），可能多為 test-only 啟用。

### Access Policy（15 條，`docs/bdd/access-policy.md`）

- [ ] **Feature 1: 建立 Access Policy**（2 條）— C1，經 I2 由 KL 交易內觸發；**實作已存在**（`CreateDefaultPolicyAsync`）。
- [ ] **Feature 2: 更新 IP 白名單**（6 條）— C2。前置：**聚合需補 `ipAllowlist` 欄位＋migration**；與 Wave 9 的「來源 IP 不在白名單」驗證場景共用同一前置。
- [ ] **Feature 3: 更新速率限制**（7 條）— C3。前置：**聚合需補 `rateLimitConfig` 欄位＋migration，且「系統預設值」需使用者裁定**（規格只寫「系統預設值」無數字）；與 Wave 9 的 `rateLimitConfig` 回應修訂共用同一前置。

### Monitoring & Detection（30 條，`docs/bdd/monitoring-detection.md`）

- [ ] **Feature 1: 管理偵測規則**（12 條）— C1–C3。前置：**整個 BC 只有空 csproj**。
- [ ] **Feature 2: 異常偵測與自動防禦**（7 條）— Detection Engine；I6 的**呼叫端**（接收端 `LockKeyEndpoint` 已存在）。
- [ ] **Feature 3: 管理安全警報**（7 條）— C4 AcknowledgeAlert／C5 ResolveAlert。
- [ ] **Feature 4: 使用基線管理**（4 條）— I4 事件消費。前置：**outbox 無消費端**（ADR-020 Relay 後置）。

### Audit & Compliance（13 條，`docs/bdd/audit-compliance.md`）

- [ ] **Feature 1: 審計記錄寫入**（6 條）— I3／I5／I9 事件消費。前置：**outbox 無消費端**——事件目前確實寫進表裡，但沒有任何東西讀它。
- [ ] **Feature 2: 審計記錄不可變性**（2 條）— 同上。
- [ ] **Feature 3: 審計記錄查詢**（5 條）— Q: SearchAuditLogs／ExportAuditLogs 端點。

---

## Data Plane（ADR-030 授權產出，非 Discovery 產物）

> **2026-07-26 使用者晉升第一批**：Data Plane 9 條中的 8 條已晉升為 **Wave 8**，順序即 `07_ValidateKey.feature` 檔內順序（首條為「成功驗證 Active 金鑰」），詳見 `tasks/bdd-progress.md`。剩餘 1 條留 Wave 9，因其需要 AccessPolicy 側 `ipAllowlist`，而 2026-07-26 題 3 裁決第一刀只查 KeyLifecycle 側欄位。

- [ ] **場景修訂：成功驗證回應補回 `rateLimitConfig`** (`07_ValidateKey.feature`)
      來源：需求變更 — 2026-07-26 使用者裁決選項 b（AccessPolicy 聚合尚無該欄位、預設值亦無規格定義，故第一刀先移除該斷言）
      說明：Wave 9 隨 AP 側欄位一起補；前置為「AccessPolicy 加 rateLimitConfig 欄位＋migration＋**使用者裁定系統預設值**」（api-spec 的 10000／PT1H／100／150 僅出現在範例 payload，非規格）。修訂走 ADR-022 §2 既有行為變更路徑
- [ ] **來源 IP 不在白名單 — 拒絕驗證** (`07_ValidateKey.feature`，置於檔尾)
      來源：ADR-030 spec-derived — api-spec §4.1 錯誤碼表 IP_NOT_ALLOWED 列（Layer 3）
      說明：**需 AP 側 ipAllowlist**；Wave 8 期間漏斗第 3 層走「無白名單即放行」的既有語意，本條隨 AP 側欄位可讀時晉升 Wave 9
