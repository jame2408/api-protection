# Tenant Management — Per-BC Detailed Design

> Step 4 展開。本 BC 是 Generic Subdomain，提供多租戶隔離的基礎身份模型。結構最簡單，無複雜業務邏輯。

**前置文件參照：**

- 領域模型：[Design Doc §4.6](../design/design-doc.md)
- 整合契約：[Integration Spec §4.1 I1](../design/context-integration-spec.md)

---

## 1. Aggregate Root: Tenant

### 1.1 Command 行為規格

#### C1: CreateTenant

```
Command:  CreateTenant
Actor:    Platform Admin
Input:    { name: String }

Guard:
  name 不可為空                                                 (輸入驗證)
  name 不重複                                                   (INV-1)

State:    → Created（status = Active）
Event:    無 Domain Event（下游 BC 以 Conformist 模式同步查詢，不訂閱事件）
```

#### C2: SuspendTenant

```
Command:  SuspendTenant
Actor:    Platform Admin
Input:    { tenantId: UUID, reason: String }

Guard:
  Tenant 存在
  AND status = Active                                           (INV-2)

State:    status → Suspended
Event:    無 Domain Event

備註：
  Tenant 暫停後，其下所有金鑰是否連帶暫停為 Open Question（Q8）。
  目前 KL 僅在金鑰建立時查詢 TM，不訂閱 TM 事件。
  暫停的 Tenant 下無法建立新金鑰（I1 ValidateConsumer 回傳 TENANT_SUSPENDED）。
```

#### C3: ReactivateTenant

```
Command:  ReactivateTenant
Actor:    Platform Admin
Input:    { tenantId: UUID }

Guard:
  Tenant 存在
  AND status = Suspended                                        (INV-2)

State:    status → Active
Event:    無 Domain Event
```

### 1.2 不變條件

| # | 不變條件 | 驗證時機 | 說明 |
|:--|:---------|:---------|:-----|
| INV-1 | Tenant name 唯一 | C1 | Repository 唯一約束 |
| INV-2 | 狀態轉換合法 | C2, C3 | Active ↔ Suspended，不存在其他轉換 |

---

## 2. Entity: Consumer

### 2.1 Command 行為規格

#### C4: RegisterConsumer

```
Command:  RegisterConsumer
Actor:    Tenant Admin
Input:    { tenantId: UUID, name: String, description: String }

Guard:
  Tenant 存在 AND status = Active                               (INV-3)
  name 在同一 Tenant 內不重複                                   (INV-4)

State:    → Created（新 Consumer）
Event:    無 Domain Event
```

#### C5: UpdateConsumer

```
Command:  UpdateConsumer
Actor:    Tenant Admin
Input:    { consumerId: UUID, tenantId: UUID, name?, description? }

Guard:
  Consumer 存在 AND 屬於該 Tenant
  name 若有提供，在同一 Tenant 內不重複                          (INV-4)

State:    對應欄位更新
Event:    無 Domain Event
```

### 2.2 不變條件

| # | 不變條件 | 驗證時機 | 說明 |
|:--|:---------|:---------|:-----|
| INV-3 | Consumer 只能建在 Active Tenant 下 | C4 | Suspended Tenant 不可新增 Consumer |
| INV-4 | Consumer name 在 Tenant 內唯一 | C4, C5 | Repository 複合唯一約束 |

---

## 3. 查詢介面（供 I1 使用）

### 3.1 ValidateConsumer（I1 契約實作）

```
Query:   ValidateConsumer
Input:   { tenantId: UUID, consumerId: UUID }
Output:  { valid: Boolean, tenantStatus: TenantStatus }
Errors:
  TENANT_NOT_FOUND
  CONSUMER_NOT_FOUND
  TENANT_SUSPENDED
```

此查詢由 Key Lifecycle 在建立金鑰前同步呼叫（I1 Conformist）。

### 3.2 其他查詢

```
GetTenant:       { tenantId } → Tenant
ListConsumers:   { tenantId } → List<Consumer>
```

---

## 4. Repository 介面

```
TenantRepository {
  save(tenant: Tenant): void
  findById(tenantId: UUID): Tenant?
  existsByName(name: String): Boolean
}

ConsumerRepository {
  save(consumer: Consumer): void
  findById(consumerId: UUID, tenantId: UUID): Consumer?
  findByTenantId(tenantId: UUID): List<Consumer>
  existsByName(name: String, tenantId: UUID): Boolean
}
```

---

## 5. Application Service 協調流程

所有流程都是單一 Aggregate 操作，無跨 BC 協作。

```
CreateTenant:
1. guard: name 唯一
2. tenant = Tenant.create(name)
3. repo.save(tenant)
4. commit

RegisterConsumer:
1. tenant = tenantRepo.findById(tenantId)
2. guard: tenant.status = Active
3. guard: consumer name 在 tenant 內唯一
4. consumer = Consumer.create(tenantId, name, description)
5. repo.save(consumer)
6. commit
```

---

## 6. 設計備註

**為何 TM 不發布 Domain Event？**

TM 是 Generic Subdomain，下游 BC（Key Lifecycle）以 Conformist 模式同步查詢。原因：

1. TM 的變更極低頻（租戶/消費者的增刪改非常少見）
2. KL 只在金鑰建立時需要驗證身份，不需要持續追蹤 TM 狀態
3. 避免為低頻場景引入事件通道的複雜度

**Q8 的影響：** 若未來決定 Tenant 暫停需級聯影響其下金鑰，則需新增 TenantSuspended 事件供 KL 訂閱。此變更的影響範圍：TM 新增 Outbox、KL 新增事件消費者、Integration Spec 新增整合關係。目前不實作。
