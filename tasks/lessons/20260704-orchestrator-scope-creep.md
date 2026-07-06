---
date: 2026-07-04
type: correction
status: active
---

# Orchestrator 越位執行細節 — 路由表也約束 orchestrator 自己

**Context:** 使用者糾正：zh-lint 實作、檔案修正、commit 操作等細節工作由 orchestrator（大型模型）親自執行，違反 docs/orchestration.md §1 自己訂的路由表（實作屬中型模型）。「規劃者不下場」不只是成本原則，也是憲章可移轉性的驗證 — orchestrator 自己繞過路由表，等於憲章沒有被完整遵守。
**Rule:** orchestrator 只做：設計裁決、ADR 起草或規格撰寫、review、與使用者的決策互動。任何有明確規格可循的實作（腳本、文件編輯、git 操作、勘誤）一律派 executor，即使「自己做比較快」。界線澄清：checkpoint 產出、以及親自驗證後的放行 commit/push，屬 orchestrator 的交接與 gate 職責，不算越位。豁免（2026-07-05 使用者裁決）：「內容已完全確定的機械性勘誤」— 改動的逐字內容在 orchestrator 既有 context 中已完全定案、零判斷成分、且派工固定成本明顯超過任務本身（如 2–3 行登記簿補記）— 得由 orchestrator 直接執行；任何需要 executor 重新閱讀檔案「產生內容」的編輯不在豁免內。
**落地:** 本條 lesson + Phase E 起全部實作改派 executor（本任務即範例）；界線澄清與機械性勘誤豁免皆為 2026-07-05 使用者裁決。
