# 知識帳面生命週期：ADR 索引機械化、phase 收尾清掃義務、plan 檔退役歸檔

> Lead-in：ADR 累積至 28 份而無總覽、活帳面（`tasks/todo.md`）滯留已結案內容、盤點 plan 檔早已被定性為歷史紀錄卻仍佔活帳面位置。本 ADR 一次固化三件知識帳面的生命週期規則：ADR 索引（含 lint 一致性檢查）、phase 收尾清掃義務、`tasks/process-improvement-plan.md` 退役歸檔。

---

## Status

Accepted (2026-07-12)

同步項目（同 commit）：`docs/adr/README.md`（新建索引，28 列）、`scripts/adr-lint.sh`（新增檢查 8：索引雙向一致性；附帶修復檢查 6／7 的 subshell 計數缺陷，若經故意紅證實）、`CLAUDE.md`（Validation 段結構性 lint 檢查清單補「索引一致」）、`docs/README.md`（「架構決策紀錄」段改為索引指針）、`docs/verification-matrix.md`（主表 adr-lint 行子檢查數更新、無防線區塊補收尾清掃列、3 處 plan 檔路徑改指 `tasks/archive/`）、`docs/orchestration.md`（§6 治理註記內 plan 檔路徑改指 `tasks/archive/`，僅路徑重定向、無語意變更）、`tasks/_templates/checkpoint.md`（出處註記路徑同步）、`scripts/machinery-check.sh`（3 處出處註解路徑同步）、`tasks/process-improvement-plan.md`（git mv 至 `tasks/archive/`＋檔頭歸檔註記）、`tasks/todo.md`（遷入 2 個殘餘開放項＋已結案段落移出）、`tasks/archive/todo-closed-2026-07-12.md`（新建，本輪清掃產物）、`tasks/checkpoint.md`（plan 檔路徑 4 處＋「如何接上」儀式句補清掃義務）。

---

## Context

### 現況

- `docs/adr/` 已達 27 份（本 ADR 為第 28 份），無索引。「現行裁決是什麼」「這議題是否已裁決過」的反查是 lessons 明文義務（「制度修訂提案前必須反查既往 ADR」條），現行成本＝逐檔開啟；本 ADR 成案過程即實際支付了一次。
- `docs/README.md` 的「架構決策紀錄」段只列 ADR-001～003（實際 27 份）——手寫清單無機械化檢查必然 drift 的現場證據，與本 repo「根因 2：規範只以文字存在，違規可以靜默存在」的既有診斷同型。
- `tasks/todo.md` 檔頭自述「開放項登記簿」，但 2026-07-10 已結案的兩節（Codex harness parity、Secret Scanner 批次自動撤銷）仍滯留檔內；`tasks/lessons/20260711-stale-directive-propagation.md` 已記錄「過期內容滯留活帳面被逐字轉抄擴散」的實際事故。
- `tasks/process-improvement-plan.md`：`tasks/checkpoint.md` 檔頭與「如何接上」段已定性其為「歷史盤點紀錄（非必讀）」，`docs/adr/adr-007-process-governance.md` 明文其「不構成規範來源」——但檔案仍留在 `tasks/` 活帳面層，殘餘開放項（zh-lint 掃描範圍觀察、Tessl 擱置裁決）混在四百餘行歷史紀錄內，等同以位置訊號否定既有定性。
- 2026-07-10 的 todo.md 歸檔 pass（`tasks/archive/todo-closed-2026-07-10.md`）是成功先例，但屬一次性動作、未成常設義務——同型清理靠「誰想起來誰做」。

### 問題嚴重度

- 索引缺失讓「反查既往裁決」義務的執行成本隨 ADR 數線性上升，反查省略的直接後果是重提已拒絕方案（lessons 已有對應糾正條目）。
- 已結案內容滯留活帳面的傷害已有實證：stale-directive 事故中過期指示滯留 6 天、被未驗證轉抄四處。

### 不決定會發生什麼

三件事各自都有「下次有人想到再做」的暫時解，但無規則承載時：索引會像 `docs/README.md` 的 ADR 清單一樣靜默過期；清掃 pass 不會再發生第二次；plan 檔繼續以活帳面位置誤導接手的 agent 與人。

---

## Decision

### 1. ADR 索引 `docs/adr/README.md` ＋ adr-lint 一致性檢查

- 新建 `docs/adr/README.md`：一行一 ADR（編號連結、Accepted 日期、標題）。索引只承載導覽資訊，不複寫決策內容（比照 `docs/adr/adr-007-process-governance.md` 規則 5 對指針文件的要求）。
- 新 ADR Accepted 的同 commit 必須在索引加對應列——這是既有「同步項目同 commit」義務的特例化，由機械檢查承載而非記憶。
- `scripts/adr-lint.sh` 新增檢查 8（索引一致性，雙向）：每個 `docs/adr/adr-*.md` 檔名必須出現在索引；索引引用的每個 `adr-*.md` 檔案必須存在；索引檔缺失本身即為違規。與檢查 5（檔名編號）相同，僅在全集 lint 時執行。
- `docs/README.md` 的「架構決策紀錄」段改為指向索引的單一指針，不再逐份手列 ADR：

