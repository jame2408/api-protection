# 專案結構設計（Project Structure）

> 本文件定義 API Key Management System 的程式碼組織方式。
> 架構風格：**Screaming Architecture + Vertical Slice**，搭配 Modular Monolith 部署模型。

---

## 1. 架構原則

### 1.1 Screaming Architecture

頂層資料夾以 Bounded Context 命名，一看就知道系統做什麼，而非看到 Controllers / Services / Repositories 等技術分層。

### 1.2 Vertical Slice

每個 use case（Command / Query）是一個獨立的 slice，包含該操作所需的所有程式碼。slice 之間互不依賴，修改一個 use case 不影響其他 slice。

### 1.3 Modular Monolith

所有 BC 共存於一個 deployable unit。BC 間的同步呼叫（I1, I2, I6）為進程內方法呼叫，非同步事件（I3, I4, I5, I9）走 MassTransit + RabbitMQ。未來可依需求拆分為獨立服務。

### 1.4 不使用 MediatR

Handler 透過介面抽象直接注入呼叫，不透過 MediatR dispatch。保持簡單，同時確保可測試性與可讀性：

- 每個 Handler 實作獨立介面（如 `ICreateApiKeyHandler`）
- Endpoint 透過 DI 取得 Handler，直接呼叫
- 驗證邏輯透過 FluentValidation 在 Handler 內執行，或透過 Endpoint Filter 統一處理
- 跨切面（logging、transaction）透過 Decorator Pattern 或 middleware 處理

---

## 2. Solution 結構

```
ApiKeyManagement.sln

src/
├── SharedKernel/                              # 共用基礎型別
│   └── ApiKeyManagement.SharedKernel.csproj
│
├── KeyLifecycle/                              # Key Lifecycle BC
│   └── ApiKeyManagement.KeyLifecycle.csproj
│
├── AccessPolicy/                              # Access Policy BC
│   └── ApiKeyManagement.AccessPolicy.csproj
│
├── Monitoring/                                # Monitoring & Detection BC
│   └── ApiKeyManagement.Monitoring.csproj
│
├── Audit/                                     # Audit & Compliance BC
│   └── ApiKeyManagement.Audit.csproj
│
├── TenantManagement/                          # Tenant Management BC
│   └── ApiKeyManagement.TenantManagement.csproj
│
├── Infrastructure/                            # 共用基礎設施
│   └── ApiKeyManagement.Infrastructure.csproj
│
└── Host/                                      # API Host（純接線）
    └── ApiKeyManagement.Api.csproj

tests/
├── FunctionalTests/                           # BDD e2e 測試（場景垂直切片，跨 BC）
│   └── ApiKeyManagement.FunctionalTests.csproj
│
├── TestInfrastructure/                        # 共用 WebApplicationFactory
│   └── ApiKeyManagement.TestInfrastructure.csproj
│
└── Architecture.Tests/                        # 架構守衛測試
    └── ApiKeyManagement.Architecture.Tests.csproj
```

---

## 3. BC 內部結構 — Vertical Slice

以 Key Lifecycle 為例：

```
src/KeyLifecycle/
│
├── Domain/                                    # 領域模型（跨 slice 共享）
│   ├── ApiKey.cs                              # Aggregate Root
│   ├── ApiKeyFactory.cs                       # Factory
│   ├── ScopeRegistry.cs                       # Reference Data
│   ├── Events/
│   │   ├── KeyCreated.cs
│   │   ├── KeyRotationInitiated.cs
│   │   ├── KeyLocked.cs
│   │   ├── KeyUnlocked.cs
│   │   ├── KeySuspended.cs
│   │   ├── KeyResumed.cs
│   │   ├── KeyRevoked.cs
│   │   ├── KeyExpired.cs
│   │   └── KeyGracePeriodExpired.cs
│   └── ValueObjects/
│       ├── KeyPrefix.cs
│       ├── KeyHash.cs
│       ├── Scope.cs
│       └── Environment.cs
│
├── CreateApiKey/                              # Vertical Slice — C1
│   ├── CreateApiKeyCommand.cs                 # Input DTO
│   ├── CreateApiKeyHandler.cs                 # 業務邏輯
│   ├── CreateApiKeyValidator.cs               # 輸入驗證
│   ├── CreateApiKeyEndpoint.cs                # Minimal API endpoint
│   └── ICreateApiKeyHandler.cs                # Handler 介面（DI + 可測試）
│
├── RotateKey/                                 # Vertical Slice — C2
│   ├── RotateKeyCommand.cs
│   ├── RotateKeyHandler.cs
│   ├── RotateKeyValidator.cs
│   ├── RotateKeyEndpoint.cs
│   └── IRotateKeyHandler.cs
│
├── LockKey/                                   # C3（System 觸發，無 Endpoint）
│   ├── LockKeyCommand.cs
│   ├── LockKeyHandler.cs
│   └── ILockKeyHandler.cs
│
├── UnlockKey/                                 # C4
├── SuspendKey/                                # C5
├── ResumeKey/                                 # C6
├── RevokeKey/                                 # C7
│
├── ExpireKey/                                 # C8（System Agent，無 Endpoint）
│   ├── ExpireKeyCommand.cs
│   ├── ExpireKeyHandler.cs
│   └── ExpireKeyJob.cs                        # 定時掃描 Job
│
├── CompleteGracePeriod/                       # C9（System Agent，無 Endpoint）
│   ├── CompleteGracePeriodCommand.cs
│   ├── CompleteGracePeriodHandler.cs
│   └── CompleteGracePeriodJob.cs
│
├── Queries/                                   # 查詢 slice
│   ├── GetKeyStatus/
│   │   ├── GetKeyStatusQuery.cs
│   │   └── GetKeyStatusHandler.cs
│   └── FindKeysByPrefix/
│       ├── FindKeysByPrefixQuery.cs
│       └── FindKeysByPrefixHandler.cs
│
├── Infrastructure/
│   └── ApiKeyConfiguration.cs                 # EF Core entity mapping
│
└── KeyLifecycleModule.cs                      # 模組註冊（DI + Endpoint mapping）
```

