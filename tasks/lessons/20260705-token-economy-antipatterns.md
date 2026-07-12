---
date: 2026-07-05
type: correction
status: archived
---

# Token 經濟四個反模式：巨型任務包、resume 大 transcript、馬拉松 session、limit 中斷後原地續舊 session

**Context:** 使用者發現單一句「先繼續」使 5h 用量瞬間 +37%。root cause 三層：(1) Phase I 規格把四階段捆成一包，養出 225K tokens / 111 tool calls 的巨型 executor；(2) orchestrator 用 SendMessage resume 該 agent 續行 — resume 會把整份巨型 transcript 無快取重讀計費，正確做法是開新 executor + 小規格（checkpoint 就是為此存在）；(3) orchestrator 自己的 session 從盤點跑到 Phase I 不曾重啟，每次使用者發話都重讀全史 — 對 executor 執行了「任務切小」卻沒對自己執行。同日 [repeat]：limit 中斷恢復後在原 session 說「繼續」+ resume 死掉的 executor → 瞬間 +13%（prompt cache TTL 5 分鐘，limit 空窗必然全冷，恢復第一輪把整段對話史與 executor transcript 以未快取輸入重讀）。
**Rule:** 四條反模式已升為 `docs/orchestration.md` §5 第 5–8 條（ADR-019 管轄），依憲章條文執行。
**落地:** docs/orchestration.md §5 第 5–8 條（docs/adr/adr-019-token-economy-charter-rules.md）。

**歸檔（2026-07-12 lessons triage，active=20 觸發）:** 規範內容已全數升為憲章條文（ADR-019 管轄），本檔僅剩指針與敘事——依 ADR-013 決策 (b)「權威落點已他處」歸檔，session 注入零增量。
