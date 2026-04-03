Feature: 鎖定與解鎖金鑰

  # === C3: LockKey ===

  @ignore
  Scenario: 系統偵測到異常 — 自動鎖定金鑰
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  System 以 ruleId "impossible-travel"、severity HIGH 鎖定 "key-A"，原因為「異地同時存取」
    Then  "key-A" 狀態變為 LOCKED
    And   系統產生 KeyLocked 事件，包含 keyId、ruleId、reason、evidence

  @ignore
  Scenario: 金鑰非 ACTIVE 狀態 — 拒絕鎖定
    Given 金鑰 "key-A" 狀態為 SUSPENDED
    When  System 對 "key-A" 發出鎖定命令
    Then  鎖定失敗，錯誤原因為「金鑰狀態非 ACTIVE」

  @ignore
  Scenario: 非 System 角色嘗試鎖定 — 拒絕
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  Security Admin（人為操作者）對 "key-A" 發出鎖定命令
    Then  鎖定失敗，錯誤原因為「只有系統可以鎖定金鑰」

  # === C4: UnlockKey ===

  @ignore
  Scenario: Security Admin 解鎖金鑰
    Given 金鑰 "key-A" 狀態為 LOCKED
    And   操作者為 Security Admin
    When  Security Admin 對 "key-A" 發出解鎖命令
    Then  "key-A" 狀態變為 ACTIVE
    And   系統產生 KeyUnlocked 事件，包含 keyId、unlockedBy

  @ignore
  Scenario: 金鑰非 LOCKED 狀態 — 拒絕解鎖
    Given 金鑰 "key-A" 狀態為 ACTIVE
    When  Security Admin 對 "key-A" 發出解鎖命令
    Then  解鎖失敗，錯誤原因為「金鑰狀態非 LOCKED」

  @ignore
  Scenario: 操作者權限不足 — 拒絕解鎖
    Given 金鑰 "key-A" 狀態為 LOCKED
    And   操作者為一般 Consumer
    When  操作者對 "key-A" 發出解鎖命令
    Then  解鎖失敗，錯誤原因為「權限不足」
