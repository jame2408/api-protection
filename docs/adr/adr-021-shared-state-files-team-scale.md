# 共享狀態檔的團隊尺度：lessons 一檔一教訓拆分，checkpoint／progress 分流規格先行

> Lead-in：`tasks/` 下多個「多寫者共用單檔」的狀態檔（lessons／checkpoint／bdd-progress）在並行 session 或多成員情境下必然產生 merge 衝突。本 ADR 把衝突根因拆成三種寫入模式並各自給處方：lessons 立即拆為一檔一教訓；checkpoint 分流與 progress 帳面生成化只定規格與觸發條件，不投機實作。

---

## Status

Accepted (2026-07-06)

- 局部承接：`docs/adr/adr-013-content-tiering-and-injection-slimming.md` 決策 (b) 的 Active／Archived 分區**判準不變**，本 ADR 只更換其載體（單檔雙區 → 目錄 + frontmatter `status` 欄）。ADR-008 的「注入有界」原則同樣不變。
- 同步項目（與本 ADR 同 commit）：見 Decision §5。

---

## Context

### 現況

三個 `tasks/` 狀態檔都是「多寫者、單檔、高頻寫入」，但寫入模式不同：

1. **`tasks/lessons.md`（累積型）** — CLAUDE.md「Self-Improvement Loop」段規定每逢糾正／自我修正／非顯然決策就追加條目；triage（todo.md 常設觸發：Active ≥ 15 或 phase 收尾）再把條目從 `## Active` 區搬到 `## Archived` 區。追加與搬移都落在同一檔案的相鄰行，兩個並行寫者幾乎保證衝突。`.claude/hooks/session-init.sh` 以 awk 解析 `## Active`／`## Archived` 分區邊界注入，對檔案結構有硬相依。

2. **`tasks/checkpoint.md`（快照型）** — 檔頭自述「唯一續接入口」，「如何接上」段明文「任務完成後回來更新本檔（覆寫……不需保留歷史版本）」。覆寫式單檔在單一協調者時零衝突；出現第二個常設寫者（並行 orchestrator session 或第二位成員）時，每次交接都是全檔衝突。

3. **`tasks/bdd-progress.md`（帳面型）** — 「目前進度」段的計數與「下一個」欄位可完全由 `grep -rn "@ignore" backend/tests/FunctionalTests/Features/` 推導（檔案自己的「如何找到下一個場景」段即用此指令），`scripts/bdd-lint.sh` 已做帳面一致性檢查。手寫的可推導資料 = 衝突面 + drift 面。

### 問題嚴重度

- lessons 是全 repo 寫入頻率最高的狀態檔（每 session 至少一寫），且 session-init 注入依賴其結構 — 衝突解錯會直接汙染每個後續 session 的注入內容。
- checkpoint／progress 目前實測壓力低（單協調者；場景實作因 `docs/orchestration.md` §1.5 build-gate 並行規則天然串行），為它們立即建機制違反已裁決的制度凍結啟發式（機制只能事故驅動）。本 ADR 由使用者主動提出團隊尺度需求成案（2026-07-06），不屬投機立法；但實作範圍仍依使用者同日裁決收斂為「lessons 先拆＋其餘定規格」。

### 不決定會發生什麼

不固化的話，第一次雙 session 並行就會在 lessons.md 撞衝突，且解衝突者必須理解 Active／Archived 分區語意才不會把條目解進錯區 — 這種「解衝突需要領域知識」的檔案正是團隊協作的隱性地雷。

---

## Decision

### 1. lessons 拆為一檔一教訓（立即實作）

`tasks/lessons.md` 退役，改為 `tasks/lessons/` 目錄，一檔一教訓：

```
tasks/lessons/
├── _README.md                       # 原檔頭：觸發條件、分區判準指針（不含教訓內容）
├── 20260705-heredoc-write-tool.md   # 一檔一教訓，檔名 YYYYMMDD-kebab-slug.md
└── ...
```

每檔 frontmatter + 原有欄位：

```markdown
---
date: 2026-07-05
type: correction        # correction | decision | info（沿用現行三類）
status: active          # active | archived（ADR-013 決策 (b) 判準不變）
---
# heredoc 寫檔在本 harness 不可靠 — 寫檔用 Write 工具

**Context:** ...
**Rule:** ...
**落地:** ...（歸檔時必填，指向接管的機械化 gate）
```

- **新增教訓 = 新增檔案** — 兩個並行寫者的檔案集天然不相交，衝突面歸零。
- **歸檔 = 改該檔 frontmatter 的 `status` 一行** — 不再跨區搬移文字。
- `.claude/hooks/session-init.sh` 改為 glob `tasks/lessons/*.md`（排除 `_README.md`），解析 frontmatter，只注入 `status: active` 條目的標題 + `**Rule:**` 行；尾行統計改自 frontmatter 計數。注入內容與現制逐字等價（僅來源結構改變）。
- 既有 26 條（Active 14／Archived 12）一次性遷移，內文逐字搬運不改寫；`date` 取條目原 `**Date:**` 欄。

