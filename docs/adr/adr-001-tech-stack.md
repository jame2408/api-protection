# 技術選型（Technology Stack）

> 本文件記錄 API Key Management System 的技術選型決策。
> 設計文件（Step 1-5）為技術無關，本文件是進入實作前的技術對應。

---

## 1. 核心技術堆疊

| 元件 | 技術 | 版本 | 說明 |
|:-----|:-----|:-----|:-----|
| Backend Framework | .NET Web API | 10 | 主要服務端框架 |
| Frontend | Nuxt.js + TypeScript | 4 | 管理介面 |
| Database | PostgreSQL + EF Core (Npgsql) | — | 主要持久化層 |
| Message Broker | RabbitMQ + MassTransit | — | BC 間非同步事件通訊 |
| Cache | Redis | — | API Key 驗證快取 + Monitoring 指標聚合 |
| BDD Test Framework | Reqnroll + xUnit | — | 執行 Step 5 的 121 個 Gherkin Scenario |
| Deployment | Kubernetes | — | 容器化部署，支援地端與雲端 |

---

## 2. 選型理由

### 2.1 RabbitMQ + MassTransit

**問題：** Integration Spec 定義了 9 條整合關係（I1-I9），其中 I3, I4, I5, I9 為非同步 Pub-Sub，需要 Outbox Pattern 確保事件可靠發布。

**決策：** RabbitMQ 作為 Message Broker，MassTransit 作為 .NET 端的 messaging 抽象層。

**理由：**

- 100 RPS 的量級，RabbitMQ 足夠且不 overkill（相比 Kafka）
- MassTransit 提供 EF Core Outbox，domain event 和 aggregate 在同一個 transaction 內寫入
- MassTransit 內建 Consumer 重試 + DLQ，對應 Integration Spec §7.4 的失敗處理策略
- K8s 上部署簡單（Helm chart / Operator）

### 2.2 Redis

**問題：** 兩個不同用途需要分散式快取：

1. Validation Layer 的 API Key 驗證資料快取（熱路徑、低延遲）
2. Monitoring BC 的 Detection Engine 需要 per-key 滑動窗口指標聚合

**決策：** 統一使用 Redis。

**理由：**

- 多 Pod 環境需要分散式快取
- Redis Sorted Set 可實現滑動窗口，100 RPS 量級不需要額外的時序資料庫
- 主動快取失效（KeyRevoked 觸發）可透過 Redis Pub/Sub 或直接 DEL

### 2.3 EF Core + Npgsql

**問題：** 需要 ORM 處理 Aggregate 持久化，且需與 MassTransit Outbox 整合。

**決策：** EF Core + Npgsql。

**理由：**

- .NET 標準 ORM，團隊熟悉
- 與 MassTransit Outbox 無縫整合（共用 DbContext + Transaction）
- Audit BC 的 Append-Only 語意可透過 EF Core 攔截器 + PostgreSQL 權限控制實現

### 2.4 Reqnroll + xUnit

**問題：** Step 5 產出 121 個 Gherkin Scenario，需要 BDD 框架將其轉為可執行測試。

**決策：** Reqnroll（SpecFlow 的 .NET 8+ 繼任者）+ xUnit。

**理由：**

- 直接消費 Step 5 的 `.feature` 文件，設計產出不浪費
- xUnit 是 .NET 主流測試框架
- Reqnroll 支援 .NET 10

---

## 3. 設計概念 → 技術對應

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

---

## 4. 程式碼規範

.NET 撰寫規範參考 `.claude/references` 內的指引文件：

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

實作時應遵循上述規範，確保程式碼風格一致。

---

## 5. 效能目標

| 指標 | 目標值 | 說明 |
|:-----|:-------|:-----|
| API Key 驗證 throughput | ≥ 100 RPS | Validation Layer 熱路徑 |
| 驗證延遲 P99 | < 50ms | Redis 快取命中時 |
| 事件投遞延遲 | < 5s | Outbox polling interval |
