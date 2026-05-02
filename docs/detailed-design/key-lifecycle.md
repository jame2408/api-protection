# Key Lifecycle — Per-BC Detailed Design

> Step 4 展開，按 [design-methodology.md](../design-methodology.md) §3 格式撰寫。
> 本文件專注於 Key Lifecycle BC 的 Aggregate 行為規格，供開發者直接實作。

**前置文件參照：**

- 領域模型：[Design Doc §4.2](../design/design-doc.md)
- 狀態機定義：[Design Doc §5](../design/design-doc.md)
- 核心流程：[Design Doc §6](../design/design-doc.md)
- 整合契約：[Integration Spec §4.1, §4.2, §4.6, §4.7](../design/context-integration-spec.md)
- Event Payload：[Integration Spec §6.1](../design/context-integration-spec.md)

---

## 1. Aggregate Root: ApiKey

### 1.1 Command 行為規格

#### C1: CreateApiKey

```
Command:  CreateApiKey
Actor:    Consumer / Service Owner
Input:    { consumerId, tenantId, name, environment, scopes, expiresAt }

Guard:
  Tenant 存在 AND 狀態為 Active                               (I1: ValidateConsumer)
  AND Consumer 存在 AND 屬於該 Tenant                         (I1: ValidateConsumer)
  AND 同 Consumer + Environment 的 Active 金鑰數 < 上限        (Repository 查詢)
  AND name 在同 Consumer + Environment 內不重複                (Repository 查詢)
  AND 所有 scopes 存在於 Scope Registry                        (Registry 查詢)
  AND scopes 至少包含一個                                      (輸入驗證)
  AND expiresAt > now                                          (輸入驗證)
  AND expiresAt ≤ now + 最大允許有效期                          (輸入驗證)

State:    → Active（新建）
Event:    KeyCreated { keyId, consumerId, tenantId, environment, scopes, keyPrefix, expiresAt, policyId }

Side Effect:
  1. ApiKeyFactory 產生金鑰材料（prefix + random + checksum + hash）
  2. 同一交易內建立 AccessPolicy（I2: Partnership）
  3. 回傳金鑰明文（Display Once — 回傳後立即清除記憶體）
```

#### C2: RotateKey

```
Command:  RotateKey
Actor:    Consumer / Service Owner
Input:    { keyId, tenantId, gracePeriod? }

Guard:
  金鑰狀態 = Active
  AND 同 Consumer + Environment 下無其他 Rotating 金鑰          (INV-4)
  AND expiresAt > now（金鑰尚未到期）

State:
  Key A: Active → Rotating
  Key B: → Active（新建）

Event:    KeyRotationInitiated { oldKeyId, newKeyId, graceDeadline }

Side Effect:
  1. 設定 Key A.successorKeyId = Key B.id
  2. 設定 Key B.predecessorKeyId = Key A.id
  3. 設定 Key A.graceDeadline = now + gracePeriod（或系統預設值）
  4. ApiKeyFactory 產生 Key B 的金鑰材料
  5. 同一交易內為 Key B 建立 AccessPolicy（I2: Partnership）
  6. 回傳 Key B 明文（Display Once）
```

#### C3: LockKey

```
Command:  LockKey
Actor:    System（Monitoring / DetectionRule）
Input:    { keyId, tenantId, ruleId, severity, reason, detectedAt, evidence }

Guard:
  金鑰狀態 = Active                                            (INV-5)
  AND Actor.type = System                                      (INV-5)

State:    Active → Locked
Event:    KeyLocked { keyId, ruleId, reason, evidence }
```

#### C4: UnlockKey

```
Command:  UnlockKey
Actor:    Security Admin
Input:    { keyId, tenantId }

Guard:
  金鑰狀態 = Locked
  AND 操作者具備 Security Admin 權限

State:    Locked → Active
Event:    KeyUnlocked { keyId, unlockedBy }
```

#### C5: SuspendKey

```
Command:  SuspendKey
Actor:    Security Admin / Service Owner
Input:    { keyId, tenantId, reason }

Guard:
  金鑰狀態 = Active                                            (INV-6)
  AND Actor.type = User                                        (INV-6)
  AND 操作者具備暫停權限
  AND reason 不為空

State:    Active → Suspended
Event:    KeySuspended { keyId, suspendedBy, reason }
```

