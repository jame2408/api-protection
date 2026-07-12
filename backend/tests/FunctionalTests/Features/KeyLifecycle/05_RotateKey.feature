Feature: 輪替金鑰

  # === C2: RotateKey ===

  Scenario: 成功啟動金鑰輪替
    Given 金鑰 "key-A" 狀態為 Active，尚未到期
    And   同一 Consumer + Environment 下沒有其他 Rotating 金鑰
    When  Consumer 對 "key-A" 發起輪替，寬限期為 24 小時
    Then  "key-A" 狀態變為 Rotating
    And   系統建立新金鑰 "key-B"，狀態為 Active
    And   "key-A".successorKeyId 指向 "key-B"
    And   "key-B".predecessorKeyId 指向 "key-A"
    And   "key-A".graceDeadline 設為 24 小時後
    And   系統產生 KeyRotationInitiated 事件，包含 oldKeyId、newKeyId、graceDeadline
    And   系統回傳 "key-B" 的金鑰明文（Display Once）
    And   同一交易內為 "key-B" 建立預設 AccessPolicy

  @ignore
  Scenario: 金鑰非 Active 狀態 — 拒絕輪替
    Given 金鑰 "key-A" 狀態為 Suspended
    When  Consumer 對 "key-A" 發起輪替
    Then  輪替失敗，錯誤原因為「金鑰狀態非 Active」

  @ignore
  Scenario: 同 Consumer + Environment 下已有 Rotating 金鑰 — 拒絕輪替
    Given 金鑰 "key-A" 狀態為 Active
    And   同一 Consumer + Environment 下已有 "key-C" 狀態為 Rotating
    When  Consumer 對 "key-A" 發起輪替
    Then  輪替失敗，錯誤原因為「已有進行中的輪替」

  @ignore
  Scenario: 金鑰已到期 — 拒絕輪替
    Given 金鑰 "key-A" 狀態為 Active，但已到期
    When  Consumer 對 "key-A" 發起輪替
    Then  輪替失敗，錯誤原因為「金鑰已到期，無法輪替」

  Scenario: 操作者無輪替權限 — 拒絕輪替
    Given 金鑰 "key-A" 狀態為 Active
    And   操作者為 Security Admin
    When  操作者對 "key-A" 發起輪替
    Then  輪替失敗，錯誤原因為「權限不足」

  Scenario: Consumer 輪替非自身金鑰 — 拒絕
    Given 金鑰 "key-A" 狀態為 Active，且屬於其他 Consumer
    And   操作者為一般 Consumer
    When  操作者對 "key-A" 發起輪替
    Then  輪替失敗，錯誤原因為「權限不足」

  # === C9: CompleteGracePeriod ===

  @ignore
  Scenario: 寬限期到期 — 自動完成輪替
    Given 金鑰 "key-A" 狀態為 Rotating
    And   當前時間已超過 "key-A" 的 graceDeadline
    When  System Agent 執行寬限期掃描
    Then  "key-A" 狀態變為 Revoked
    And   清除 successorKeyId / predecessorKeyId 關聯
    And   系統產生 KeyGracePeriodExpired 事件，包含 keyId、successorKeyId
    And   觸發主動快取失效

  @ignore
  Scenario: 寬限期尚未到期 — 不處理
    Given 金鑰 "key-A" 狀態為 Rotating
    And   當前時間尚未超過 "key-A" 的 graceDeadline
    When  System Agent 執行寬限期掃描
    Then  "key-A" 狀態保持 Rotating，不產生任何事件

  @ignore
  Scenario: 非 Rotating 狀態 — 拒絕完成寬限期
    Given 金鑰 "key-A" 狀態為 Active
    When  System Agent 對 "key-A" 執行 CompleteGracePeriod
    Then  操作被忽略，錯誤原因為「金鑰狀態非 Rotating」
