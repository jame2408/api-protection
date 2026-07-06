---
date: 2026-07-06
type: correction
status: active
---

# 制度修訂提案前必須反查既往 ADR 是否已裁決過同議題 — Alternatives 的 Rejected 段也是裁決

**Context:** lessons triage 時 orchestrator 建議「開 ADR 擴充歸檔判準」並獲初步核准，事後才 grep 到 ADR-019 Alternatives 段已明文拒絕同一提案（二階制度修訂違反制度凍結、稀釋「防線代記」語意），使用者知情後改裁「瘦身不歸檔」。檢索範圍不能只查「誰引用了要改的文字」（另一條 grep 反查 lesson 只覆蓋這種），還要查「這個提案本身是否被裁決過」。
**Rule:** 任何制度／判準修訂提案，成案前必須 grep `docs/adr/` 全文（含各 ADR Alternatives／Rejected 段）確認同議題是否已有裁決；重提已被拒絕的提案時，必須明列原拒絕理由與新事證，交使用者知情裁決。
**落地:** 本條 lesson。
