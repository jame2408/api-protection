---
date: 2026-07-05
type: correction
status: archived
---

# Refactor 判斷被 spec 管道繞過 — skill 步驟不入 spec 就不會發生

**Context:** bdd-vertical-slice skill 步驟 9（Refactor）與完整 checklist 早已存在，但 8/44–10/44 全部經「orchestrator spec → executor」管道執行，spec 未含步驟 9，重構判斷整段未發生，Wave 1 收齊後才以補救式重構 pass 收拾。使用者糾正：重構判斷屬每個 scenario 循環內的義務，補救不是常態做法。
**Rule:** skill 定義的必經步驟，凡以 spec 派工執行，spec 範本必須逐一鏡射（或明文引用該步驟並要求回報）；「判斷不做」也是一種判斷，必須留痕（enablement commit 的 Refactor-assessment trailer，commit-msg hook 強制）。新增流程步驟時同步檢查 spec 範本是否承載。
**落地:** `scripts/git-hooks/commit-msg`（`Refactor-assessment:` trailer 機械化強制，矩陣 9f）＋ `tasks/_templates/executor-spec.md`「重構評估」必填欄（commit `129ecc9`）。
