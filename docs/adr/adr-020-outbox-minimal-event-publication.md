# 事件發佈最小落地：Transactional Outbox（同交易收割 Domain Events），Relay 後置

> Lead-in：Domain Event 至今只存在 aggregate 記憶體、隨 GC 丟棄（Stryker A2 調查證實無任何發佈管道），而 RevokeKey 場景的 Then（KeyRevoked 事件、觸發主動快取失效）需要事件真正可觀測。本 ADR 依 `docs/design/context-integration-spec.md` 既定的 Outbox 方向，固化最小落地範圍：outbox 表＋同交易收割，RabbitMQ relay 明文後置。

---

## Status

Accepted (2026-07-05)

同步項目：無 reference docs / CLAUDE.md 需改；`tasks/archive/stryker-baseline-2026-07-05.md` A2 條目的閉環驗證（KeyCreated 斷言＋mutant 轉 killed）在後續 test-only commit 落地，不在本 ADR 的同 commit 義務內。

---

## Context

### 現況

三份文件與程式碼的矛盾並排：

- `docs/design/context-integration-spec.md`「建立金鑰交易」明文：「Outbox: 寫入 KeyCreated + PolicyCreated 事件」；§3 定義統一事件信封（eventId / eventType / aggregateId / occurredAt / version / correlationId / causationId / payload）；§4.7 I7 投影表規定 KeyRevoked 需「主動快取失效」。
- 實際程式碼：`SharedKernel/Domain/AggregateRoot.cs` 的 `_domainEvents` 只存記憶體；所有 EntityTypeConfiguration 以 `builder.Ignore(...DomainEvents)` 排除；`AppDbContext` 無 SaveChanges override、無 interceptor；migration 僅五張表、無 outbox；`ClearDomainEvents()` 無人呼叫 — 事件加入後隨 aggregate 被 GC 丟棄。
- 測試現況：`CreateApiKeySteps.cs` 的「系統產生 KeyCreated 事件」Then 實際只斷言 HTTP response body（Stryker 基線 A2 已正名此為名不符實；`ApiKey.cs` 的 `AddDomainEvent(new KeyCreated(...))` 整行刪除測試照綠）。

`02_RevokeKey.feature`「從 Active 狀態撤銷」的 Then「系統產生 KeyRevoked 事件」「觸發主動快取失效」在此現況下無法誠實實作。

### 不決定會發生什麼

- RevokeKey 場景被迫重演 response-body 代理斷言 — 明知名不符實而再犯，A2 正名作廢。
- 每個後續生命週期場景（Rotate / Lock / Suspend…全數含事件 Then）各自即興發明事件觀測法，drift 面擴大。

---

## Decision

### 1. 新增 `OutboxMessages` 表，信封欄位對齊 integration spec §3

```csharp
public class OutboxMessage
{
    public Guid EventId { get; init; }               // 發布端產生，消費端去重
    public string EventType { get; init; }           // 事件 record 型別名，如 "KeyRevoked"
    public string AggregateId { get; init; }         // 產生事件的 Aggregate ID（字串化）
    public DateTimeOffset OccurredAt { get; init; }
    public string Payload { get; init; }             // System.Text.Json camelCase 序列化的事件 record
    public DateTimeOffset CreatedAt { get; init; }   // 落表時間
    public DateTimeOffset? ProcessedAt { get; set; } // Relay 完成時間；本 ADR 階段恆為 null
}
```

`version` / `correlationId` / `causationId` 三欄**本階段不建**：它們服務消費端因果鏈與去重進階需求，目前零消費端，建了只會累積無人校驗的欄位；Relay ADR（見 §3）落地時依 integration spec §3 補齊 schema migration。

### 2. `AppDbContext.SaveChangesAsync` override 同交易收割

SharedKernel 新增非泛型介面 `IHasDomainEvents`（`DomainEvents` + `ClearDomainEvents()`），`AggregateRoot<TId>` 實作之。`AppDbContext` override `SaveChangesAsync`：從 ChangeTracker 收集所有 `IHasDomainEvents` 實體的事件 → 逐一映射為 `OutboxMessage` 加入 DbSet → `ClearDomainEvents()` → 呼叫 base。事件與業務資料**同一交易**落庫 — 不存在「狀態改了、事件丟了」的窗口。

```csharp
// after（示意）：
public override async Task<int> SaveChangesAsync(CancellationToken cancel = default)
{
    CollectDomainEventsIntoOutbox();
    return await base.SaveChangesAsync(cancel);
}
```

### 3. Relay（RabbitMQ 發佈器）明文後置 — 不在本 ADR 範圍

背景發佈器、重試、毒訊息處理、`ProcessedAt` 生命週期，一律待首個真實消費端 BC（Validation Model 或 Audit）落地時另開 ADR。本階段 outbox 是事件的**權威落點與觀測點**，不是佇列的暫存區。

### 4. BDD 事件斷言的觀測契約

