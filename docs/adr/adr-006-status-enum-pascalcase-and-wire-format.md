# 狀態 enum 採 PascalCase + JsonStringEnumConverter 統一 wire format

> 本 ADR 終結 `ApiKeyStatus` 等狀態 enum 的 `ALL_CAPS` 命名與 `api-spec.md` PascalCase wire format 之間的不一致。

---

## Status

Accepted (2026-05-02)

---

## Context

### 現況衝突

`docs/design/api-spec.md` §3.2.1 success body example：

```json
{
  "lifecycleStatus": "Active",
  ...
}
```

§3.2.2 query parameter：

```
?status=Active|Rotating|Locked|Suspended|Expired|Revoked
```

但生產 `backend/src/KeyLifecycle/Domain/ApiKeyStatus.cs`：

```csharp
public enum ApiKeyStatus
{
    ACTIVE,
    LOCKED,
    SUSPENDED,
    REVOKED,
    ROTATING,
    EXPIRED,
}
```

`CreateApiKeyHandler.cs:75` 序列化方式：

```csharp
LifecycleStatus: apiKey.Status.ToString()
```

`enum.ToString()` 預設輸出 enum member 名稱原文，所以 wire 上會送 `"ACTIVE"` 而非 spec 規定的 `"Active"`。

### 問題嚴重度

- **API contract drift**：client 收到 `"ACTIVE"`，但 OpenAPI / 文件 / SDK 都說會是 `"Active"`。任何嚴格 schema 比對的 client 會 fail。
- **.NET 命名違規**：enum member 應該 PascalCase（`ApiKeyStatus.Active`），ALL_CAPS 是 C 與 Java 早期慣例，違反 .NET Framework Design Guidelines。
- **未來查詢 endpoint 也會踩到**：`GET /tenants/{tenantId}/consumers/{consumerId}/keys?status=Active` 進來，Minimal API 的 enum parameter binding 會 fail（因為 enum member 是 ALL_CAPS）。BDD scenarios 02–06 還沒實作，所以還沒爆，但一定會。
- **EF Core column conversion**：`HasConversion<string>()` 把 enum 轉成 DB 欄位字串。enum member rename 後，新插入的 row 自然會是 `"Active"`。本系統尚未部署，DB 內僅有 testcontainer 動態產生的測試資料，無需 data migration。

---

## Decision

### 1. enum member 改 PascalCase

```csharp
public enum ApiKeyStatus
{
    Active,
    Locked,
    Suspended,
    Revoked,
    Rotating,
    Expired,
}
```

### 2. JSON 序列化用 `JsonStringEnumConverter` 對齊 PascalCase