#### C6: ResumeKey

```
Command:  ResumeKey
Actor:    Security Admin / Service Owner
Input:    { keyId, tenantId }

Guard:
  金鑰狀態 = Suspended
  AND 操作者具備恢復權限

State:    Suspended → Active
Event:    KeyResumed { keyId, resumedBy }
```

#### C7: RevokeKey

```
Command:  RevokeKey
Actor:    Security Admin / Service Owner / Secret Scanner
Input:    { keyId, tenantId, reason }

Guard:
  金鑰狀態 ∈ { Active, Rotating, Locked, Suspended }           (非終態)
  AND reason 不為空                                             (INV-7)

State:（依來源狀態）
  Active    → Revoked
  Rotating  → Revoked
  Locked    → Revoked
  Suspended → Revoked

Event:    KeyRevoked { keyId, previousStatus, reason, revokedBy }

Side Effect:
  1. 觸發主動快取失效（Pub/Sub 廣播至所有 Gateway 節點）
  2. 若來源狀態為 Rotating：清除 successorKeyId / predecessorKeyId 關聯
```

#### C8: ExpireKey

```
Command:  ExpireKey
Actor:    System Agent（定時掃描）
Input:    { keyId, tenantId }

Guard:
  now ≥ expiresAt
  AND 金鑰狀態 ∈ { Active, Rotating, Suspended, Locked }       (非終態)

State:（依當前狀態分流 — ADR-03）
  Active    → Expired
  Rotating  → Expired
  Suspended → Expired
  Locked    → Revoked（保留安全上下文）

Event:
  Locked → Revoked:
    KeyRevoked { keyId, previousStatus: Locked,
      reason: "System: locked key expired (original lock rule: {ruleId})",
      revokedBy: System }
  其他 → Expired:
    KeyExpired { keyId, previousStatus }

Side Effect:
  Locked → Revoked 時觸發主動快取失效
```

#### C9: CompleteGracePeriod

```
Command:  CompleteGracePeriod
Actor:    System Agent（定時掃描）
Input:    { keyId, tenantId }

Guard:
  金鑰狀態 = Rotating
  AND now ≥ graceDeadline

State:    Rotating → Revoked

Event:    KeyGracePeriodExpired { keyId, successorKeyId }

Side Effect:
  清除 successorKeyId / predecessorKeyId 關聯
  觸發主動快取失效
```

### 1.2 不變條件驗證方式

| # | 不變條件 | 驗證時機 | 由誰驗證 |
|:--|:---------|:---------|:---------|
| INV-1 | 終態不可逆 | 所有命令 | Aggregate：Expired / Revoked 拒絕所有轉換 |
| INV-2 | Rotating 必有 Successor | C2 | Aggregate：rotate() 設定 successorKeyId |
| INV-3 | Successor 必為 Active | C2 | Application Service：建立 Key B 後驗證 |
| INV-4 | 單一 Rotating | C2 | Application Service：Repository 查詢 |
| INV-5 | Locked 僅限系統 | C3 | Aggregate：檢查 Actor.type = System |
| INV-6 | Suspended 僅限人為 | C5 | Aggregate：檢查 Actor.type = User |
| INV-7 | 撤銷必須有因 | C7, C8(Locked) | Aggregate：reason 非空 |
| INV-8 | 環境不可變 | — | Aggregate：environment 為不可變欄位 |

**職責分界原則：**

- **Aggregate 內部驗證**：單一 Aggregate 自身可完成的檢查（狀態轉換合法性、Actor 類型、必填欄位）。
- **Application Service 驗證**：需要 Repository 查詢或跨 BC 呼叫才能完成的檢查（數量上限、名稱唯一性、Tenant 有效性）。

### 1.3 Transition → Command 對照

| Transition | From → To | Command |
|:-----------|:----------|:--------|
| T1 | Active → Rotating | C2: RotateKey |
| T2 | Active → Locked | C3: LockKey |
| T3 | Active → Suspended | C5: SuspendKey |
| T4 | Active → Revoked | C7: RevokeKey |
| T5 | Active → Expired | C8: ExpireKey |
| T6 | Rotating → Revoked | C7: RevokeKey / C9: CompleteGracePeriod |
| T7 | Rotating → Expired | C8: ExpireKey |
| T8 | Locked → Active | C4: UnlockKey |
| T9 | Locked → Revoked | C7: RevokeKey |
| T10 | Locked → Revoked | C8: ExpireKey |
| T11 | Suspended → Active | C6: ResumeKey |
| T12 | Suspended → Revoked | C7: RevokeKey |
| T13 | Suspended → Expired | C8: ExpireKey |

