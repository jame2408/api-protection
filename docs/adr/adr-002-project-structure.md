# 專案結構（Project Structure）

> 本 ADR 記錄 API Key Management System 的程式碼組織決策：採用 **Screaming Architecture + Vertical Slice + Modular Monolith** 三組合，並固化 BC 邊界、共用專案、測試組織等具體形狀。

---

## Status

Accepted (2026-04-03)

---

## Context

設計階段已決定以 Bounded Context 為主軸切分系統（PRD / Detailed Design），但程式碼層面仍需決定：

- 頂層資料夾要按 BC 切（領域中心）還是按技術分層切（Controllers / Services / Repositories）。
- 每個 use case 的相關檔案（Command / Handler / Validator / Endpoint）要集中放還是分散到技術分層。
- 多 BC 是分多個 deployable unit、還是合一個 modular monolith。
- BC 之間禁止互相直接參照，但同步呼叫（I1 / I2 / I6）的介面該放哪裡。
- BDD 測試該按 BC 拆 assembly，還是依場景組織。

未統一前，新 BC 容易自行創造組織方式，導致 hardening pass 必須回頭整理。

---

## Decision

### 1. 三組合：Screaming Architecture + Vertical Slice + Modular Monolith

- **Screaming Architecture**：頂層資料夾以 BC 命名（`KeyLifecycle/`、`AccessPolicy/`、`Monitoring/`、`Audit/`、`TenantManagement/`），不以 Controllers / Services / Repositories 等技術分層命名。
- **Vertical Slice**：每個 use case（Command / Query）是一個獨立 slice，集中該操作所需的所有檔案（Command、Handler、Validator、Endpoint、Handler interface）。slice 之間互不依賴。
- **Modular Monolith**：所有 BC 共存於一個 deployable unit。同步呼叫（I1 / I2 / I6）走進程內方法，非同步事件（I3 / I4 / I5 / I9）走 MassTransit + RabbitMQ。

### 2. 不使用 MediatR

Handler 透過介面抽象直接注入呼叫，不透過 MediatR dispatch：

- 每個 Handler 實作獨立介面（如 `ICreateApiKeyHandler`）。
- Endpoint 透過 DI 取得 Handler，直接呼叫。
- 驗證邏輯透過 FluentValidation 在 Handler 內執行，或透過 Endpoint Filter 統一處理。
- 跨切面（logging、transaction）透過 Decorator Pattern 或 middleware 處理。

### 3. Solution 結構

```
backend/ApiKeyManagement.slnx

src/
├── SharedKernel/                 # 共用基礎型別 + 跨 BC contract 介面
├── KeyLifecycle/                 # Key Lifecycle BC
├── AccessPolicy/                 # Access Policy BC
├── Monitoring/                   # Monitoring & Detection BC
├── Audit/                        # Audit & Compliance BC
├── TenantManagement/             # Tenant Management BC
├── Infrastructure/               # 共用基礎設施（DbContext + EF 全集中設定）
│   └── Persistence/
│       ├── AppDbContext.cs
│       └── Configurations/       # 所有 BC 的 IEntityTypeConfiguration<T> 集中於此
└── Host/                         # API Host（純接線）

tests/
├── FunctionalTests/              # BDD e2e（場景垂直切片，跨 BC）
├── TestInfrastructure/           # 共用 WebApplicationFactory
└── Architecture.Tests/            # 架構守衛測試
```

### 4. BC 內部結構（以 KeyLifecycle 為例）

```
src/KeyLifecycle/
├── Domain/                       # 領域模型（跨 slice 共享）
│   ├── ApiKey.cs                 # Aggregate Root
│   ├── Events/                   # KeyCreated / KeyRevoked / ...
│   └── ValueObjects/             # KeyPrefix / KeyHash / Scope / Environment
├── CreateApiKey/                 # Vertical Slice — C1
│   ├── CreateApiKeyCommand.cs
│   ├── CreateApiKeyHandler.cs
│   ├── CreateApiKeyValidator.cs
│   ├── CreateApiKeyEndpoint.cs
│   └── ICreateApiKeyHandler.cs
├── RotateKey/ LockKey/ ...       # 其他 slices
├── Queries/                      # 查詢 slices
└── KeyLifecycleModule.cs         # 模組註冊（Handler DI + Endpoint mapping）
```

> EF Core entity configuration **不放在 BC 資料夾**。所有 `IEntityTypeConfiguration<T>` 集中於 `Infrastructure/Persistence/Configurations/`，由 `AppDbContext.OnModelCreating` 透過 `ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)` 一次掃描註冊。理由與 dependency direction 詳見 §5。

