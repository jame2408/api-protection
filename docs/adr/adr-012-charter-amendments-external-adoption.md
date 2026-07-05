# 憲章修訂：unverified_success 條款、並行派工規則、checkpoint 加欄、冷啟動 prompt、TBD 分支紀律

> 本 ADR 是 `docs/orchestration.md` 與 `tasks/_templates/checkpoint.md` 修改的合法通道（依 `docs/adr/adr-007-process-governance.md` 規則 4）。把外部借鏡（`zeuikli/claude-code-workspace`，見 `tasks/process-improvement-plan.md` §10）中對我們有用的四個協調層機制，連同使用者對 TBD 分支的裁決，一次性納入憲章。

---

## Status

Accepted (2026-07-05)

同步項目：`docs/orchestration.md`（新 §1.5 並行派工規則、§2 Executor Contract 追加第 5 條、新 §6 冷啟動標準 prompt、新 §7 分支與部署紀律）、`tasks/_templates/checkpoint.md`（「待驗證」之後加「已嘗試且失敗的方法」欄）、`docs/verification-matrix.md`（新增 P1 hook 行、`machinery-check.sh` 行，第 21 行補 unverified_success 指針）、`tasks/process-improvement-plan.md`（§8.2 增列 Phase I 行、§9.2 O-8 標 ✅、§8.5 更新）。全部於本 ADR 同一 commit 落地。

---

## Context

### 現況

`tasks/process-improvement-plan.md` §9 盤點出協調層的五個缺口（O 系列），其中三個目前仍是「規則只存在於對話與人的記憶」：

- **O-8**：`docs/orchestration.md` 的「Executor Contract」（§2）目前只有 4 條義務（進度同 commit、Green before commit、誠實申報 blocker、結束產出 checkpoint），沒有任何一條規定「subagent／executor 自報的成功，協調者要怎麼覆核」。本次 §9 盤點時，一個 subagent 宣稱「repo 無 `GEMINI.md`」，實際上該檔案存在於 `.claude/references/general/` — 靠 orchestrator 人工覆核抓到，但這個覆核動作純屬自覺，沒有寫成規則。
- **並行派工**：`docs/orchestration.md` 目前完全沒有並行執行的規則。§8.2 的落地紀錄本身就留有兩次教訓：Phase B / Phase C 曾同時搶改 `docs/verification-matrix.md`、Phase F / Phase G 曾因同時觸碰 build 產物鏈而互相干擾，兩次都是靠人工事後手動串行化解決，不是靠規則預先避免。
- **checkpoint 缺「已嘗試且失敗的方法」欄**：`tasks/_templates/checkpoint.md` 現有欄位是「分支 / 已完成 / 待驗證 / 待裁決 / 下一步 / 工作區狀態警告 / 如何接上」，沒有任何欄位記錄「這個方法已經試過且失敗」。下一個執行者接手時，只能從「下一步」猜測前人試過什麼，容易重試死路。
- **冷啟動 prompt**：目前每次交接開新 session，靠使用者臨場手寫「讀 §8.5 依 checkpoint 接手」這類指示；`docs/orchestration.md` 沒有把這句開場白寫下來，換一個不熟悉這個 repo 慣例的人下指令，措辭容易漂移。
- **TBD 分支紀律**：`tasks/process-improvement-plan.md` §10.3 已有使用者裁決——改用 trunk-based development，main 直進，hardening 長命分支併回後退役——但這條規則寫在 plan 檔案裡，依 `docs/adr/adr-007-process-governance.md` 規則 1（「任何預期被當作規則長期遵守的工程規範…只能透過 Accepted ADR 新增或修改；`tasks/process-improvement-plan.md` 的文字本身不構成規範來源」），分支紀律屬於長期遵守的工程規範，尚未有 Accepted ADR 承載，現況等同「沒有生效」。

### 為何借外部 repo 的機制而不是自己重新設計

