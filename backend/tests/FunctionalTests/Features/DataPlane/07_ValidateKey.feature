# 場景來源：docs/design/api-spec.md §4.1「POST /api/internal/v1/validate-key — 金鑰驗證」
# 產出授權：docs/adr/adr-030-discovery-freeze-scoped-lift.md（Data Plane 範圍化解除；
#           每條場景可指回 §4.1 的請求欄位表、成功回應欄位表或驗證失敗錯誤碼表某一列）
# 執行側：  docs/adr/adr-029-validation-funnel-execution-side.md（漏斗五層在系統內執行）
Feature: 金鑰驗證（Data Plane）

  # --- 通過全部五層 ---

  @ignore
  Scenario: 成功驗證 Active 金鑰
    Given 金鑰 "key-A" 狀態為 Active
    And   "key-A" 的 scopes 為 ["orders:read", "orders:write"]
    When  Gateway 以 "key-A" 的完整金鑰、來源 IP "203.0.113.42"、requestedScope "orders:read" 呼叫驗證
    Then  驗證通過，valid 為 true
    And   回應包含 keyId、tenantId、consumerId、environment、scopes、rateLimitConfig

  @ignore
  Scenario: Rotating 金鑰在寬限期內仍可驗證
    Given 金鑰 "key-A" 狀態為 Rotating
    And   "key-A" 的 scopes 為 ["orders:read"]
    When  Gateway 以 "key-A" 的完整金鑰、來源 IP "203.0.113.42"、requestedScope "orders:read" 呼叫驗證
    Then  驗證通過，valid 為 true

  # --- Layer 1: 格式檢查 ---

  @ignore
  Scenario: 金鑰格式不合法 — 拒絕驗證
    Given 一個前綴不合法的金鑰字串
    When  Gateway 以該金鑰字串、來源 IP "203.0.113.42"、requestedScope "orders:read" 呼叫驗證
    Then  驗證失敗，errorCode 為 "KEY_FORMAT_INVALID"，httpStatusHint 為 401

  # --- Layer 2: 狀態檢查 ---

  @ignore
  Scenario: 金鑰已暫停 — 拒絕驗證
    Given 金鑰 "key-A" 狀態為 Suspended
    When  Gateway 以 "key-A" 的完整金鑰、來源 IP "203.0.113.42"、requestedScope "orders:read" 呼叫驗證
    Then  驗證失敗，errorCode 為 "KEY_INACTIVE"，httpStatusHint 為 401

  @ignore
  Scenario: 金鑰已過期 — 拒絕驗證
    Given 金鑰 "key-A" 狀態為 Expired
    When  Gateway 以 "key-A" 的完整金鑰、來源 IP "203.0.113.42"、requestedScope "orders:read" 呼叫驗證
    Then  驗證失敗，errorCode 為 "KEY_EXPIRED"，httpStatusHint 為 401

  @ignore
  Scenario: 金鑰已撤銷 — 拒絕驗證
    Given 金鑰 "key-A" 狀態為 Revoked
    When  Gateway 以 "key-A" 的完整金鑰、來源 IP "203.0.113.42"、requestedScope "orders:read" 呼叫驗證
    Then  驗證失敗，errorCode 為 "KEY_REVOKED"，httpStatusHint 為 401

  # --- Layer 3: IP 檢查 ---

  @ignore
  Scenario: 來源 IP 不在白名單 — 拒絕驗證
    Given 金鑰 "key-A" 狀態為 Active
    And   "key-A" 的 IP 白名單為 ["203.0.113.42"]
    When  Gateway 以 "key-A" 的完整金鑰、來源 IP "198.51.100.7"、requestedScope "orders:read" 呼叫驗證
    Then  驗證失敗，errorCode 為 "IP_NOT_ALLOWED"，httpStatusHint 為 403

  # --- Layer 4: 雜湊驗證 ---

  @ignore
  Scenario: 金鑰雜湊不匹配 — 拒絕驗證且不區分不存在與錯誤
    Given 金鑰 "key-A" 狀態為 Active
    When  Gateway 以格式合法但雜湊不匹配的金鑰、來源 IP "203.0.113.42"、requestedScope "orders:read" 呼叫驗證
    Then  驗證失敗，errorCode 為 "KEY_NOT_FOUND"，httpStatusHint 為 401

  # --- Layer 5: 權限檢查 ---

  @ignore
  Scenario: Scope 不涵蓋請求的 Endpoint — 拒絕驗證
    Given 金鑰 "key-A" 狀態為 Active
    And   "key-A" 的 scopes 為 ["orders:read"]
    When  Gateway 以 "key-A" 的完整金鑰、來源 IP "203.0.113.42"、requestedScope "orders:write" 呼叫驗證
    Then  驗證失敗，errorCode 為 "SCOPE_INSUFFICIENT"，httpStatusHint 為 403
