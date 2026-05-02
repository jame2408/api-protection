# Audit & Compliance — Per-BC Detailed Design

> Step 4 展開。本 BC 是 Supporting Domain，核心特性是**純消費、不可變**：無用戶命令，僅透過事件訂閱寫入，寫入後不可修改。

**前置文件參照：**

- 領域模型：[Design Doc §4.4](../design/design-doc.md)
- 整合契約：[Integration Spec §4.3 I3, §4.5 I5, §4.8 I9](../design/context-integration-spec.md)
- Event Payload：[Integration Spec §6](../design/context-integration-spec.md)

---

## 1. Aggregate Root: AuditEntry

AuditEntry 不是傳統意義上的 Aggregate — 它沒有行為變更，只有「建立」。建立後即不可變（WORM 語意）。

### 1.1 屬性

| 屬性 | 類型 | 說明 |
|:-----|:-----|:-----|
| eventId | UUID | 即源事件的 eventId（去重依據） |
| tenantId | TenantId | 所屬租戶（必填，查詢隔離） |
| actor | Actor | 執行者（User 或 System） |
| action | AuditAction | 操作類型 |
| resourceType | String | 受影響的資源類型（如 "ApiKey", "AccessPolicy"） |
| resourceId | UUID | 受影響的資源 ID |
| snapshotBefore | JSON? | 修改前快照（建立類事件為 null） |
| snapshotAfter | JSON? | 修改後快照（刪除類事件為 null） |
| reason | String? | 操作理由（撤銷、暫停為必填） |
| context | EventContext | 來源 IP、User-Agent 等環境資訊 |
| occurredAt | Timestamp | 事件發生時間（來自 EventEnvelope） |
| correlationId | UUID | 業務流程關聯 ID |

### 1.2 不變條件

| # | 不變條件 | 說明 |
|:--|:---------|:-----|
| INV-1 | 寫入後不可變 | AuditEntry 一經建立即不可修改或刪除。儲存層必須支援 WORM 語意 |
| INV-2 | eventId 唯一 | 以 eventId 去重，同一事件不產生重複記錄 |
| INV-3 | tenantId 必填 | 所有 AuditEntry 都必須歸屬租戶，確保查詢隔離 |

---

## 2. Value Objects

**AuditAction**

```
AuditAction ∈ {
  // Key Lifecycle
  KEY_CREATED,
  KEY_ROTATION_INITIATED,
  KEY_GRACE_PERIOD_EXPIRED,
  KEY_REVOKED,
  KEY_EXPIRED,
  KEY_LOCKED,
  KEY_UNLOCKED,
  KEY_SUSPENDED,
  KEY_RESUMED,

  // Access Policy
  POLICY_CREATED,
  POLICY_UPDATED,

  // Monitoring
  ANOMALY_DETECTED,
  IMPOSSIBLE_TRAVEL_DETECTED
}
```

**EventContext**

```
EventContext {
  sourceIp:  String?     — 操作者來源 IP（系統事件可能為 null）
  userAgent: String?     — User-Agent
  requestId: UUID?       — 原始請求 ID（可追蹤）
}
```

---

## 3. 事件消費與轉換

本 BC 的核心邏輯是**事件 → AuditEntry 的轉換**。無用戶命令。

### 3.1 轉換規則

```
EventToAuditEntry(envelope: EventEnvelope):

  1. 冪等檢查：eventId 是否已存在 → 跳過
  2. 建立 AuditEntry：
     eventId        = envelope.eventId
     tenantId       = envelope.tenantId
     actor          = envelope.actor
     action         = mapEventTypeToAction(envelope.eventType)
     resourceType   = envelope.aggregateType
     resourceId     = envelope.aggregateId
     snapshotBefore = extractBefore(envelope.payload)  — 事件特定
     snapshotAfter  = extractAfter(envelope.payload)   — 事件特定
     reason         = extractReason(envelope.payload)   — 僅部分事件有
     context        = extractContext(envelope)
     occurredAt     = envelope.occurredAt
     correlationId  = envelope.correlationId
  3. 寫入 Append-only 儲存
  4. ACK
```

