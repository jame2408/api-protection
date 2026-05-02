# Access Policy — Per-BC Detailed Design

> Step 4 展開。本文件專注於 Access Policy BC 的 Aggregate 行為規格。

**前置文件參照：**

- 領域模型：[Design Doc §4.3](../design/design-doc.md)
- 整合契約：[Integration Spec §4.2, §4.5](../design/context-integration-spec.md)
- Event Payload：[Integration Spec §6.2](../design/context-integration-spec.md)

---

## 1. Aggregate Root: AccessPolicy

### 1.1 Command 行為規格

#### C1: CreatePolicy

```
Command:  CreatePolicy
Actor:    System（由 Key Lifecycle 的 CreateApiKey / RotateKey 交易內觸發）
Input:    { keyId, tenantId }

Guard:
  keyId 尚未關聯任何 Policy                                    (INV-1: 1:1)

State:    → Created（新建，ipAllowlist 為空集合，rateLimitConfig 為系統預設值）
Event:    PolicyCreated { policyId, keyId, ipAllowlist, rateLimitConfig }

備註：
  此命令不單獨對外暴露，僅由 Application Service 在 CreateApiKey / RotateKey
  的同一交易中呼叫（I2: Partnership）。
```

#### C2: UpdateIpAllowlist

```
Command:  UpdateIpAllowlist
Actor:    Consumer / Service Owner
Input:    { policyId, tenantId, newIpAllowlist: Set<CidrRange> }

Guard:
  Policy 存在
  AND 關聯金鑰非終態（Expired / Revoked）                      (跨 BC 查詢 KL)
  AND newIpAllowlist 中所有項目為合法 CIDR 格式                 (輸入驗證)
  AND 不包含 0.0.0.0/0 或 ::/0（禁止全開白名單）               (INV-3)

State:    ipAllowlist 更新為 newIpAllowlist
Event:    PolicyUpdated { policyId, keyId, changedFields: ["ipAllowlist"], before, after }

備註：
  傳入空集合 = 移除所有 IP 限制（回到預設開放）。
```

#### C3: UpdateRateLimit

```
Command:  UpdateRateLimit
Actor:    Consumer / Service Owner
Input:    { policyId, tenantId, newRateLimitConfig: RateLimitConfig }

Guard:
  Policy 存在
  AND 關聯金鑰非終態（Expired / Revoked）                      (跨 BC 查詢 KL)
  AND quotaLimit > 0                                           (輸入驗證)
  AND rateLimit > 0                                            (輸入驗證)
  AND burstLimit ≥ rateLimit                                   (INV-4)
  AND quotaPeriod ∈ 允許的週期集合                              (輸入驗證)

State:    rateLimitConfig 更新為 newRateLimitConfig
Event:    PolicyUpdated { policyId, keyId, changedFields: ["rateLimitConfig"], before, after }
```

### 1.2 不變條件

| # | 不變條件 | 驗證時機 | 由誰驗證 |
|:--|:---------|:---------|:---------|
| INV-1 | 1:1 關係 | C1 | Application Service：同一交易中建立，Repository 唯一約束 |
| INV-2 | 不可修改終態金鑰的策略 | C2, C3 | Application Service：跨 BC 查詢金鑰狀態 |
| INV-3 | 禁止 0.0.0.0/0 全開白名單 | C2 | Aggregate：CidrRange 驗證 |
| INV-4 | burstLimit ≥ rateLimit | C3 | Aggregate：RateLimitConfig 建構驗證 |

### 1.3 關於「不修改終態金鑰策略」的設計決策

金鑰進入 Expired / Revoked 後，其 AccessPolicy 不再有任何業務作用（驗證漏斗在第 2 層狀態檢查即被攔截）。允許修改只會造成困惑，不會帶來實際效果。

此檢查由 Application Service 透過跨 BC 查詢 Key Lifecycle 完成。AccessPolicy aggregate 本身不持有金鑰狀態資訊（資料隔離原則）。