`zeuikli/claude-code-workspace`（152 stars 的 Claude Code workspace 設定倉庫）的 `core.md` 與 `HANDOFF.md` 已經有現成的、經過使用場景驗證的表述方式（unverified_success 閘門、fan-out 上限與 worktree 隔離、「嘗試過的方法」表格、標準冷啟動 prompt）。這些機制解決的正是我們自己盤點出的缺口，直接借鏡可以避免重新發明措辭、也避免遺漏該 repo 已經踩過的坑。

### 易混淆概念釐清

| 概念 | 是什麼 | 本 ADR 是否規範 |
|---|---|---|
| Executor 自報「已完成」 | 執行者對自身工作的宣稱 | ✅ 本 ADR (a)：一律視為中間態 |
| 協調者的確定性檢查 | 測試指令、lint、grep、檔案存在性等可重現驗證 | ✅ 本 ADR (a)：是唯一能升級為「已驗證」的手段 |
| 並行執行者的檔案範圍規劃 | 派工前決定誰改哪些檔案 | ✅ 本 ADR (b) |
| 執行者之間的技術協作機制（如共用函式庫） | 程式碼層級的協作 | ❌ 不規範，那是一般軟體工程慣例，不是協調層問題 |
| checkpoint 既有六個欄位的定義 | `tasks/_templates/checkpoint.md` 既有內容 | ❌ 不變更既有欄位，只新增一個 |
| 分支保護的 GitHub 設定操作本身 | `gh api` 對 required status check 的增刪 | ❌ 不規範操作細節，只規範分支紀律本身 |

---

## Decision

### (a) unverified_success 條款

`docs/orchestration.md` §2 Executor Contract 追加第 5 條義務：

```diff
  ## 2. Executor Contract
  ...
  4. **結束必產出 checkpoint**：...
+ 5. **unverified_success 為預設狀態**：任何執行者（含 subagent）對自身工作提出的「已完成」「已驗證」「測試通過」等宣稱，協調者一律視為未經驗證的中間態；協調者必須親自執行對應的確定性檢查（測試指令、lint、grep、檔案存在性、實際輸出比對）並取得可重現結果後，才能將該項目的狀態升級為「已驗證」。確定性 gate 不得透過 subagent 的轉述或摘要滿足——協調者必須直接執行指令或直接讀取原始輸出，不接受二手概括。
```

本條款關閉 `tasks/process-improvement-plan.md` §9.2 O-8。

### (b) 並行派工規則

`docs/orchestration.md` 新增 §1.5（緊接 §1 模型分級路由表之後，不重排既有 §2–§5 編號）：

```
## 1.5 並行派工規則

多個執行者同時工作時，適用以下規則；違反任一條視為協調失敗，須立即停止並重新分派，不得事後補救：

1. 檔案集不相交：同時派工的多個執行者，預期改動的檔案集合必須兩兩不相交；協調者派工前必須明確列出每個執行者的檔案範圍。
2. build 產物鏈任務不得與跑 build gate 的任務並行：任何會觸碰 *.csproj / *.props / *.targets / .editorconfig / backend/src/** 的任務，與任何會執行 build gate（scripts/ci-checks.sh full、CI）的任務，只能串行執行，或使用 git worktree 隔離工作目錄後再合併。
3. 同時並行數上限 4：任一時刻協調者派出且仍在執行中的 executor 數量不得超過 4。
4. Executor 之間不得直接通訊：執行者只能與協調者溝通，不得互相傳遞指令或狀態。
5. 不得自行重試已失敗的他人任務：一個執行者發現另一個執行者的任務失敗，必須回報協調者，不得未經協調者同意逕自重試或接手。
```

### (c) checkpoint 模板加「已嘗試且失敗的方法」欄

`tasks/_templates/checkpoint.md` 在「待驗證」之後、「待裁決」之前插入：

```diff
  ## 待驗證

  - <尚未跑過驗收指令、或驗收指令跑過但結果未覆核的項目>

+ ## 已嘗試且失敗的方法
+
+ - <方法 1> — 失敗原因：<一句話>
+ - <方法 2> — 失敗原因：<一句話>
+
+ > 記錄目的：防止下一個執行者重試已知死路。每項只需一句話失敗原因，不必寫完整除錯過程；若本次任務沒有失敗嘗試，本欄可留空但不得刪除欄位本身。

  ## 待裁決
```

