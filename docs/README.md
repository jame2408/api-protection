# API 金鑰管理系統 — 文件導覽

本專案採用 5 步驟設計方法論，從需求到可執行規格逐步展開。建議按以下順序閱讀。

## 設計方法論

- [design-methodology.md](./design-methodology.md) — 5 步驟設計方法論說明，理解整套文件的結構與推導邏輯

## Step 1 → Step 3：高階設計（`design/`）

依序閱讀，每一步都建立在前一步之上：

1. [PRD](./design/prd.md) — **Step 1** 產品需求文件，定義問題域與功能範圍
2. [Design Doc](./design/design-doc.md) — **Step 2** 高階設計，包含領域模型、狀態機、核心流程、Context Map
3. [Context Integration Spec](./design/context-integration-spec.md) — **Step 3** BC 間整合契約，定義事件 Payload、同步 API、失敗處理

## Step 4：Per-BC 詳細設計（`detailed-design/`）

各 Bounded Context 的 Aggregate 行為規格，可依興趣選讀：

- [Key Lifecycle](./detailed-design/key-lifecycle.md) — 金鑰生命週期（Core Domain）
- [Access Policy](./detailed-design/access-policy.md) — 存取策略管理
- [Monitoring & Detection](./detailed-design/monitoring-detection.md) — 監控與異常偵測
- [Audit & Compliance](./detailed-design/audit-compliance.md) — 審計與合規
- [Tenant Management](./detailed-design/tenant-management.md) — 租戶管理（Generic Subdomain）

## Step 5：BDD 規格（`bdd/`）

由 Step 4 的 Command/Guard/State/Event 機械式推導出的 Gherkin 場景：

- [Key Lifecycle](./bdd/key-lifecycle.md) — 44 scenarios
- [Access Policy](./bdd/access-policy.md) — 24 scenarios
- [Monitoring & Detection](./bdd/monitoring-detection.md) — 22 scenarios
- [Audit & Compliance](./bdd/audit-compliance.md) — 16 scenarios
- [Tenant Management](./bdd/tenant-management.md) — 15 scenarios

## API 規格（`design/`）

- [API Specification](./design/api-spec.md) — REST API Endpoint 契約（29 個 endpoints），含 Control Plane + Data Plane

## 架構決策紀錄（`adr/`）

- [ADR-001: Tech Stack](./adr/adr-001-tech-stack.md) — 技術選型決策
- [ADR-002: Project Structure](./adr/adr-002-project-structure.md) — 專案結構與架構模式
- [ADR-003: Error Handling and Cross-BC Contracts](./adr/adr-003-error-handling-and-cross-bc-contracts.md) — Repository、Handler、HTTP boundary 與跨 BC contract 的錯誤處理責任分工

## 資料夾結構

```
docs/
├── README.md                  ← 你在這裡
├── design-methodology.md      ← 方法論（通用參考）
├── design/                    ← Step 1-3 高階設計
│   ├── prd.md
│   ├── design-doc.md
│   └── context-integration-spec.md
├── detailed-design/           ← Step 4 各 BC 詳細設計
│   ├── key-lifecycle.md
│   ├── access-policy.md
│   ├── monitoring-detection.md
│   ├── audit-compliance.md
│   └── tenant-management.md
├── bdd/                       ← Step 5 BDD 場景
│   ├── key-lifecycle.md
│   ├── access-policy.md
│   ├── monitoring-detection.md
│   ├── audit-compliance.md
│   └── tenant-management.md
└── adr/                       ← 架構決策紀錄
    ├── adr-001-tech-stack.md
    ├── adr-002-project-structure.md
    └── adr-003-error-handling-and-cross-bc-contracts.md
```