---

## 2. Value Objects

**CidrRange**

```
CidrRange {
  value: String                    — 如 "192.168.1.0/24" 或 "2001:db8::/32"

  validate():
    - 必須為合法的 IPv4 或 IPv6 CIDR 格式
    - 前綴長度在合理範圍內（IPv4: /8 ~ /32, IPv6: /16 ~ /128）
    - 禁止 0.0.0.0/0 和 ::/0（等同無限制，應使用空集合表示）

  contains(ip: String): Boolean    — 判斷給定 IP 是否在此 CIDR 範圍內
}
```

**RateLimitConfig**

```
RateLimitConfig {
  quotaLimit:  Integer             — 週期內總請求次數上限
  quotaPeriod: Duration            — 配額週期（如 1 小時、1 天）
  rateLimit:   Integer             — 瞬時 RPS 上限
  burstLimit:  Integer             — 突發容忍量（≥ rateLimit）

  validate():
    - quotaLimit > 0
    - rateLimit > 0
    - burstLimit ≥ rateLimit
    - quotaPeriod ∈ { 1min, 1hour, 1day, 1month }
}
```

---

## 3. Repository 介面

```
AccessPolicyRepository {
  save(policy: AccessPolicy): void
  findById(policyId: UUID, tenantId: UUID): AccessPolicy?
  findByKeyId(keyId: UUID, tenantId: UUID): AccessPolicy?
}
```

極簡。AccessPolicy 的查詢需求有限——主要透過 keyId 查找。

---

## 4. 設計模式

| Pattern | 用途 | 說明 |
|:--------|:-----|:-----|
| **Value Object** | CidrRange, RateLimitConfig | 封裝驗證邏輯與格式化，確保建構時即合法 |
| **Domain Event** | PolicyCreated, PolicyUpdated | 透過 Outbox 發布，Audit 和 Validation Model 訂閱 |

---

## 5. Application Service 協調流程

### 5.1 CreatePolicy（被 Key Lifecycle 呼叫）

```
// 在 CreateApiKey / RotateKey 的交易內被呼叫
1. policy = AccessPolicy.create(keyId, tenantId, defaultRateLimitConfig)
2. repo.save(policy)
3. outbox.write(PolicyCreated)
// 不獨立 commit — 由外層交易統一提交
```

### 5.2 UpdateIpAllowlist

```
1. policy = repo.findByKeyId(keyId, tenantId)
2. keyStatus = keyLifecycleQuery.getStatus(keyId, tenantId)   — 跨 BC 查詢
3. guard: keyStatus ∉ { Expired, Revoked }
4. policy.updateIpAllowlist(newIpAllowlist)                   — Aggregate 驗證 CIDR 格式
5. repo.save(policy)
6. outbox.write(PolicyUpdated)
7. commit
```

### 5.3 UpdateRateLimit

```
1. policy = repo.findByKeyId(keyId, tenantId)
2. keyStatus = keyLifecycleQuery.getStatus(keyId, tenantId)   — 跨 BC 查詢
3. guard: keyStatus ∉ { Expired, Revoked }
4. policy.updateRateLimit(newRateLimitConfig)                  — Aggregate 驗證限流參數
5. repo.save(policy)
6. outbox.write(PolicyUpdated)
7. commit
```

---

## 6. 上層文件回饋

1. **禁止全開白名單（0.0.0.0/0）**：Design Doc §4.3 業務規則只提到「白名單為空 = 不限制」，未明確禁止 0.0.0.0/0。建議在業務規則中補充：設定白名單時禁止使用 0.0.0.0/0 或 ::/0，因為這等同於不設白名單但會產生誤導。應使用空集合表示不限制。

2. **quotaPeriod 的允許值**：Design Doc §4.3 的 RateLimitConfig 定義中 quotaPeriod 為 Duration，但未限定允許的週期。建議限定為固定集合（如 1min / 1hour / 1day / 1month），避免任意時間窗口造成計算複雜度。