欄位結構修改依 `docs/adr/adr-007-process-governance.md` 規則 4（`tasks/_templates/checkpoint.md` 欄位定義受 ADR-007 管轄）走本 ADR，符合既有治理層級。

### (d) 冷啟動標準 prompt

`docs/orchestration.md` 新增 §6（Token 節約原則之後）：

```
## 6. 冷啟動標準 prompt

給使用者 / 協調者在開新 session 接手時使用的固定開場文字，取代每次臨場手寫指示。內容只放指針，不重複規範本體（比照 §5 原則 2）：

    讀 `tasks/checkpoint.md`（Resume Checkpoint）掌握現況，
    再讀本文件（協調憲章）掌握模型分級、executor 義務、全域停止條件與
    checkpoint 格式；依 `tasks/checkpoint.md` 記載的「下一步」清單接手，若
    清單已空則向規格擁有者確認下一個任務來源。

修改本節文字，比照本文件其餘章節的治理層級，須先開新 ADR。
```

> **2026-07-05 修訂**（`docs/adr/adr-013-content-tiering-and-injection-slimming.md` 決策 (c)）：交接入口從 `tasks/process-improvement-plan.md` §8.5 遷至獨立檔案 `tasks/checkpoint.md`；上方 prompt 文字已就地更新為新指針，原文字（指向 §8.5）不再有效，歷史版本見 git log。

### (e) TBD 分支紀律

`docs/orchestration.md` 新增 §7（緊接冷啟動 prompt 之後）：

```
## 7. 分支與部署紀律

本 repo 採 Trunk-Based Development：所有階段完成後直接於 main 上 commit + push，不再開長命功能分支（既有 hardening 分支併入 main 後即退役）。防線分工：

- 本機 pre-push full gate（scripts/ci-checks.sh full）是主防線，與 CI 執行同一支腳本。
- CI（push-to-main 後觸發）是驗證訊號，非首要防線；CI 紅視同 build 壞掉，最高優先修復。
- main 的 required status check 已解除（阻擋直接 push 的機制與 trunk-based 策略衝突，故移除）；防線改由本機 gate + CI 訊號雙軌承擔。

修改本節文字，比照本文件其餘章節的治理層級，須先開新 ADR。
```

### 出處註記

(a)(b)(c)(d) 借鏡 `zeuikli/claude-code-workspace`（見 `tasks/process-improvement-plan.md` §10.1）；(e) 為使用者 2026-07-05 裁決（`tasks/process-improvement-plan.md` §10.3）。

### 不在本 ADR 範圍

- 不新增任何工具強制 subagent 附上原始輸出而非摘要——(a) 是協調者的行為義務，不是可機械檢驗的規則，機械化強制屬於未來可能的 Phase 6（PreToolUse / 工具層級變更），本 ADR 不預先擴張。
- 不建立自動化的並行任務排程器或衝突偵測工具——(b) 是派工前的人工/協調者判斷規則，不是執行時的機械化 gate。
- 不變更 `tasks/_templates/checkpoint.md` 既有六個欄位的定義或順序，只新增一個欄位。
- 不重寫 `docs/orchestration.md` §1–§5 既有內容，僅新增 §1.5 / §2 第 5 條 / §6 / §7。
- 不處理分支保護以外的 GitHub repo 設定（如 CODEOWNERS、merge queue）。

---

## Rationale

### 為何 unverified_success 由協調者負責覆核，而不是要求 subagent 自證

Subagent 缺乏「跳出自己任務框架看全局」的視角，且其匯報渠道本身可能是造成失真的環節（摘要化、選擇性呈現）；要求 subagent 自證等於讓同一個可能失真的來源既生產宣稱又驗證宣稱，沒有解決根本問題。協調者是唯一有動機、有全局視角、且能親自執行確定性檢查的角色，因此覆核責任只能落在協調者身上——這也是 `CLAUDE.md` §2「Subagent 自報成功不可信」既有原則的自然延伸，本 ADR 只是把它從一句提醒升級為 Executor Contract 的正式條款。

