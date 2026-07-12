# ADR 索引

> 一行一 ADR：編號、Accepted 日期、標題。本檔只承載導覽資訊，不複寫決策內容；「現行裁決是什麼」以各 ADR 本文為權威。
>
> 維護規則（`docs/adr/adr-028-knowledge-ledger-lifecycle-and-adr-index.md` 決策 §1）：新 ADR Accepted 的同 commit 必須在此加列；一致性由 `scripts/adr-lint.sh` 檢查 8 機械化（雙向：檔案缺列＝紅、列指向不存在的檔案＝紅）。

| ADR | Accepted | 標題 |
|---|---|---|
| [001](adr-001-tech-stack.md) | 2026-04-03 | 技術選型（Technology Stack） |
| [002](adr-002-project-structure.md) | 2026-04-03 | 專案結構（Project Structure） |
| [003](adr-003-error-handling-and-cross-bc-contracts.md) | 2026-04-30 | 錯誤處理與跨 BC Contract 決策 |
| [004](adr-004-failure-shape-and-claude-md-alignment.md) | 2026-05-01 | Failure 形狀單欄位定案 與 CLAUDE.md 對齊 |
| [005](adr-005-primary-constructor-as-default-injection.md) | 2026-05-01 | Primary Constructor 為預設「依賴注入」方式，field-based injection 為例外 |
| [006](adr-006-status-enum-pascalcase-and-wire-format.md) | 2026-05-02 | 狀態 enum 採 PascalCase + JsonStringEnumConverter 統一 wire format |
| [007](adr-007-process-governance.md) | 2026-07-04 | 治理規則正式化：ADR 為唯一通道、同步同 commit、lessons 分類、協調憲章納管 |
| [008](adr-008-learning-loop-injection-and-pending-lessons.md) | 2026-07-04 | 學習迴圈機械層重設計：session 注入有界化、pending-lessons 管線退役 |
| [009](adr-009-traditional-chinese-and-zh-lint.md) | 2026-07-04 | Repo 文件語言規範：正體中文 + 簡體字機械化防線（zh-lint） |
| [010](adr-010-norm-doc-discovery-wiring.md) | 2026-07-04 | 規範文件可發現性接線：新規範文件必須同 commit 接上自動載入面 |
| [011](adr-011-naming-rules-editorconfig-enforcement.md) | 2026-07-04 | 命名規則機械化：`.editorconfig` `dotnet_naming_*` + `EnforceCodeStyleInBuild` |
| [012](adr-012-charter-amendments-external-adoption.md) | 2026-07-05 | 憲章修訂：unverified_success 條款、並行派工規則、checkpoint 加欄、冷啟動 prompt、TBD 分支紀律 |
| [013](adr-013-content-tiering-and-injection-slimming.md) | 2026-07-05 | 內容分級（四級載入制度）與注入瘦身：session 固定成本治理 |
| [014](adr-014-handler-coverage-gate.md) | 2026-07-05 | Handler coverage gate — DoD「≥ 80%」門檻機械化與度量解讀固化 |
| [015](adr-015-dependency-vulnerability-audit-gate.md) | 2026-07-05 | 依賴弱點 audit gate — NU1903/NU1904 升為 build error 並消除既有 High 弱點 |
| [016](adr-016-roslyn-analyzer-gate.md) | 2026-07-05 | Roslyn analyzer gate — 語意層程式碼品質檢驗從 AI review 移交編譯器 |
| [017](adr-017-key-hash-hmac-and-hotpath-contract.md) | 2026-07-05 | 金鑰儲存雜湊改用 HMAC-SHA256 + pepper，並固化驗證熱路徑合約 |
| [018](adr-018-failure-triage-and-observations-retirement.md) | 2026-07-05 | 學習迴圈回饋化：failures triage 義務與 observations 除役 |
| [019](adr-019-token-economy-charter-rules.md) | 2026-07-05 | 憲章 token 經濟條款：四條反模式對策升為 §5 可打勾規則 |
| [020](adr-020-outbox-minimal-event-publication.md) | 2026-07-05 | 事件發佈最小落地：Transactional Outbox（同交易收割 Domain Events），Relay 後置 |
| [021](adr-021-shared-state-files-team-scale.md) | 2026-07-06 | 共享狀態檔的團隊尺度：lessons 一檔一教訓拆分，checkpoint／progress 分流規格先行 |
| [022](adr-022-bdd-requirement-type-routing.md) | 2026-07-06 | BDD 需求類型分流：既有行為變更、缺陷再現與行為移除的場景修訂流程 |
| [023](adr-023-cross-harness-hook-and-skill-parity.md) | 2026-07-10 | Claude Code 與 Codex 共用 hook 核心及 skill 單一來源 |
| [024](adr-024-control-plane-jwt-auth-and-actor-propagation.md) | 2026-07-10 | Control Plane JWT 認證與 Actor 傳遞：AuthToken 基礎設施最小落地 |
| [025](adr-025-agent-engineering-kit-cross-project-portability.md) | 2026-07-11 | AI Agent Engineering Kit 跨專案可攜化與分階段發佈 |
| [026](adr-026-role-based-model-routing-and-codex-subagents.md) | 2026-07-11 | 角色型模型路由與 Codex Subagent Adapter |
| [027](adr-027-team-access-bc-and-sharedkernel-contract.md) | 2026-07-11 | Team Access BC 落地形態與 SharedKernel 契約 |
| [028](adr-028-knowledge-ledger-lifecycle-and-adr-index.md) | 2026-07-12 | 知識帳面生命週期：ADR 索引機械化、phase 收尾清掃義務、plan 檔退役歸檔 |
