# 協調憲章（Orchestration Charter）

> 目的：讓「協調者」這個角色不依賴特定產品或特定人的臨場記憶就能運作 — 任何常設的中大型模型，讀完本文件即可接手調度。本文件使用 harness 中立用語（「模型」「執行者」「協調者」），不假設任何特定 CLI 或介面。
>
> 治理：本文件受 `docs/adr/adr-007-process-governance.md` 管轄。**修改本文件任一章節，必須先開新 ADR** — 比照 `CLAUDE.md` 的治理層級，不得直接編輯生效。規則本體不在此複寫第二份；本文件與 `CLAUDE.md` 的關係是「協調層補充」，不是「規範替代」。

---

## 1. 模型分級路由表

任務先問「機械化能不能做」，機械化做不到的部分才問「該派給哪一級模型」。

| 任務類型 | 執行者 | 理由 |
|---|---|---|
| 格式檢查、lint、跑測試、跑腳本 | **腳本優先**（`scripts/ci-checks.sh`、`scripts/adr-lint.sh`、`scripts/source-lint.sh` 等） | 機械化檢驗不會漂移、不消耗模型 token、結果可重現 |
| 大量讀取、掃 repo、摘要、read-back 覆核 | **小型模型 / 子代理**（廣度優先、可平行派發） | 這類任務吃 token 但不需要深度判斷；用小模型平行掃描比用大模型序列讀更省成本 |
| 實作、code review、修 bug | **中型模型** | 需要對程式碼做語意判斷與局部設計決策，但範圍通常侷限在單一任務 |
| 架構決策、規格裁決、跨規範衝突判斷 | **大型模型**（或人類） | 影響面跨檔案、跨規範，錯誤成本高，需要能寫出「為什麼選 X 不選 Y」的論證（見 `docs/adr/_template.md` 的 Rationale 要求） |

**明文規則 (i)：驗證優先機械化。** 任何能寫成腳本 / 測試 / lint 的檢驗，一律用腳本；AI review（無論哪一級模型）只負責補機械化做不到的部分（語意正確性、設計取捨、規格是否被誤解）。不得用 AI review 取代本來就能機械化的檢查。

**明文規則 (ii)：協調者角色不依賴短期顧問級模型。** 例行執行與 AI review 的分級路由，不得假設某個特定的、非常設的「顧問級」模型持續在場提供監督。協調者角色必須可以由任何常設的大型模型，僅依本文件（模型分級路由表 + executor contract + 全域停止條件）接手，不需要額外的臨場指導。若某項調度規則只有在特定模型在場時才說得通，代表這條規則沒有落地完整，應該修正規則本身而非依賴人力補位。

---

## 1.5 並行派工規則

> 治理：本節受 `docs/adr/adr-012-charter-amendments-external-adoption.md` 決策 (b) 管轄，修改任一條須先開新 ADR。

多個執行者同時工作時，適用以下規則；違反任一條視為協調失敗，須立即停止並重新分派，不得事後補救：

1. **檔案集不相交**：同時派工的多個執行者，預期改動的檔案集合必須兩兩不相交；協調者派工前必須明確列出每個執行者的檔案範圍。
2. **build 產物鏈任務不得與跑 build gate 的任務並行**：任何會觸碰 `*.csproj` / `*.props` / `*.targets` / `.editorconfig` / `backend/src/**` 的任務，與任何會執行 build gate（`scripts/ci-checks.sh full`、CI）的任務，只能串行執行，或使用 git worktree 隔離工作目錄後再合併。
3. **同時並行數上限 4**：任一時刻協調者派出且仍在執行中的 executor 數量不得超過 4。
4. **executor 之間不得直接通訊**：執行者只能與協調者溝通，不得互相傳遞指令或狀態。
5. **不得自行重試已失敗的他人任務**：一個執行者發現另一個執行者的任務失敗，必須回報協調者，不得未經協調者同意逕自重試或接手。

---

## 2. Executor Contract

任何模型以「執行者」角色進行任務時，無論其分級為何，皆受下列義務約束：

1. **進度檔與實作同 commit**：追蹤進度的檔案（如 `tasks/bdd-progress.md`、`tasks/todo.md`）的更新，必須與對應的實作變更在同一個 commit 內一起提交，不得分開。
2. **Green before commit**：提交前，受影響範圍的測試套件（至少 `scripts/ci-checks.sh fast`；涉及 production 程式碼變更則需 `full`）必須通過。不得帶著已知失敗的測試提交。
3. **誠實申報 blocker**：遇到規格模糊、能力不足、或發現任務超出原定範圍時，必須明確記錄為 blocker 並停止相關子任務，禁止用臆測填補、禁止悄悄擴大或縮小任務範圍後假裝完成。
4. **結束必產出 checkpoint**：任務結束（無論完成、中斷、或因 blocker 停下）時，必須產出交接紀錄，欄位比照 `tasks/_templates/checkpoint.md`（見 §4）。不產出 checkpoint 的任務視為未完整結束。
5. **unverified_success 為預設狀態**（`docs/adr/adr-012-charter-amendments-external-adoption.md` 決策 (a)）：任何執行者（含 subagent）對自身工作提出的「已完成」「已驗證」「測試通過」等宣稱，協調者一律視為未經驗證的中間態；協調者必須親自執行對應的確定性檢查（測試指令、lint、grep、檔案存在性、實際輸出比對）並取得可重現結果後，才能將該項目的狀態升級為「已驗證」。確定性 gate 不得透過 subagent 的轉述或摘要滿足——協調者必須直接執行指令或直接讀取原始輸出，不接受二手概括。

