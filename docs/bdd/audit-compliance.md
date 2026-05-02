# Audit & Compliance — BDD Specification

> Step 5 展開，按 [design-methodology.md](../design-methodology.md) §5 格式撰寫。
> 本 BC 無用戶命令（純事件消費），場景由 [bc-audit-compliance.md](../detailed-design/audit-compliance.md) 的事件消費規則、不變條件與查詢介面推導。

---

## Feature 對照

| Feature | 對應來源 | Scenario 數 |
|:--------|:---------|:------------|
| 1. 審計記錄寫入 | 事件消費（I3 + I5 + I9）、INV-2、INV-3 | 6 |
| 2. 審計記錄不可變性 | INV-1（WORM） | 2 |
| 3. 審計記錄查詢 | SearchAuditLogs、ExportAuditLogs | 5 |

---

## Feature 1: 審計記錄寫入

```gherkin
Feature: 審計記錄寫入

  # --- 各整合通道的代表場景 ---

  Scenario: Key Lifecycle 建立事件 → 產生審計記錄（I3）
    Given Key Lifecycle 發布 KeyCreated 事件，keyId "key-A", tenantId "tenant-A"
    When  Audit 接收到該事件
    Then  系統建立 AuditEntry：action 為 KEY_CREATED, resourceType 為 "ApiKey", resourceId 為 "key-A"
    And   snapshotBefore 為 null，snapshotAfter 為完整金鑰屬性（不含 hash）
    And   tenantId 為 "tenant-A"

  Scenario: Key Lifecycle 狀態變更事件 → 包含 before/after 與 reason（I3）
    Given Key Lifecycle 發布 KeyRevoked 事件，keyId "key-A", previousStatus Active, reason "安全疑慮"
    When  Audit 接收到該事件
    Then  系統建立 AuditEntry：action 為 KEY_REVOKED
    And   snapshotBefore 為 { status: Active }，snapshotAfter 為 { status: Revoked }
    And   reason 為「安全疑慮」

  Scenario: Access Policy 變更事件 → 產生審計記錄（I5）
    Given Access Policy 發布 PolicyUpdated 事件，changedFields ["ipAllowlist"], 含 before 和 after
    When  Audit 接收到該事件
    Then  系統建立 AuditEntry：action 為 POLICY_UPDATED, resourceType 為 "AccessPolicy"
    And   snapshotBefore 和 snapshotAfter 分別反映修改前後的 ipAllowlist

  Scenario: Monitoring 偵測事件 → 產生審計記錄（I9）
    Given Monitoring 發布 AnomalyDetected 事件，keyId "key-B", ruleId "high-failure-rate"
    When  Audit 接收到該事件
    Then  系統建立 AuditEntry：action 為 ANOMALY_DETECTED, resourceType 為 "ApiKey"
    And   snapshotBefore 為 null，snapshotAfter 包含偵測 details

  # --- 不變條件反向 ---

  Scenario: 重複事件 — 冪等跳過
    Given Audit 已存在 eventId "evt-123" 的 AuditEntry
    When  系統再次投遞 eventId "evt-123" 的事件
    Then  不建立新的 AuditEntry，直接 ACK

  Scenario: 事件缺少 tenantId — 拒絕寫入
    Given 收到一筆事件，tenantId 為空
    When  Audit 嘗試處理該事件
    Then  寫入失敗，事件進入 DLQ 待人工處理
```

---

## Feature 2: 審計記錄不可變性

```gherkin
Feature: 審計記錄不可變性

  Scenario: 寫入後不可修改
    Given AuditEntry "entry-1" 已寫入
    When  任何操作嘗試修改 "entry-1" 的內容
    Then  操作被拒絕，錯誤原因為「審計記錄不可修改」

  Scenario: 寫入後不可刪除
    Given AuditEntry "entry-1" 已寫入
    When  任何操作嘗試刪除 "entry-1"
    Then  操作被拒絕，錯誤原因為「審計記錄不可刪除」
```

---

## Feature 3: 審計記錄查詢

```gherkin
Feature: 審計記錄查詢

  # --- SearchAuditLogs ---

  Scenario: 按租戶查詢審計記錄 — 租戶隔離
    Given "tenant-A" 有 50 筆審計記錄，"tenant-B" 有 30 筆
    When  查詢 "tenant-A" 的審計記錄
    Then  只回傳 "tenant-A" 的記錄，不包含 "tenant-B" 的資料

  Scenario: 多條件篩選
    Given "tenant-A" 有多筆審計記錄
    When  查詢 "tenant-A" 的記錄，篩選 action = KEY_REVOKED, timeFrom = 7 天前, resourceType = "ApiKey"
    Then  只回傳符合所有條件的記錄

  Scenario: Cursor 分頁
    Given "tenant-A" 有 150 筆審計記錄
    When  查詢 "tenant-A" 的記錄，pageSize 為 50
    Then  回傳前 50 筆記錄和 nextCursor
    When  使用 nextCursor 查詢下一頁
    Then  回傳第 51-100 筆記錄和下一個 nextCursor

  # --- ExportAuditLogs ---

  Scenario: 匯出審計記錄
    Given "tenant-A" 有過去 30 天的審計記錄
    When  要求匯出 "tenant-A" 過去 30 天的記錄，format 為 CSV
    Then  系統建立非同步匯出任務，回傳任務 ID
    And   完成後通知下載

  # --- Guard 反向 ---

  Scenario: pageSize 超過上限 — 拒絕查詢
    When  查詢審計記錄，pageSize 為 200（超過上限 100）
    Then  查詢失敗，錯誤原因為「pageSize 不可超過 100」
```
