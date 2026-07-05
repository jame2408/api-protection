# 內容分級（四級載入制度）與注入瘦身：session 固定成本治理

> 本 ADR 把「什麼內容值得每個 session 自動付費讀取」明文化為四級分級制度，並依此重新設計 lessons 注入格式、checkpoint 交接位置、`CLAUDE.md` 瘦身原則。本 ADR 是 `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` 決策 §2 / Implementation Rule 2，與 `docs/adr/adr-012-charter-amendments-external-adoption.md` 決策 (d) 的合法修訂通道。

---

## Status

Accepted (2026-07-05)

同步項目：`CLAUDE.md`（瘦身至 ~100 行）、`tasks/lessons.md`（分 Active/Archived 兩區）、`.claude/hooks/session-init.sh`（注入邏輯改讀 Active 區）、`scripts/hook-smoke.sh`（斷言同步新格式）、`tasks/checkpoint.md`（新建，取代 `tasks/process-improvement-plan.md` §8.5 作為交接入口）、`tasks/process-improvement-plan.md`（§8.5 改三行指針）、`docs/orchestration.md`（§6 冷啟動 prompt 文字改指 `tasks/checkpoint.md`）、`docs/adr/_template.md`（新增 Review Checklist 註解區）、`docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md`（決策 §2 / Implementation Rule 2 依本 ADR 決策 (b) 修訂）、`docs/adr/adr-012-charter-amendments-external-adoption.md`（決策 (d) 冷啟動 prompt 文字依本 ADR 決策 (c) 修訂）。全部於本 ADR 同一 commit 落地。

---

## Context

### 現況：三個各自增長、互不知情的固定成本

1. **`CLAUDE.md` 已膨脹到 197 行**，且與 `.claude/references/dotnet/*.rule.md`、`docs/adr/adr-003-error-handling-and-cross-bc-contracts.md`、`docs/adr/adr-004-failure-shape-and-claude-md-alignment.md`、`docs/verification-matrix.md` 存在內容重複——同一條「Service 層禁 throw、用 Result」規則，`exceptions.rule.md` 有完整範例，`CLAUDE.md` §4 又重複一次細節，兩處要同步維護。`tasks/lessons.md` 2026-07-05 條目「自動載入面有 token 預算」已指出這是反覆發生的模式：加東西容易，判斷「這內容有沒有權威落點、該不該放自動載入面」的機制卻不存在。
2. **`session-init.sh` 的 lessons 注入無視「教訓是否已被機械化」**：`docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` 決策 §2 / Implementation Rule 2 規定「must-read + 最近 8 條 lessons 全文 + 計數指針」，但 8 條裡目前有 6 條的「落地」欄位已指向具體測試 / lint / hook（如 `HandlerResultReturnTests.cs`、`scripts/zh-lint.sh`）——這些教訓造成的風險已經被機械化防線接管，防線本身就是記憶，全文注入是純粹的 token 浪費，且隨 lessons 增加只會更嚴重（`tasks/lessons.md` 現有 13 條，其中 6 條已可歸類為「已機械化」）。
3. **checkpoint 交接入口散落在一份會員長的 plan 檔案的一個章節裡**：`tasks/process-improvement-plan.md` §8.5 目前身兼「Resume Checkpoint 內容」與「plan 檔案本體」兩職，接手者要在數百行的 plan 檔案中定位一個章節；`docs/adr/adr-012-charter-amendments-external-adoption.md` 決策 (d) 的冷啟動標準 prompt 因此把 `tasks/process-improvement-plan.md §8.5` 寫死為指針目標，而 `tasks/_templates/checkpoint.md`（同一批 ADR-012 新增）已經定義好完整欄位 schema——模板與實際使用位置是分開的兩個檔案，沒有理由不讓交接內容直接落在對應模板欄位所在的獨立檔案。

### 三者的共同根因