### 為何並行上限選 4 而非其他數字，或乾脆不設數字

不設數字會重演本 ADR 想終結的問題——「派多少個算太多」回到臨場判斷，且我們已經在 Phase B/C、F/G 實際踩過並行衝突。選 4 是借鏡來源的既有門檻，沒有我們自己的實測數據支撐更精確的數字；若未來有實際案例顯示 4 太寬或太窄，屬於規則調整，走 Implementation Rules 的治理條款開新 ADR 即可，不需要現在就做無量測依據的精算（呼應 `tasks/process-improvement-plan.md` §10.2「不採用」項對 token 數字預算的立場——沒有實測依據的精確數字是偽精確；但這裡的「4」是有明確來源的既有實務門檻，不是憑空編造的數字，兩者性質不同：一個是外部驗證過的協作上限，一個是無來源的 token 額度猜測）。

### 為何分支紀律要進 ADR 而不是留在 `process-improvement-plan.md` §10.3

`docs/adr/adr-007-process-governance.md` 規則 1 已經明文：長期遵守的工程規範只能透過 Accepted ADR 生效，plan 檔案的文字不構成規範來源。§10.3 的裁決記錄本身寫得很清楚，但它是「決策記錄」不是「規範生效點」——兩者混為一談正是 ADR-007 想終結的問題重演。

---

## Consequences

### Positive

- O-8 正式關閉：`docs/orchestration.md` 第一次有成文規則規定「協調者何時能把 executor 的宣稱升級為已驗證」，不再只靠個別 orchestrator 的自覺。
- 並行派工衝突有預先規則可循，不必每次都事後靠人工串行化補救。
- Checkpoint 多一個「已嘗試且失敗的方法」欄後，下一個執行者可以直接跳過已知死路，減少重工。
- 冷啟動 prompt 成文後，交接品質不再取決於「當時負責交接的人有沒有把話講清楚」。
- 分支紀律取得 Accepted ADR 地位，`docs/orchestration.md` §7 成為往後任何「要不要開長命分支」爭議的唯一權威來源。

### Negative / Trade-offs

- unverified_success 條款會增加協調者的驗證工作量（不能只讀 subagent 摘要就結案）。
  - Mitigation: 這個成本本來就存在（GEMINI.md 誤報事故已證明摘要不可靠），本條款只是把「協調者本來就該做但可能偷懶跳過」的動作寫成義務，不是新增額外工作，而是防止該做的工作被省略。
- 並行上限 4 與檔案不相交規則，會在任務數量多但檔案改動範圍難以完全切乾淨時，迫使部分任務改為串行，拉長總完成時間。
  - Mitigation: 犧牲部分並行效率換取確定性上的安全邊際是刻意取捨；且規則允許用 git worktree 隔離作為並行的替代手段，不是強制全面串行。
- checkpoint 新欄位若執行者疏於填寫（留空但應該有內容），會退化成裝飾性欄位，不產生實際防呆效果。
  - Mitigation: 欄位本身要求「若無失敗嘗試可留空但不得刪除欄位」，讓「有沒有填」本身成為可見的訊號（空白 vs. 缺欄位不同），協調者 review checkpoint 時可以直接檢查該欄位是否被恰當使用。

---

## Alternatives Considered

### Alternative A：把 (a)–(d) 四項機制個別拆成四個獨立 ADR

Rejected. 四項機制都是同一批外部借鏡分析（`tasks/process-improvement-plan.md` §10）在同一天由使用者一次裁決「P1+P2+P3 全採用」，且全部落在 `docs/orchestration.md` 這一份文件的修改範圍內，屬於同一批憲章修訂；拆成四個 ADR 只會製造四倍的格式開銷（Status/Context/Rationale/Alternatives 各寫一次），內容之間又沒有獨立的爭議點需要分別論證，不符合 `docs/adr/_template.md` 的成本效益。

