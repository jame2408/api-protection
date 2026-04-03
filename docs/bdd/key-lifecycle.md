# Key Lifecycle — BDD Specification

> Step 5 展開，按 [design-methodology.md](../design-methodology.md) §5 格式撰寫。
> 場景由 [bc-key-lifecycle.md](../detailed-design/key-lifecycle.md) 的 Command / Guard / State / Event 機械式推導。

**推導規則：**

- `Guard` → Given 的變體（通過 vs 不通過）
- `Command` → When
- `State` + `Event` → Then

---

## Command → Feature 對照

| Feature | 對應 Command | Scenario 數 |
|:--------|:-------------|:------------|
| 1. 建立 API 金鑰 | C1: CreateApiKey | 10 |
| 2. 輪替金鑰 | C2: RotateKey, C9: CompleteGracePeriod | 7 |
| 3. 鎖定與解鎖金鑰 | C3: LockKey, C4: UnlockKey | 6 |
| 4. 暫停與恢復金鑰 | C5: SuspendKey, C6: ResumeKey | 8 |
| 5. 撤銷金鑰 | C7: RevokeKey | 7 |
| 6. 金鑰到期處理 | C8: ExpireKey | 6 |

---

## Feature 1: 建立 API 金鑰

```gherkin
Feature: 建立 API 金鑰

  # --- Guard 全部通過 ---

  Scenario: 成功建立金鑰
    Given 租戶 "tenant-A" 狀態為 Active
    And   Consumer "consumer-1" 屬於 "tenant-A"
    And   "consumer-1" 在 Production 環境的 ACTIVE 金鑰數為 3，上限為 10
    And   "consumer-1" 在 Production 環境沒有名為 "order-service-key" 的金鑰
    And   Scopes "orders:read", "orders:write" 已在 Scope Registry 註冊
    And   指定到期時間為 180 天後，未超過最大允許有效期
    When  "consumer-1" 在 Production 環境建立金鑰，名稱 "order-service-key"，scopes ["orders:read", "orders:write"]，到期 180 天後
    Then  金鑰狀態為 ACTIVE
    And   系統產生 KeyCreated 事件，包含 keyId、consumerId、tenantId、environment、scopes、keyPrefix、expiresAt、policyId
    And   系統回傳金鑰明文（Display Once）
    And   同一交易內建立預設 AccessPolicy

  # --- 逐 Guard 反向 ---

  Scenario: 租戶不存在 — 拒絕建立
    Given 租戶 "tenant-X" 不存在
    When  Consumer 嘗試在 "tenant-X" 下建立金鑰
    Then  建立失敗，錯誤原因為「租戶不存在」

  Scenario: 租戶狀態非 Active — 拒絕建立
    Given 租戶 "tenant-A" 狀態為 Suspended
    When  Consumer 嘗試在 "tenant-A" 下建立金鑰
    Then  建立失敗，錯誤原因為「租戶未啟用」

  Scenario: Consumer 不屬於該租戶 — 拒絕建立
    Given Consumer "consumer-2" 不屬於 "tenant-A"
    When  "consumer-2" 嘗試在 "tenant-A" 下建立金鑰
    Then  建立失敗，錯誤原因為「Consumer 不屬於該租戶」

  Scenario: ACTIVE 金鑰數達到上限 — 拒絕建立
    Given "consumer-1" 在 Production 環境的 ACTIVE 金鑰數為 10，上限為 10
    When  "consumer-1" 在 Production 環境建立新金鑰
    Then  建立失敗，錯誤原因為「超過金鑰數量上限」

  Scenario: 金鑰名稱在同 Consumer + Environment 下重複 — 拒絕建立
    Given "consumer-1" 在 Production 環境已有名為 "order-service-key" 的金鑰
    When  "consumer-1" 在 Production 環境建立名為 "order-service-key" 的金鑰
    Then  建立失敗，錯誤原因為「金鑰名稱重複」

  Scenario: 指定的 Scope 不存在 — 拒絕建立
    Given Scope "payments:refund" 未在 Scope Registry 註冊
    When  Consumer 建立金鑰，scopes 包含 "payments:refund"
    Then  建立失敗，錯誤原因為「Scope 不存在：payments:refund」

  Scenario: 未指定任何 Scope — 拒絕建立
    When  Consumer 建立金鑰，scopes 為空
    Then  建立失敗，錯誤原因為「至少需要一個 Scope」

  Scenario: 到期時間已過 — 拒絕建立
    When  Consumer 建立金鑰，到期時間為昨天
    Then  建立失敗，錯誤原因為「到期時間必須在未來」

  Scenario: 到期時間超過最大允許有效期 — 拒絕建立
    When  Consumer 建立金鑰，到期時間為 5 年後
    Then  建立失敗，錯誤原因為「超過最大允許有效期」
```

