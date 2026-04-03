# Tenant Management — BDD Specification

> Step 5 展開，按 [design-methodology.md](../design-methodology.md) §5 格式撰寫。
> 場景由 [bc-tenant-management.md](../detailed-design/tenant-management.md) 的 Command / Guard / State 及查詢介面推導。

---

## Command → Feature 對照

| Feature | 對應 Command / Query | Scenario 數 |
|:--------|:---------------------|:------------|
| 1. 管理租戶 | C1: CreateTenant, C2: SuspendTenant, C3: ReactivateTenant | 8 |
| 2. 管理 Consumer | C4: RegisterConsumer, C5: UpdateConsumer | 7 |
| 3. 驗證 Consumer 身份 | ValidateConsumer（I1 查詢） | 4 |

---

## Feature 1: 管理租戶

```gherkin
Feature: 管理租戶

  # === C1: CreateTenant ===

  Scenario: 成功建立租戶
    Given 沒有名為 "Acme Corp" 的租戶
    When  Platform Admin 建立租戶，name 為 "Acme Corp"
    Then  租戶建立成功，status 為 Active

  Scenario: 租戶名稱為空 — 拒絕建立
    When  Platform Admin 建立租戶，name 為空
    Then  建立失敗，錯誤原因為「租戶名稱不可為空」

  Scenario: 租戶名稱重複 — 拒絕建立
    Given 已存在名為 "Acme Corp" 的租戶
    When  Platform Admin 建立名為 "Acme Corp" 的租戶
    Then  建立失敗，錯誤原因為「租戶名稱已存在」

  # === C2: SuspendTenant ===

  Scenario: 暫停租戶
    Given 租戶 "tenant-A" status 為 Active
    When  Platform Admin 暫停 "tenant-A"，reason 為「違反使用條款」
    Then  "tenant-A" 的 status 變為 Suspended

  Scenario: 租戶非 Active — 拒絕暫停
    Given 租戶 "tenant-A" status 為 Suspended
    When  Platform Admin 暫停 "tenant-A"
    Then  暫停失敗，錯誤原因為「租戶狀態非 Active」

  # === C3: ReactivateTenant ===

  Scenario: 重新啟用租戶
    Given 租戶 "tenant-A" status 為 Suspended
    When  Platform Admin 重新啟用 "tenant-A"
    Then  "tenant-A" 的 status 變為 Active

  Scenario: 租戶非 Suspended — 拒絕重新啟用
    Given 租戶 "tenant-A" status 為 Active
    When  Platform Admin 重新啟用 "tenant-A"
    Then  操作失敗，錯誤原因為「租戶狀態非 Suspended」

  Scenario: 租戶不存在 — 拒絕操作
    Given 租戶 "tenant-X" 不存在
    When  Platform Admin 嘗試暫停 "tenant-X"
    Then  操作失敗，錯誤原因為「租戶不存在」
```

---

## Feature 2: 管理 Consumer

```gherkin
Feature: 管理 Consumer

  # === C4: RegisterConsumer ===

  Scenario: 成功註冊 Consumer
    Given 租戶 "tenant-A" status 為 Active
    And   "tenant-A" 內沒有名為 "order-service" 的 Consumer
    When  Tenant Admin 在 "tenant-A" 註冊 Consumer，name "order-service", description "訂單服務"
    Then  Consumer 建立成功，歸屬 "tenant-A"

  Scenario: 租戶已暫停 — 拒絕註冊
    Given 租戶 "tenant-A" status 為 Suspended
    When  Tenant Admin 嘗試在 "tenant-A" 註冊 Consumer
    Then  註冊失敗，錯誤原因為「租戶已暫停，無法新增 Consumer」

  Scenario: Consumer 名稱在同一 Tenant 內重複 — 拒絕註冊
    Given 租戶 "tenant-A" 內已有名為 "order-service" 的 Consumer
    When  Tenant Admin 在 "tenant-A" 註冊名為 "order-service" 的 Consumer
    Then  註冊失敗，錯誤原因為「Consumer 名稱在此租戶內已存在」

  # === C5: UpdateConsumer ===

  Scenario: 成功更新 Consumer
    Given Consumer "consumer-1" 存在且屬於 "tenant-A"
    When  Tenant Admin 更新 "consumer-1" 的 description 為「訂單服務 v2」
    Then  "consumer-1" 的 description 更新為「訂單服務 v2」

  Scenario: Consumer 不存在 — 拒絕更新
    Given Consumer "consumer-X" 不存在
    When  Tenant Admin 嘗試更新 "consumer-X"
    Then  更新失敗，錯誤原因為「Consumer 不存在」

  Scenario: Consumer 不屬於該 Tenant — 拒絕更新
    Given Consumer "consumer-1" 屬於 "tenant-A"
    When  Tenant Admin 以 "tenant-B" 身份嘗試更新 "consumer-1"
    Then  更新失敗，錯誤原因為「Consumer 不屬於該租戶」

  Scenario: 更新名稱與同 Tenant 下其他 Consumer 重複 — 拒絕更新
    Given "tenant-A" 內已有 Consumer "order-service" 和 "payment-service"
    When  Tenant Admin 將 "payment-service" 的 name 更新為 "order-service"
    Then  更新失敗，錯誤原因為「Consumer 名稱在此租戶內已存在」
```

---

## Feature 3: 驗證 Consumer 身份

```gherkin
Feature: 驗證 Consumer 身份（I1 查詢）

  Scenario: 驗證通過 — 租戶 Active 且 Consumer 存在
    Given 租戶 "tenant-A" status 為 Active
    And   Consumer "consumer-1" 屬於 "tenant-A"
    When  Key Lifecycle 查詢 ValidateConsumer(tenantId: "tenant-A", consumerId: "consumer-1")
    Then  回傳 valid = true, tenantStatus = Active

  Scenario: 租戶不存在 — 驗證失敗
    Given 租戶 "tenant-X" 不存在
    When  Key Lifecycle 查詢 ValidateConsumer(tenantId: "tenant-X", consumerId: "consumer-1")
    Then  回傳錯誤 TENANT_NOT_FOUND

  Scenario: 租戶已暫停 — 驗證失敗
    Given 租戶 "tenant-A" status 為 Suspended
    When  Key Lifecycle 查詢 ValidateConsumer(tenantId: "tenant-A", consumerId: "consumer-1")
    Then  回傳錯誤 TENANT_SUSPENDED

  Scenario: Consumer 不存在 — 驗證失敗
    Given 租戶 "tenant-A" status 為 Active
    And   Consumer "consumer-X" 不屬於 "tenant-A"
    When  Key Lifecycle 查詢 ValidateConsumer(tenantId: "tenant-A", consumerId: "consumer-X")
    Then  回傳錯誤 CONSUMER_NOT_FOUND
```
