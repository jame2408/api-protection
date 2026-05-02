# 技術選型（Technology Stack）

> 本 ADR 記錄 API Key Management System 的技術選型決策。設計文件（Step 1–5）為技術無關，本 ADR 是進入實作前的技術對應，並把選型固化為可被後續 ADR 引用的基準。

---

## Status

Accepted (2026-04-03)

---

## Context

設計文件（PRD / RFC / Detailed Design / BDD Scenarios / Integration Spec）刻意保持技術無關，但實作前必須把概念對應到具體技術，否則：

- 各 BC 自行選技術會導致部署模型分裂（例如有的用 RabbitMQ、有的用 Kafka），共用基礎設施成本翻倍。
- 未綁定的「概念 → 技術」對應，會讓未來新人 / AI agent 無從判斷該不該引入新依賴（例如新增時序資料庫處理 sliding window）。
- 跨 BC 整合（Outbox Pattern、I3/I4/I5/I9 Pub-Sub）需要單一 messaging 抽象層，否則 Outbox 與 Consumer 的契約會分歧。

需要在進入實作前，一次定下核心技術堆疊。

---

## Decision

### 1. 核心技術堆疊

| 元件 | 技術 | 版本 | 說明 |
|:-----|:-----|:-----|:-----|
| Backend Framework | .NET Web API | 10 | 主要服務端框架 |
| Frontend | Nuxt.js + TypeScript | 4 | 管理介面 |
| Database | PostgreSQL + EF Core (Npgsql) | — | 主要持久化層 |
| Message Broker | RabbitMQ + MassTransit | — | BC 間非同步事件通訊 |
| Cache | Redis | — | API Key 驗證快取 + Monitoring 指標聚合 |
| BDD Test Framework | Reqnroll + xUnit | — | 執行 Step 5 的 121 個 Gherkin Scenario |
| Deployment | Kubernetes | — | 容器化部署，支援地端與雲端 |

### 2. 設計概念 → 技術對應

| 設計概念 | 技術實現 |
|:---------|:---------|
| Outbox Pattern（Integration Spec §7.1） | MassTransit EF Core Outbox |
| Pub-Sub 事件（I3, I4, I5, I9） | MassTransit + RabbitMQ Exchange |
| 同步查詢（I1, I2, I6） | 進程內方法呼叫（同一 deployable unit）或 HTTP API |
| API Key 驗證快取 | Redis Hash（keyPrefix → 驗證資料） |
| 主動快取失效 | MassTransit Consumer 訂閱 KeyRevoked → Redis DEL |
| Sliding Window 指標 | Redis Sorted Set（score = timestamp） |
| WORM 審計儲存（INV-1） | PostgreSQL + REVOKE UPDATE/DELETE + EF Core 攔截器 |
| BDD Scenario 執行 | Reqnroll `.feature` 文件 + Step Definitions |
| 冪等 Consumer（Audit INV-2） | MassTransit Idempotent Consumer + eventId 唯一約束 |

### 3. 程式碼規範來源

.NET 撰寫規範參考 `.claude/references/`：