每個 BC 提供 `{BC}Module.cs`，負責：

1. 註冊該 BC 的所有 Handler（介面 → 實作的 DI 綁定）。
2. 註冊該 BC 的 MassTransit consumers（若有）。
3. 提供 `Map{BC}Endpoints()` 擴充方法註冊 Minimal API routes。

### 5. 專案相依方向

```
⛔ BC 之間不直接參照

KeyLifecycle      ──→ SharedKernel
AccessPolicy     ──→ SharedKernel
Monitoring        ──→ SharedKernel
Audit             ──→ SharedKernel
TenantManagement  ──→ SharedKernel

Infrastructure    ──→ SharedKernel
Infrastructure    ──→ KeyLifecycle / AccessPolicy / Monitoring / Audit / TenantManagement
                       （AppDbContext 必須看到各 BC Aggregate 型別才能 ApplyConfigurationsFromAssembly）

Host              ──→ 所有 BC + Infrastructure（純接線）
```

依賴方向重點：

- BC 只依賴 SharedKernel，**不依賴** Infrastructure；BC 不需要也不該知道 EF Core / DbContext。
- Infrastructure 依賴 BC 與 SharedKernel，因為 EF entity configuration 集中在 Infrastructure 並需 reference BC Aggregate 型別。這個方向確保 Domain 層不被 persistence 細節污染。
- BC 之間不互相 csproj reference；同步呼叫透過 SharedKernel contract 介面解耦。

同步查詢的介面放在 `SharedKernel/Contracts/`，由實作 BC 實作、Host 接線：

```
src/SharedKernel/Contracts/
├── IConsumerValidator.cs         # I1: TM 實作，KL 使用
├── IAccessPolicyService.cs       # I2: AP 實作，KL 使用
└── IKeyLockService.cs            # I6: KL 實作，MD 使用
```

### 6. 測試組織：場景垂直切片，不依 BC 分 assembly

BDD 測試集中在單一 `FunctionalTests` assembly，feature 檔以資料夾分 BC，step definitions 共享：

```
tests/FunctionalTests/
├── Features/
│   ├── KeyLifecycle/             # feature 檔按 BC 資料夾組織
│   ├── AccessPolicy/
│   ├── TenantManagement/
│   ├── Monitoring/
│   └── Audit/
├── Steps/                        # Step Definitions（按 feature 或共用）
├── Infrastructure/               # Reqnroll lifecycle + 共用 Given/When/Then 狀態
└── reqnroll.json
```

### 7. Architecture Tests 守衛架構約束

```
tests/Architecture.Tests/
├── BcBoundaryTests.cs            # BC 之間不直接參照
├── DomainPurityTests.cs          # Domain 不依賴 Infrastructure
├── SliceIsolationTests.cs        # Slice 之間不互相引用
└── NamingConventionTests.cs      # 命名慣例檢查
```

使用 NetArchTest 或 ArchUnitNET 實作。

---

## Rationale

### 為何 Screaming Architecture

頂層資料夾以 BC 命名，新人 / AI agent 一打開 `src/` 就知道系統的領域語言（金鑰生命週期、存取政策、監控偵測…），而不是看到 `Controllers / Services / Repositories` 後仍需逐層探索才知道系統做什麼。Robert C. Martin 的「架構應該尖叫出系統的意圖」即此意。

### 為何 Vertical Slice

每個 use case 集中放置，修改一個 use case 不會散到 5 個資料夾。slice 之間互不依賴，新增 / 刪除 use case 是純加減法，不會牽動其他 slice。當 slice 數量變多時，組織壓力被吸收在「BC 內部資料夾」而非「跨技術分層的散布」。

### 為何 Modular Monolith 而非 Microservices

100 RPS 規模、單一團隊開發，microservices 的網路 / 部署 / 觀測成本不對等。Modular Monolith 在程式層面已用 BC 邊界 + SharedKernel contract 切清楚，未來真要拆分，每個 BC 已是獨立 csproj，物理拆分成本可控。

### 為何不使用 MediatR

MediatR 的 dispatch 在 Vertical Slice 架構中價值有限：每個 slice 的 Handler 只有一個呼叫點（Endpoint），介面抽象 + DI 直接注入即可達成可測試性與解耦。引入 MediatR 反而增加：

- runtime dispatch 的隱性連結，IDE「Find usages」失效。
- 額外的 Pipeline Behavior 抽象，與 Decorator / Middleware 重疊。
- 對新人 / AI agent 多一層心智負擔。