### 3.1 其他 BC 的 Slice 對照

**Access Policy**

```
src/AccessPolicy/
├── Domain/
│   ├── AccessPolicy.cs
│   ├── Events/
│   └── ValueObjects/    (CidrRange, RateLimitConfig)
├── CreatePolicy/                              # C1（System 觸發，無 Endpoint）
├── UpdateIpAllowlist/                         # C2
├── UpdateRateLimit/                           # C3
├── Infrastructure/
└── AccessPolicyModule.cs
```

**Monitoring & Detection**

```
src/Monitoring/
├── Domain/
│   ├── DetectionRule.cs
│   ├── SecurityAlert.cs
│   ├── UsageBaseline.cs
│   └── ValueObjects/    (RuleCondition, RuleAction, Severity)
├── CreateRule/                                # C1
├── UpdateRule/                                # C2
├── ToggleRule/                                # C3
├── AcknowledgeAlert/                          # C4
├── ResolveAlert/                              # C5
├── DetectionEngine/                           # 核心偵測流程（非 CRUD slice）
│   ├── MetricsAggregator.cs
│   ├── RuleEvaluator.cs
│   └── DetectionEngineService.cs
├── EventHandlers/                             # I4 事件消費
│   ├── OnKeyCreatedHandler.cs
│   └── OnKeyRevokedHandler.cs
├── Infrastructure/
└── MonitoringModule.cs
```

**Audit & Compliance**

```
src/Audit/
├── Domain/
│   ├── AuditEntry.cs
│   └── ValueObjects/    (AuditAction, EventContext)
├── EventHandlers/                             # I3 + I5 + I9 事件消費
│   ├── EventToAuditEntryMapper.cs
│   └── DomainEventAuditHandler.cs
├── SearchAuditLogs/                           # Query slice
├── ExportAuditLogs/                           # Query slice
├── Infrastructure/
└── AuditModule.cs
```

**Tenant Management**

```
src/TenantManagement/
├── Domain/
│   ├── Tenant.cs
│   └── Consumer.cs
├── CreateTenant/                              # C1
├── SuspendTenant/                             # C2
├── ReactivateTenant/                          # C3
├── RegisterConsumer/                          # C4
├── UpdateConsumer/                            # C5
├── Queries/
│   └── ValidateConsumer/                      # I1 查詢
├── Infrastructure/
└── TenantManagementModule.cs
```

---

## 4. 共用專案

### 4.1 SharedKernel

```
src/SharedKernel/
├── Domain/
│   ├── Entity.cs                              # 基礎 Entity（Id + Equality）
│   ├── AggregateRoot.cs                       # Aggregate 基類（Domain Events 收集）
│   ├── ValueObject.cs                         # Value Object 基類（Structural Equality）
│   └── IDomainEvent.cs                        # Domain Event 介面
├── Results/
│   ├── Result.cs                              # Result Pattern（Success / Failure）
│   └── Error.cs                               # 錯誤型別
└── Events/
    └── EventEnvelope.cs                       # 事件信封（eventId, tenantId, occurredAt...）
```

### 4.2 Infrastructure

