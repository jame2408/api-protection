---
date: 2026-07-05
type: correction
status: active
---

# 自動載入面有 token 預算 — 不放日期出處、不複寫憲章、先查既有落點

**Context:** 使用者連環糾正 CLAUDE.md 新增的 Orchestrator Brief：加了無操作意義的日期、五條內容有四條複寫 orchestration.md（違反 ADR-007 規則 5 / SSOT）、CLAUDE.md 因此變胖 — 且這是反射式補丁，動手前沒全盤檢查內容是否已有權威落點。
**Rule:** 動自動載入面（CLAUDE.md / session-init 注入 / AGENTS.md）前先問三題：(1) 這內容已有權威落點嗎？有 → 只放指針；(2) 每一行對「下個 session 的行為」有操作意義嗎？沒有（日期、出處、敘事）→ 刪，出處查 git；(3) 改完後自動載入總量是變大還是持平？CLAUDE.md 的正確內容 = 高層 workflow + non-negotiable + 指派與指針（§2 根因 1 處理原則早已寫明）。
**落地:** Brief 12 行縮至 3 行（本 commit）；本條 lesson。