### 為何 BC 不直接參照、由 SharedKernel 提供 contract

- BC 之間互相參照會把「同步呼叫」從 contract 升級為「程式碼 dependency」，未來想物理拆分時必須先解開所有相互引用。
- 介面放在 SharedKernel，BC 只依賴抽象、Host 負責接線；新增 BC 不必修改既有 BC。
- Architecture Tests 可機械化驗證「BC 之間零引用」這條規則。

### 為何測試不依 BC 分 assembly

- 一個 BDD 場景的 Given/When/Then 經常橫跨 TenantManagement、KeyLifecycle、AccessPolicy 多個 BC。
- 分 assembly 導致 step definitions 無法共享、容器重複啟動，顯著拖慢測試速度。
- BDD 的單位是**場景**，不是 BC。assembly 切分應對齊測試生命週期，不對齊領域邊界。

---

## Consequences

### Positive

- 領域語言在程式碼結構就能讀出，新人上手成本降低。
- Vertical Slice 讓 use case 的新增 / 修改範圍可預測。
- BC 邊界由 Architecture Tests 鎖死，不會隨開發 drift。
- 單一 deployable unit 簡化 dev / staging / prod 的部署 pipeline。
- BDD 測試共享容器與 step definitions，執行成本可控。

### Negative / Trade-offs

- Modular Monolith 在團隊規模成長後可能成為瓶頸（單一 build / deploy 影響半徑大）。
  - Mitigation: 每個 BC 已是獨立 csproj，未來物理拆分成本受限於資料庫與 messaging，程式層改動可控。
- 不使用 MediatR 意味需自己處理 cross-cutting（logging / transaction），略增初期工作。
  - Mitigation: Decorator Pattern + Middleware 已涵蓋常見場景；範例放在 `.claude/references/`。
- 測試集中在單一 assembly，scenario 一多 build 時間與 test discovery 成本上升。
  - Mitigation: Reqnroll 的 feature → tag 過濾、xUnit 平行執行可緩解；若真成瓶頸，未來再依 BC 拆分。

---

## Alternatives Considered

### Alternative A: 技術分層架構（Controllers / Services / Repositories 為頂層）

Rejected. 領域意圖被技術分層遮蔽；新人需逐層 trace 才能拼出系統做什麼。Vertical Slice 在這種架構下難以實現。

### Alternative B: Microservices（每個 BC 獨立部署）

Rejected for now. 100 RPS 量級不需要 microservices 的隔離；網路 / 觀測 / 部署成本不對等。Modular Monolith 已保留物理拆分的能力。

### Alternative C: 使用 MediatR

Rejected. 在 Vertical Slice 架構中，MediatR 的 dispatch 抽象成本高於收益（見 Rationale）。介面 + DI 即可。

### Alternative D: 測試依 BC 分 assembly

Rejected. 場景跨 BC，分 assembly 會切壞 step definitions 共享與容器生命週期，顯著拖慢執行（見 Rationale）。

### Alternative E: Domain 不放在 BC 資料夾、改放共用 Domain 專案

Rejected. Aggregate / ValueObject 是 BC 內部知識，外部不需要也不應該看到；放到共用 Domain 會誘導跨 BC 引用，違背 Modular Monolith 的隔離意圖。

---

## Implementation Rules

1. 頂層 `src/` 資料夾以 BC 命名；技術分層字眼（Controllers / Services / Repositories）不出現在頂層。
2. 每個 BC 內部以 use case slice 組織（`CreateApiKey/` / `RotateKey/` / ...），每個 slice 集中放置 Command / Handler / Validator / Endpoint / Handler interface。
3. BC 之間不直接 csproj 參照；同步呼叫的介面放在 `SharedKernel/Contracts/`，由實作 BC 實作、Host 負責接線。
4. 不使用 MediatR；Handler 透過介面 + DI 直接注入。Cross-cutting 走 Decorator 或 Middleware。
5. 每個 BC 提供 `{BC}Module.cs`，承擔 Handler DI 註冊、MassTransit consumers、`Map{BC}Endpoints()`。EF Core entity configuration **不**由 BC Module 註冊 — 集中於 `Infrastructure/Persistence/Configurations/`，由 `AppDbContext.ApplyConfigurationsFromAssembly` 掃描自身組件。
6. BDD 測試集中在單一 `FunctionalTests` assembly；feature 檔按 BC 資料夾組織，step definitions 共享。
7. Architecture Tests 必須包含：BC 邊界、Domain 純度、Slice 隔離、命名慣例四項守衛。
8. 任何提案修改 1–7，必須先開新 ADR。