三個問題都是同一件事：**沒有「內容分級」的判準**，導致「已經有權威落點的內容」「已經機械化不需要記憶的內容」「應該獨立成檔而非塞進大檔案一個章節的內容」都預設留在最貴的自動載入面（每個 session 都讀）。`tasks/lessons.md` 2026-07-05 條目已提出過三題判準（是否已有權威落點／是否有操作意義／注入總量是否增減），但那只是單次補丁的心法，尚未固化成可重複套用的分級制度。

### 不決定會發生什麼

若不建立分級制度，`CLAUDE.md`、lessons 注入、checkpoint 內容會持續各自線性成長——每個新規則、新教訓、新交接記錄都預設留在最貴的位置，session 固定成本沒有上限，且沒有機制判斷「這條內容現在還需要每個 session 都讀嗎」。

---

## Decision

### (a) 四級載入分級制度

任何內容進 Tier 0 的唯一資格是「**全局且每 session 都需要**」——兩個條件缺一都不夠格：只全局但不是每個任務都用得到（例如某個 BC 的內部細節）不算；每 session 都用得到但不全局（例如單一任務的臨場筆記）也不算。

| Tier | 定義 | 落點 | 觸發方式 |
|---|---|---|---|
| **Tier 0** | 每-session 自動載入，有預算意識 | `CLAUDE.md` + `.claude/hooks/session-init.sh` 注入 | 自動（UserPromptSubmit hook） |
| **Tier 1** | 任務型必讀 | rule 檔（`.claude/references/**/*.rule.md`）、Status 為 Accepted 的 ADR、`docs/design/api-spec.md` | 由 skill 的 must-read 段觸發（見 `coding-style` / `code-review` SKILL.md 的 Project Must-Read） |
| **Tier 2** | 按需指針鏈 | `docs/orchestration.md`（憲章）、`docs/verification-matrix.md`（矩陣）、`docs/design/*.md`（design doc） | 從 Tier 0/1 的指針點進去，需要時才讀 |
| **Tier 3** | 歸檔 | git 歷史、`tasks/lessons.md` 的 Archived 區、未來的 archive 目錄 | 只在追溯調查時主動查找 |

```diff
- 判準不存在：新規則、新教訓、新交接記錄預設留在 Tier 0（CLAUDE.md / 注入）
+ 判準存在：新內容先問「全局且每 session 需要？」——
+   是 → Tier 0（且仍要問「有沒有更省的表達方式」）
+   否，但任務相關 → Tier 1（skill must-read 觸發）
+   否，且是背景知識 → Tier 2（指針鏈）
+   風險已被機械化接管 → Tier 3（歸檔）
```

### (b) lessons 分區 + 注入只讀 Active 的 Rule 行

`tasks/lessons.md` 分兩個一級小節：

- `## Active`：尚未被機械化防線接管的教訓，維持原本欄位（標題 / **Date:** / **Context:** / **Rule:** / **落地:**）。
- `## Archived（已機械化 — 防線代記）`：落地欄位已指向具體測試 / lint / hook（即該教訓描述的風險，現在違反會被機械 gate 攔下）的教訓，欄位內容不變，只搬移位置。

判準：**落地已成為機械化 gate（測試 / lint / hook）者歸檔**；落地只是「本 commit」「本條 lesson」「一次性程式碼修正」而未伴隨一個會在未來持續攔截同類違規的檢驗，維持 Active。

`.claude/hooks/session-init.sh` 的注入邏輯：

