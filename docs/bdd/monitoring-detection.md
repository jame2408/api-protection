# Monitoring & Detection — BDD Specification

> Step 5 展開，按 [design-methodology.md](../design-methodology.md) §5 格式撰寫。
> 場景由 [bc-monitoring-detection.md](../detailed-design/monitoring-detection.md) 的 Command / Guard / State / Event 及 Detection Engine 流程推導。

---

## Command → Feature 對照

| Feature | 對應來源 | Scenario 數 |
|:--------|:---------|:------------|
| 1. 管理偵測規則 | C1: CreateRule, C2: UpdateRule, C3: ToggleRule | 12 |
| 2. 異常偵測與自動防禦 | Detection Engine（§4） | 7 |
| 3. 管理安全警報 | C4: AcknowledgeAlert, C5: ResolveAlert | 7 |
| 4. 使用基線管理 | I4 事件消費（§7.3） | 4 |

---

## Feature 1: 管理偵測規則

```gherkin
Feature: 管理偵測規則

  # === C1: CreateRule ===

  Scenario: 成功建立偵測規則
    Given 沒有名為 "high-failure-rate" 的偵測規則
    When  Security Admin 建立偵測規則：name "high-failure-rate", metric AUTH_FAILURE_RATE, window 5min, operator GT, threshold 50%, action Lock, cooldown 30min
    Then  規則建立成功，isActive 為 true

  Scenario: 規則名稱為空 — 拒絕建立
    When  Security Admin 建立偵測規則，name 為空
    Then  建立失敗，錯誤原因為「規則名稱不可為空」

  Scenario: 規則名稱重複 — 拒絕建立
    Given 已存在名為 "high-failure-rate" 的偵測規則
    When  Security Admin 建立名為 "high-failure-rate" 的偵測規則
    Then  建立失敗，錯誤原因為「規則名稱已存在」

  Scenario: condition 結構不合法 — 拒絕建立
    When  Security Admin 建立偵測規則，metric 為不支援的類型 "UNKNOWN_METRIC"
    Then  建立失敗，錯誤原因為「condition 結構不合法」

  Scenario: cooldown 為零或負數 — 拒絕建立
    When  Security Admin 建立偵測規則，cooldown 為 0
    Then  建立失敗，錯誤原因為「cooldown 必須大於 0」

  # === C2: UpdateRule ===

  Scenario: 成功更新偵測規則
    Given 偵測規則 "rule-1" 存在，threshold 為 50%
    When  Security Admin 更新 "rule-1" 的 threshold 為 70%
    Then  "rule-1" 的 threshold 更新為 70%

  Scenario: 規則不存在 — 拒絕更新
    Given 偵測規則 "rule-X" 不存在
    When  Security Admin 嘗試更新 "rule-X"
    Then  更新失敗，錯誤原因為「規則不存在」

  Scenario: 未提供任何更新欄位 — 拒絕更新
    Given 偵測規則 "rule-1" 存在
    When  Security Admin 更新 "rule-1"，未提供任何欄位
    Then  更新失敗，錯誤原因為「至少須提供一個更新欄位」

  Scenario: 更新的 condition 不合法 — 拒絕更新
    Given 偵測規則 "rule-1" 存在
    When  Security Admin 更新 "rule-1" 的 window 為 -1min
    Then  更新失敗，錯誤原因為「condition 結構不合法」

  # === C3: ToggleRule ===

  Scenario: 停用偵測規則
    Given 偵測規則 "rule-1" 存在，isActive 為 true
    When  Security Admin 將 "rule-1" 設為停用
    Then  "rule-1" 的 isActive 變為 false

  Scenario: 啟用偵測規則
    Given 偵測規則 "rule-1" 存在，isActive 為 false
    When  Security Admin 將 "rule-1" 設為啟用
    Then  "rule-1" 的 isActive 變為 true

  Scenario: 規則不存在 — 拒絕切換
    Given 偵測規則 "rule-X" 不存在
    When  Security Admin 嘗試切換 "rule-X"
    Then  操作失敗，錯誤原因為「規則不存在」
```

---

## Feature 2: 異常偵測與自動防禦

