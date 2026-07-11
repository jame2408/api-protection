---
date: 2026-07-11
type: correction
status: active
---

# Lesson／checkpoint 內的前瞻指示引用前必須驗證兌現狀態 — 過期「下一步」滯留會被逐字轉抄擴散

**Context:** `20260705-governance-freeze-heuristic.md` 寫「下一份 ADR 應是 hash 演算法」，ADR-017 同日即 Accepted，但 lesson 未同步修訂。此後該句每 session 注入，orchestrator 於 2026-07-11 未驗證即轉抄「hash ADR pending」進四處文件（skill spec 活體驗證例、checkpoint 下一步、upstream-map 與 checkpoint 的回放旁證敘述），直到 `/domain-discovery` 首次活體使用時「事實蒐集先於提問」紀律撞上 `docs/adr/adr-017-*` 才現形。與既有教訓「『不存在』的斷言也要機械化驗證」互為對偶：「仍待辦」的斷言同樣要驗證。

**Rule:** (1) 引用 lesson／checkpoint／todo 中的**前瞻性指示**（「下一步應是 X」「X 尚待辦」）前，必須先機械化驗證 X 的兌現狀態（如 `grep -rl <關鍵詞> docs/adr/`），不得以注入內容為據直接轉述。(2) lesson 內的前瞻指示在兌現的當個 commit 或 checkpoint 輪即應修訂原 lesson，不留過期指示給後續 session。

**落地:** 本條 lesson（機械化候選：無穩定簽名可 lint，前瞻句式屬自然語言，維持裁決習慣承載）。
