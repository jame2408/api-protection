# API 金鑰管理系統 — API Specification

> 本文件定義系統所有 REST API Endpoint 的契約規格。
> API 設計與 [Context Map](./design-doc.md#32-context-map) 嚴格對齊，每個 BC 擁有獨立的 Endpoint 群組。

---

## 1. 概述

### 1.1 設計原則

1. **BC-Aligned** — Endpoint 按 Bounded Context 分群，URL 結構反映領域邊界
2. **Tenant-Scoped by Default** — 租戶資源掛在 `/tenants/{tenantId}/` 下；平台管理操作掛在 `/admin/`
3. **Resource + Action** — CRUD 使用標準 HTTP Method；狀態轉換使用 `POST /resource/{id}/{action}`
4. **Control Plane / Data Plane 分離** — 管理 API（低頻）與驗證 API（高頻）各自獨立，契約互不影響

### 1.2 版本策略

URL Path Versioning：`/api/v1/...`

- Major 版號在 URL 路徑
- Minor / Patch 以向後相容方式演進，不影響 URL
- 破壞性變更遞增版號（`/api/v2/`），舊版本維護至少 6 個月

### 1.3 Base URL

| 環境 | Base URL |
|:-----|:---------|
| Development | `https://localhost:5001/api/v1` |
| Staging | `https://api-stg.example.com/api/v1` |
| Production | `https://api.example.com/api/v1` |
| Data Plane (Internal) | `https://internal-api.example.com/api/internal/v1` |

### 1.4 API 分類

| 分類 | 路徑前綴 | 用途 | 頻率 |
|:-----|:---------|:-----|:-----|
| Admin (Platform) | `/api/v1/admin/...` | 平台管理員操作（Tenant CRUD、Detection Rules） | 極低 |
| Tenant-Scoped | `/api/v1/tenants/{tenantId}/...` | 租戶內資源管理（Keys、Policies、Alerts、Audit） | 低～中 |
| Data Plane | `/api/internal/v1/...` | Gateway 金鑰驗證（mTLS） | 極高 |

---

## 2. 共用規範

### 2.1 認證與授權

**Control Plane（管理 API）：**

| Header | 格式 |
|:-------|:-----|
| `Authorization` | `Bearer {JWT}` |

JWT Claims：

- `sub` — 使用者 ID
- `tenantId` — 所屬租戶（PlatformAdmin 不含此欄位）
- `role` — 角色
- `consumerId` — 所屬 Consumer（僅 Consumer 角色）

**角色與可存取範圍：**

| 角色 | 說明 | 可存取前綴 |
|:-----|:-----|:-----------|
| PlatformAdmin | 平台超級管理員 | `/admin/*`、所有 `/tenants/*` |
| SecurityAdmin | 安全管理員 | `/admin/detection-rules`、`/tenants/*/alerts`、`/tenants/*/audit-logs` |
| TenantAdmin | 租戶管理員 | `/tenants/{own-tenantId}/*` |
| Consumer | API 消費者 | `/tenants/{own-tenantId}/consumers/{own-consumerId}/keys`、`/tenants/{own-tenantId}/keys/{own-keys}/*` |

> TenantId 取自 URL path 並與 JWT claims 交叉驗證，防止 IDOR。

**Data Plane（驗證 API）：**

- mTLS 或 Internal Service Token
- 僅限內部服務（Gateway / Sidecar）呼叫，不走 JWT

### 2.2 Error Response

所有錯誤遵循 [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457.html)：

```json
{
  "type": "https://api.example.com/errors/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "The 'name' field is required.",
  "errorCode": "VALIDATION_ERROR",
  "errors": [
    { "field": "name", "message": "Name is required." }
  ],
  "traceId": "abc-123-def"
}
```

**共用錯誤碼：**

| errorCode | HTTP Status | 說明 |
|:----------|:------------|:-----|
| VALIDATION_ERROR | 400 | 輸入驗證失敗（`errors` 陣列含各欄位錯誤） |
| UNAUTHORIZED | 401 | 認證失敗或缺少 Token |
| FORBIDDEN | 403 | 無權限執行此操作 |
| NOT_FOUND | 404 | 資源不存在 |
| CONFLICT | 409 | 業務規則衝突（名稱重複、狀態不允許等） |
| RATE_LIMITED | 429 | 超過 API 速率限制 |
| INTERNAL_ERROR | 500 | 伺服器內部錯誤 |

> 各 BC 可擴展自訂 errorCode（如 `KEY_IN_TERMINAL_STATE`），HTTP Status 使用最接近語意的標準碼。

### 2.3 分頁

所有集合查詢使用 **Cursor-based** 分頁：

**Request Query Parameters：**

| 參數 | 類型 | 預設 | 說明 |
|:-----|:-----|:-----|:-----|
| `pageSize` | Integer | 20 | 每頁筆數（上限 100） |
| `cursor` | String? | — | 分頁游標（首次查詢不傳） |

**Response 格式：**

```json
{
  "data": [ ... ],
  "pagination": {
    "nextCursor": "eyJpZCI6MTAwfQ==",
    "hasMore": true,
    "totalCount": 250
  }
}
```

- `nextCursor` 為 `null` 時表示已到最後一頁
- `totalCount` 為符合篩選條件的總筆數

### 2.4 Common Headers

| Header | 方向 | 必填 | 說明 |
|:-------|:-----|:-----|:-----|
| `Authorization` | Request | 是 | Bearer JWT（Control Plane） |
| `Content-Type` | Both | 是 | `application/json` |
| `X-Request-Id` | Request | 否 | 客戶端請求追蹤 ID（未提供則伺服器自動產生） |
| `X-Correlation-Id` | Response | — | 伺服器端關聯 ID，追蹤同一業務流程 |

### 2.5 HTTP Status Code 慣例

| 場景 | Status Code |
|:-----|:------------|
| 建立資源成功 | `201 Created` |
| 查詢 / 更新 / 狀態轉換成功 | `200 OK` |
| 無回應主體的成功操作 | `204 No Content` |
| 非同步任務已接受 | `202 Accepted` |

### 2.6 URL 命名慣例

- 路徑使用 **kebab-case**（`/audit-logs`、`/ip-allowlist`）
- JSON 屬性使用 **camelCase**（`tenantId`、`createdAt`）
- 集合名詞使用**複數**（`/keys`、`/consumers`、`/alerts`）
- 時間格式一律 **ISO 8601**（`2024-01-01T00:00:00Z`）
- Duration 格式使用 **ISO 8601**（`PT24H`、`PT1H`）

---

## 3. Control Plane APIs

### 3.1 Tenant Management

> **BC 對照**：[Tenant Management Detailed Design](../detailed-design/tenant-management.md)
> **整合關係**：I1 — Key Lifecycle 以 Conformist 模式同步查詢 TM 驗證身份

---

#### 3.1.1 POST /admin/tenants — 建立租戶

| 項目 | 值 |
|:-----|:---|
| Authorization | PlatformAdmin |
| Command | C1: CreateTenant |

**Request Body：**

```json
{
  "name": "Acme Corp"
}
```

**Response `201 Created`：**

```json
{
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Acme Corp",
  "status": "Active",
  "createdAt": "2024-01-15T10:30:00Z"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| VALIDATION_ERROR | 400 | `name` 為空 |
| TENANT_NAME_DUPLICATE | 409 | `name` 已存在（INV-1） |

---

#### 3.1.2 GET /admin/tenants/{tenantId} — 查詢租戶

| 項目 | 值 |
|:-----|:---|
| Authorization | PlatformAdmin |

**Response `200 OK`：**

```json
{
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Acme Corp",
  "status": "Active",
  "createdAt": "2024-01-15T10:30:00Z"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | Tenant 不存在 |

---

#### 3.1.3 POST /admin/tenants/{tenantId}/suspend — 暫停租戶

| 項目 | 值 |
|:-----|:---|
| Authorization | PlatformAdmin |
| Command | C2: SuspendTenant |

**Request Body：**

```json
{
  "reason": "Payment overdue"
}
```

**Response `200 OK`：**

```json
{
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Acme Corp",
  "status": "Suspended"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | Tenant 不存在 |
| INVALID_STATE_TRANSITION | 409 | status ≠ Active（INV-2） |

---

#### 3.1.4 POST /admin/tenants/{tenantId}/reactivate — 重新啟用租戶

| 項目 | 值 |
|:-----|:---|
| Authorization | PlatformAdmin |
| Command | C3: ReactivateTenant |

**Response `200 OK`：**

```json
{
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Acme Corp",
  "status": "Active"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | Tenant 不存在 |
| INVALID_STATE_TRANSITION | 409 | status ≠ Suspended（INV-2） |

---

#### 3.1.5 POST /tenants/{tenantId}/consumers — 註冊 Consumer

| 項目 | 值 |
|:-----|:---|
| Authorization | TenantAdmin |
| Command | C4: RegisterConsumer |

**Request Body：**

```json
{
  "name": "Order Service",
  "description": "Handles order processing"
}
```

**Response `201 Created`：**

```json
{
  "consumerId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Order Service",
  "description": "Handles order processing",
  "createdAt": "2024-01-15T11:00:00Z"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| VALIDATION_ERROR | 400 | `name` 為空 |
| TENANT_SUSPENDED | 403 | Tenant 狀態為 Suspended（INV-3） |
| CONSUMER_NAME_DUPLICATE | 409 | 同 Tenant 內 `name` 已存在（INV-4） |

---

#### 3.1.6 GET /tenants/{tenantId}/consumers — 查詢 Consumer 列表

| 項目 | 值 |
|:-----|:---|
| Authorization | TenantAdmin |

**Query Parameters：**

| 參數 | 類型 | 說明 |
|:-----|:-----|:-----|
| `pageSize` | Integer? | 分頁大小 |
| `cursor` | String? | 分頁游標 |

**Response `200 OK`：**

```json
{
  "data": [
    {
      "consumerId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "tenantId": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Order Service",
      "description": "Handles order processing",
      "createdAt": "2024-01-15T11:00:00Z"
    }
  ],
  "pagination": {
    "nextCursor": null,
    "hasMore": false,
    "totalCount": 1
  }
}
```

---

#### 3.1.7 GET /tenants/{tenantId}/consumers/{consumerId} — 查詢單一 Consumer

| 項目 | 值 |
|:-----|:---|
| Authorization | TenantAdmin, Consumer（限自身） |

**Response `200 OK`：**

```json
{
  "consumerId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Order Service",
  "description": "Handles order processing",
  "createdAt": "2024-01-15T11:00:00Z"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | Consumer 不存在或不屬於該 Tenant |

---

#### 3.1.8 PUT /tenants/{tenantId}/consumers/{consumerId} — 更新 Consumer

| 項目 | 值 |
|:-----|:---|
| Authorization | TenantAdmin |
| Command | C5: UpdateConsumer |

**Request Body：**

```json
{
  "name": "Order Service v2",
  "description": "Updated order processing service"
}
```

> 所有欄位皆須提供（full replacement）。

**Response `200 OK`：**

```json
{
  "consumerId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Order Service v2",
  "description": "Updated order processing service",
  "createdAt": "2024-01-15T11:00:00Z"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | Consumer 不存在 |
| CONSUMER_NAME_DUPLICATE | 409 | 同 Tenant 內 `name` 已存在（INV-4） |

---

<!-- 以下章節將依 Deep Dive 模式逐步展開 -->

### 3.2 Key Lifecycle

> **BC 對照**：[Key Lifecycle Detailed Design](../detailed-design/key-lifecycle.md)
> **整合關係**：I1（查詢 TM）、I2（同一交易建立 Policy）、I3/I4（發布事件至 Audit/Monitoring）
>
> **不暴露的 Command**：C3 LockKey（Monitoring 內部觸發 I6）、C8 ExpireKey / C9 CompleteGracePeriod（System Agent Job）

---

#### 3.2.1 POST /tenants/{tenantId}/consumers/{consumerId}/keys — 建立 API 金鑰

| 項目 | 值 |
|:-----|:---|
| Authorization | TenantAdmin, Consumer（限自身 consumerId） |
| Command | C1: CreateApiKey |

**Request Body：**

```json
{
  "name": "order-service-key",
  "environment": "Production",
  "scopes": ["orders:read", "orders:write"],
  "expiresAt": "2024-07-15T10:30:00Z"
}
```

| 欄位 | 類型 | 必填 | 說明 |
|:-----|:-----|:-----|:-----|
| `name` | String | 是 | 金鑰名稱，同一 Consumer + Environment 內不可重複 |
| `environment` | String | 是 | `Sandbox` 或 `Production` |
| `scopes` | String[] | 是 | 至少一個，須在 Scope Registry 中存在 |
| `expiresAt` | Timestamp | 是 | 必須在未來且不超過最大允許有效期 |

**Response `201 Created`：**

```json
{
  "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "consumerId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "order-service-key",
  "keyPrefix": "acme_prod",
  "truncatedKey": "...a9B3",
  "environment": "Production",
  "scopes": ["orders:read", "orders:write"],
  "lifecycleStatus": "Active",
  "policyId": "d4e5f6a7-b8c9-0123-4567-890abcdef012",
  "createdAt": "2024-01-15T12:00:00Z",
  "expiresAt": "2024-07-15T10:30:00Z",
  "rawKey": "acme_prod_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6_xY9z"
}
```

> ⚠️ **Display Once**：`rawKey` 僅在此回應中出現一次，後續查詢不再回傳。客戶端必須立即安全儲存。

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| VALIDATION_ERROR | 400 | 欄位驗證失敗（name 空、scopes 空、expiresAt 已過期等） |
| TENANT_NOT_FOUND | 404 | Tenant 不存在（I1） |
| CONSUMER_NOT_FOUND | 404 | Consumer 不存在或不屬於該 Tenant（I1） |
| TENANT_SUSPENDED | 403 | Tenant 已暫停（I1） |
| KEY_LIMIT_EXCEEDED | 409 | 同 Consumer + Environment 下 ACTIVE 金鑰數達上限 |
| KEY_NAME_DUPLICATE | 409 | 同 Consumer + Environment 下名稱重複 |
| SCOPE_NOT_FOUND | 422 | Scope 不存在於 Registry |
| EXPIRES_AT_EXCEEDS_MAX | 422 | 超過最大允許有效期 |

---

#### 3.2.2 GET /tenants/{tenantId}/consumers/{consumerId}/keys — 查詢 Consumer 的金鑰列表

| 項目 | 值 |
|:-----|:---|
| Authorization | TenantAdmin, Consumer（限自身） |

**Query Parameters：**

| 參數 | 類型 | 說明 |
|:-----|:-----|:-----|
| `environment` | String? | 篩選環境（`Sandbox` / `Production`） |
| `status` | String? | 篩選狀態（`Active` / `Rotating` / `Locked` / `Suspended` / `Expired` / `Revoked`） |
| `pageSize` | Integer? | 分頁大小 |
| `cursor` | String? | 分頁游標 |

**Response `200 OK`：**

```json
{
  "data": [
    {
      "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "name": "order-service-key",
      "keyPrefix": "acme_prod",
      "truncatedKey": "...a9B3",
      "environment": "Production",
      "scopes": ["orders:read", "orders:write"],
      "lifecycleStatus": "Active",
      "createdAt": "2024-01-15T12:00:00Z",
      "expiresAt": "2024-07-15T10:30:00Z",
      "lastUsedAt": "2024-01-20T08:30:00Z"
    }
  ],
  "pagination": {
    "nextCursor": null,
    "hasMore": false,
    "totalCount": 1
  }
}
```

> 列表不回傳 `policyId`、`successorKeyId` 等詳細欄位，需查詢單一金鑰取得。

---

#### 3.2.3 GET /tenants/{tenantId}/keys/{keyId} — 查詢單一金鑰詳情

| 項目 | 值 |
|:-----|:---|
| Authorization | TenantAdmin, Consumer（限自身金鑰） |

**Response `200 OK`：**

```json
{
  "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "consumerId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "order-service-key",
  "keyPrefix": "acme_prod",
  "truncatedKey": "...a9B3",
  "environment": "Production",
  "scopes": ["orders:read", "orders:write"],
  "lifecycleStatus": "Rotating",
  "policyId": "d4e5f6a7-b8c9-0123-4567-890abcdef012",
  "createdAt": "2024-01-15T12:00:00Z",
  "expiresAt": "2024-07-15T10:30:00Z",
  "lastUsedAt": "2024-01-20T08:30:00Z",
  "successorKeyId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "predecessorKeyId": null,
  "graceDeadline": "2024-01-22T12:00:00Z"
}
```

> `successorKeyId`、`predecessorKeyId`、`graceDeadline` 僅在輪替相關狀態有值，其他狀態為 `null`。

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | 金鑰不存在或不屬於該 Tenant |

---

#### 3.2.4 POST /tenants/{tenantId}/keys/{keyId}/rotate — 輪替金鑰

| 項目 | 值 |
|:-----|:---|
| Authorization | TenantAdmin, Consumer（限自身金鑰） |
| Command | C2: RotateKey |

**Request Body：**

```json
{
  "gracePeriod": "PT24H"
}
```

| 欄位 | 類型 | 必填 | 說明 |
|:-----|:-----|:-----|:-----|
| `gracePeriod` | Duration | 否 | 新舊金鑰並行的寬限期，未提供則使用系統預設值 |

**Response `200 OK`：**

```json
{
  "oldKey": {
    "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "lifecycleStatus": "Rotating",
    "graceDeadline": "2024-01-22T12:00:00Z",
    "successorKeyId": "b2c3d4e5-f6a7-8901-bcde-f12345678901"
  },
  "newKey": {
    "keyId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
    "name": "order-service-key",
    "keyPrefix": "acme_prod",
    "truncatedKey": "...xK7m",
    "environment": "Production",
    "scopes": ["orders:read", "orders:write"],
    "lifecycleStatus": "Active",
    "policyId": "e5f6a7b8-c901-2345-6789-0abcdef12345",
    "createdAt": "2024-01-21T12:00:00Z",
    "expiresAt": "2024-07-15T10:30:00Z",
    "predecessorKeyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "rawKey": "acme_prod_q9w8e7r6t5y4u3i2o1p0a1s2d3f4g5h6_mN3x"
  }
}
```

> ⚠️ **Display Once**：`newKey.rawKey` 僅在此回應中出現一次。

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | 金鑰不存在 |
| INVALID_STATE_TRANSITION | 409 | 金鑰狀態非 Active |
| ROTATION_IN_PROGRESS | 409 | 同 Consumer + Environment 下已有 ROTATING 金鑰（INV-4） |
| KEY_ALREADY_EXPIRED | 409 | 金鑰已到期，無法輪替 |

---

#### 3.2.5 POST /tenants/{tenantId}/keys/{keyId}/suspend — 暫停金鑰

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin, TenantAdmin |
| Command | C5: SuspendKey |

**Request Body：**

```json
{
  "reason": "Suspicious activity under investigation"
}
```

| 欄位 | 類型 | 必填 | 說明 |
|:-----|:-----|:-----|:-----|
| `reason` | String | 是 | 暫停原因 |

**Response `200 OK`：**

```json
{
  "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "lifecycleStatus": "Suspended",
  "suspendedBy": "user-admin-001",
  "reason": "Suspicious activity under investigation"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | 金鑰不存在 |
| INVALID_STATE_TRANSITION | 409 | 金鑰狀態非 Active（INV-6） |

---

#### 3.2.6 POST /tenants/{tenantId}/keys/{keyId}/resume — 恢復金鑰

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin, TenantAdmin |
| Command | C6: ResumeKey |

**Response `200 OK`：**

```json
{
  "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "lifecycleStatus": "Active",
  "resumedBy": "user-admin-001"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | 金鑰不存在 |
| INVALID_STATE_TRANSITION | 409 | 金鑰狀態非 Suspended |

---

#### 3.2.7 POST /tenants/{tenantId}/keys/{keyId}/unlock — 解鎖金鑰

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin |
| Command | C4: UnlockKey |

> 僅 SecurityAdmin 可解鎖。LOCKED 狀態僅由系統（Monitoring）觸發，解鎖是人工確認安全後的操作。

**Response `200 OK`：**

```json
{
  "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "lifecycleStatus": "Active",
  "unlockedBy": "security-admin-001"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | 金鑰不存在 |
| INVALID_STATE_TRANSITION | 409 | 金鑰狀態非 Locked |

---

#### 3.2.8 POST /tenants/{tenantId}/keys/{keyId}/revoke — 撤銷金鑰

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin, TenantAdmin |
| Command | C7: RevokeKey |

**Request Body：**

```json
{
  "reason": "Key leaked in public repository"
}
```

| 欄位 | 類型 | 必填 | 說明 |
|:-----|:-----|:-----|:-----|
| `reason` | String | 是 | 撤銷原因（INV-7） |

**Response `200 OK`：**

```json
{
  "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "previousStatus": "Active",
  "lifecycleStatus": "Revoked",
  "revokedBy": "security-admin-001",
  "reason": "Key leaked in public repository"
}
```

> 撤銷可從 Active / Rotating / Locked / Suspended 任何非終態觸發（見狀態機 T4, T6, T9, T12）。

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | 金鑰不存在 |
| VALIDATION_ERROR | 400 | `reason` 為空（INV-7） |
| KEY_IN_TERMINAL_STATE | 409 | 金鑰已為 Expired 或 Revoked |

### 3.3 Access Policy

> **BC 對照**：[Access Policy Detailed Design](../detailed-design/access-policy.md)
> **整合關係**：I2（KL Partnership，同一交易建立）、I5（發布事件至 Audit）、I7（投影至 Validation Model）
>
> **不暴露的 Command**：C1 CreatePolicy（由 KL 交易內觸發 I2）
>
> Policy 透過 `keyId` 定位，反映 1:1 關係。Consumer 思考模式是「我這把金鑰的策略」而非「policyId XXX」。

---

#### 3.3.1 GET /tenants/{tenantId}/keys/{keyId}/policy — 查詢金鑰的存取策略

| 項目 | 值 |
|:-----|:---|
| Authorization | TenantAdmin, Consumer（限自身金鑰） |

**Response `200 OK`：**

```json
{
  "policyId": "d4e5f6a7-b8c9-0123-4567-890abcdef012",
  "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "ipAllowlist": [
    "192.168.1.0/24",
    "10.0.0.0/8"
  ],
  "rateLimitConfig": {
    "quotaLimit": 10000,
    "quotaPeriod": "PT1H",
    "rateLimit": 100,
    "burstLimit": 150
  }
}
```

> `ipAllowlist` 為空陣列表示不限制來源 IP（預設開放）。

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | 金鑰或策略不存在 |

---

#### 3.3.2 PUT /tenants/{tenantId}/keys/{keyId}/policy/ip-allowlist — 更新 IP 白名單

| 項目 | 值 |
|:-----|:---|
| Authorization | TenantAdmin, Consumer（限自身金鑰） |
| Command | C2: UpdateIpAllowlist |

**Request Body：**

```json
{
  "ipAllowlist": ["192.168.1.0/24", "10.0.0.0/8"]
}
```

| 欄位 | 類型 | 必填 | 說明 |
|:-----|:-----|:-----|:-----|
| `ipAllowlist` | String[] | 是 | CIDR 格式。空陣列 = 移除所有限制（回到預設開放） |

> Full replacement — 傳入的列表完整取代現有白名單。

**Response `200 OK`：**

```json
{
  "policyId": "d4e5f6a7-b8c9-0123-4567-890abcdef012",
  "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "ipAllowlist": ["192.168.1.0/24", "10.0.0.0/8"]
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | 金鑰或策略不存在 |
| VALIDATION_ERROR | 400 | CIDR 格式不合法 |
| WILDCARD_CIDR_FORBIDDEN | 422 | 包含 `0.0.0.0/0` 或 `::/0`（INV-3） |
| KEY_IN_TERMINAL_STATE | 409 | 關聯金鑰已為 Expired / Revoked（INV-2） |

---

#### 3.3.3 PUT /tenants/{tenantId}/keys/{keyId}/policy/rate-limit — 更新速率限制

| 項目 | 值 |
|:-----|:---|
| Authorization | TenantAdmin, Consumer（限自身金鑰） |
| Command | C3: UpdateRateLimit |

**Request Body：**

```json
{
  "quotaLimit": 50000,
  "quotaPeriod": "PT1H",
  "rateLimit": 200,
  "burstLimit": 300
}
```

| 欄位 | 類型 | 必填 | 說明 |
|:-----|:-----|:-----|:-----|
| `quotaLimit` | Integer | 是 | 週期內總請求次數上限，必須 > 0 |
| `quotaPeriod` | Duration | 是 | 配額週期，限 `PT1M` / `PT1H` / `P1D` / `P1M` |
| `rateLimit` | Integer | 是 | 瞬時 RPS 上限，必須 > 0 |
| `burstLimit` | Integer | 是 | 突發容忍量，必須 ≥ `rateLimit`（INV-4） |

> Full replacement — 所有欄位皆須提供。

**Response `200 OK`：**

```json
{
  "policyId": "d4e5f6a7-b8c9-0123-4567-890abcdef012",
  "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "rateLimitConfig": {
    "quotaLimit": 50000,
    "quotaPeriod": "PT1H",
    "rateLimit": 200,
    "burstLimit": 300
  }
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | 金鑰或策略不存在 |
| VALIDATION_ERROR | 400 | 欄位驗證失敗（quotaLimit ≤ 0、quotaPeriod 不在允許值等） |
| BURST_LIMIT_INVALID | 422 | `burstLimit` < `rateLimit`（INV-4） |
| KEY_IN_TERMINAL_STATE | 409 | 關聯金鑰已為 Expired / Revoked（INV-2） |

---

### 3.4 Monitoring & Detection

> **BC 對照**：[Monitoring & Detection Detailed Design](../detailed-design/monitoring-detection.md)
> **整合關係**：I4（訂閱 KL 事件）、I6（同步呼叫 KL LockKey）、I8（接收 Validation Layer 遙測）、I9（發布事件至 Audit）
>
> **混合作用域**：DetectionRule 是平台級 Global 資源（`/admin/`）；SecurityAlert 是 Tenant-scoped。

---

#### 3.4.1 POST /admin/detection-rules — 建立偵測規則

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin |
| Command | C1: CreateRule |

**Request Body：**

```json
{
  "name": "High Frequency Auth Failure",
  "condition": {
    "metric": "AUTH_FAILURE_COUNT",
    "window": "PT1M",
    "operator": "GT",
    "threshold": { "type": "static", "value": 50 }
  },
  "action": "Lock",
  "cooldown": "PT10M"
}
```

| 欄位 | 類型 | 必填 | 說明 |
|:-----|:-----|:-----|:-----|
| `name` | String | 是 | 規則名稱，不可重複（INV-1） |
| `condition.metric` | String | 是 | `AUTH_FAILURE_COUNT` / `AUTH_FAILURE_RATE` / `REQUEST_RATE` / `GEO_DISTANCE` |
| `condition.window` | Duration | 是 | 偵測時間窗口 |
| `condition.operator` | String | 是 | `GT` / `GTE` |
| `condition.threshold` | Object | 是 | 靜態 `{ "type": "static", "value": N }` 或基線 `{ "type": "baseline", "multiplier": N, "baseline": "P95" }` |
| `action` | String | 是 | `Lock` / `Notify` |
| `cooldown` | Duration | 是 | 冷却時間，防止重複觸發，必須 > 0 |

**Response `201 Created`：**

```json
{
  "ruleId": "f1e2d3c4-b5a6-7890-1234-567890abcdef",
  "name": "High Frequency Auth Failure",
  "condition": { "..." : "..." },
  "action": "Lock",
  "cooldown": "PT10M",
  "isActive": true,
  "createdAt": "2024-01-15T14:00:00Z"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| VALIDATION_ERROR | 400 | 欄位驗證失敗 |
| RULE_NAME_DUPLICATE | 409 | `name` 已存在（INV-1） |

---

#### 3.4.2 GET /admin/detection-rules — 查詢偵測規則列表

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin |

**Query Parameters：**

| 參數 | 類型 | 說明 |
|:-----|:-----|:-----|
| `isActive` | Boolean? | 篩選啟用/停用 |
| `pageSize` | Integer? | 分頁大小 |
| `cursor` | String? | 分頁游標 |

**Response `200 OK`：**

```json
{
  "data": [
    {
      "ruleId": "f1e2d3c4-b5a6-7890-1234-567890abcdef",
      "name": "High Frequency Auth Failure",
      "condition": { "metric": "AUTH_FAILURE_COUNT", "..." : "..." },
      "action": "Lock",
      "cooldown": "PT10M",
      "isActive": true,
      "createdAt": "2024-01-15T14:00:00Z"
    }
  ],
  "pagination": { "nextCursor": null, "hasMore": false, "totalCount": 1 }
}
```

---

#### 3.4.3 PUT /admin/detection-rules/{ruleId} — 更新偵測規則

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin |
| Command | C2: UpdateRule |

**Request Body：**

```json
{
  "condition": {
    "metric": "AUTH_FAILURE_COUNT",
    "window": "PT5M",
    "operator": "GT",
    "threshold": { "type": "static", "value": 100 }
  },
  "action": "Lock",
  "cooldown": "PT15M"
}
```

> Full replacement — 所有欄位皆須提供（`name` 不可變更）。

**Response `200 OK`：**

```json
{
  "ruleId": "f1e2d3c4-b5a6-7890-1234-567890abcdef",
  "name": "High Frequency Auth Failure",
  "condition": { "..." : "..." },
  "action": "Lock",
  "cooldown": "PT15M",
  "isActive": true
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | Rule 不存在 |
| VALIDATION_ERROR | 400 | condition 結構不合法、cooldown ≤ 0 |

---

#### 3.4.4 POST /admin/detection-rules/{ruleId}/toggle — 啟用/停用規則

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin |
| Command | C3: ToggleRule |

**Request Body：**

```json
{
  "isActive": false
}
```

**Response `200 OK`：**

```json
{
  "ruleId": "f1e2d3c4-b5a6-7890-1234-567890abcdef",
  "name": "High Frequency Auth Failure",
  "isActive": false
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | Rule 不存在 |

---

#### 3.4.5 GET /tenants/{tenantId}/alerts — 查詢安全警報列表

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin, TenantAdmin |

**Query Parameters：**

| 參數 | 類型 | 說明 |
|:-----|:-----|:-----|
| `status` | String? | `Open` / `Acknowledged` / `Resolved` |
| `severity` | String? | `Low` / `Medium` / `High` / `Critical` |
| `keyId` | UUID? | 篩選特定金鑰的警報 |
| `pageSize` | Integer? | 分頁大小 |
| `cursor` | String? | 分頁游標 |

**Response `200 OK`：**

```json
{
  "data": [
    {
      "alertId": "c1d2e3f4-a5b6-7890-cdef-1234567890ab",
      "tenantId": "550e8400-e29b-41d4-a716-446655440000",
      "ruleId": "f1e2d3c4-b5a6-7890-1234-567890abcdef",
      "ruleName": "High Frequency Auth Failure",
      "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "severity": "High",
      "status": "Open",
      "detectedAt": "2024-01-20T09:15:00Z",
      "details": {
        "metric": "AUTH_FAILURE_COUNT",
        "threshold": 50,
        "actual": 87,
        "window": "PT1M"
      }
    }
  ],
  "pagination": { "nextCursor": null, "hasMore": false, "totalCount": 1 }
}
```

---

#### 3.4.6 POST /tenants/{tenantId}/alerts/{alertId}/acknowledge — 確認警報

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin |
| Command | C4: AcknowledgeAlert |

**Response `200 OK`：**

```json
{
  "alertId": "c1d2e3f4-a5b6-7890-cdef-1234567890ab",
  "status": "Acknowledged",
  "acknowledgedBy": "security-admin-001",
  "acknowledgedAt": "2024-01-20T09:30:00Z"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | Alert 不存在 |
| INVALID_STATE_TRANSITION | 409 | status 非 Open（INV-2） |

---

#### 3.4.7 POST /tenants/{tenantId}/alerts/{alertId}/resolve — 解決警報

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin |
| Command | C5: ResolveAlert |

**Request Body：**

```json
{
  "resolution": "False positive - traffic spike from deployment"
}
```

| 欄位 | 類型 | 必填 | 說明 |
|:-----|:-----|:-----|:-----|
| `resolution` | String | 是 | 解決說明 |

**Response `200 OK`：**

```json
{
  "alertId": "c1d2e3f4-a5b6-7890-cdef-1234567890ab",
  "status": "Resolved",
  "resolvedBy": "security-admin-001",
  "resolution": "False positive - traffic spike from deployment",
  "resolvedAt": "2024-01-20T10:00:00Z"
}
```

**Errors：**

| errorCode | HTTP | 條件 |
|:----------|:-----|:-----|
| NOT_FOUND | 404 | Alert 不存在 |
| VALIDATION_ERROR | 400 | `resolution` 為空 |
| INVALID_STATE_TRANSITION | 409 | status 已為 Resolved（INV-2） |

### 3.5 Audit & Compliance

> **BC 對照**：[Audit & Compliance Detailed Design](../detailed-design/audit-compliance.md)
> **整合關係**：I3 + I5 + I9（訂閱所有 BC 的 Domain Events）
>
> Audit BC 無用戶命令介面，僅透過事件訂閱寫入。對外僅提供查詢。

---

#### 3.5.1 GET /tenants/{tenantId}/audit-logs — 搜尋審計日誌

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin, TenantAdmin |
| Query | SearchAuditLogs |

**Query Parameters：**

| 參數 | 類型 | 必填 | 說明 |
|:-----|:-----|:-----|:-----|
| `timeFrom` | Timestamp? | 否 | 起始時間 |
| `timeTo` | Timestamp? | 否 | 結束時間 |
| `actor` | String? | 否 | 執行者 ID |
| `action` | String? | 否 | 操作類型（`KEY_CREATED` / `KEY_REVOKED` / `POLICY_UPDATED` 等） |
| `resourceType` | String? | 否 | `ApiKey` / `AccessPolicy` / `SecurityAlert` |
| `resourceId` | UUID? | 否 | 特定資源 ID |
| `pageSize` | Integer? | 否 | 每頁筆數（上限 100） |
| `cursor` | String? | 否 | 分頁游標 |

**Response `200 OK`：**

```json
{
  "data": [
    {
      "eventId": "e1f2a3b4-c5d6-7890-ef12-34567890abcd",
      "tenantId": "550e8400-e29b-41d4-a716-446655440000",
      "actor": { "type": "User", "id": "user-admin-001", "name": "James Wang" },
      "action": "KEY_REVOKED",
      "resourceType": "ApiKey",
      "resourceId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "snapshotBefore": { "status": "Active" },
      "snapshotAfter": { "status": "Revoked" },
      "reason": "Key leaked in public repository",
      "context": {
        "sourceIp": "203.0.113.42",
        "userAgent": "Mozilla/5.0",
        "requestId": "req-abc-123"
      },
      "correlationId": "corr-xyz-789",
      "occurredAt": "2024-01-20T08:30:00Z"
    }
  ],
  "pagination": {
    "nextCursor": "eyJvY2N1cnJlZEF0IjoiMjAyNC0wMS0yMFQwODoyOTowMFoifQ==",
    "hasMore": true,
    "totalCount": 1250
  }
}
```

---

#### 3.5.2 POST /tenants/{tenantId}/audit-logs/export — 匹次匯出審計日誌

| 項目 | 值 |
|:-----|:---|
| Authorization | SecurityAdmin, TenantAdmin |
| Query | ExportAuditLogs |

**Request Body：**

```json
{
  "timeFrom": "2024-01-01T00:00:00Z",
  "timeTo": "2024-01-31T23:59:59Z",
  "format": "JSON"
}
```

| 欄位 | 類型 | 必填 | 說明 |
|:-----|:-----|:-----|:-----|
| `timeFrom` | Timestamp | 是 | 起始時間 |
| `timeTo` | Timestamp | 是 | 結束時間 |
| `format` | String | 是 | `JSON` 或 `CSV` |

**Response `202 Accepted`：**

```json
{
  "exportId": "exp-a1b2c3d4-e5f6-7890",
  "status": "Processing",
  "estimatedCompletionAt": "2024-01-21T10:05:00Z"
}
```

> 非同步任務，完成後透過通知系統提供下載連結。

---

## 4. Data Plane API（Internal）

> **用途**：Gateway / Sidecar / SDK 在每次 API 請求時呼叫，執行金鑰驗證與存取控制。
> **認證**：mTLS 或 Internal Service Token，不走 JWT。
> **效能要求**：此 API 的延遲直接影響所有業務 API 的回應時間。目標 p99 < 5ms（含快取命中）。
>
> **對照**：[Design Doc §4.7 Validation Read Model](./design-doc.md#47-validation-read-model)

---

### 4.1 POST /api/internal/v1/validate-key — 金鑰驗證

#### Request

Gateway 不只傳金鑰字串，還必須提供驗證上下文，以便系統執行 IP 白名單、Scope 權限檢查。

```json
{
  "apiKey": "acme_prod_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6_xY9z",
  "sourceIp": "203.0.113.42",
  "requestedScope": "orders:read",
  "requestId": "gw-req-abc-123"
}
```

| 欄位 | 類型 | 必填 | 說明 |
|:-----|:-----|:-----|:-----|
| `apiKey` | String | 是 | 完整金鑰字串 |
| `sourceIp` | String | 是 | 請求來源 IP（用於 IP 白名單檢查） |
| `requestedScope` | String | 是 | 目標 Endpoint 需要的 Scope（如 `orders:read`） |
| `requestId` | String | 否 | Gateway 請求追蹤 ID |

#### Response — 驗證成功 `200 OK`

不僅回答「是否合法」，還提供 Gateway 執行邊緣防禦所需的 Metadata。

```json
{
  "valid": true,
  "keyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "consumerId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "environment": "Production",
  "scopes": ["orders:read", "orders:write"],
  "rateLimitConfig": {
    "quotaLimit": 10000,
    "quotaPeriod": "PT1H",
    "rateLimit": 100,
    "burstLimit": 150
  }
}
```

| 欄位 | 說明 |
|:-----|:-----|
| `valid` | 金鑰是否通過所有驗證層 |
| `keyId` | Gateway 可用於日誌記錄、帳單計算 |
| `tenantId` / `consumerId` | Gateway 可注入至下游 Header（`X-Tenant-Id`、`X-Consumer-Id`） |
| `scopes` | Gateway 可執行細粒度 Endpoint 權限檢查 |
| `rateLimitConfig` | Gateway 執行限流決策 |

#### Response — 驗證失敗

回傳明確錯誤碼，讓 Gateway 知道該回 `401` 還是 `403`。

```json
{
  "valid": false,
  "errorCode": "KEY_REVOKED",
  "httpStatusHint": 401,
  "detail": "The API key has been revoked."
}
```

**驗證失敗錯誤碼：**

| errorCode | httpStatusHint | 對應驗證漏斗層 | 說明 |
|:----------|:---------------|:---------------------|:-----|
| KEY_FORMAT_INVALID | 401 | Layer 1: 格式檢查 | 前綴/長度/checksum 不合法 |
| KEY_NOT_FOUND | 401 | Layer 4: 雜湊驗證 | Hash 不匹配（不區分「不存在」與「錯誤」以防列舉） |
| KEY_INACTIVE | 401 | Layer 2: 狀態檢查 | 金鑰狀態非 Active 或 Rotating |
| KEY_EXPIRED | 401 | Layer 2: 狀態檢查 | 金鑰已過期 |
| KEY_REVOKED | 401 | Layer 2: 狀態檢查 | 金鑰已撤銷 |
| IP_NOT_ALLOWED | 403 | Layer 3: IP 檢查 | 來源 IP 不在白名單 |
| SCOPE_INSUFFICIENT | 403 | Layer 5: 權限檢查 | 金鑰 Scope 不涵蓋請求的 Endpoint |

> **安全設計**：401 代表「你是誰」辨識失敗（金鑰無效）；403 代表「你不能」（金鑰有效但權限不足）。
> Gateway 根據 `httpStatusHint` 直接回傳對應的 HTTP Status 給客戶端。

---

## 5. BC Map ↔ API 對照表

統整所有 Bounded Context 的 Command / Query 與 API Endpoint 的對應關係。

### 5.1 Tenant Management

| Command / Query | HTTP | Endpoint | 外部暴露 |
|:----------------|:-----|:---------|:---------|
| C1: CreateTenant | POST | /admin/tenants | ✅ |
| C2: SuspendTenant | POST | /admin/tenants/{tenantId}/suspend | ✅ |
| C3: ReactivateTenant | POST | /admin/tenants/{tenantId}/reactivate | ✅ |
| C4: RegisterConsumer | POST | /tenants/{tenantId}/consumers | ✅ |
| C5: UpdateConsumer | PUT | /tenants/{tenantId}/consumers/{consumerId} | ✅ |
| Q: GetTenant | GET | /admin/tenants/{tenantId} | ✅ |
| Q: ListConsumers | GET | /tenants/{tenantId}/consumers | ✅ |
| Q: GetConsumer | GET | /tenants/{tenantId}/consumers/{consumerId} | ✅ |
| Q: ValidateConsumer | — | — | ❌ 內部介面（I1，KL 同步查詢） |

### 5.2 Key Lifecycle

| Command / Query | HTTP | Endpoint | 外部暴露 |
|:----------------|:-----|:---------|:---------|
| C1: CreateApiKey | POST | /tenants/{tenantId}/consumers/{consumerId}/keys | ✅ |
| C2: RotateKey | POST | /tenants/{tenantId}/keys/{keyId}/rotate | ✅ |
| C3: LockKey | — | — | ❌ 內部介面（I6，Monitoring 同步呼叫） |
| C4: UnlockKey | POST | /tenants/{tenantId}/keys/{keyId}/unlock | ✅ |
| C5: SuspendKey | POST | /tenants/{tenantId}/keys/{keyId}/suspend | ✅ |
| C6: ResumeKey | POST | /tenants/{tenantId}/keys/{keyId}/resume | ✅ |
| C7: RevokeKey | POST | /tenants/{tenantId}/keys/{keyId}/revoke | ✅ |
| C8: ExpireKey | — | — | ❌ System Agent Job |
| C9: CompleteGracePeriod | — | — | ❌ System Agent Job |
| Q: GetKey | GET | /tenants/{tenantId}/keys/{keyId} | ✅ |
| Q: ListKeysByConsumer | GET | /tenants/{tenantId}/consumers/{consumerId}/keys | ✅ |

### 5.3 Access Policy

| Command / Query | HTTP | Endpoint | 外部暴露 |
|:----------------|:-----|:---------|:---------|
| C1: CreatePolicy | — | — | ❌ 內部介面（I2，KL 交易內觸發） |
| C2: UpdateIpAllowlist | PUT | /tenants/{tenantId}/keys/{keyId}/policy/ip-allowlist | ✅ |
| C3: UpdateRateLimit | PUT | /tenants/{tenantId}/keys/{keyId}/policy/rate-limit | ✅ |
| Q: GetPolicy | GET | /tenants/{tenantId}/keys/{keyId}/policy | ✅ |

### 5.4 Monitoring & Detection

| Command / Query | HTTP | Endpoint | 外部暴露 |
|:----------------|:-----|:---------|:---------|
| C1: CreateRule | POST | /admin/detection-rules | ✅ |
| C2: UpdateRule | PUT | /admin/detection-rules/{ruleId} | ✅ |
| C3: ToggleRule | POST | /admin/detection-rules/{ruleId}/toggle | ✅ |
| C4: AcknowledgeAlert | POST | /tenants/{tenantId}/alerts/{alertId}/acknowledge | ✅ |
| C5: ResolveAlert | POST | /tenants/{tenantId}/alerts/{alertId}/resolve | ✅ |
| Q: ListRules | GET | /admin/detection-rules | ✅ |
| Q: ListAlerts | GET | /tenants/{tenantId}/alerts | ✅ |

### 5.5 Audit & Compliance

| Command / Query | HTTP | Endpoint | 外部暴露 |
|:----------------|:-----|:---------|:---------|
| 事件消費（I3 + I5 + I9） | — | — | ❌ 內部事件訂閱 |
| Q: SearchAuditLogs | GET | /tenants/{tenantId}/audit-logs | ✅ |
| Q: ExportAuditLogs | POST | /tenants/{tenantId}/audit-logs/export | ✅ |

### 5.6 Data Plane

| 操作 | HTTP | Endpoint | 用途 |
|:-----|:-----|:---------|:-----|
| ValidateKey | POST | /api/internal/v1/validate-key | Gateway / Sidecar 金鑰驗證 |

---

## 6. Endpoint 統計

| 分類 | Endpoint 數 |
|:-----|:-----------|
| Tenant Management | 8 |
| Key Lifecycle | 8 |
| Access Policy | 3 |
| Monitoring & Detection | 7 |
| Audit & Compliance | 2 |
| Data Plane (Internal) | 1 |
| **總計** | **29** |
