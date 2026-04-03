# Step 3 — Context Integration Spec

## Table of Contents

- [Purpose](#purpose)
- [Content Structure](#content-structure)
- [Format Template](#format-template)
- [Example](#example)
- [Completion Checklist](#completion-checklist)

## Purpose

Define contracts between BCs so different teams can **develop in parallel**. Every relationship on the Context Map must have a corresponding integration spec.

## Content Structure

For each pair of interacting BCs, describe:

```
BC-A → BC-B
├── Trigger Scenario: what business event initiates communication
├── Communication Method: Synchronous API / Asynchronous Event / Mixed
├── Contract Spec:
│   ├── Sync: Command name, Input, Output, Error codes
│   └── Async: Event name, Payload Schema, Ordering guarantees
├── Failure Handling: Retry strategy, degradation behavior, idempotency requirements
└── Data Consistency: Eventually consistent / Strong consistent, compensation mechanism
```

## Format Template

Use the following Markdown structure for each BC pair. Wrap all structured content in fenced code blocks to prevent rendering issues.

```markdown
## BC-A → BC-B

### 觸發場景
[描述什麼業務事件會引發這對 BC 之間的通訊]

### 通訊方式
- [ ] 同步 API
- [ ] 非同步 Event
- [ ] 混合

### 契約規格

#### 同步 API（如適用）

| 欄位 | 值 |
|------|-----|
| Command | `[命令名稱]` |
| Endpoint / Method | `[e.g., POST /api/v1/keys 或 gRPC: KeyService.Create]` |
| Input | `{ field1: type, field2: type }` |
| Output | `{ field1: type }` |
| 錯誤碼 | `[錯誤碼列表與說明]` |

#### 非同步 Event（如適用）

| 欄位 | 值 |
|------|-----|
| Event 名稱 | `[事件名稱]` |
| Payload | `{ field1: type, field2: type }` |
| 順序保證 | 無序 / 分區有序 / 全域有序 |

### 失敗處理

| 策略 | 說明 |
|------|------|
| 重試 | [策略：指數退避 / 固定間隔 / 不重試] |
| 降級 | [降級行為描述] |
| 冪等性 | [是否要求冪等，以及冪等鍵為何] |

### 資料一致性
- **模式：** 最終一致 / 強一致
- **補償機制：** [描述補償流程]
```

## Example

```markdown
## Key Lifecycle → Access Policy

### 觸發場景
當 API Key 被建立、啟用、暫停或撤銷時，Access Policy BC 需要同步更新該金鑰的存取權限快取。

### 通訊方式
- [x] 非同步 Event

### 契約規格

#### 非同步 Event

| 欄位 | 值 |
|------|-----|
| Event 名稱 | `ApiKeyStatusChanged` |
| Payload | `{ keyId: string, tenantId: string, newStatus: enum, scope: string[], changedAt: datetime }` |
| 順序保證 | 分區有序（以 keyId 為分區鍵） |

### 失敗處理

| 策略 | 說明 |
|------|------|
| 重試 | 指數退避，最多 3 次 |
| 降級 | 若快取更新失敗，fallback 至資料庫即時查詢 |
| 冪等性 | 是。以 `keyId + changedAt` 為冪等鍵 |

### 資料一致性
- **模式：** 最終一致
- **補償機制：** 每 5 分鐘執行快取與資料庫的比對修復程式
```

## Completion Checklist

- [ ] Context Map 上的所有關係都有對應的整合規格
- [ ] 每個非同步 Event 都有明確的 Payload Schema
- [ ] 所有失敗場景都有定義處理策略
- [ ] 冪等性要求已明確標示
- [ ] Payload 欄位型別已標註
