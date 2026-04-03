# Monitoring & Detection — Per-BC Detailed Design

> Step 4 展開。本 BC 是 Supporting Domain，核心職責是**反應式**的：接收使用遙測、評估規則、觸發警報與自動防禦。

**前置文件參照：**

- 領域模型：[Design Doc §4.5](../design/design-doc.md)
- 整合契約：[Integration Spec §4.4 I4, §4.6 I6, §5.1 I8](../design/context-integration-spec.md)
- Event Payload：[Integration Spec §6.3](../design/context-integration-spec.md)

---

## 1. Entity: DetectionRule

### 1.1 Command 行為規格

#### C1: CreateRule

```
Command:  CreateRule
Actor:    Security Admin
Input:    { name, condition: RuleCondition, action: RuleAction, cooldown: Duration }

Guard:
  name 不可為空且不重複                                         (INV-1)
  condition 結構合法（見 §3 Value Objects）                     (輸入驗證)
  cooldown > 0                                                  (輸入驗證)

State:    → Created（isActive = true）
Event:    無 Domain Event（BC 內部配置變更）
```

#### C2: UpdateRule

```
Command:  UpdateRule
Actor:    Security Admin
Input:    { ruleId, condition?, action?, cooldown? }

Guard:
  Rule 存在
  至少提供一個更新欄位                                          (輸入驗證)
  condition 若有提供須結構合法                                   (輸入驗證)

State:    對應欄位更新
Event:    無 Domain Event
```

#### C3: ToggleRule

```
Command:  ToggleRule
Actor:    Security Admin
Input:    { ruleId, isActive: Boolean }

Guard:
  Rule 存在

State:    isActive 更新
Event:    無 Domain Event
```

---

## 2. Entity: SecurityAlert

### 2.1 狀態流轉

```
[*] → Open          — DetectionEngine 觸發
Open → Acknowledged  — Security Admin 確認已看到
Open → Resolved      — 自動解除或 Admin 直接解決
Acknowledged → Resolved — Admin 確認已處理
```

### 2.2 Command 行為規格

#### C4: AcknowledgeAlert

```
Command:  AcknowledgeAlert
Actor:    Security Admin
Input:    { alertId }

Guard:
  Alert 存在
  AND status = Open                                             (INV-2)

State:    status → Acknowledged
Event:    無 Domain Event
```

#### C5: ResolveAlert

```
Command:  ResolveAlert
Actor:    Security Admin
Input:    { alertId, resolution: String }

Guard:
  Alert 存在
  AND status ∈ { Open, Acknowledged }                           (INV-2)
  AND resolution 不可為空                                       (輸入驗證)

State:    status → Resolved
Event:    無 Domain Event
```

### 2.3 不變條件

| # | 不變條件 | 驗證時機 | 說明 |
|:--|:---------|:---------|:-----|
| INV-1 | Rule name 唯一 | C1 | Repository 唯一約束 |
| INV-2 | Alert 狀態只能往前推進 | C4, C5 | Open → Acknowledged → Resolved，不可逆轉 |
| INV-3 | cooldown 內不重複觸發 | Detection Engine | 同一 Rule + 同一 Key，cooldown 內不產生新 Alert |

---

## 3. Value Objects

**RuleCondition**

```
RuleCondition {
  metric:    MetricType      — 監測指標
  window:    Duration        — 偵測時間窗口
  operator:  Operator        — GT / GTE
  threshold: Threshold       — 靜態值 或 基線倍率

  MetricType ∈ {
    AUTH_FAILURE_COUNT,       — 驗證失敗次數
    AUTH_FAILURE_RATE,        — 驗證失敗率（%）
    REQUEST_RATE,             — 請求速率（RPS）
    GEO_DISTANCE              — Impossible Travel 地理距離（km）
  }

  Threshold = StaticThreshold { value: Number }
            | BaselineThreshold { multiplier: Number, baseline: "P95" | "AVG" }
}
```

**RuleAction**

```
RuleAction ∈ { Lock, Notify, Throttle }

Lock      — 同步呼叫 KL LockKey（I6）
Notify    — 發送通知給 Security Admin（外部通知系統）
Throttle  — 下發動態限流指令至 Validation Layer（未來擴展，目前不實作）
```

**Severity**

```
Severity ∈ { Low, Medium, High, Critical }
```

---

## 4. Detection Engine（核心流程）

本 BC 的核心不是 CRUD，而是 Detection Engine — 一個持續運行的處理流程。

### 4.1 資料流

```
ValidationAttempt（I8: 高頻遙測）
  → 指標聚合（per-key 滑動窗口）
    → 規則評估（活躍的 DetectionRule）
      → 觸發?
        → 建立 SecurityAlert
        → 執行 RuleAction（Lock / Notify）
        → 發布 Domain Event（AnomalyDetected / ImpossibleTravelDetected）
```

### 4.2 指標聚合

```
MetricsAggregator:

  輸入：ValidationAttempt 事件流
  輸出：per-key 即時指標

  聚合邏輯（per keyId）：
    AUTH_FAILURE_COUNT  = 滑動窗口內 success=false 的次數
    AUTH_FAILURE_RATE   = 滑動窗口內 失敗次數 / 總次數
    REQUEST_RATE        = 滑動窗口內 總請求數 / 窗口秒數
    GEO_DISTANCE        = 最近兩筆不同 sourceIp 的地理距離
```

### 4.3 規則評估