---

## Feature 2: 輪替金鑰

```gherkin
Feature: 輪替金鑰

  # === C2: RotateKey ===

  # --- Guard 全部通過 ---

  Scenario: 成功啟動金鑰輪替
    Given 金鑰 "key-A" 狀態為 ACTIVE，尚未到期
    And   同一 Consumer + Environment 下沒有其他 ROTATING 金鑰
    When  Consumer 對 "key-A" 發起輪替，寬限期為 24 小時
    Then  "key-A" 狀態變為 ROTATING
    And   系統建立新金鑰 "key-B"，狀態為 ACTIVE
    And   "key-A".successorKeyId 指向 "key-B"
    And   "key-B".predecessorKeyId 指向 "key-A"
    And   "key-A".graceDeadline 設為 24 小時後
    And   系統產生 KeyRotationInitiated 事件，包含 oldKeyId、newKeyId、graceDeadline
    And   系統回傳 "key-B" 的金鑰明文（Display Once）
    And   同一交易內為 "key-B" 建立預設 AccessPolicy

  # --- 逐 Guard 反向 ---

  Scenario: 金鑰非 ACTIVE 狀態 — 拒絕輪替
    Given 金鑰 "key-A" 狀態為 SUSPENDED
    When  Consumer 對 "key-A" 發起輪替
    Then  輪替失敗，錯誤原因為「金鑰狀態非 ACTIVE」

  Scenario: 同 Consumer + Environment 下已有 ROTATING 金鑰 — 拒絕輪替
    Given 金鑰 "key-A" 狀態為 ACTIVE
    And   同一 Consumer + Environment 下已有 "key-C" 狀態為 ROTATING
    When  Consumer 對 "key-A" 發起輪替
    Then  輪替失敗，錯誤原因為「已有進行中的輪替」

  Scenario: 金鑰已到期 — 拒絕輪替
    Given 金鑰 "key-A" 狀態為 ACTIVE，但已到期
    When  Consumer 對 "key-A" 發起輪替
    Then  輪替失敗，錯誤原因為「金鑰已到期，無法輪替」

  # === C9: CompleteGracePeriod ===

  # --- Guard 全部通過 ---

  Scenario: 寬限期到期 — 自動完成輪替
    Given 金鑰 "key-A" 狀態為 ROTATING
    And   當前時間已超過 "key-A" 的 graceDeadline
    When  System Agent 執行寬限期掃描
    Then  "key-A" 狀態變為 REVOKED
    And   清除 successorKeyId / predecessorKeyId 關聯
    And   系統產生 KeyGracePeriodExpired 事件，包含 keyId、successorKeyId
    And   觸發主動快取失效

  # --- 逐 Guard 反向 ---

  Scenario: 寬限期尚未到期 — 不處理
    Given 金鑰 "key-A" 狀態為 ROTATING
    And   當前時間尚未超過 "key-A" 的 graceDeadline
    When  System Agent 執行寬限期掃描
    Then  "key-A" 狀態保持 ROTATING，不產生任何事件

  Scenario: 非 ROTATING 狀態 — 拒絕完成寬限期
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  System Agent 對 "key-A" 執行 CompleteGracePeriod
    Then  操作被忽略，錯誤原因為「金鑰狀態非 ROTATING」
```

---

## Feature 3: 鎖定與解鎖金鑰