事件類 Then（「系統產生 X 事件」「觸發主動快取失效」）一律斷言 outbox row：`EventType`、`AggregateId`、payload 關鍵欄位（如 `previousStatus`）。「觸發主動快取失效」的斷言語意 = 對應事件已入 outbox，並在 step 註解引用 integration spec §4.7 I7 投影表（KeyRevoked → 主動失效「是」）說明為何事件即觸發器。

---

## Rationale

### 為什麼選 SaveChanges override 而不是 EF Interceptor

單一 DbContext 的 repo，override 是最短路徑、無額外註冊面；Interceptor 的價值（跨多 context 復用、可插拔）在本 repo 無對應需求。兩者交易語意等價。

### 為什麼 Relay 後置

目前零消費端：發到 RabbitMQ 的訊息無人接、無法端到端斷言正確性，只會產生「看起來完成了」的假信號。Outbox 表本身已滿足（a）事件不丟（同交易）、（b）測試可觀測、（c）未來 relay 只讀 outbox 不動業務碼三個目標。

### 為什麼不補 version/correlation/causation 欄

零消費端時這些欄位無校驗者，先建 = 投機性 schema。Relay ADR 補齊時有 migration 機制承接，成本不會變高。

---

## Consequences

### Positive

- 事件類 Then 從 response-body 代理升級為真發佈斷言；A2（`ApiKey.cs` 的 `AddDomainEvent` 刪除存活）可閉環。
- 後續生命週期場景（Rotate / Lock / Suspend）的事件 Then 有統一觀測契約，不再逐場景即興。
- KeyCreated 自動獲得同管道發佈（收割是通用機制），CreateApiKey 場景的事件斷言可補真。

### Negative / Trade-offs

- Outbox 表只進不出（`ProcessedAt` 恆 null），資料無限增長。
  - Mitigation: 測試環境每場景重建 DB 無此問題；production 清理策略屬 Relay ADR 範圍，該 ADR 落地前本表僅測試與稽核用途。
- `SaveChangesAsync` override 對所有 aggregate 生效，未來事件量大時序列化成本進熱路徑。
  - Mitigation: 生命週期操作皆非 hotpath（hotpath 是 validation 查詢，ADR-017 已界定）；validation slice 落地時的效能 smoke（checkpoint 既定義務）會覆蓋此點。
- 信封欄位暫缺 version/correlation/causation，與 integration spec §3 不完全一致。
  - Mitigation: 本 ADR §1 明文記錄缺口與補齊時點（Relay ADR），非默默偏離。

---

## Alternatives Considered

### Alternative A: 同步直接發佈 RabbitMQ（無 outbox）

Rejected. 雙寫問題（DB commit 成功、publish 失敗 → 事件丟失）正是 outbox pattern 要解的；且目前零消費端，發了無人接。

### Alternative B: EF SaveChanges Interceptor 收割

Rejected. 與 override 交易語意等價，但多一層 DI 註冊面與間接性；單 DbContext repo 無復用需求，違反最小機制原則。

### Alternative C: 維持 response-body 代理斷言，事件基礎設施整包延後

Rejected. A2 調查已正名該模式名不符實；RevokeKey 場景的 Then 明文要求事件與快取失效，代理斷言 = 明知故犯，且 `@ignore` 單移紀律下場景 Then 不可選擇性實作。

### Alternative D: 引入 MassTransit / MediatR 全套事件管線

Rejected. 依賴預算與制度凍結啟發式：新依賴需事故驅動；目前需求（同交易落表＋測試觀測）用 EF 原生能力即滿足。

---

## Implementation Rules

1. `OutboxMessages` 表欄位 = §1 清單，不多不少；`Payload` 為 System.Text.Json camelCase 序列化之事件 record。
2. 收割在 `AppDbContext.SaveChangesAsync` override 內完成：收集 → 映射 → `ClearDomainEvents()` → base；不得在 Handler / Repository 手動寫 outbox。
3. `SharedKernel` 的 `IHasDomainEvents` 為唯一收割介面；新 aggregate 繼承 `AggregateRoot<TId>` 即自動入列，無需註冊。
4. 事件類 BDD Then 斷言 outbox row（EventType + AggregateId + payload 關鍵欄位），禁止以 response body 代理；「主動快取失效」類 Then 依 §4 附 I7 引用註解。
5. Relay、`ProcessedAt` 生命週期、version/correlation/causation 欄位，屬未來 Relay ADR — 在該 ADR 前，任何向訊息broker 發佈 outbox 內容的程式碼不得進入 `backend/src/`。
6. **驗收**：

   ```bash
   git --no-pager grep -n -E 'RabbitMQ|IConnection|BasicPublish' -- backend/src/ ':!*.csproj'
   # 預期 0 命中（relay 未落地前 src 端不得接 broker）
   git --no-pager grep -rn 'ClearDomainEvents()' -- backend/src/
   # 預期恰在 AppDbContext 收割路徑命中（不再是 0 呼叫）
   ```

7. 任何提案修改 1–6，必須先開新 ADR。