```diff
- - [ADR-001: Tech Stack](./adr/adr-001-tech-stack.md) — 技術選型決策
- - [ADR-002: Project Structure](./adr/adr-002-project-structure.md) — 專案結構與架構模式
- - [ADR-003: Error Handling and Cross-BC Contracts](./adr/adr-003-error-handling-and-cross-bc-contracts.md) — …
+ - 完整索引見 [adr/README.md](./adr/README.md)（一行一 ADR，lint 機械化維持一致）
```

### 2. phase 收尾清掃義務（人工儀式，明文不機械化）

- phase 收尾更新 `tasks/checkpoint.md` 時（與 `scripts/failure-triage.sh` 同一時機），必須巡一遍活帳面（`tasks/todo.md` 為主），把「已結案」的段落／條目移至 `tasks/archive/`。作法依 2026-07-10 先例：一檔一 pass（`todo-closed-YYYY-MM-DD.md`）、內容逐字保留、原檔留一行歸檔指針。
- 「已結案」判定屬語意判斷，明文不機械化（比照驗證矩陣無防線區塊「Refactor 紀律」「Guard 正負配對」兩個既有的不機械化裁決）；義務由 `tasks/checkpoint.md`「如何接上」儀式句承載，並登記於 `docs/verification-matrix.md` 無防線區塊誠實列示。

### 3. `tasks/process-improvement-plan.md` 退役歸檔

- `git mv tasks/process-improvement-plan.md tasks/archive/process-improvement-plan.md`，檔頭加歸檔註記，內文逐字保留（含既有 `zh-lint:allow` 行級標記）。
- 殘餘開放項遷入 `tasks/todo.md`：zh-lint 掃描範圍觀察（入 Non-blocking follow-ups）、Tessl 擱置裁決（入觸發制擱置項）。
- 原則化：`tasks/` 根層只承載活帳面；被定性為歷史紀錄的檔案一律移 `tasks/archive/`（與 `docs/adr/adr-021-shared-state-files-team-scale.md`「一檔一紀錄」同構）。
- 舊路徑引用的處置分兩類：受 `scripts/machinery-check.sh` 指針檢查的活文件（`docs/orchestration.md`、`docs/verification-matrix.md`）與活帳面（`tasks/checkpoint.md`、`tasks/_templates/checkpoint.md` 出處註記）同 commit 更新；既有 ADR 本文與 `tasks/archive/` 內的歷史引用不回改（ADR-021 同步項目「歷史文件不回改」先例）。

### 4. 不在本 ADR 範圍

- 不新增索引自動生成腳本（見 Alternative A）。
- 不變更 checkpoint 欄位 schema——`tasks/_templates/checkpoint.md` 欄位結構不動，本 ADR 對該模板的同步僅限出處註記的路徑重定向。
- 不處置其餘 `tasks/` 檔案的形態（`tasks/todo.md` 的合併衝突議題已由 ADR-021 裁決不處置，本 ADR 的清掃義務不改變其單檔形態）。
- lessons 的生命週期已由 ADR-013／ADR-018／ADR-021 承載，本 ADR 不重複規範。

---

## Rationale

### 為什麼手寫索引＋lint，而不是生成腳本

ADR 新增頻率低（一次一列），手寫成本近零；drift 的危險不在「寫錯」而在「忘記寫」與「靜默過期」，雙向 lint 檢查已把這兩者都轉成 commit 前的紅燈。生成腳本是一個新機制維護面（腳本自身可壞、可 drift），在頻率這麼低的寫入場景，增量價值不成比例——與制度凍結啟發式的最小機制傾向一致。

### 為什麼收尾清掃不機械化

「已結案」不是字樣問題而是裁決語意問題（同一個「✅」可以是子項完成而段落仍開放）。字樣比對必然誤報／漏報，而誤搬活項目比漏搬結案項目傷害更大。本 repo 已有兩個同型裁決（Refactor 紀律、Guard 正負配對皆因「語意判斷、機械比對必然誤報」不機械化），本 ADR 沿用同一判準，並以「與 failure-triage 綁同一時機」把遺忘機率壓到既有儀式的水準。

### 為什麼 plan 檔要移走，而不是原地標註

檔案位置本身是訊號：`tasks/` 根層＝活帳面，是每個接手 session 的掃描面。stale-directive 事故的根因正是歷史內容佔據活位置後被當作現行指示轉抄。checkpoint 已把 plan 檔定性為歷史紀錄，位置與定性不一致正是本 ADR 要終結的矛盾；原地 tombstone 只解決「讀了會知道」，不解決「先被掃到」。