**dotnet/**

| 文件 | 涵蓋範圍 |
|:-----|:---------|
| `naming.guide.md` | 命名慣例 |
| `async.rule.md` | 非同步程式設計規則 |
| `di.rule.md` | 依賴注入規則 |
| `ef-core.rule.md` | EF Core 使用規則 |
| `exceptions.rule.md` | 例外處理規則 |
| `linq.rule.md` | LINQ 使用規則 |
| `security.rule.md` | 安全性規則 |
| `testing.guide.md` | 測試撰寫指引 |

**general/**

| 文件 | 涵蓋範圍 |
|:-----|:---------|
| `solid.rule.md` | SOLID 原則 |

### 4. 效能目標

| 指標 | 目標值 | 說明 |
|:-----|:-------|:-----|
| API Key 驗證 throughput | ≥ 100 RPS | Validation Layer 熱路徑 |
| 驗證延遲 P99 | < 50ms | Redis 快取命中時 |
| 事件投遞延遲 | < 5s | Outbox polling interval |

---

## Rationale

### RabbitMQ + MassTransit

100 RPS 量級，RabbitMQ 足夠且不 overkill（相比 Kafka）。MassTransit 提供 EF Core Outbox，讓 domain event 與 aggregate 在同一 transaction 寫入，並內建 Consumer 重試 + DLQ，對應 Integration Spec §7.4 的失敗處理策略。K8s Helm chart / Operator 部署成熟。

### Redis（單一快取技術）

兩個用途（驗證快取、Monitoring sliding window）都要分散式快取。Redis Sorted Set 即可實現滑動窗口，在 100 RPS 量級不需另引時序資料庫。主動快取失效可走 Pub/Sub 或直接 DEL。引入第二種快取技術成本不划算。

### EF Core + Npgsql

.NET 標準 ORM、團隊熟悉。與 MassTransit Outbox 共用 DbContext + Transaction 無縫整合。Audit BC 的 Append-Only 語意可透過 EF Core 攔截器 + PostgreSQL 權限控制實現。

### Reqnroll + xUnit

直接消費 Step 5 的 `.feature` 文件，設計產出不浪費。xUnit 為 .NET 主流測試框架；Reqnroll 是 SpecFlow 的 .NET 8+ 繼任者，支援 .NET 10。

---

## Consequences

### Positive

- 概念 → 技術一對一對應，新 BC 不必各自決定基礎設施。
- Outbox / Pub-Sub / 快取失效路徑統一，跨 BC 整合契約清楚。
- 效能目標可被 functional / load test 直接驗收。

### Negative / Trade-offs

- 綁定 .NET 10 + Nuxt 4 等較新版本，CI / 雲端 base image 需追上。
  - Mitigation: K8s + container 抽象，base image 升級不影響應用程式碼。
- Redis 同時承擔驗證快取與 sliding window，單點負載較重。
  - Mitigation: 兩種用途的 key namespace 隔離；100 RPS 量級下 Redis 單實例足夠，未來可水平拆。
- MassTransit 的 abstraction 偶爾遮蔽 RabbitMQ 細節，debug 需要額外熟悉度。
  - Mitigation: `.claude/references/` 的 messaging 範例與 Outbox migration 流程文件化。

---

## Alternatives Considered

### Alternative A: Kafka 取代 RabbitMQ

Rejected. Kafka 在 100 RPS 量級是過度工程；replay / partition 等優勢在本專案無對應需求，但運維成本（Zookeeper / KRaft、broker 調校、磁碟容量）顯著高於 RabbitMQ。

### Alternative B: 引入時序資料庫處理 sliding window

Rejected. Redis Sorted Set 在 100 RPS 與 per-key window 規模下足夠；多引一種儲存意味多一條備份 / 監控 / 升級路徑，收益不對等。

### Alternative C: Dapper 取代 EF Core

Rejected. EF Core 與 MassTransit Outbox 的整合是首要考量；Dapper 雖效能微優但需自行處理 unit of work 與 outbox transaction，違背「最少基礎設施」原則。

### Alternative D: NUnit / MSTest 取代 xUnit

Rejected. xUnit 是 .NET 社群目前最主流選擇，與 Reqnroll 整合範例最完整。

---

## Implementation Rules

1. 新 BC / 新 use case 必須使用 §1 列出的核心技術；引入新技術需先開新 ADR。
2. BC 間非同步事件必須走 MassTransit + Outbox，不得直接呼叫 RabbitMQ client API。
3. 分散式快取一律使用 Redis，不引入第二種快取技術。
4. ORM 一律使用 EF Core；read model 若需要 raw SQL，仍透過 EF Core `FromSql` 或 `ExecuteSqlRaw`。
5. BDD `.feature` 與 step definition 一律走 Reqnroll + xUnit。
6. 程式碼風格遵循 §3 列出的 `.claude/references/` 規範，不另立 BC-local 風格。
7. 效能目標（§4）為驗收條件；hotpath 變更必須附 load test 結果。
8. 任何提案修改 1–7，必須先開新 ADR。