```gherkin
Feature: 鎖定與解鎖金鑰

  # === C3: LockKey ===

  # --- Guard 全部通過 ---

  Scenario: 系統偵測到異常 — 自動鎖定金鑰
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  System 以 ruleId "impossible-travel"、severity HIGH 鎖定 "key-A"，原因為「異地同時存取」
    Then  "key-A" 狀態變為 LOCKED
    And   系統產生 KeyLocked 事件，包含 keyId、ruleId、reason、evidence

  # --- 逐 Guard 反向 ---

  Scenario: 金鑰非 ACTIVE 狀態 — 拒絕鎖定
    Given 金鑰 "key-A" 狀態為 SUSPENDED
    When  System 對 "key-A" 發出鎖定命令
    Then  鎖定失敗，錯誤原因為「金鑰狀態非 ACTIVE」

  Scenario: 非 System 角色嘗試鎖定 — 拒絕
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  Security Admin（人為操作者）對 "key-A" 發出鎖定命令
    Then  鎖定失敗，錯誤原因為「只有系統可以鎖定金鑰」

  # === C4: UnlockKey ===

  # --- Guard 全部通過 ---

  Scenario: Security Admin 解鎖金鑰
    Given 金鑰 "key-A" 狀態為 LOCKED
    And   操作者為 Security Admin
    When  Security Admin 對 "key-A" 發出解鎖命令
    Then  "key-A" 狀態變為 ACTIVE
    And   系統產生 KeyUnlocked 事件，包含 keyId、unlockedBy

  # --- 逐 Guard 反向 ---

  Scenario: 金鑰非 LOCKED 狀態 — 拒絕解鎖
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  Security Admin 對 "key-A" 發出解鎖命令
    Then  解鎖失敗，錯誤原因為「金鑰狀態非 LOCKED」

  Scenario: 操作者權限不足 — 拒絕解鎖
    Given 金鑰 "key-A" 狀態為 LOCKED
    And   操作者為一般 Consumer
    When  操作者對 "key-A" 發出解鎖命令
    Then  解鎖失敗，錯誤原因為「權限不足」
```

---

## Feature 4: 暫停與恢復金鑰

```gherkin
Feature: 暫停與恢復金鑰

  # === C5: SuspendKey ===

  # --- Guard 全部通過 ---

  Scenario: 成功暫停金鑰
    Given 金鑰 "key-A" 狀態為 ACTIVE
    And   操作者為 Security Admin（人為操作者）
    When  Security Admin 暫停 "key-A"，原因為「維護排程」
    Then  "key-A" 狀態變為 SUSPENDED
    And   系統產生 KeySuspended 事件，包含 keyId、suspendedBy、reason

  # --- 逐 Guard 反向 ---

  Scenario: 金鑰非 ACTIVE 狀態 — 拒絕暫停
    Given 金鑰 "key-A" 狀態為 LOCKED
    When  Security Admin 暫停 "key-A"，原因為「維護排程」
    Then  暫停失敗，錯誤原因為「金鑰狀態非 ACTIVE」

  Scenario: System 嘗試暫停 — 拒絕
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  System（非人為操作者）對 "key-A" 發出暫停命令
    Then  暫停失敗，錯誤原因為「暫停操作僅限人為操作」

  Scenario: 操作者無暫停權限 — 拒絕
    Given 金鑰 "key-A" 狀態為 ACTIVE
    And   操作者為一般 Consumer（無暫停權限）
    When  操作者暫停 "key-A"，原因為「維護排程」
    Then  暫停失敗，錯誤原因為「權限不足」

  Scenario: 未提供暫停原因 — 拒絕
    Given 金鑰 "key-A" 狀態為 ACTIVE
    And   操作者為 Security Admin
    When  Security Admin 暫停 "key-A"，未提供原因
    Then  暫停失敗，錯誤原因為「必須提供暫停原因」

  # === C6: ResumeKey ===

  # --- Guard 全部通過 ---

  Scenario: 成功恢復金鑰
    Given 金鑰 "key-A" 狀態為 SUSPENDED
    And   操作者具備恢復權限
    When  操作者恢復 "key-A"
    Then  "key-A" 狀態變為 ACTIVE
    And   系統產生 KeyResumed 事件，包含 keyId、resumedBy

  # --- 逐 Guard 反向 ---

  Scenario: 金鑰非 SUSPENDED 狀態 — 拒絕恢復
    Given 金鑰 "key-A" 狀態為 LOCKED
    When  操作者恢復 "key-A"
    Then  恢復失敗，錯誤原因為「金鑰狀態非 SUSPENDED」

  Scenario: 操作者無恢復權限 — 拒絕恢復
    Given 金鑰 "key-A" 狀態為 SUSPENDED
    And   操作者為一般 Consumer（無恢復權限）
    When  操作者恢復 "key-A"
    Then  恢復失敗，錯誤原因為「權限不足」
```

---

## Feature 5: 撤銷金鑰