---

## Consequences

### Positive

- 「現行裁決是什麼」的反查成本從逐檔開啟降為讀一份索引；反查義務（lessons 條目）的執行阻力下降。
- 「`tasks/` 根層＝開放項」成為可依賴的不變式，接手 agent 的掃描面縮小且無過期內容污染。
- 清掃從一次性善舉升為常設義務，且掛在既有儀式（failure-triage 時機）上，不新增獨立流程。

### Negative / Trade-offs

- 每份新 ADR 多一個同 commit 動作（索引加列）。
  - Mitigation: adr-lint 檢查 8 在 commit 前即紅，遺忘的修復成本是一行 diff；不依賴記憶。
- 收尾清掃無機械防線，可能再度遺忘。
  - Mitigation: 儀式句與 failure-triage 同時機掛載（既有習慣），驗證矩陣無防線區塊誠實登記，loop 巡檢時可稽核執行紀錄（archive 檔案有日期戳）。
- 歷史文件內殘留的舊路徑（不回改）會讓讀者循指針撲空。
  - Mitigation: 舊路徑的新終點 `tasks/archive/process-improvement-plan.md` 檔頭有歸檔註記說明遷移與殘項去向；豁免範圍與 ADR-021 既有先例一致。

---

## Alternatives Considered

### Alternative A: 索引由腳本自動生成（掃描檔名＋標題重產 README）

Rejected. 生成器是新機制維護面：腳本自身需要「綠＋故意紅」、需要登記矩陣、需要跨 harness 考量，而它服務的寫入頻率是「每份 ADR 一次」。手寫一列＋雙向 lint 已消除 drift 的靜默性；生成器只把「一行手寫」變成「跑一個指令」，增量價值不成比例。

### Alternative B: 不建索引，靠檔名 grep 與 `git log`

Rejected. 反查義務的實際成本已發生（本 ADR 成案前的既往裁決反查需掃全部 Alternatives 段）；檔名是 kebab 縮寫、無 Accepted 日期，無法承載一眼總覽；且「哪些議題已有裁決」正是新接手 agent 的第一個問題，逐檔開啟與 28 份的規模不相稱。

### Alternative C: 收尾清掃機械化（lint 掃「已結案」「✅」字樣強制搬移）

Rejected. 判定屬語意（結案與否取決於裁決內容，非字樣；todo.md 內大量「✅ 子項完成」出現在仍開放的段落）。字樣比對必然誤報，誤搬活項目的傷害大於漏搬結案項；比照 Refactor 紀律與 Guard 正負配對兩個既有不機械化裁決。

### Alternative D: plan 檔原地保留＋檔頭 tombstone 註記

Rejected. tombstone 只在「被讀到之後」生效，不改變「先被掃到」——`tasks/` 根層是接手 session 的掃描面，位置訊號比檔頭文字更早起作用。stale-directive 事故已實證歷史內容佔活位置的轉抄風險；且 checkpoint 已定性其為歷史紀錄，位置應與定性一致。

### Alternative E: 索引放 `docs/README.md`（擴寫既有「架構決策紀錄」段）

Rejected. `docs/README.md` 是設計文件導覽（Step 1–5 閱讀順序），ADR 索引與 `docs/adr/` 目錄同居才能就近 lint、目錄內自足；且該段已實證 drift（僅列 3／27 份）——把權威清單放回同一個沒有機械檢查覆蓋慣性的位置，等於重演。

---

## Implementation Rules

1. 新 ADR Accepted 的同 commit，必須在 `docs/adr/README.md` 加對應列（編號連結＋Accepted 日期＋標題）。
2. `scripts/adr-lint.sh` 檢查 8 維持雙向：`adr-*.md` 檔案缺索引列＝紅；索引列指向不存在的檔案＝紅；索引檔本身缺失＝紅。
3. phase 收尾更新 `tasks/checkpoint.md` 前，與 failure-triage 同一時機巡活帳面（`tasks/todo.md` 為主），已結案項移至 `tasks/archive/`：一檔一 pass、內容逐字保留、原檔留一行歸檔指針。
4. `tasks/` 根層只承載活帳面；被定性為歷史紀錄的檔案移至 `tasks/archive/`。搬移時，受 `scripts/machinery-check.sh` 指針檢查的活文件引用同 commit 更新；既有 ADR 本文與 `tasks/archive/` 內的歷史引用不回改。
5. **驗收**：

   ```bash
   scripts/adr-lint.sh          # 含檢查 8，預期綠
   test ! -f tasks/process-improvement-plan.md \
     && test -f tasks/archive/process-improvement-plan.md && echo OK
   scripts/machinery-check.sh   # 活文件指針完整性，預期綠
   ```

6. 任何提案修改 1–5，必須先開新 ADR。
