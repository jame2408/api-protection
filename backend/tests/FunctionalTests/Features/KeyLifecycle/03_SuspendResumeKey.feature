Feature: 暫停與恢復金鑰

  # === C5: SuspendKey ===

  Scenario: 成功暫停金鑰
    Given 金鑰 "key-A" 狀態為 Active
    And   操作者為 Security Admin（人為操作者）
    When  Security Admin 暫停 "key-A"，原因為「維護排程」
    Then  "key-A" 狀態變為 Suspended
    And   系統產生 KeySuspended 事件，包含 keyId、suspendedBy、reason

  Scenario: 金鑰非 Active 狀態 — 拒絕暫停
    Given 金鑰 "key-A" 狀態為 Locked
    When  Security Admin 暫停 "key-A"，原因為「維護排程」
    Then  暫停失敗，錯誤原因為「金鑰狀態非 Active」

  Scenario: System 嘗試暫停 — 拒絕
    Given 金鑰 "key-A" 狀態為 Active
    When  System（非人為操作者）對 "key-A" 發出暫停命令
    Then  暫停失敗，錯誤原因為「暫停操作僅限人為操作」

  Scenario: 操作者無暫停權限 — 拒絕
    Given 金鑰 "key-A" 狀態為 Active
    And   操作者為一般 Consumer（無暫停權限）
    When  操作者暫停 "key-A"，原因為「維護排程」
    Then  暫停失敗，錯誤原因為「權限不足」

  Scenario: 未提供暫停原因 — 拒絕
    Given 金鑰 "key-A" 狀態為 Active
    And   操作者為 Security Admin
    When  Security Admin 暫停 "key-A"，未提供原因
    Then  暫停失敗，錯誤原因為「必須提供暫停原因」

  # === C6: ResumeKey ===

  @ignore
  Scenario: 成功恢復金鑰
    Given 金鑰 "key-A" 狀態為 Suspended
    And   操作者具備恢復權限
    When  操作者恢復 "key-A"
    Then  "key-A" 狀態變為 Active
    And   系統產生 KeyResumed 事件，包含 keyId、resumedBy

  @ignore
  Scenario: 金鑰非 Suspended 狀態 — 拒絕恢復
    Given 金鑰 "key-A" 狀態為 Locked
    When  操作者恢復 "key-A"
    Then  恢復失敗，錯誤原因為「金鑰狀態非 Suspended」

  @ignore
  Scenario: 操作者無恢復權限 — 拒絕恢復
    Given 金鑰 "key-A" 狀態為 Suspended
    And   操作者為一般 Consumer（無恢復權限）
    When  操作者恢復 "key-A"
    Then  恢復失敗，錯誤原因為「權限不足」
