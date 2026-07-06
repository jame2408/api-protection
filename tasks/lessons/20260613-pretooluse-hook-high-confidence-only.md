---
date: 2026-06-13
type: decision
status: archived
---

# 寫的當下 PreToolUse hook 只攔「高信心、與下游一致」的 pattern，不攔 throw

**Context:** 補最內層防線（編輯當下攔截）時，plan §B2 原列要攔 `new Failure(`/`throw`/`ILogger`/`ct` 四類。實查 `throw new` 在 src 的分布：`Result.cs` 存取器守衛、`InfrastructureModule.cs` 設定守衛、`IConsumerValidator` 參數驗證——全是合法 throw。文字層級攔 `throw` 會大量誤報，而誤報的 hook 比沒有更糟（訓練人/agent 忽略或關掉它）。
**Rule:** 寫的當下 hook 只攔「文字層級可零誤報、且已在 source-lint/架構測試強制」的 pattern（`new Failure(` 豁免 FailureProvider、bare-string code、`cancel` 命名、`ILogger<` 於 Service/Domain/Handler 路徑）。需要語意判斷或會誤報的（如 `throw`）留給 reflection 架構測試的結構性檢查，不放進文字 hook。hook 與 source-lint 共用同一組 pattern → 四層防線（寫/commit/push/CI）規則一致不漂移。
**落地:** `.claude/hooks/pre-tool-edit.py` + `.claude/settings.json` PreToolUse 註冊（matcher `Edit|Write|MultiEdit`）；exit 2 擋並回報。9 情境測試全對。
