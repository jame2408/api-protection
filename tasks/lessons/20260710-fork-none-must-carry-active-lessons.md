---
date: 2026-07-10
type: correction
status: active
---
# fork_turns=none 派工必須在 spec 顯式攜帶 active lessons 讀取義務

**Context:** Orchestrator 為節省 transcript 使用 `fork_turns=none`，派工 spec 雖列出規則正典，卻漏列 active lessons；executor 因未收到 root session 的 lesson injection，重踩既有 zsh `status` 唯讀變數 lesson，後由監控發現並在編輯前補讀 16 條 active lessons。
**Rule:** 使用 `fork_turns=none` 派 executor 時，派工 spec 必須顯式要求在任何編輯／腳本組裝前讀取 `tasks/lessons/` 中所有 `status: active` 條目；不得假設 subagent 會繼承 root session 的 lesson 注入。
**落地:** 本條 lesson；本次 checkpoint 的流程異常紀錄（尚無機械化 gate）