### 2. checkpoint 分流 — 規格先行，觸發制（本輪不實作）

- **觸發條件**：出現第二個常設寫者 — 兩個以上長期並行的 orchestrator session，或第二位人類成員開始產出 checkpoint。
- **觸發後的形態**：`tasks/checkpoints/<workstream>.md`（workstream slug 於派工／開工時決定）；`tasks/checkpoint.md` 降為路由頁，只列各 workstream checkpoint 的一行指針。「唯一續接入口」語意由路由頁承接，`docs/orchestration.md` §6 冷啟動 prompt 的入口指針**不需修改**。
- 觸發前不建目錄、不動檔案。欄位 schema 維持 `tasks/_templates/checkpoint.md` 不變。

### 3. bdd-progress 帳面生成化 — 規格先行，觸發制（本輪不實作）

- **觸發條件**：與 §2 相同（第二個常設寫者），或帳面衝突實際發生一次。
- **觸發後的形態**：「目前進度」段（已通過計數、「下一個」欄）由腳本自 `@ignore` grep 重產（實作落點建議：`scripts/bdd-lint.sh` 的檢查邏輯反轉為 `--fix` 產生器，不另立新腳本）；手寫內容縮減為 Wave 概覽表與基礎設施解鎖點。「進度檔更新與實作同 commit」規則（CLAUDE.md、`docs/orchestration.md` §2 第 1 條）不變 — 生成動作在 commit 前執行即可。衝突時的處置標準化為「重跑產生器」。

### 4. 明文不在本 ADR 範圍

- `tasks/todo.md`、`tasks/bdd-backlog.md`：混合型／低頻寫入，不處置；除非實際衝突事故，不再議（制度凍結啟發式）。
- 不引入 `.gitattributes merge=union`、不引入外部 issue tracker（見 Alternatives）。
- 需求類型與 BDD 流程分流屬 `docs/adr/adr-022-bdd-requirement-type-routing.md`，不在本 ADR。

### 5. 本 ADR 接受時的同步項目（同 commit）

依 2026-07-06 全 repo 逐字引用掃描（`git grep -n 'lessons\.md'`，106 命中）分類：

- `tasks/lessons/`：目錄建立 + 26 條遷移 + `_README.md`（承載原檔頭與分區判準指針）。遷移須保留條目內既有的 `zh-lint:allow` 行級標記（ADR-009 佈設）。
- `tasks/lessons.md`：刪除。
- `.claude/hooks/session-init.sh`：注入來源改 glob + frontmatter 解析（見 §1），註解同步更新。
- **`scripts/hook-smoke.sh`：斷言來源同步改讀 `tasks/lessons/`**（現以 `tasks/lessons.md` 動態抽取斷言內容；不同步改則 ci-checks fast 立即紅）。
- `.claude/hooks/pre-tool-bash.py`：錯誤訊息內「見 tasks/lessons.md heredoc 條」指針改指新載體檔案。
- `.claude/skills/lesson/SKILL.md`：寫入目標與格式改為一檔一教訓。
- `.claude/skills/bdd-vertical-slice/SKILL.md`：步驟 3 的 lessons 指針一行同步。
- `CLAUDE.md`：「Self-Improvement Loop」段與「Task Management Protocol」段的 `tasks/lessons.md` 字樣改為 `tasks/lessons/`。
- `docs/verification-matrix.md`：主表引用「tasks/lessons.md Archived「…」」與「Active 區」的各行（9a／9b／9c／15／16／21／23／23a）指針改指新載體。
- `scripts/source-lint.sh`、`scripts/git-hooks/commit-msg`、`.claude/hooks/post-edit-validate.sh`：註解內 lessons 指針同步。
- `tasks/todo.md`：「Lessons triage 常設觸發」項的載體敘述同步（判準與門檻不變）。
- `tasks/checkpoint.md`：「如何接上」段的注入敘述同步。
- 歷史文件不回改：`docs/adr/` 既有 ADR 本文、`tasks/archive/`、`tasks/process-improvement-plan.md`（checkpoint 已定性為歷史盤點紀錄）。
- 驗收指令見 Implementation Rules。

---

## Rationale

### 為什麼是「一檔一事實」而不是更聰明的合併策略

repo 內已有同構的成功先例：`docs/adr/` 一檔一決策、`tasks/archive/` 一檔一紀錄 — 新增即新檔，從未發生衝突。lessons 的寫入模式（追加事實、狀態遷移）與 ADR 完全同構，套用同一模式是最小驚訝原則；合併策略類方案（union merge）只緩解追加、不緩解 triage 搬移，見 Alternatives A。

### 為什麼 checkpoint／progress 只定規格不實作

