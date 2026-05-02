# Access Policy — BDD Specification

> Step 5 展開，按 [design-methodology.md](../design-methodology.md) §5 格式撰寫。
> 場景由 [bc-access-policy.md](../detailed-design/access-policy.md) 的 Command / Guard / State / Event 機械式推導。

---

## Command → Feature 對照

| Feature | 對應 Command | Scenario 數 |
|:--------|:-------------|:------------|
| 1. 建立 Access Policy | C1: CreatePolicy | 2 |
| 2. 更新 IP 白名單 | C2: UpdateIpAllowlist | 6 |
| 3. 更新速率限制 | C3: UpdateRateLimit | 7 |

---

## Feature 1: 建立 Access Policy

```gherkin
Feature: 建立 Access Policy

  # --- Guard 全部通過 ---

  Scenario: 建立金鑰時自動建立預設 Policy
    Given 金鑰 "key-A" 尚未關聯任何 AccessPolicy
    When  系統在 CreateApiKey 交易內為 "key-A" 建立 Policy
    Then  產生新的 AccessPolicy，ipAllowlist 為空集合，rateLimitConfig 為系統預設值
    And   系統產生 PolicyCreated 事件，包含 policyId、keyId、ipAllowlist、rateLimitConfig

  # --- Guard 反向 ---

  Scenario: 金鑰已關聯 Policy — 拒絕重複建立
    Given 金鑰 "key-A" 已關聯一個 AccessPolicy
    When  系統嘗試為 "key-A" 再次建立 Policy
    Then  建立失敗，錯誤原因為「該金鑰已有關聯的 AccessPolicy」
```

---

## Feature 2: 更新 IP 白名單

```gherkin
Feature: 更新 IP 白名單

  # --- Guard 全部通過 ---

  Scenario: 成功設定 IP 白名單
    Given 金鑰 "key-A" 的 AccessPolicy 存在
    And   "key-A" 狀態為 Active（非終態）
    When  Consumer 將 "key-A" 的 IP 白名單更新為 ["192.168.1.0/24", "10.0.0.0/8"]
    Then  AccessPolicy 的 ipAllowlist 更新為 ["192.168.1.0/24", "10.0.0.0/8"]
    And   系統產生 PolicyUpdated 事件，changedFields 包含 "ipAllowlist"，含 before 和 after

  Scenario: 清空白名單 — 回到預設開放
    Given 金鑰 "key-A" 的 AccessPolicy 存在，ipAllowlist 為 ["192.168.1.0/24"]
    And   "key-A" 狀態為 Active
    When  Consumer 將 "key-A" 的 IP 白名單更新為空集合
    Then  AccessPolicy 的 ipAllowlist 更新為空集合（不限制 IP）
    And   系統產生 PolicyUpdated 事件

  # --- 逐 Guard 反向 ---

  Scenario: Policy 不存在 — 拒絕更新
    Given 金鑰 "key-X" 沒有關聯的 AccessPolicy
    When  Consumer 嘗試更新 "key-X" 的 IP 白名單
    Then  更新失敗，錯誤原因為「Policy 不存在」

  Scenario: 關聯金鑰已在終態 — 拒絕更新
    Given 金鑰 "key-A" 的 AccessPolicy 存在
    And   "key-A" 狀態為 Expired
    When  Consumer 嘗試更新 "key-A" 的 IP 白名單
    Then  更新失敗，錯誤原因為「金鑰已在終態，無法修改 Policy」

  Scenario: CIDR 格式不合法 — 拒絕更新
    Given 金鑰 "key-A" 的 AccessPolicy 存在
    And   "key-A" 狀態為 Active
    When  Consumer 將 IP 白名單更新為 ["not-a-cidr"]
    Then  更新失敗，錯誤原因為「CIDR 格式不合法」

  Scenario: 包含 0.0.0.0/0 全開白名單 — 拒絕更新
    Given 金鑰 "key-A" 的 AccessPolicy 存在
    And   "key-A" 狀態為 Active
    When  Consumer 將 IP 白名單更新為 ["192.168.1.0/24", "0.0.0.0/0"]
    Then  更新失敗，錯誤原因為「禁止使用 0.0.0.0/0，如需不限制請使用空集合」
```

---

## Feature 3: 更新速率限制

```gherkin
Feature: 更新速率限制

  # --- Guard 全部通過 ---

  Scenario: 成功更新速率限制
    Given 金鑰 "key-A" 的 AccessPolicy 存在
    And   "key-A" 狀態為 Active（非終態）
    When  Consumer 更新 "key-A" 的速率限制：quotaLimit 10000, quotaPeriod 1hour, rateLimit 100, burstLimit 200
    Then  AccessPolicy 的 rateLimitConfig 更新為指定值
    And   系統產生 PolicyUpdated 事件，changedFields 包含 "rateLimitConfig"，含 before 和 after

  # --- 逐 Guard 反向 ---

  Scenario: Policy 不存在 — 拒絕更新
    Given 金鑰 "key-X" 沒有關聯的 AccessPolicy
    When  Consumer 嘗試更新 "key-X" 的速率限制
    Then  更新失敗，錯誤原因為「Policy 不存在」

  Scenario: 關聯金鑰已在終態 — 拒絕更新
    Given 金鑰 "key-A" 的 AccessPolicy 存在
    And   "key-A" 狀態為 Revoked
    When  Consumer 嘗試更新 "key-A" 的速率限制
    Then  更新失敗，錯誤原因為「金鑰已在終態，無法修改 Policy」

  Scenario: quotaLimit 為零或負數 — 拒絕更新
    When  Consumer 更新速率限制：quotaLimit 0
    Then  更新失敗，錯誤原因為「quotaLimit 必須大於 0」

  Scenario: rateLimit 為零或負數 — 拒絕更新
    When  Consumer 更新速率限制：rateLimit -1
    Then  更新失敗，錯誤原因為「rateLimit 必須大於 0」

  Scenario: burstLimit 小於 rateLimit — 拒絕更新
    When  Consumer 更新速率限制：rateLimit 100, burstLimit 50
    Then  更新失敗，錯誤原因為「burstLimit 必須大於或等於 rateLimit」

  Scenario: quotaPeriod 不在允許集合 — 拒絕更新
    When  Consumer 更新速率限制：quotaPeriod 5min
    Then  更新失敗，錯誤原因為「quotaPeriod 必須為 1min、1hour、1day 或 1month」
```