```gherkin
Feature: 異常偵測與自動防禦

  # --- 偵測觸發 ---

  Scenario: 靜態閾值觸發 — 自動鎖定金鑰
    Given 偵測規則 "high-failure-rate" 為啟用狀態：metric AUTH_FAILURE_RATE, window 5min, operator GT, threshold 50%, action Lock
    And   金鑰 "key-A" 在過去 5 分鐘內驗證失敗率為 65%
    When  Detection Engine 評估規則
    Then  系統建立 SecurityAlert，status 為 Open，關聯 "key-A" 和 "high-failure-rate"
    And   系統呼叫 Key Lifecycle LockKey（I6），鎖定 "key-A"
    And   系統發布 AnomalyDetected 事件

  Scenario: 基線倍率觸發 — 通知 Security Admin
    Given 偵測規則 "spike-detection" 為啟用狀態：metric REQUEST_RATE, window 10min, operator GT, threshold baseline P95 × 3, action Notify
    And   金鑰 "key-B" 的 baseline P95 請求速率為 100 RPS
    And   "key-B" 當前請求速率為 350 RPS（超過 P95 × 3 = 300）
    When  Detection Engine 評估規則
    Then  系統建立 SecurityAlert，status 為 Open
    And   系統發送通知給 Security Admin
    And   系統發布 AnomalyDetected 事件

  Scenario: Impossible Travel 偵測
    Given 偵測規則 "impossible-travel" 為啟用狀態：metric GEO_DISTANCE, window 1min, operator GT, threshold 500km, action Lock
    And   金鑰 "key-C" 在 1 分鐘內從台北和紐約發出請求（距離 > 500km）
    When  Detection Engine 評估規則
    Then  系統建立 SecurityAlert，status 為 Open
    And   系統呼叫 Key Lifecycle LockKey（I6），鎖定 "key-C"
    And   系統發布 ImpossibleTravelDetected 事件

  # --- 不觸發 ---

  Scenario: 指標未超過閾值 — 不觸發
    Given 偵測規則 "high-failure-rate" 為啟用狀態：threshold 50%
    And   金鑰 "key-A" 在過去 5 分鐘內驗證失敗率為 30%
    When  Detection Engine 評估規則
    Then  不建立 SecurityAlert，不執行任何 action

  Scenario: Cooldown 內同一規則 + 同一金鑰 — 不重複觸發
    Given 偵測規則 "high-failure-rate" 的 cooldown 為 30 分鐘
    And   "high-failure-rate" 在 10 分鐘前已對 "key-A" 觸發過 Alert
    And   "key-A" 的驗證失敗率仍超過閾值
    When  Detection Engine 評估規則
    Then  不為 "key-A" 建立新的 SecurityAlert（cooldown 期間內）

  Scenario: 規則已停用 — 不評估
    Given 偵測規則 "high-failure-rate" 的 isActive 為 false
    And   金鑰 "key-A" 的驗證失敗率超過閾值
    When  Detection Engine 評估規則
    Then  "high-failure-rate" 被跳過，不建立任何 Alert

  # --- LockKey 呼叫失敗 ---

  Scenario: LockKey 呼叫失敗 — 重試後進入 DLQ
    Given Detection Engine 觸發 Lock action 對 "key-A"
    And   Key Lifecycle LockKey API 連續 3 次回傳錯誤
    When  Detection Engine 重試 3 次（指數退避）均失敗
    Then  該鎖定請求進入 DLQ
    And   系統發出 Critical 等級告警
```

---

## Feature 3: 管理安全警報

```gherkin
Feature: 管理安全警報

  # === C4: AcknowledgeAlert ===

  Scenario: 確認 Alert（Open → Acknowledged）
    Given SecurityAlert "alert-1" 狀態為 Open
    When  Security Admin 確認 "alert-1"
    Then  "alert-1" 狀態變為 Acknowledged

  # === C5: ResolveAlert ===

  Scenario: 從 Open 直接解決 Alert
    Given SecurityAlert "alert-1" 狀態為 Open
    When  Security Admin 解決 "alert-1"，resolution 為「誤報，該金鑰為內部測試」
    Then  "alert-1" 狀態變為 Resolved

  Scenario: 從 Acknowledged 解決 Alert
    Given SecurityAlert "alert-1" 狀態為 Acknowledged
    When  Security Admin 解決 "alert-1"，resolution 為「已手動撤銷相關金鑰」
    Then  "alert-1" 狀態變為 Resolved

  # --- Guard 反向 ---

  Scenario: Alert 不存在 — 拒絕操作
    Given SecurityAlert "alert-X" 不存在
    When  Security Admin 嘗試確認 "alert-X"
    Then  操作失敗，錯誤原因為「Alert 不存在」

  Scenario: 非 Open 狀態 — 拒絕確認
    Given SecurityAlert "alert-1" 狀態為 Acknowledged
    When  Security Admin 嘗試確認 "alert-1"
    Then  確認失敗，錯誤原因為「只有 Open 狀態的 Alert 可以確認」

  Scenario: 已 Resolved — 拒絕任何操作
    Given SecurityAlert "alert-1" 狀態為 Resolved
    When  Security Admin 嘗試解決 "alert-1"
    Then  操作失敗，錯誤原因為「Alert 已解決，無法再操作」

  Scenario: 未提供 resolution — 拒絕解決
    Given SecurityAlert "alert-1" 狀態為 Open
    When  Security Admin 解決 "alert-1"，resolution 為空
    Then  解決失敗，錯誤原因為「必須提供解決說明」
```

---

## Feature 4: 使用基線管理

```gherkin
Feature: 使用基線管理

  Scenario: 金鑰建立 — 開始基線觀察期
    Given Key Lifecycle 發布 KeyCreated 事件，keyId 為 "key-A"
    When  Monitoring 接收到事件
    Then  系統為 "key-A" 建立 UsageBaseline，標記為觀察期
    And   開始收集原始指標資料

  Scenario: 金鑰撤銷或到期 — 停止計算並歸檔
    Given "key-A" 已有 UsageBaseline
    And   Key Lifecycle 發布 KeyRevoked 事件，keyId 為 "key-A"
    When  Monitoring 接收到事件
    Then  系統停止 "key-A" 的 baseline 計算
    And   歸檔既有 baseline 資料

  Scenario: 金鑰被鎖定 — 暫停基線更新
    Given "key-A" 已有 UsageBaseline
    And   Key Lifecycle 發布 KeyLocked 事件，keyId 為 "key-A"
    When  Monitoring 接收到事件
    Then  系統暫停 "key-A" 的 baseline 更新（鎖定期間流量不納入計算）

  Scenario: 金鑰解鎖 — 恢復基線計算
    Given "key-A" 的 baseline 更新已暫停
    And   Key Lifecycle 發布 KeyUnlocked 事件，keyId 為 "key-A"
    When  Monitoring 接收到事件
    Then  系統恢復 "key-A" 的 baseline 計算
```