```diff
- 抽取「### [」錨點切塊的「最後 8 條」全文（含 Context / 落地）
+ 只讀 `## Active` 區塊內的每一條，輸出其「### [」標題行 + 「**Rule:**」行
+ （不輸出 Context / 落地）；末尾附 Active／Archived 計數指針
```

取代 `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` 決策 §2 / Implementation Rule 2 的「最近 8 條全文」設計——不再需要數量上限，因為 Active 區的教訓量本來就會隨機械化持續流向 Archived 而收斂，注入的是「標題 + 一行 Rule」而非整條全文，單條成本已大幅下降。

### (c) checkpoint 遷出

新建 `tasks/checkpoint.md`，欄位比照 `tasks/_templates/checkpoint.md`（分支 / 已完成含 commit hash / 待驗證 / 已嘗試且失敗的方法 / 待裁決 / 下一步 / 工作區狀態警告 / 如何接上），內容自 `tasks/process-improvement-plan.md` §8.5 現有實況遷入。遷出後：

```diff
- tasks/process-improvement-plan.md §8.5 身兼「plan 章節」與「交接內容本體」
+ tasks/checkpoint.md 是唯一續接入口
+ tasks/process-improvement-plan.md §8.5 只留三行指針（歷史紀錄保留在 git log）
```

`docs/adr/adr-012-charter-amendments-external-adoption.md` 決策 (d) 的冷啟動標準 prompt、`docs/orchestration.md` §6 的對應文字、`CLAUDE.md` 的 Orchestrator Brief，三處對「交接入口」的指針同步改指 `tasks/checkpoint.md`。

### (d) CLAUDE.md 瘦身原則

`CLAUDE.md` 只保留「全局且每 session 需要」的內容：指令（Commands）、Working Agreement 的 Brief / 自治範圍 / 變更紀律、Non-Negotiable Constraints 的短句版 + 指針。細則一律指針化，不在 `CLAUDE.md` 內重複已有權威落點的內容。

```diff
- §4 Verification Standards 的 Error Handling / Code Quality 逐條列出完整規則細節（各 4-5 行）
+ 保留 DoD 高層條目（BDD 過、架構測試過、證據要求），細則壓成指針：
+   權威來源 → `.claude/references/dotnet/*.rule.md`、`docs/adr/adr-003-*.md` / `adr-004-*.md`
+   驗證登記 → `docs/verification-matrix.md`

- ADR 段內嵌 7 項 Review checklist 全文
+ Review checklist 移入 `docs/adr/_template.md` 的 Review Checklist 註解區，CLAUDE.md 留一行指針

