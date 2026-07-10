---
date: 2026-04-03
type: correction
status: archived
---

# 寫 production code 前必須主動載入 .claude/references 規則檔

**Context:** Wave 1 初始實作時，CreateApiKeyHandler 用 throw 做業務邏輯、CancellationToken 命名 ct、ConsumerValidatorService 在 Scoped 服務內建多餘子 scope，三個問題都是因為沒有載入 .claude/references/dotnet/*.rule.md 就直接寫程式造成的。事後 code review 才全部補救。
**Rule:** 每次對這個 project 寫 production code（Handler、Service、Repository、Endpoint）前，必須先讀取 .claude/references/general/*.rule.md 和 .claude/references/dotnet/*.rule.md，確認再動手。核心規則：(1) Service 層用 Result<T,Failure> + FailureProvider.CreateFailure()，不 throw；(2) CancellationToken 參數一律命名 cancel；(3) Scoped 服務直接注入依賴，不用 IServiceScopeFactory.CreateScope()。
**落地:** 三條核心規則全數機械化，不再依賴「動手前讀規則」的人工紀律本身：(1) Result → `backend/tests/Architecture.Tests/HandlerResultReturnTests.cs`；(2) `cancel` 命名 → `scripts/source-lint.sh`（`bad_cancel` 段）+ `scripts/agent/hook.py` `pre-tool-edit`（寫的當下）+ Roslyn CA2016（`docs/adr/adr-016-roslyn-analyzer-gate.md`）；(3) `IServiceScopeFactory.CreateScope()` 禁令 → `scripts/source-lint.sh` CreateScope 段（本 commit，`*Middleware.cs`/`Program.cs` 豁免）。「動手前讀規則」的提醒本身仍由 `scripts/agent/hook.py` `session-context` 每個 session 注入。
