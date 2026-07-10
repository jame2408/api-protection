Feature: 建立 API 金鑰

  # --- Guard 全部通過 ---

  Scenario: 成功建立金鑰
    Given 租戶 "tenant-A" 狀態為 Active
    And   Consumer "consumer-1" 屬於 "tenant-A"
    And   "consumer-1" 在 Production 環境的 Active 金鑰數為 3，上限為 10
    And   "consumer-1" 在 Production 環境沒有名為 "order-service-key" 的金鑰
    And   Scopes "orders:read", "orders:write" 已在 Scope Registry 註冊
    And   指定到期時間為 180 天後，未超過最大允許有效期
    When  "consumer-1" 在 Production 環境建立金鑰，名稱 "order-service-key"，scopes ["orders:read", "orders:write"]，到期 180 天後
    Then  金鑰狀態為 Active
    And   系統產生 KeyCreated 事件，包含 keyId、consumerId、tenantId、name、environment、scopes、keyPrefix、expiresAt、policyId
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

  Scenario: Active 金鑰數達到上限 — 拒絕建立
    Given "consumer-1" 在 Production 環境的 Active 金鑰數為 10，上限為 10
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

  # mutation 基線驅動的邊界場景，使用者 2026-07-05 核准新增
  Scenario: 到期時間恰為現在 — 拒絕建立
    When  Consumer 建立金鑰，到期時間恰為現在
    Then  建立失敗，錯誤原因為「到期時間必須在未來」

  Scenario: 到期時間恰為最大允許有效期 — 成功建立
    When  Consumer 建立金鑰，到期時間恰為最大允許有效期
    Then  金鑰狀態為 Active
