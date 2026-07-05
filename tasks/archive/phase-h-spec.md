# Phase H 任務規格 — skill must-read 強制（§3-D 最後殘項）（executor 級：Sonnet）

> 目標：關閉 `tasks/process-improvement-plan.md` §3-D / §4 Phase 4 的最後殘項 — `coding-style` 與 `code-review` 兩個 skill 在本專案執行時，強制載入 references 與 Accepted ADR，且缺少非本專案 stack 目錄時 skip 不報錯。可重派指令包。

## 角色與義務

- 誠實申報到「Blockers / 不確定」節；模糊 → 停該項回報。
- 允許最後一個 commit（訊息開頭 `governance(skill-must-read):`）+ push（pre-push full gate 屬預期）+ `gh pr checks 1` 確認 CI 重跑綠。
- 只碰本規格明列檔案。

## 先讀（依序）

1. `tasks/process-improvement-plan.md` §4「Phase 4」段（原始需求描述）
2. `.claude/skills/coding-style/SKILL.md`、`.claude/skills/code-review/SKILL.md`（現況全文）
3. `CLAUDE.md` §0 Reference Loading（must-read 的權威來源 — skill 只放指針與流程，不複寫規則清單）

## 交付物

1. **`.claude/skills/coding-style/SKILL.md`**：
   - 加入本專案強制段：偵測到 .NET / C# 任務時，必讀 `CLAUDE.md`（§0 起）、`.claude/references/dotnet/*.rule.md`、`.claude/references/general/*.rule.md`、`docs/adr/` 內**所有 Status 為 Accepted 的 ADR**（用動態描述，**不要硬編 ADR 編號清單** — 避免每加一個 ADR 就 stale）。
   - stack 目錄（如 `nodejs/`、`python/`）不存在時明文 skip-if-missing，不視為錯誤。
2. **`.claude/skills/code-review/SKILL.md`**：Self Mode 與 PR Mode 都強制讀 environment references；rule-loading 段對不存在的 stack 目錄 skip-if-missing（若先前修正已涵蓋，驗證後如實回報「已涵蓋，僅補強措辭」或「無需改動」，不要為改而改）。
3. **`tasks/process-improvement-plan.md` 回寫**：§8.2 增列 Phase H 行；§8.3「§3-D 殘項」標 ✅ 關閉；§8.5 殘項清單同步（關閉後應只剩 O-8 與 Tessl 擱置項）。
4. **§8.3 追加一條低優先開環紀錄**（orchestrator 交辦的觀察，一行）：「zh-lint 只掃 `git ls-files`（index），新檔在 `git add` 前的工作期不可見 — commit gate 不受影響（staged 即可見），若要擴大掃描範圍到 untracked 檔屬 ADR-009 範圍變更，須開新 ADR。（2026-07-04，Phase F 執行中實際發生一次）」

## 驗收（輸出貼報告）

- 兩份 SKILL.md 的變更 diff 摘要（新增段落原文）。
- 自查：SKILL.md 內不得複寫任何規則內容（只有指針與流程）— 貼出你據以判斷的段落。
- `bash scripts/zh-lint.sh` 綠；`bash scripts/ci-checks.sh fast` 綠。
- commit + push + `gh pr checks 1` 綠。
- 最終報告：交付清單 + 驗證輸出 + Blockers/不確定。