- Non-Negotiable Constraints 四條無標注哪些已有機械化防線
+ 逐條加已機械化標注（例：`(架構測試強制：BoundedContextIsolationTests.cs)`），讓讀者一眼判斷「這條不遵守會不會被攔下」
```

### 不在本 ADR 範圍

- 不處理 `tasks/phase-*-spec.md` 等歷史執行紀錄內對 §8.5 的舊引用——那些是完成當下的執行記錄，如同 git commit message，不因交接入口遷移而回頭改寫；歷史準確性優先於指針一致性。
- 不處理目錄歸檔（`.claude/skills/tessl__*`、`docs/arch-flow.html` 等）——依 `tasks/archive/phase-j-spec.md` 開頭裁決，另開 Phase K。
- 不重建或修改 `.claude/hooks/pre-tool-edit.py`、`observations.jsonl` / `failures.jsonl` 的行為——與注入無關。
- 不機械化「新內容該落在哪一 Tier」的判斷本身（見 Rationale「為何不機械化」段）。
- 不變更 `tasks/_templates/checkpoint.md` 既有欄位定義——`tasks/checkpoint.md` 只是套用該模板產生的實例檔案。

---

## Rationale

### 為何 Tier 0 資格用「全局且每 session 需要」而不是「重要」

「重要」是主觀且會不斷擴張的判準——幾乎任何規則在寫下的當下都會被作者認為重要，這正是 `CLAUDE.md` 膨脹到 197 行的成因。「全局且每 session 需要」是兩個可回答的具體問題（這條內容是否只對特定任務類型有意義？這條內容是否有其他觸發機制可以觸及，例如 skill must-read 或指針鏈？），能直接對應到 Tier 1/2 的存在，而不是把「不夠格進 Tier 0」的內容直接判死刪除。

### 為何 lessons 注入放棄數量上限（8 條），改用 Active/Archived 分層

數量上限（`docs/adr/adr-008-*.md` 原決策）是在「無法判斷教訓是否還有效」時的權宜設計——用「最近的最相關」代替真正的判斷。但本專案的教訓天生就有客觀的「是否已被機械化接管」判準（`docs/adr/adr-007-process-governance.md` 已要求每條教訓必有「落地」欄位），比起「最近 8 條」的時間近似值，直接依落地狀態分層更準確：已被防線接管的教訓，注入它不會增加安全性（防線已經在做這件事），只會增加 token 成本。

### 為何 checkpoint 獨立成檔而不是留在 plan 章節內加強格式

`tasks/process-improvement-plan.md` 是一份持續累積的歷史記錄（§1–§9 涵蓋多個階段的盤點與決策），交接內容卻是「當下這一刻」的快照，兩者的更新頻率與讀者需求完全不同：接手者只需要交接快照，不需要先穿過整份 plan 的歷史才能定位到最後一節。獨立成檔讓 `tasks/checkpoint.md` 可以被直接、完整地讀取，不需要在大檔案裡搜尋章節。

### 為何不機械化「新內容該落在哪個 Tier」的判斷

Tier 判斷的核心問題（「這條內容對所有任務都有意義嗎」「有沒有更省的表達方式」）需要語意判斷，取決於內容本身的性質，不是可以用 grep/lint 檢查的結構性特徵（不像「ADR 是否有 Governance clause」這種格式檢查）。目前沒有證據顯示這個判斷會被系統性地做錯到需要機械化的地步——`tasks/lessons.md` 2026-07-05 的教訓正是靠人工（使用者糾正）攔下的一次性事件，尚不構成「重複發生到需要工具」的門檻；若未來重複出現同類過度膨脹，才是開新 ADR 加機械檢查的時機。

---

## Consequences

### Positive

- `CLAUDE.md` 從 197 行降到約 100 行，每個 session 的固定讀取成本減半，且不重複已有權威落點的內容。
- lessons 注入從「最近 8 條全文（不論是否已解決）」改為「Active 全部但精簡（標題 + Rule 一行）」，已機械化的教訓不再佔用注入 token，且不再有隱性的「8 條之後就看不到」問題（本次 Active 僅 7 條，全部可見）。
- `tasks/checkpoint.md` 成為單一、可直接完整讀取的交接入口，接手成本不再取決於在 plan 檔案裡搜尋章節的能力。
- 四級分級制度給未來「這個東西該放哪」的爭議一個可重複套用的判準，不需要每次重新論證。

### Negative / Trade-offs

- Active 區沒有數量上限，若未來持續產生新教訓卻遲遲不歸檔，仍可能無界成長。
  - Mitigation: 歸檔判準（落地是否已機械化）會持續把成熟的教訓移出 Active；若實測顯示 Active 仍然無界成長，屬於規則調整，可另開 ADR 加數量上限，不影響本 ADR 其餘決策。
- `docs/adr/adr-008-*.md` 與 `docs/adr/adr-012-*.md` 的部分條文被本 ADR 就地修訂，日後翻閱那兩份 ADR 時，若忽略 Status 的同步項目說明，可能誤以為修訂後的文字是原始決策。
  - Mitigation: 修訂處直接標註「依 `docs/adr/adr-013-*.md` 決策 (b)/(c) 修訂」，且本 ADR 的 Status 同步項目已明列被修訂的兩份文件，可交叉核對。
- 四級分級制度只是原則性判準，沒有機械化 lint 驗證「新增到 CLAUDE.md 或注入的內容確實符合 Tier 0 資格」。
  - Mitigation: 依 CLAUDE.md 既有的 ADR review checklist（判斷型）與 orchestrator review 把關；機械化本身不是本 ADR 範圍（見 Rationale「為何不機械化」段），有重複違規證據時再議。

---

## Alternatives Considered

### Alternative A：只做 lessons 分區，不改注入格式（維持最近 8 條全文）

Rejected. 分區後若注入邏輯不變，Archived 教訓一旦不巧落在「最近 8 條」窗口內仍會被全文注入，分區沒有實際降低 token 成本，只是多了一層無效的分類。

### Alternative B：已機械化的教訓直接從 `tasks/lessons.md` 刪除，而非歸檔

Rejected. `docs/adr/adr-007-process-governance.md` 建立的 lessons 機制隱含教訓需要可追溯（每條都有 Date / Context / 落地），刪除會讓「這個坑當初為什麼被踩、如何被機械化」的推理鏈消失，未來若機械化防線本身被意外移除，也失去了教訓可以重新浮現的依據。歸檔的成本（保留在同一檔案的第二區塊）遠低於刪除的風險。

### Alternative C：checkpoint 不獨立成檔，改為每次交接開一個新的輕量 ADR 記錄現況

Rejected. ADR 治理的是「規則」，checkpoint 記錄的是「頻繁變動的任務狀態」——兩者更新頻率與審查層級完全不同，把交接狀態塞進 ADR 會讓 `docs/adr/` 充斥非規則性內容，且 `scripts/adr-lint.sh` 的結構性要求（Alternatives、Rationale 等）對一份純狀態快照是不必要的負擔。

### Alternative D：`CLAUDE.md` 保留全部原文，只在旁邊加註解式指標，不刪減內容本身

Rejected. 加註解不會讓行數下降，達不到瘦身目標，反而讓同一條規則在 `CLAUDE.md` 與其權威來源（rule.md / ADR）各存在一份，兩處要同步維護——這正是 Context 描述的重複維護問題本身，不是解法。

### Alternative E：四級分級制度用機械化工具強制（例如 lint 檢查每份文件的內容屬於哪一 Tier）

Rejected. Tier 判斷需要語意理解（這條內容是否全局適用），不是結構性特徵，目前沒有可行的機械化檢驗方式；勉強做規則式近似（例如按檔案路徑分類）會產生大量誤判，屬於過度工程化。見 Rationale「為何不機械化」段。

---

## Implementation Rules

1. 任何提案新增內容到 `CLAUDE.md` 本體或 `.claude/hooks/session-init.sh` 的自動注入段，動手前須用決策 (a) 的「全局且每 session 需要」判準檢驗；不符合者改放 Tier 1（skill must-read）或 Tier 2（指針鏈）。
2. `tasks/lessons.md` 必須維持 `## Active` 與 `## Archived（已機械化 — 防線代記）` 兩個一級小節；新教訓一律先進 Active，歸檔判準見決策 (b)。
3. `.claude/hooks/session-init.sh` 的 lessons 注入邏輯只可讀取 `## Active` 區塊內容，輸出每條的 `### [` 標題行與 `**Rule:**` 行（不含 Context / 落地），末尾附 Active／Archived 計數指針；本規則取代 `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md` 決策 §2 / Implementation Rule 2 原「最近 8 條全文」設計。
4. `scripts/hook-smoke.sh` 的斷言必須驗證：Active 區最後一條的標題與 Rule 行片段出現在注入輸出中，且其 Context 行片段不出現；原有的 marker dedup（同 session 不重複注入）與缺 `session_id` 保守注入兩項斷言維持不變。
5. `tasks/checkpoint.md` 是交接的唯一權威位置；`tasks/process-improvement-plan.md` §8.5 只保留三行指針，不得再於 plan 檔案內新增或更新交接內容本體。
6. `CLAUDE.md` 的新增或修改，比照決策 (d) 判準——細節一律指針化到權威文件（rule.md / Accepted ADR / `docs/verification-matrix.md`），不得重複已有權威落點的內容全文。
7. **驗收**：

   ```bash
   bash scripts/adr-lint.sh
   # 預期 0 violation

   grep -n "^## Active" tasks/lessons.md
   grep -n "^## Archived" tasks/lessons.md
   # 各預期至少 1 命中

   grep -n "tasks/checkpoint.md" CLAUDE.md docs/orchestration.md
   # 預期至少各 1 命中

   wc -l CLAUDE.md
   # 目標約 100 行（容許 ±15 行）

   bash scripts/hook-smoke.sh
   bash scripts/ci-checks.sh fast
   # 兩者皆預期 0 violation / exit 0
   ```

8. 任何提案修改 1–7，必須先開新 ADR。
