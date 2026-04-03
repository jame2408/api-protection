Feature: 輪替金鑰

  # === C2: RotateKey ===

  @ignore
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

  @ignore
  Scenario: 金鑰非 ACTIVE 狀態 — 拒絕輪替
    Given 金鑰 "key-A" 狀態為 SUSPENDED
    When  Consumer 對 "key-A" 發起輪替
    Then  輪替失敗，錯誤原因為「金鑰狀態非 ACTIVE」

  @ignore
  Scenario: 同 Consumer + Environment 下已有 ROTATING 金鑰 — 拒絕輪替
    Given 金鑰 "key-A" 狀態為 ACTIVE
    And   同一 Consumer + Environment 下已有 "key-C" 狀態為 ROTATING
    When  Consumer 對 "key-A" 發起輪替
    Then  輪替失敗，錯誤原因為「已有進行中的輪替」

  @ignore
  Scenario: 金鑰已到期 — 拒絕輪替
    Given 金鑰 "key-A" 狀態為 ACTIVE，但已到期
    When  Consumer 對 "key-A" 發起輪替
    Then  輪替失敗，錯誤原因為「金鑰已到期，無法輪替」

  # === C9: CompleteGracePeriod ===

  @ignore
  Scenario: 寬限期到期 — 自動完成輪替
    Given 金鑰 "key-A" 狀態為 ROTATING
    And   當前時間已超過 "key-A" 的 graceDeadline
    When  System Agent 執行寬限期掃描
    Then  "key-A" 狀態變為 REVOKED
    And   清除 successorKeyId / predecessorKeyId 關聯
    And   系統產生 KeyGracePeriodExpired 事件，包含 keyId、successorKeyId
    And   觸發主動快取失效

  @ignore
  Scenario: 寬限期尚未到期 — 不處理
    Given 金鑰 "key-A" 狀態為 ROTATING
    And   當前時間尚未超過 "key-A" 的 graceDeadline
    When  System Agent 執行寬限期掃描
    Then  "key-A" 狀態保持 ROTATING，不產生任何事件

  @ignore
  Scenario: 非 ROTATING 狀態 — 拒絕完成寬限期
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  System Agent 對 "key-A" 執行 CompleteGracePeriod
    Then  操作被忽略，錯誤原因為「金鑰狀態非 ROTATING」