```
src/Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs                        # 共用 DbContext
│   └── Migrations/
├── Messaging/
│   ├── MassTransitConfiguration.cs            # MassTransit + RabbitMQ 設定
│   └── OutboxConfiguration.cs                 # Outbox 設定
├── Caching/
│   └── RedisConfiguration.cs
└── InfrastructureModule.cs                    # 基礎設施 DI 註冊
```

### 4.3 Host

```
src/Host/
├── Program.cs                                 # 應用程式入口 — 純接線
├── appsettings.json
├── appsettings.Development.json
└── Dockerfile
```

`Program.cs` 只做模組註冊，不含業務邏輯：

```csharp
// 概念示意，非最終程式碼
builder.Services.AddSharedKernel();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKeyLifecycleModule();
builder.Services.AddAccessPolicyModule();
builder.Services.AddMonitoringModule();
builder.Services.AddAuditModule();
builder.Services.AddTenantManagementModule();

var app = builder.Build();

app.MapKeyLifecycleEndpoints();
app.MapAccessPolicyEndpoints();
app.MapMonitoringEndpoints();
app.MapAuditEndpoints();
app.MapTenantManagementEndpoints();
```

---

## 5. 測試結構 — 場景垂直切片

BDD 測試不依 BC 分 assembly，而是以**場景垂直切片**組織：一個場景可能橫跨多個 BC，step definitions 需要共享。

```
tests/FunctionalTests/
├── Features/
│   ├── KeyLifecycle/                          # feature 檔按 BC 資料夾組織
│   │   ├── CreateApiKey.feature
│   │   ├── RotateKey.feature
│   │   ├── LockUnlockKey.feature
│   │   ├── SuspendResumeKey.feature
│   │   ├── RevokeKey.feature
│   │   └── ExpireKey.feature
│   ├── AccessPolicy/
│   ├── TenantManagement/
│   ├── Monitoring/
│   └── Audit/
│
├── Steps/                                     # Step Definitions（按 feature 或共用）
│   └── CreateApiKeySteps.cs
│
├── Infrastructure/
│   ├── TestHooks.cs                           # Reqnroll lifecycle（一組 container）
│   └── FunctionalTestContext.cs               # 共用 Given/When/Then 狀態
│
└── reqnroll.json
```

### 5.1 為什麼不用 BC-based test assembly

- 一個場景的 Given/When/Then 可能橫跨 TenantManagement、KeyLifecycle、AccessPolicy 多個 BC
- 分 assembly 導致 step definitions 無法共享
- 分 assembly 導致容器重複啟動，顯著拖慢測試速度
- BDD 的單位是**場景（scenario）**，不是 BC

---

## 6. 專案相依關係

```
Host
 ├── KeyLifecycle
 ├── AccessPolicy
 ├── Monitoring
 ├── Audit
 ├── TenantManagement
 └── Infrastructure

KeyLifecycle ──→ SharedKernel
AccessPolicy ──→ SharedKernel
Monitoring ───→ SharedKernel
Audit ────────→ SharedKernel
TenantManagement → SharedKernel
Infrastructure → SharedKernel

⛔ BC 之間不直接參照
   KL → TM 的 I1 查詢：透過 SharedKernel 定義的介面，TM 實作，Host 接線
   KL → AP 的 I2 呼叫：透過 SharedKernel 定義的介面，AP 實作，Host 接線
   MD → KL 的 I6 呼叫：透過 SharedKernel 定義的介面，KL 實作，Host 接線
```

### 6.1 BC 間通訊的介面定義

同步查詢的介面放在 SharedKernel，避免 BC 互相參照：

```
src/SharedKernel/
└── Contracts/
    ├── IConsumerValidator.cs                  # I1: TM 實作，KL 使用
    ├── IAccessPolicyService.cs                # I2: AP 實作，KL 使用
    └── IKeyLockService.cs                     # I6: KL 實作，MD 使用
```

---

## 7. 模組註冊慣例

每個 BC 提供一個 `{BC}Module.cs`，負責：

1. 註冊該 BC 的所有 Handler（透過介面 → 實作的 DI 綁定）
2. 註冊該 BC 的 EF Core entity configurations
3. 註冊該 BC 的 MassTransit consumers
4. 提供 `Map{BC}Endpoints()` 擴充方法註冊 Minimal API routes

---

## 8. Architecture Tests 守衛

```
tests/Architecture.Tests/
├── BcBoundaryTests.cs                         # BC 之間不直接參照
├── DomainPurityTests.cs                       # Domain 不依賴 Infrastructure
├── SliceIsolationTests.cs                     # Slice 之間不互相引用
└── NamingConventionTests.cs                   # 命名慣例檢查
```

使用 NetArchTest 或 ArchUnitNET 實作，確保架構約束不隨開發腐化。