```
RuleEvaluator:

  每次聚合指標更新後觸發（或定期批次觸發）：

  for each activeRule in rules:
    for each keyId with updated metrics:
      1. 取得 keyId 的 metric[rule.condition.metric] within rule.condition.window
      2. 計算 threshold:
         - StaticThreshold:   直接使用 value
         - BaselineThreshold: 查詢 UsageBaseline，取 baseline × multiplier
      3. 比較 metric operator threshold
      4. 若觸發:
         a. 檢查 cooldown（INV-3）
         b. 建立 SecurityAlert { status: Open, severity, details }
         c. 執行 rule.action:
            - Lock   → 呼叫 KL LockKey（I6 同步 API）
            - Notify → 發送通知至外部系統
         d. 發布 AnomalyDetected / ImpossibleTravelDetected 事件
```

### 4.4 LockKey 呼叫（I6）

Detection Engine 觸發 Lock action 時，遵循 Integration Spec §4.6 的契約：

```
1. 呼叫 KL LockKey { keyId, tenantId, ruleId, severity, reason, evidence }
2. 結果處理：
   成功 / KEY_ALREADY_LOCKED / KEY_ALREADY_SUSPENDED / KEY_IN_TERMINAL_STATE → 完成
   失敗 → 重試策略（3 次指數退避 → DLQ + Critical 告警）
```

---

## 5. UsageBaseline（計算模型）

UsageBaseline 不是 Aggregate，是**定期計算的統計快照**：

```
UsageBaseline {
  keyId:           UUID
  period:          Duration         — 計算週期
  avgRequestRate:  Number
  p95RequestRate:  Number
  lastCalculated:  Timestamp
}
```

**計算時機：**

- 定期批次（如每小時 / 每天）
- 金鑰新建後，經過足夠的初始觀察期（如 7 天）才開始建立 baseline

**生命週期（由 I4 事件驅動）：**

| KL Event | Baseline 動作 |
|:---------|:-------------|
| KeyCreated | 標記「觀察期」，開始收集原始指標 |
| KeyRevoked / KeyExpired | 停止計算，歸檔 baseline |
| KeyLocked | 暫停更新（鎖定期間的流量不納入 baseline） |
| KeyUnlocked | 恢復計算 |

---

## 6. Repository 介面

```
DetectionRuleRepository {
  save(rule: DetectionRule): void
  findById(ruleId: UUID): DetectionRule?
  findAllActive(): List<DetectionRule>
}

SecurityAlertRepository {
  save(alert: SecurityAlert): void
  findById(alertId: UUID): SecurityAlert?
  findByKeyId(keyId: UUID): List<SecurityAlert>
  findByStatus(status: AlertStatus): List<SecurityAlert>
}

UsageBaselineRepository {
  save(baseline: UsageBaseline): void
  findByKeyId(keyId: UUID): UsageBaseline?
}
```

**備註：** DetectionRule 和 SecurityAlert 不帶 tenantId — DetectionRule 是全局配置（所有租戶共用），SecurityAlert 透過 keyId 間接關聯租戶。

---

## 7. Application Service 協調流程

### 7.1 規則管理（簡單 CRUD，略）

直接操作 DetectionRule，無跨 BC 協作。

### 7.2 Alert 管理

```
AcknowledgeAlert:
1. alert = repo.findById(alertId)
2. guard: alert.status = Open
3. alert.acknowledge()
4. repo.save(alert)
5. commit

ResolveAlert:
1. alert = repo.findById(alertId)
2. guard: alert.status ∈ { Open, Acknowledged }
3. alert.resolve(resolution)
4. repo.save(alert)
5. commit
```

### 7.3 事件消費（I4: KL → Monitoring）

```
on KeyCreated:
  baseline = UsageBaseline.initObservation(keyId)
  baselineRepo.save(baseline)

on KeyRevoked / KeyExpired:
  baseline = baselineRepo.findByKeyId(keyId)
  baseline.archive()
  baselineRepo.save(baseline)
  // 可選：清理 in-memory 指標聚合快取
```

---

## 8. 設計模式

| Pattern | 用途 | 說明 |
|:--------|:-----|:-----|
| **Event-Driven Consumer** | I4 事件消費、I8 遙測接收 | Monitoring 是典型的事件消費者 |
| **Sliding Window** | 指標聚合 | per-key 滑動窗口計算即時指標 |
| **Value Object** | RuleCondition, RuleAction, Severity | 封裝規則定義的結構驗證 |

---

## 9. 上層文件回饋

1. **DetectionRule 變更是否需要審計？** Design Doc 的 Context Map 沒有 Monitoring → Audit 的事件通道。但從安全合規角度，偵測規則的新增/修改/停用是安全配置變更，應可被追溯。建議在 Design Doc §3.2 新增 I9: Monitoring → Audit (Pub-Sub)，或在 Integration Spec 補充。若認為規則變更不需審計（僅是運維配置），則無需新增。

2. **SecurityAlert 是否需要 tenantId？** Design Doc §4.5 SecurityAlert 只有 keyId，沒有 tenantId。查詢 Alert 時需先解析 keyId → tenantId（跨 BC 查詢），或在建立 Alert 時冗餘儲存 tenantId。建議 SecurityAlert 新增 tenantId 欄位，避免查詢時的額外跳轉。

3. **Design Doc §4.8 事件不一致**：§3.4 列出 Monitoring 事件為 AnomalyDetected 和 AlertTriggered，但 §4.8 列出的是 AnomalyDetected 和 ImpossibleTravelDetected。建議統一——ImpossibleTravelDetected 是 AnomalyDetected 的特化子類型，而非獨立事件。或者明確定義 AlertTriggered 是否等同於 AnomalyDetected。
