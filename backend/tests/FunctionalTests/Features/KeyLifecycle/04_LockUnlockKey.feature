Feature: 鎖定與解鎖金鑰

  # === C3: LockKey ===

  Scenario: 系統偵測到異常 — 自動鎖定金鑰
    Given 金鑰 "key-A" 狀態為 Active
    When  System 以 ruleId "impossible-travel"、severity HIGH 鎖定 "key-A"，原因為「異地同時存取」
    Then  "key-A" 狀態變為 Locked
    And   系統產生 KeyLocked 事件，包含 keyId、ruleId、reason、evidence

  Scenario: 金鑰非 Active 狀態 — 拒絕鎖定
    Given 金鑰 "key-A" 狀態為 Suspended
    When  System 對 "key-A" 發出鎖定命令
    Then  鎖定失敗，錯誤原因為「金鑰狀態非 Active」

  Scenario: 非 System 角色嘗試鎖定 — 拒絕
    Given 金鑰 "key-A" 狀態為 Active
    When  Security Admin（人為操作者）對 "key-A" 發出鎖定命令
    Then  鎖定失敗，錯誤原因為「只有系統可以鎖定金鑰」

  # === C4: UnlockKey ===

  Scenario: Security Admin 解鎖金鑰
    Given 金鑰 "key-A" 狀態為 Locked
    And   操作者為 Security Admin
    When  Security Admin 對 "key-A" 發出解鎖命令
    Then  "key-A" 狀態變為 Active
    And   系統產生 KeyUnlocked 事件，包含 keyId、unlockedBy

  @ignore
  Scenario: 金鑰非 Locked 狀態 — 拒絕解鎖
    Given 金鑰 "key-A" 狀態為 Active
    When  Security Admin 對 "key-A" 發出解鎖命令
    Then  解鎖失敗，錯誤原因為「金鑰狀態非 Locked」

  @ignore
  Scenario: 操作者權限不足 — 拒絕解鎖
    Given 金鑰 "key-A" 狀態為 Locked
    And   操作者為一般 Consumer
    When  操作者對 "key-A" 發出解鎖命令
    Then  解鎖失敗，錯誤原因為「權限不足」