制度凍結啟發式（lessons `## Active` 既有裁決）：機制跟著觀察到的失敗走。lessons 的衝突壓力是結構性的（每 session 必寫）；checkpoint／progress 的多寫者壓力目前為零或被並行規則天然抑制。先把規格寫死、觸發條件明文，屆時實作零設計成本，但不提前支付機制維護費。此範圍收斂為使用者 2026-07-06 裁決。

### 為什麼歸檔用 frontmatter 而不是搬移到 archive 子目錄

`git mv` 到子目錄同樣無衝突，但 session-init 的 glob 與統計要同時處理兩個位置；frontmatter 單欄位翻轉讓「歸檔」成為單行 diff，review 一眼可核，且檔案路徑穩定 — 其他文件對單條 lesson 的引用不會因歸檔而斷鏈。

---

## Consequences

### Positive

- lessons 寫入衝突面歸零；解衝突不再需要理解分區語意。
- 歸檔操作從「跨區搬移大段文字」降為單行 frontmatter diff，triage 成本下降。
- 單條 lesson 有穩定路徑，可被 spec／ADR 以檔名精準引用。
- checkpoint／progress 的團隊化路徑已定案，觸發時零設計成本。

### Negative / Trade-offs

- 一次性遷移改動面大（26 檔新增 + 引用全修），遷移錯漏會汙染注入。
  - Mitigation: 遷移逐字搬運不改寫；驗收含「注入輸出與遷移前逐字 diff 等價」的機械比對（Implementation Rules 3）。
- `session-init.sh` 需新增 frontmatter 解析，複雜度略升。
  - Mitigation: 解析限定「frontmatter 三欄 + 標題行 + Rule 行」，綠＋故意紅（合成 payload）取證；`scripts/machinery-check.sh` 既有自體健檢涵蓋 hook 存活。
- 教訓總量大時目錄檔案數增長。
  - Mitigation: 現量 26 檔、年增量以十計，遠低於目錄可維護上限；歸檔條目不注入，token 成本不隨檔案數增長。

---

## Alternatives Considered

### Alternative A: 保留單檔，`.gitattributes` 設 `merge=union`

Rejected. union merge 只對「雙方各自在檔尾追加」安全；lessons 的實際寫入含 triage 搬移（Active → Archived）與同區中段插入，union 會把兩側改動無腦串接，產生重複條目或條目落錯分區 — 錯誤是靜默的，比顯式衝突更危險。

### Alternative B: 保持單檔，約定「lessons 寫入必須串行」

Rejected. 行為約定無機械保證（違反「驗證優先機械化」，`docs/orchestration.md` §1 明文規則 (i)），並行 session 情境下必然被違反；且約定本身成為新的注入／記憶負擔。

### Alternative C: 外部系統承載（issue tracker／資料庫）

Rejected. lessons 是 session-init 注入來源，必須離線可讀、可 grep、可隨 repo checkout 攜帶；外部系統破壞 repo-first 與可審計 diff，並新增一個憲章外的依賴面。

### Alternative D: 三檔全套本輪實作（checkpoint 分流 + progress 生成化一併落地）

Rejected. 使用者 2026-07-06 裁決採「lessons 先拆＋其餘定規格」：checkpoint／progress 現無實測多寫者壓力，立即實作違反制度凍結啟發式；規格與觸發條件已在 §2／§3 定案，觸發時零設計成本。

---

## Implementation Rules

1. 新增教訓一律寫入 `tasks/lessons/YYYYMMDD-kebab-slug.md`（frontmatter 三欄 `date`／`type`／`status` 必填），不得再建立集中式教訓檔。
2. 歸檔一律以該檔 frontmatter `status: active` → `archived` 完成，同時補「**落地:**」欄指向接管 gate；不得移動或刪除檔案。
3. 遷移驗收：遷移 commit 內，`session-init.sh` 新版輸出的「標題 + Rule 行」序列必須與遷移前舊版輸出逐字等價（順序允許重排為檔名序）；以實跑輸出 diff 取證。
4. `session-init.sh` 改版必須「綠＋故意紅」：合成一條 `status: active` 假教訓證明會注入、合成 `status: archived` 證明不注入、frontmatter 缺欄證明 fail-loud 或安全略過（擇一並註明）。
5. checkpoint 分流與 progress 生成化在 §2／§3 觸發條件成立前不得實作；觸發時依本 ADR 規格執行，不需新開 ADR（形態變更才需要）。
6. **驗收**：

   ```bash
   # lessons.md 已退役，無殘存指涉（歷史文件豁免：docs/adr/、tasks/archive/、
   # tasks/process-improvement-plan.md、git log）。pattern 取全路徑精確比對 —
   # 裸 'lessons\.md' 會誤中 adr-008 檔名尾碼（...-pending-lessons.md）。
   test ! -f tasks/lessons.md
   git --no-pager grep -n 'tasks/lessons\.md' -- . \
     ':!docs/adr/' ':!tasks/archive/' ':!tasks/process-improvement-plan.md'
   # 預期 0 命中
   ```

7. 任何提案修改 1–N，必須先開新 ADR。