---

## 3. 全域停止條件

以下條件出現時，執行者必須停止當前子任務並依對應動作處理，不得靠重試或臆測繞過：

| 條件 | 動作 |
|---|---|
| 同一測試 / 檢驗連續失敗 3 次 | 停止重試，寫下 blocker（含已嘗試的方法與失敗訊息），交由更高層級判斷 |
| 需求或規格模糊、可有多種合理解讀 | 停止實作，向規格擁有者提問；不得自行選擇一種解讀後默默繼續 |
| 發現任務範圍超出原定規格邊界 | 停止該項擴大範圍的部分，回報範圍已超出，其餘不受影響的項目可繼續 |
| Context（可用注入 / 對話長度）將耗盡 | 先依 §4 產出 checkpoint，再結束，不得在 checkpoint 產出前先耗盡 context |

這四條是「局部停止規則」（如 BDD cycle 的「一次一個 @ignore」「Green before commit」）之外的全域規則，適用於所有任務類型，不因任務種類而豁免。

---

## 4. Checkpoint Schema

交接格式的欄位定義單一來源在 `tasks/_templates/checkpoint.md`，本文件不重複列出欄位內容，僅指向該模板。任何 checkpoint 產出都應包含該模板的全部欄位（分支 / 已完成含 commit hash / 待驗證 / 待裁決 / 下一步 / 工作區狀態警告 / 如何接上）。

修改該模板的欄位結構，比照本文件本身的治理層級 — 須先開新 ADR。

---

## 5. Token 節約原則

1. **注入有上限**：session 啟動或任務交接時自動注入的規則內容（must-read、lessons 摘要等）應有明確上限（筆數或 token 數），不得隨規範或紀錄增長而無界擴張。
2. **細節單一來源，其餘放指針**：同一條規則的完整內容只存在於一個檔案（通常是 Accepted ADR 或 `CLAUDE.md`）；其他文件（`docs/orchestration.md`、`AGENTS.md`、rule.md）只放「檔案 + 段落標題」形式的指針，不複製規則全文。
3. **續接靠 checkpoint，不靠重讀全史**：任務交接時，下一個執行者應優先讀取 checkpoint（§4），僅在 checkpoint 指向特定歷史內容時才回頭讀取對應的原始紀錄；不應預設要重讀整個對話歷史或整份 plan 檔案才能接手。
4. **大範圍掃描派小型模型**：需要讀取或摘要大量檔案、但不需要深度架構判斷的任務（見 §1 路由表），派給小型模型或子代理平行執行，協調者本身不消耗 token 做這類工作。

---

## 6. 冷啟動標準 prompt

> 治理：本節受 `docs/adr/adr-012-charter-amendments-external-adoption.md` 決策 (d) 管轄，修改文字須先開新 ADR。

給使用者 / 協調者在開新 session 接手時使用的固定開場文字，取代每次臨場手寫指示。內容只放指針，不重複規範本體（比照 §5 原則 2）：

    讀 `tasks/process-improvement-plan.md` §8.5（Resume Checkpoint）掌握現況，
    再讀本文件（協調憲章）掌握模型分級、executor 義務、全域停止條件與
    checkpoint 格式；依 §8.5 記載的「下一步」清單接手，若清單已空則向規格
    擁有者確認下一個任務來源。

---

## 7. 分支與部署紀律

> 治理：本節受 `docs/adr/adr-012-charter-amendments-external-adoption.md` 決策 (e) 管轄，修改文字須先開新 ADR。

本 repo 採 Trunk-Based Development：所有階段完成後直接於 main 上 commit + push，不再開長命功能分支（既有 hardening 分支併入 main 後即退役）。防線分工：

- 本機 pre-push full gate（`scripts/ci-checks.sh full`）是主防線，與 CI 執行同一支腳本。
- CI（push-to-main 後觸發）是驗證訊號，非首要防線；CI 紅視同 build 壞掉，最高優先修復。
- main 的 required status check 已解除（阻擋直接 push 的機制與 trunk-based 策略衝突，故移除）；防線改由本機 gate + CI 訊號雙軌承擔。
