# 使用者操作手冊 — 驅動多模型開發 Loop

> 讀者：規格擁有者（你）。模型側的權威是 `docs/orchestration.md`（協調憲章）與 `CLAUDE.md`，本檔不複寫它們的條文，只寫「**你**要做什麼、說什麼、看什麼」。內容分級依 ADR-013：細則一律指針。

## 1. 這套系統是什麼

本 repo 的真正產品是「多模型統一開發 loop」——API 金鑰管理服務只是載體。你扮演**規格擁有者與最終裁決者**；接手的常設模型扮演**協調者**（Orchestrator 角色：設計裁決、規格撰寫、review、派工）；實作由協調者派 executor 完成。任何具備 Orchestrator 角色能力的常設模型都能只靠文件接手（憲章 §1 明文規則 (ii)），不依賴特定模型在場——Fable 5 退場後照常運作。

## 2. 開工：每個新 session 的固定動作

1. 貼上冷啟動 prompt（原文在 `docs/orchestration.md` §6，勿改寫）。
2. 模型會自己讀 `tasks/checkpoint.md` 接手——**不要重述背景**；若模型要求重述，指它讀 checkpoint。
3. Codex 側限定：hook 定義變更後，在 Codex `/hooks` 檢視並 trust（安全邊界，只有你能按）。

Session 紀律（token 經濟，憲章 §5）：一個 session 做一個 phase 收尾就換新；不要 resume 大 transcript、不要馬拉松。

## 3. 只有你能做的事（權力與義務清單）

| 事項 | 規則出處 |
|------|---------|
| BDD backlog → progress 升格、插入位置、`.feature` 前綴調整 | `tasks/bdd-backlog.md` 檔頭（模型不得自主升格） |
| Feature 任務的計畫核准（`tasks/todo.md` 先寫計畫，你點頭才動工） | CLAUDE.md Autonomy Scope |
| 業務邏輯與 domain 裁決（模型必停必問） | CLAUDE.md Autonomy Scope |
| ADR 接受與 review（7 項判斷型 checklist） | `docs/adr/_template.md` 內建註解區 |
| 制度修訂裁決（事故驅動才立法；重提被拒案需新事證） | lessons：governance-freeze、ADR 反查 |
| 逃生口授權：`ALLOW_MULTI_IGNORE=1`（相同新 step 定義的多 `@ignore`）、`ALLOW_FEATURE_MAINTENANCE=1`（限機械性整理） | CLAUDE.md BDD Constraints、ADR-022 |
| Discovery 解凍：首個「真新需求」出現時，指示模型開解凍 ADR | `tasks/todo.md` 觸發制擱置項 |

Bug 回報不在此列——模型全自主（分析、修復、驗證），你只看結果。

## 4. 日常驅動語彙（你說什麼 → 模型做什麼）

| 你說 | 發生什麼 |
|------|---------|
| 「下一個場景」 | 依 `tasks/bdd-progress.md` 佇列（`grep @ignore` 取字典序第一個）走 `/bdd-vertical-slice`：Red→Green→重構評估→trailer→帳面同 commit |
| 回報一個 bug | 模型自主追根因、修、驗證、附測試輸出 |
| 丟一個新想法／新需求 | `/domain-discovery` 開場（它會先做事實蒐集——題目若已被既有 ADR 裁決會直接告訴你，不浪費問答）→ grilling 一次一問（「不知道」是合法答案）→ 你裁決 BC 模型 → 交棒 `doc-coauthoring`（PRD/Design）→ `/requirements-analysis-design`（Step 3–5；Step 5 產場景需先解凍）→ `/backlog-decomposition` 出 wave 排序提案 → **你**升格 |
| 「切 wave」「場景排序」 | `/backlog-decomposition`：輸出提案文件（wave 表＋解鎖點＋前綴建議），不動帳面 |
| 「review」「幫我看」 | `/code-review`（PR 或本地 diff） |
| 「盤點工程」「防止 drift」 | `/loop-engineering` 四迴圈巡檢 |
| 修訂既有場景／缺陷再現／移除行為 | ADR-022 §1 分流表路由（不受 Discovery 凍結限制），commit 帶 `Spec-change:` trailer |
| 「記一條 lesson」 | `/lesson` → `tasks/lessons/` 一檔一教訓 |

## 5. 你要看什麼（驗證與信任）

機械防線自動跑，你**不需要**逐條盯：寫入當下（PreToolUse hook）→ commit（pre-commit fast gate）→ push（pre-push full gate，含全測試＋coverage）→ CI（同一支 `scripts/ci-checks.sh`）。登記表：`docs/verification-matrix.md`。

你值得抽查的三個點：
1. **checkpoint 已完成欄**：每項有 commit hash＋測試證據原文；「宣稱完成但無輸出」違反 executor contract。
2. **commit trailers**：`@ignore` 移除必有 `Refactor-assessment:`；非機械性 `.feature` 變更必有 `Spec-change:`。
3. **新檢驗的「綠＋故意紅」**：任何新防線／新 skill 必須同時證明「會過」與「會擋」；只有綠沒有紅＝未驗證。

## 6. 出事時

- 模型連續失敗 3 次、規格模糊、範圍超出 → 它會依全域停止條件（憲章 §3）停下回報，等你裁決；不會硬衝。
- 每個 phase 收尾模型自動跑 `scripts/failure-triage.sh`：REPEAT 簽名要處置、active lessons ≥ 20 觸發 triage——這些它自己做，異常會寫進 checkpoint 工作區狀態警告欄。
- 觸發制擱置項（`tasks/todo.md` 專段）：條件成立時（如純文件 push 有痛感、第二常設寫者出現）由你告知模型啟動，規格已預先定好，不需新裁決。

## 7. 文件地圖（單一來源，迷路時查這張表）

| 要找什麼 | 去哪 |
|----------|------|
| 現在做到哪、下一步 | `tasks/checkpoint.md`（唯一續接入口） |
| 模型怎麼協調、何時停 | `docs/orchestration.md` |
| 哪條規則由什麼機制驗證 | `docs/verification-matrix.md` |
| 工程紀律與紅線 | `CLAUDE.md`（Claude）／`AGENTS.md`（其他 harness 薄入口） |
| 架構決策與理由 | `docs/adr/`（24 篇，Accepted 為準） |
| 上游工作流（想法→backlog）現況 | `tasks/upstream-map.md` |
| 設計文件（PRD→BDD 五步） | `docs/README.md` 導覽 |
| 歷次教訓 | `tasks/lessons/`（`status: active` 者自動注入 session） |
| 開放項與擱置項 | `tasks/todo.md` |
