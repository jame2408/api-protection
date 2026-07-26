Feature: 金鑰到期處理

  Scenario: Active 金鑰到期
    Given 金鑰 "key-A" 狀態為 Active
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 狀態變為 Expired
    And   系統產生 KeyExpired 事件，previousStatus 為 Active

  Scenario: Rotating 金鑰到期
    Given 金鑰 "key-A" 狀態為 Rotating
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 狀態變為 Expired
    And   系統產生 KeyExpired 事件，previousStatus 為 Rotating

  Scenario: Suspended 金鑰到期
    Given 金鑰 "key-A" 狀態為 Suspended
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 狀態變為 Expired
    And   系統產生 KeyExpired 事件，previousStatus 為 Suspended

  Scenario: Locked 金鑰到期 — 轉為 Revoked 以保留安全上下文
    Given 金鑰 "key-A" 狀態為 Locked，原始鎖定 ruleId 為 "impossible-travel"
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 狀態變為 Revoked（非 Expired）
    And   系統產生 KeyRevoked 事件，reason 包含原始鎖定 ruleId
    And   觸發主動快取失效

  Scenario: 金鑰尚未到期 — 不處理
    Given 金鑰 "key-A" 狀態為 Active
    And   當前時間尚未超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 不在掃描結果中，狀態保持 Active

  Scenario: 金鑰已在終態 — 不處理
    Given 金鑰 "key-A" 狀態為 Revoked
    When  System Agent 執行到期掃描
    Then  "key-A" 不在掃描結果中，不產生任何事件

  Scenario: 已撤銷金鑰即使過期也不重新處理
    Given 金鑰 "key-A" 狀態為 Revoked
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 不在掃描結果中，狀態保持 Revoked

  Scenario: 已到期金鑰不重複發出到期事件
    Given 金鑰 "key-A" 狀態為 Expired
    And   當前時間已超過 "key-A" 的 expiresAt
    When  System Agent 執行到期掃描
    Then  "key-A" 不在掃描結果中，不產生任何事件