### Alternative B：分支紀律 (e) 另開一個獨立 ADR，不與 (a)–(d) 合併

Rejected. (e) 雖然來源不同（使用者裁決，非外部借鏡），但同樣是 `docs/orchestration.md` 的修改，且與 (a)–(d) 同一批任務規格（`tasks/phase-i-spec.md`）一次執行、一次 commit；分成兩個 ADR 會讓同一個 commit 同時觸發兩個 ADR 的 lint 與 review，徒增流程負擔，且兩者都不需要獨立的 Alternatives 論證深度。

### Alternative C：用機械化工具（例如要求 subagent 回傳結構化 raw output，而非摘要）取代 unverified_success 的人工覆核義務

Rejected. 目前沒有現成工具可以強制 subagent 的回傳格式（取決於底層 agent 平台的實作），且即使能強制附上 raw output，判斷「這段 raw output 是否真的證明任務完成」仍然需要語意判斷，不是純機械比對能取代的。人工（協調者）覆核義務是現階段成本最低的解法；機械化輔助工具屬於未來可能的 Phase 6 範疇，非本 ADR 範圍。

### Alternative D：checkpoint 的「已嘗試且失敗的方法」併入既有「下一步」欄位，用文字說明代替新增獨立欄位

Rejected. 「下一步」欄位的目的是列出可執行的下一個動作，「已嘗試且失敗」的目的是列出不該再做的動作，兩者語意相反；混在同一欄位會讓執行者必須逐條分辨哪些是建議、哪些是禁止，增加誤讀風險。獨立欄位讓兩種訊號在結構上就分開，符合 `tasks/_templates/checkpoint.md` 現有「欄位即語意」的設計原則。

### Alternative E：main required status check 保留，改用 admin bypass 或 auto-merge 繞過阻擋

Rejected. 保留 required status check 但另開後門，等於維持了「兩人小團隊要繞過自己設的規則」的隱性摩擦，且與使用者裁決的「PR 流程過重、改用直接 push main」的初衷矛盾——與其保留規則再開特例，不如直接移除規則本身，並用本機 pre-push full gate 承擔原本 required check 的防線責任。

---

## Implementation Rules

1. `docs/orchestration.md` §2 Executor Contract 的第 5 條（unverified_success）必須逐字比照本 ADR 決策 (a) 的條文，不得在移植時弱化「協調者必須親自執行」的要求。
2. `docs/orchestration.md` §1.5（並行派工規則）的 5 條規則，修改任一條須先開新 ADR；新增第 6 條同樣需要新 ADR，不得直接編輯追加。
3. `tasks/_templates/checkpoint.md` 新增的「已嘗試且失敗的方法」欄位不得刪除，即使空白也必須保留欄位標題本身（讓「刻意留空」與「忘記寫」在結構上可辨識——欄位存在但空白＝刻意留空，欄位整段消失＝不合規）。
4. `docs/orchestration.md` §6（冷啟動標準 prompt）與 §7（分支與部署紀律）的文字，修改須先開新 ADR，比照本文件其餘章節的治理層級。
5. `docs/verification-matrix.md` 必須在本 ADR 落地的同一 commit 內，新增 `.claude/hooks/post-edit-validate.sh` 與 `scripts/machinery-check.sh` 兩條主表行，並在既有「Orchestrator review executor 產出」行補上本 ADR (a) 的指針。
6. **驗收**：

   ```bash
   bash scripts/adr-lint.sh
   # 預期 0 violation

   grep -n "unverified_success" docs/orchestration.md
   # 預期至少 1 命中（§2 第 5 條標題）

   grep -n "已嘗試且失敗的方法" tasks/_templates/checkpoint.md
   # 預期 1 命中

   grep -n "並行派工規則" docs/orchestration.md
   # 預期至少 1 命中（§1.5 標題）
   ```

7. 任何提案修改 1–6，必須先開新 ADR。