```gherkin
Feature: 撤銷金鑰

  # --- 各來源狀態的正向場景 ---

  Scenario: 從 ACTIVE 狀態撤銷
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  操作者撤銷 "key-A"，原因為「不再使用」
    Then  "key-A" 狀態變為 REVOKED
    And   系統產生 KeyRevoked 事件，previousStatus 為 ACTIVE
    And   觸發主動快取失效

  Scenario: 從 ROTATING 狀態撤銷 — 同時清除輪替關聯
    Given 金鑰 "key-A" 狀態為 ROTATING，successorKeyId 為 "key-B"
    When  操作者撤銷 "key-A"，原因為「安全疑慮」
    Then  "key-A" 狀態變為 REVOKED
    And   清除 "key-A" 與 "key-B" 之間的 successorKeyId / predecessorKeyId 關聯
    And   系統產生 KeyRevoked 事件，previousStatus 為 ROTATING
    And   觸發主動快取失效

  Scenario: 從 LOCKED 狀態撤銷
    Given 金鑰 "key-A" 狀態為 LOCKED
    When  Security Admin 撤銷 "key-A"，原因為「確認遭入侵」
    Then  "key-A" 狀態變為 REVOKED
    And   系統產生 KeyRevoked 事件，previousStatus 為 LOCKED
    And   觸發主動快取失效

  Scenario: 從 SUSPENDED 狀態撤銷
    Given 金鑰 "key-A" 狀態為 SUSPENDED
    When  操作者撤銷 "key-A"，原因為「永久停用」
    Then  "key-A" 狀態變為 REVOKED
    And   系統產生 KeyRevoked 事件，previousStatus 為 SUSPENDED
    And   觸發主動快取失效

  # --- Guard 反向 ---

  Scenario: 金鑰已在終態 — 拒絕撤銷
    Given 金鑰 "key-A" 狀態為 EXPIRED
    When  操作者撤銷 "key-A"，原因為「補撤銷」
    Then  撤銷失敗，錯誤原因為「金鑰已在終態，無法撤銷」

  Scenario: 未提供撤銷原因 — 拒絕
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  操作者撤銷 "key-A"，未提供原因
    Then  撤銷失敗，錯誤原因為「必須提供撤銷原因」

  # --- 特殊場景：Secret Scanner ---

  Scenario: Secret Scanner 偵測到金鑰洩漏 — 批次自動撤銷
    Given 金鑰 "key-A"（prefix "pk_live_abc"）狀態為 ACTIVE
    And   Secret Scanner 在公開儲存庫偵測到 prefix "pk_live_abc"
    When  Secret Scanner 對所有符合該 prefix 的非終態金鑰發出撤銷命令
    Then  "key-A" 狀態變為 REVOKED
    And   系統產生 KeyRevoked 事件，reason 為 "Key leaked in public repository"
    And   系統通知 Security Admin 和 Consumer
    And   觸發主動快取失效
```

---

## Feature 6: 金鑰到期處理

```gherkin
Feature: 金鑰到期處理

  # --- 各來源狀態的正向場景 ---

  Scenario: ACTIVE 金鑰到期
    Given 金鑰 "key-A" 狀態為 ACTIVE
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 狀態變為 EXPIRED
    And   系統產生 KeyExpired 事件，previousStatus 為 ACTIVE

  Scenario: ROTATING 金鑰到期
    Given 金鑰 "key-A" 狀態為 ROTATING
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 狀態變為 EXPIRED
    And   系統產生 KeyExpired 事件，previousStatus 為 ROTATING

  Scenario: SUSPENDED 金鑰到期
    Given 金鑰 "key-A" 狀態為 SUSPENDED
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 狀態變為 EXPIRED
    And   系統產生 KeyExpired 事件，previousStatus 為 SUSPENDED

  Scenario: LOCKED 金鑰到期 — 轉為 REVOKED 以保留安全上下文
    Given 金鑰 "key-A" 狀態為 LOCKED，原始鎖定 ruleId 為 "impossible-travel"
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 狀態變為 REVOKED（非 EXPIRED）
    And   系統產生 KeyRevoked 事件，reason 包含原始鎖定 ruleId
    And   觸發主動快取失效

  # --- Guard 反向 ---

  Scenario: 金鑰尚未到期 — 不處理
    Given 金鑰 "key-A" 狀態為 ACTIVE
    And   當前時間尚未超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 不在掃描結果中，狀態保持 ACTIVE

  Scenario: 金鑰已在終態 — 不處理
    Given 金鑰 "key-A" 狀態為 REVOKED
    When  System Agent 執行到期掃描
    Then  "key-A" 不在掃描結果中，不產生任何事件
```