---

## 2. Factory: ApiKeyFactory

```
ApiKeyFactory {
  create(consumerId, tenantId, name, env, scopes, expiresAt): (ApiKey, RawKeyMaterial)
}
```

**職責：**

1. 透過 CSPRNG 產生隨機部分（256-bit entropy）
2. 組裝金鑰格式：`{prefix}_{random}_{checksum}`
3. 計算 KeyHash（per-key 獨立鹽值，`$algorithm$salt$hash` 格式）
4. 回傳 ApiKey aggregate 實例 + 原始金鑰明文

```
RawKeyMaterial {
  fullKey:   String    — 完整金鑰明文（{prefix}_{random}_{checksum}）
  truncated: String    — 後四碼（如 "...a9B3"），用於管理介面顯示
}
```

**設計規則：**

- RawKeyMaterial 只在 Application Service 回傳給呼叫端後立即銷毀。
- Factory 產生的 ApiKey aggregate 僅包含 keyHash 和 truncatedKey，不含明文。
- CreateApiKey 和 RotateKey 共用同一個 Factory。

---

## 3. 參考資料：Scope Registry

Scope Registry 是 Key Lifecycle Context 的參考資料（非 Aggregate），紀錄所有可用的 `resource:action` 組合。

```
ScopeRegistry {
  exists(scope: Scope): Boolean
  existsAll(scopes: Set<Scope>): Boolean
  findByResource(resource: String): Set<Scope>
}
```

- 由 Service Owner 維護（透過管理介面註冊 / 移除 Scope）。
- CreateApiKey 的 Guard 會檢查所有 scopes 都存在於 Registry。
- Wildcard 展開：`orders:*` → 查詢該 resource 下所有已註冊的 action。
- Scope 移除時不影響已關聯的金鑰，但發出孤兒 Scope 警告（Design Doc §4.2）。

---

## 4. Repository 介面

```
ApiKeyRepository {
  // 寫入
  save(apiKey: ApiKey): void

  // 讀取
  findById(keyId: UUID, tenantId: UUID): ApiKey?

  // Guard 查詢
  countActiveByConsumerAndEnv(
    consumerId: UUID, env: Environment, tenantId: UUID
  ): Integer

  existsRotatingByConsumerAndEnv(
    consumerId: UUID, env: Environment, tenantId: UUID
  ): Boolean

  existsByNameAndConsumerAndEnv(
    name: String, consumerId: UUID, env: Environment, tenantId: UUID
  ): Boolean

  // 系統代理查詢
  findExpired(now: Timestamp): List<ApiKey>
  findGracePeriodExpired(now: Timestamp): List<ApiKey>
  findExpiringSoon(threshold: Duration): List<ApiKey>
  findInactiveSince(since: Timestamp, tenantId: UUID): List<ApiKey>

  // Secret Scanner 查詢（跨 Tenant）
  findNonTerminalByPrefix(prefix: String): List<ApiKey>
}
```

**設計規則：**

- 除以下例外，所有查詢都攜帶 `tenantId` 作為隔離條件。
- `findExpired` / `findGracePeriodExpired`：系統代理的全局掃描，跨 Tenant。
- `findNonTerminalByPrefix`：Secret Scanner 通報洩漏時使用，需跨 Tenant 查詢。僅返回非終態金鑰（終態金鑰無需撤銷）。

---

## 5. 設計模式

| Pattern | 用途 | 說明 |
|:--------|:-----|:-----|
| **State Machine** | ApiKey aggregate | 核心模式。Aggregate 內部維護狀態轉換表，拒絕非法轉換 |
| **Factory** | ApiKeyFactory | 金鑰建立邏輯（CSPRNG、格式組裝、雜湊）封裝在 Factory，CreateApiKey / RotateKey 共用 |
| **Value Object** | KeyPrefix, KeyHash, Scope, Environment | 不可變物件，封裝驗證與比較邏輯 |
| **Domain Event** | 所有命令 | 每次狀態變更產生事件，透過 Outbox 發布 |

---