### 3.2 各事件的快照提取

| 來源事件 | snapshotBefore | snapshotAfter | reason |
|:---------|:---------------|:--------------|:-------|
| KeyCreated | null | 完整金鑰屬性（不含 hash） | — |
| KeyRotationInitiated | { status: Active } | { status: Rotating, successorKeyId } | — |
| KeyRevoked | { status: previousStatus } | { status: Revoked } | payload.reason |
| KeyExpired | { status: previousStatus } | { status: Expired } | — |
| KeyLocked | { status: Active } | { status: Locked } | payload.reason |
| KeyUnlocked | { status: Locked } | { status: Active } | — |
| KeySuspended | { status: Active } | { status: Suspended } | payload.reason |
| KeyResumed | { status: Suspended } | { status: Active } | — |
| PolicyCreated | null | 完整策略配置 | — |
| PolicyUpdated | payload.before | payload.after | — |
| AnomalyDetected | null | payload.details | — |
| ImpossibleTravelDetected | null | payload（含 locations） | — |

---

## 4. 查詢介面

### 4.1 SearchAuditLogs

```
Query:  SearchAuditLogs
Input: {
  tenantId:     UUID                — 必填（租戶隔離）
  timeFrom?:    Timestamp           — 起始時間
  timeTo?:      Timestamp           — 結束時間
  actor?:       String              — 執行者 ID
  action?:      AuditAction         — 操作類型
  resourceId?:  UUID                — 資源 ID
  resourceType?: String             — 資源類型
  pageSize:     Integer             — 每頁筆數（上限 100）
  cursor?:      String              — 分頁游標
}

Output: {
  entries:    List<AuditEntry>
  nextCursor: String?               — 下一頁游標（null 表示最後一頁）
  totalCount: Integer               — 符合條件的總筆數
}
```

**設計決策：** 使用 cursor-based 分頁而非 offset-based，因為審計日誌量大且持續增長，offset 在深頁查詢時效能差。

### 4.2 ExportAuditLogs（批次匯出）

```
Query:  ExportAuditLogs
Input: {
  tenantId:   UUID
  timeFrom:   Timestamp             — 必填
  timeTo:     Timestamp             — 必填
  format:     "JSON" | "CSV"
}

Output: 非同步匯出任務 ID，完成後通知下載
```

用途：匯出至外部 SIEM 或合規報告。

---

## 5. Repository 介面

```
AuditEntryRepository {
  append(entry: AuditEntry): void          — 僅支援新增
  existsByEventId(eventId: UUID): Boolean  — 冪等檢查
  search(criteria: SearchCriteria): Page<AuditEntry>
}
```

**禁止的操作：** 不提供 update、delete 方法。儲存層設計必須在技術層面防止修改和刪除。

---

## 6. Application Service 協調流程

### 6.1 事件消費（I3 + I5 + I9）

```
on DomainEvent(envelope):
  1. 冪等檢查：repo.existsByEventId(envelope.eventId)
     → true: skip + ACK
  2. entry = EventToAuditEntry(envelope)
  3. repo.append(entry)
  4. ACK
```

三個整合通道（I3 KL 事件、I5 AP 事件、I9 MD 事件）使用相同的消費邏輯，僅 mapEventTypeToAction 的映射不同。

---

## 7. 設計模式

| Pattern | 用途 | 說明 |
|:--------|:-----|:-----|
| **Event-Driven Consumer** | I3, I5, I9 事件消費 | 訂閱三個上游 BC 的 Domain Events |
| **Append-Only Store** | AuditEntry 儲存 | WORM 語意，禁止修改和刪除 |
| **Idempotent Consumer** | eventId 去重 | 確保重複投遞不產生重複記錄 |

---

## 8. 上層文件回饋

1. **Design Doc §4.4 AuditEntry 缺少 resourceType 和 correlationId**：目前 AuditEntry 屬性表中沒有 resourceType（無法區分是 ApiKey 還是 AccessPolicy 的操作）和 correlationId（無法追蹤同一業務流程的多筆記錄）。建議補充這兩個欄位。
