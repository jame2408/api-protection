# Lessons Learned

Patterns and lessons captured during development. Updated automatically per Self-Improvement Loop rules in CLAUDE.md.

一檔一教訓：`tasks/lessons/YYYYMMDD-kebab-slug.md`，frontmatter 三欄 `date` / `type` / `status`（`_README.md` 本身不是一條教訓，注入邏輯與計數皆排除它）。

> 分區治理：`status: active` / `status: archived` 判準受 `docs/adr/adr-013-content-tiering-and-injection-slimming.md` 決策 (b) 管轄——判準本身不變，只是載體從單檔雙區改為目錄 + frontmatter（`docs/adr/adr-021-shared-state-files-team-scale.md`）。歸檔判準：落地已成為機械化 gate（測試 / lint / hook）者歸檔——把該檔 frontmatter 改為 `status: archived` 並補「**落地:**」欄，不搬移或刪除檔案。`scripts/agent/hook.py` 的 `session-context` action 只注入 `status: active` 條目的標題 + `**Rule:**` 行。修改分區判準須先開新 ADR。

## Trigger conditions
- User correction or pushback
- Self-correction after failed command or wrong approach
- Non-obvious technical decision (architecture, library choice, tradeoff)
- Non-trivial or surprising bug root cause
- Repeated issue (second occurrence)
- User confirms a non-obvious approach worked

## Entry format

```markdown
---
date: YYYY-MM-DD
type: correction        # correction | decision | info
status: active          # active | archived
---
# 標題（一句話）

**Context:** 簡述當時情境（1-2句）
**Rule:** 未來應遵循的具體做法（可執行，不模糊）
**落地:** 防線／檔案落點（若尚無機械化防線，填「本條 lesson」；歸檔時必填）
```