## 6. Application Service 協調流程

以下描述關鍵命令的完整協調流程，展示 Aggregate、Factory、Repository、跨 BC 呼叫如何組合。

### 6.1 CreateApiKey

```
1.  validateConsumer(tenantId, consumerId)              — I1: 同步查詢 TM
2.  repo.countActiveByConsumerAndEnv(...)               — Guard: 數量上限
3.  repo.existsByNameAndConsumerAndEnv(...)              — Guard: 名稱唯一
4.  scopeRegistry.existsAll(scopes)                     — Guard: Scope 存在
5.  (apiKey, rawKey) = factory.create(...)               — 產生 ApiKey + 金鑰明文
6.  accessPolicyService.createDefault(apiKey.keyId)      — I2: 同一交易建立 Policy
7.  repo.save(apiKey)                                    — 儲存
8.  outbox.write(KeyCreated, PolicyCreated)               — 事件寫入 Outbox
9.  commit                                               — 提交交易
10. return rawKey                                        — Display Once
11. rawKey.clear()                                       — 安全清除記憶體
```

### 6.2 RotateKey

```
1.  keyA = repo.findById(keyId, tenantId)                — 取得 Key A
2.  guard: keyA.status = Active                          — 狀態檢查
3.  guard: keyA.expiresAt > now                          — 尚未到期
4.  repo.existsRotatingByConsumerAndEnv(...)              — Guard: 無其他 Rotating
5.  keyA.initiateRotation(gracePeriod)                    — Key A: Active → Rotating
6.  (keyB, rawKey) = factory.create(...)                  — 產生 Key B
7.  keyA.setSuccessor(keyB.keyId)                         — 建立關聯
8.  keyB.setPredecessor(keyA.keyId)
9.  accessPolicyService.createDefault(keyB.keyId)         — I2: Policy for Key B
10. repo.save(keyA)
11. repo.save(keyB)
12. outbox.write(KeyRotationInitiated, KeyCreated, PolicyCreated)
13. commit
14. return rawKey                                        — Display Once
15. rawKey.clear()
```

### 6.3 RevokeKey

```
1. apiKey = repo.findById(keyId, tenantId)
2. apiKey.revoke(reason, actor)                          — Aggregate 內部驗證 + 狀態轉換
3. repo.save(apiKey)
4. outbox.write(KeyRevoked)
5. commit
```

### 6.4 ExpireKey（System Agent 批次）

```
1. expiredKeys = repo.findExpired(now)                   — 全局掃描
2. for each key in expiredKeys:
     key.expire()                                        — Aggregate 內部分流邏輯
     repo.save(key)
     outbox.write(KeyExpired or KeyRevoked)               — 依分流結果
3. commit（可分批提交，避免超大交易）
```

### 6.5 Secret Scanner 自動撤銷

```
1. keys = repo.findNonTerminalByPrefix(leakedPrefix)     — 跨 Tenant 查詢
2. for each key in keys:
     key.revoke("Key leaked in public repository", SystemActor)
     repo.save(key)
     outbox.write(KeyRevoked)
3. commit
4. notify(SecurityAdmin, Consumer)                       — 緊急通知
```

---

## 7. 上層文件回饋

撰寫過程中發現以下需回饋至上層文件的項目：

### 回饋至 Design Doc

1. **ApiKey 缺少 `name` 欄位**：Design Doc §4.2 的 ApiKey 屬性中沒有 `name`。金鑰需要名稱讓使用者識別（如 "my-production-key"）。建議在 ApiKey 新增 `name: String` 屬性。

2. **最大有效期未定義**：Design Doc 規定「不允許永久有效」，但沒有定義最大有效期上限。建議新增為 Open Question 或在 Tenant 配置中定義（如 Production 最長 1 年，Sandbox 最長 90 天）。

3. **SuspendKey 應要求 reason**：Design Doc §5.3 T3 的前置條件未明確要求 reason，但基於審計完整性，建議 SuspendKey 也必須提供暫停原因。

### 回饋至 Integration Spec

1. **RotateKey 的多事件產出**：RotateKey 在同一交易中產生 KeyRotationInitiated + KeyCreated(Key B) + PolicyCreated 三個事件。Integration Spec §4.4 應補充說明 Monitoring 也需訂閱輪替中產生的 KeyCreated，以開始監控 Key B。