在 `Program.cs` 或 endpoint group 上設定：

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false));
});
```

預設 `JsonStringEnumConverter` 會用 enum member 名稱原文，因此 PascalCase enum member → wire format `"Active"`。對齊 `api-spec.md`。

`allowIntegerValues: false` 的目的：禁止 client 在 request body 用 numeric enum value（例如 `0`），強制 wire 上只接受 string literal，避免 public API 偷偷支援兩種輸入。

### 2a. Query string parameter binding 屬另一條路徑

`JsonStringEnumConverter` 只影響 **JSON body / response** 序列化，**不影響** Minimal API 的 query string binding。`?status=Active` 的解析走 ASP.NET Core 的 `TryParse` / model binder，由 enum 自身的 `Enum.TryParse` 行為處理。

本專案的 wire 承諾：

- 只承諾 `?status=Active`（PascalCase 字面值）作為合法輸入。
- `?status=ACTIVE` 不作為相容輸入；`?status=Bogus` 等非法值同等處理。
- 實際 binding 行為（case-sensitivity、解析失敗的 HTTP status）以 functional test 鎖定，不在 ADR 中對 ASP.NET Core 實作細節下保證。

未來實作 list endpoint 時的驗收項目：

- functional test 驗證 `?status=Active` 可正確 bind 到 `ApiKeyStatus.Active`。
- functional test 驗證非合法值（如 `?status=ACTIVE`、`?status=Bogus`）回傳 `400 BadRequest` 而非 500。

### 3. EF Core 欄位轉換沿用 `HasConversion<string>()`，無需 data migration

`ApiKeyConfiguration.cs` 已用：

```csharp
builder.Property(x => x.Status).HasConversion<string>();
```

enum member 改名後，新插入的 row 自然會是 `"Active"`。

**不需 data migration**：本系統尚未部署到任何環境，DB 內僅有 testcontainer 在每次 functional test 啟動時動態建立的資料，重啟即清空。production 上線後若再做類似 enum rename，再單獨開 ADR 處理 data migration 設計（含 quoted identifier、Up/Down 對稱、rollout 策略）。

驗收標準：

- `dotnet ef migrations add` 不應產生新 schema migration（schema 沒變，只有 enum 字串值變）。若 EF 偵測到 schema diff，需理解原因；通常為 false positive，可在 Up/Down 留空或刪除該 migration。
- 所有 integration / functional test 在 testcontainer 環境跑 Green。

### 4. Handler / Endpoint 不再呼叫 `.ToString()`，交由 serializer

`CreateApiKeyResponse.LifecycleStatus` 改成 `ApiKeyStatus` 而不是 `string`：

```csharp
public record CreateApiKeyResponse(
    Guid KeyId,
    ...
    ApiKeyStatus LifecycleStatus,  // 不是 string
    ...
);
```

Handler 直接傳 enum value：

```csharp
return new CreateApiKeyResponse(
    ...
    LifecycleStatus: apiKey.Status,
    ...
);
```

`JsonStringEnumConverter` 接管序列化。

### 5. Functional test 必須鎖定 raw JSON wire literal，不能僅比 enum value

問題：若把 `CreateApiKeyResponse.LifecycleStatus` 改成 `ApiKeyStatus`，再用 `JsonSerializer.Deserialize<CreateApiKeyResponse>` 還原成 enum 比較，這只驗證了「enum 來回 round-trip 成功」，**並未驗證 wire 上實際輸出的字串是 `"Active"`**。API contract test 必須鎖定 raw JSON。

實作要求：

1. **Feature / step wording**：把 `backend/tests/FunctionalTests/Features/KeyLifecycle/*.feature` 全部 feature 檔中的 `ACTIVE / ROTATING / SUSPENDED / REVOKED / EXPIRED / LOCKED` 等狀態字面值同步改為 PascalCase，包括：
   - `01_CreateApiKey.feature`（已知 :8、:13、:39 等行有 `ACTIVE`）
   - `02_RevokeKey.feature`
   - `03_SuspendResumeKey.feature`
   - `04_LockUnlockKey.feature`
   - `05_RotateKey.feature`
   - `06_ExpireKey.feature`
   - 對應 step regex（如 `[Then(@"金鑰狀態為 ACTIVE")]` → `[Then(@"金鑰狀態為 Active")]`）必須一起改，否則 binding 會 broken。
2. **Step 實作 assertion**：用 `JsonDocument` 或 raw string contains 直接驗證 JSON property，例如：

   ```csharp
   using var doc = JsonDocument.Parse(_ctx.ResponseBody!);
   doc.RootElement.GetProperty("lifecycleStatus").GetString().Should().Be("Active");
   ```

   不可只 deserialize 成 `CreateApiKeyResponse` 然後比 `body.LifecycleStatus.Should().Be(ApiKeyStatus.Active)`。
3. **DTO 改 enum 之後**，若仍想保留 strongly-typed assertion，可額外比較，但 raw JSON literal assertion 必須存在且作為主要驗證。
4. `tasks/todo.md` 若有「wire-format 表格 intentional 字面值」的記錄，需同步把 `ACTIVE` 改成 `Active`。

### 6. Reference / design / BDD 文件全面同步 PascalCase

採取 Option A（全 repo 統一）：code、DB、wire、docs 四處的狀態字面值一致，杜絕 drift。

**.claude/references（agent 學習材料 — 必改）**：

- `.claude/references/dotnet/di.rule.md:55`：`ApiKeyStatus.ACTIVE` → `ApiKeyStatus.Active`
- `.claude/references/dotnet/exceptions.rule.md:196`：`ApiKeyStatus.ACTIVE` → `ApiKeyStatus.Active`
- `naming.guide.md` §B「Enum PascalCase (singular)」已正確，僅需確認無 ALL_CAPS 範例。

**design / detailed-design / bdd 文件（必改）**：

- `docs/design/prd.md`
- `docs/design/design-doc.md`
- `docs/design/api-spec.md`（已大致 PascalCase，補完殘留）
- `docs/design/context-integration-spec.md`
- `docs/detailed-design/key-lifecycle.md`
- `docs/detailed-design/tenant-management.md`
- `docs/detailed-design/access-policy.md`
- `docs/detailed-design/audit-compliance.md`
- `docs/detailed-design/monitoring-detection.md`
- `docs/bdd/key-lifecycle.md`
- `docs/bdd/tenant-management.md`
- `docs/bdd/access-policy.md`
- `docs/bdd/audit-compliance.md`

把 `ACTIVE / ROTATING / SUSPENDED / REVOKED / EXPIRED / LOCKED` 等狀態字面值改為 `Active / Rotating / Suspended / Revoked / Expired / Locked`。

**驗收 grep**（acceptance commit 前必須歸零；優先用 `rg`，避開 BSD/GNU grep 的 `\b` / `\|` 平台差異）：

```bash
# 1. C# enum reference 必須全 PascalCase
#    pattern 要求第二字也是大寫，避免誤抓 PascalCase 的首字 A（如 Active）
git --no-pager grep -n -E 'ApiKeyStatus\.[A-Z][A-Z_]+\b' \
  -- backend .claude/references .claude/skills
# 預期 0 命中

# 2. wire / docs / feature / step 字面值必須全 PascalCase
git --no-pager grep -n -E '\b(ACTIVE|ROTATING|SUSPENDED|REVOKED|EXPIRED|LOCKED)\b' \
  -- docs \
     backend/tests/FunctionalTests/Features \
     backend/tests/FunctionalTests/Steps \
     .claude/references \
     .claude/skills \
     ':!docs/adr/adr-006-status-enum-pascalcase-and-wire-format.md'
# 預期 0 命中
```

選 `git grep` 而非 `rg` 是因為 `git grep` 隨 git 一起安裝、跨平台一致，無外部依賴；
`-E` 啟用 ERE，避免 BSD/GNU `grep` 對 `\|` / `\b` 的行為差異。

**例外**：本 ADR 自身為了示範前後對比保留了 ALL_CAPS 字串，屬合理例外，已在驗收 grep 第 2 條以 git pathspec `:!docs/adr/adr-006-status-enum-pascalcase-and-wire-format.md` 排除。

---

## Rationale

### 為什麼 enum 改 PascalCase 而不是改 spec

- **.NET Framework Design Guidelines** 明文：enum 用 PascalCase。`ALL_CAPS` 是極舊風格、違反主流 .NET。
- **`naming.guide.md` 自己就規定 PascalCase enum**，當前 enum member 違反自己的規範。
- **`api-spec.md` 已是 PascalCase**。改 spec 反而要重簽 RFC、改 OpenAPI、改 SDK；而生產還沒對外暴露完整實作，現在改 enum 成本最低。

### 為什麼用 `JsonStringEnumConverter` 而不是手寫轉換

- 標準函式庫，零 maintenance。
- 自動處理 JSON body 雙向反序列化（request body 字串 → enum）。
- 與 OpenAPI generator 相容，schema 自動產生 `"enum": ["Active", "Locked", ...]`。

注意：`JsonStringEnumConverter` 的作用範圍是 JSON body / response，**不**負責 query string parameter binding。query string `?status=Active` 由 Minimal API model binder 與 enum `TryParse` 處理，與本 converter 無關。enum member 改為 PascalCase 後，default case-sensitive parsing 會直接 match `Active`，無需額外 converter。

### 為什麼 DB 欄位也要改

統一原則：DB / wire / code 三處的 enum 字面值必須一致。如果 DB 存 `"ACTIVE"`、wire 送 `"Active"`，未來新增 admin 查詢 endpoint 帶 `?status=Active` 會 round-trip 失敗，不符合直覺。

實作面：本系統尚未部署，DB 內僅有 testcontainer 動態資料，無 production data migration 成本。若未來部署後再做類似 enum rename，需另開 ADR 處理。

---

## Consequences

### Positive

- API contract（OpenAPI、API spec、實作）三方一致。
- 符合 .NET Framework Design Guidelines。
- 未來 query endpoint 帶 `?status=Active` 的 reverse parsing 自然 work。
- Schema-strict client 與 SDK generator 不再卡住。

### Negative / Trade-offs

- **既有測試 hardcode `"ACTIVE"` 會 red**。
  - Mitigation: 本 ADR 第 5 條明文列出 `.feature` 與 step `assertion` 的修正點。`grep` 驗收一次掃乾淨。
- **跨 BC / 多文件協同改動**：reference rule files、design docs、BDD docs 共 14 處需同步更新。
  - Mitigation: 第 6 條列出全清單與驗收 grep；acceptance commit 前必須歸零。
- **rotating wave 兼容性**：若未來新增服務（Gateway、SIEM、Audit consumer）試圖 consume 舊 `"ACTIVE"` 字串，需同步更新。
  - Mitigation: 目前無其他服務實際 consume 此欄位；Audit / Monitoring BC 都還是空殼。本 ADR 應在那兩個 BC 實作前完成。

---

## Alternatives Considered

### Alternative A: 維持 enum `ALL_CAPS`，在序列化時轉 PascalCase

```csharp
options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
```

或自己寫 converter。

Rejected. 兩端不一致是長期維護負擔；C# 端讀到 `ApiKeyStatus.ACTIVE` 永遠都會困惑「為什麼跟 wire 對不上」。也違反 enum PascalCase 命名標準。

### Alternative B: 改 `api-spec.md` 為 ALL_CAPS

Rejected。理由不是「PascalCase 是 REST 標準」（業界沒有單一標準，public APIs 同時存在 lowercase、snake_case、SCREAMING_SNAKE_CASE 與 PascalCase 多種風格），而是：

1. 本專案 `api-spec.md` 已明確承諾 PascalCase；改 spec 是對外承諾的破壞。
2. C# enum member 應符合 .NET PascalCase 命名標準。
3. 兩端對齊到任一風格都可以，但既然 spec 已選 PascalCase 並符合 .NET 慣例，code 端跟齊比改 spec 便宜。

### Alternative C: 用 `[JsonStringEnumMemberName]` per-member 自訂 wire 字串

```csharp
public enum ApiKeyStatus
{
    [JsonStringEnumMemberName("Active")]
    ACTIVE,
    ...
}
```

Rejected. 每個 member 都要標註、容易漏；且 enum member 自己違反 .NET 命名標準的問題沒解決。

---

## Implementation Rules

1. `ApiKeyStatus` enum member 採 PascalCase（`Active`、`Locked`、`Suspended`、`Revoked`、`Rotating`、`Expired`）。
2. 任何新增的 status / lifecycle / type enum 都採 PascalCase。
3. JSON 序列化全域使用 `JsonStringEnumConverter(allowIntegerValues: false)`，無 naming policy override；禁止 wire 接受 numeric enum value。
4. EF Core 欄位用 `HasConversion<string>()` 自動處理 DB 端字串。
5. DTO 欄位類型用 enum 而非 `string`，由 serializer 統一處理。
6. Functional test 必須以 raw JSON literal（如 `JsonDocument` + `GetString().Should().Be("Active")`）驗證 wire-format 字串；不可僅以 enum value 比較替代。
7. Query string enum binding 由 Minimal API model binder 與 `Enum.TryParse` 處理，與 `JsonStringEnumConverter` 無關；list endpoint 實作時必須加 `?status=Active` 與 `?status=Bogus` 的 functional test。
8. 本系統尚未部署，本次 enum rename 不需 data migration；未來如再有類似 enum rename 且涉及已部署環境，必須另開 ADR 並含 quoted identifier 的 Up/Down 對稱 migration 設計。
9. enum 命名與 wire 字面值的同步範圍包含：`backend/src/`、`backend/tests/FunctionalTests/Features/`、`backend/tests/FunctionalTests/Steps/`、`.claude/references/`、`docs/design/`、`docs/detailed-design/`、`docs/bdd/`。acceptance commit 前依 §6 的 `rg` 驗收指令必須 0 命中（C# enum reference 與 wire 字面值兩條 grep 都要過）。
10. 任何提案修改 1–9，必須先開新 ADR。
