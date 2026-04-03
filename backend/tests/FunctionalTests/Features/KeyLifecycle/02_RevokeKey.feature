Feature: 撤銷金鑰

  @ignore
  Scenario: 從 ACTIVE 狀態撤銷
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  操作者撤銷 "key-A"，原因為「不再使用」
    Then  "key-A" 狀態變為 REVOKED
    And   系統產生 KeyRevoked 事件，previousStatus 為 ACTIVE
    And   觸發主動快取失效

  @ignore
  Scenario: 從 ROTATING 狀態撤銷 — 同時清除輪替關聯
    Given 金鑰 "key-A" 狀態為 ROTATING，successorKeyId 為 "key-B"
    When  操作者撤銷 "key-A"，原因為「安全疑慮」
    Then  "key-A" 狀態變為 REVOKED
    And   清除 "key-A" 與 "key-B" 之間的 successorKeyId / predecessorKeyId 關聯
    And   系統產生 KeyRevoked 事件，previousStatus 為 ROTATING
    And   觸發主動快取失效

  @ignore
  Scenario: 從 LOCKED 狀態撤銷
    Given 金鑰 "key-A" 狀態為 LOCKED
    When  Security Admin 撤銷 "key-A"，原因為「確認遭入侵」
    Then  "key-A" 狀態變為 REVOKED
    And   系統產生 KeyRevoked 事件，previousStatus 為 LOCKED
    And   觸發主動快取失效

  @ignore
  Scenario: 從 SUSPENDED 狀態撤銷
    Given 金鑰 "key-A" 狀態為 SUSPENDED
    When  操作者撤銷 "key-A"，原因為「永久停用」
    Then  "key-A" 狀態變為 REVOKED
    And   系統產生 KeyRevoked 事件，previousStatus 為 SUSPENDED
    And   觸發主動快取失效

  @ignore
  Scenario: 金鑰已在終態 — 拒絕撤銷
    Given 金鑰 "key-A" 狀態為 EXPIRED
    When  操作者撤銷 "key-A"，原因為「補撤銷」
    Then  撤銷失敗，錯誤原因為「金鑰已在終態，無法撤銷」

  @ignore
  Scenario: 未提供撤銷原因 — 拒絕
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  操作者撤銷 "key-A"，未提供原因
    Then  撤銷失敗，錯誤原因為「必須提供撤銷原因」

  @ignore
  Scenario: Secret Scanner 偵測到金鑰洩漏 — 批次自動撤銷
    Given 金鑰 "key-A"（prefix "pk_live_abc"）狀態為 ACTIVE
    And   Secret Scanner 在公開儲存庫偵測到 prefix "pk_live_abc"
    When  Secret Scanner 對所有符合該 prefix 的非終態金鑰發出撤銷命令
    Then  "key-A" 狀態變為 REVOKED
    And   系統產生 KeyRevoked 事件，reason 為 "Key leaked in public repository"
    And   系統通知 Security Admin 和 Consumer
    And   觸發主動快取失效
