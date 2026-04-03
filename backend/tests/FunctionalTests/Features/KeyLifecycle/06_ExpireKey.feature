Feature: 金鑰到期處理

  @ignore
  Scenario: ACTIVE 金鑰到期
    Given 金鑰 "key-A" 狀態為 ACTIVE
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 狀態變為 EXPIRED
    And   系統產生 KeyExpired 事件，previousStatus 為 ACTIVE

  @ignore
  Scenario: ROTATING 金鑰到期
    Given 金鑰 "key-A" 狀態為 ROTATING
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 狀態變為 EXPIRED
    And   系統產生 KeyExpired 事件，previousStatus 為 ROTATING

  @ignore
  Scenario: SUSPENDED 金鑰到期
    Given 金鑰 "key-A" 狀態為 SUSPENDED
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 狀態變為 EXPIRED
    And   系統產生 KeyExpired 事件，previousStatus 為 SUSPENDED

  @ignore
  Scenario: LOCKED 金鑰到期 — 轉為 REVOKED 以保留安全上下文
    Given 金鑰 "key-A" 狀態為 LOCKED，原始鎖定 ruleId 為 "impossible-travel"
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 狀態變為 REVOKED（非 EXPIRED）
    And   系統產生 KeyRevoked 事件，reason 包含原始鎖定 ruleId
    And   觸發主動快取失效

  @ignore
  Scenario: 金鑰尚未到期 — 不處理
    Given 金鑰 "key-A" 狀態為 ACTIVE
    And   當前時間尚未超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 不在掃描結果中，狀態保持 ACTIVE

  @ignore
  Scenario: 金鑰已在終態 — 不處理
    Given 金鑰 "key-A" 狀態為 REVOKED
    When  System Agent 執行到期掃描
    Then  "key-A" 不在掃描結果中，不產生任何事件
