---
date: 2026-07-06
type: correction
status: archived
---
# 串行多 ADR 派工時，未輪到的 ADR 草稿不得以 untracked 狀態留在 docs/adr/

**Context:** ADR-021／ADR-022 兩案串行派工，orchestrator 先把兩份草稿都寫進 `docs/adr/`。Executor A 的 commit 因 pre-commit「docs/adr/ 有 staged 變更時禁止該目錄存在 untracked adr-*.md」guard 被擋 — 肇因是尚未派工的 adr-022 還躺在工作區。Executor 正確停止回報（該檔不在其檔案集），由 orchestrator 暫移 scratchpad、放行 commit 後還原解套。
**Rule:** 一次規劃多份 ADR 時，只把「當前 commit 要落地」的那份放 `docs/adr/`；後續 ADR 草稿暫存 scratchpad，輪到才移入。orchestrator 放行任何含 `docs/adr/` staged 變更的 commit 前，先跑 `git status --short docs/adr/` 確認無 untracked 草稿。
**落地:** 防線代記歸檔（2026-07-10 triage）— 本條描述的風險由 `scripts/git-hooks/pre-commit` 既有 untracked `adr-*.md` guard 攔截（事發當時即為該 guard 開火），原缺矩陣登記，已補為矩陣 10a；gate 即記憶，不再付注入 token。
