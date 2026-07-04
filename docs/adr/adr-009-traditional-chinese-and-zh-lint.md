# Repo 文件語言規範：正體中文 + 簡體字機械化防線（zh-lint）

> 「禁用簡體字」原本只存在於使用者的全域層級規則，repo 內無明文、無檢驗。同一天內三次違規事故（兩名 executor 產出、一次 orchestrator commit message）證明此規則對任何等級的模型都需要機械防線。本 ADR 把規則落點定在 repo 內，並建立以 OpenCC 字表為基礎的 lint。

---

## Status

Accepted (2026-07-04)

同步項目：`scripts/zh-lint.sh`（新增）、`scripts/data/opencc-STCharacters.txt`（vendored 字表）、`scripts/ci-checks.sh`（fast 與 full 皆接入 `zh_lint`）、既有違規修正（`.claude/references/dotnet/ef-core.rule.md`、`docs/design/api-spec.md`、`docs/design/design-doc.md` 各一處；`CLAUDE.md` 一處以獨立 commit `adb4fd8` 外科手術式修正，避免混入其未提交的既有改動）、兩處刻意引用行加 `zh-lint:allow` 標記（`tasks/lessons.md`、`tasks/process-improvement-plan.md` §8.3）、`docs/verification-matrix.md` 增列 zh-lint 行並自「無防線區塊」移除對應項、lessons [correction] 落地欄位更新。無需在 `CLAUDE.md` 新增規則條文 — 規則本體在本 ADR（依 ADR-007 規則 1），驗證矩陣與 lint 錯誤訊息已提供發現路徑。

---

## Context

### 實證事故（2026-07-04 一日之內）

1. Phase A executor（Sonnet 級）在 ADR-007 Rationale 寫出「執行」的簡體寫法，orchestrator review 以手寫字元清單掃描攔下。
2. Phase C executor（Sonnet 級）在驗證矩陣寫出「確定」的簡體寫法 — 而 orchestrator 的手寫掃描清單**漏了這個字**，第二次擴充清單時又發現 grep 的多位元組字元類在某些 locale 下按 byte 比對，產生大量誤報，被迫改用 Python。
3. Orchestrator（大型模型）本人在 commit message 中寫出「簡體字」的簡體寫法，事後 amend 修正。

### 結構性結論

- 手寫字元清單**兩度漏字**：這類清單依賴撰寫者記憶，本質上與「靠人工記得規則」同構，正是防線迴圈要消除的模式。
- 違規者涵蓋 executor 級與 orchestrator 級模型：這不是「弱模型才會犯」的問題，任何依 token 機率生成中文的模型都可能混入簡體字形。
- lint 以完整字表首次執行時，在 repo 既有文件中找到 **8 處**所有先前人工掃描都漏掉的真實簡體字（含 `CLAUDE.md`、`docs/design/api-spec.md`、`docs/design/design-doc.md`、reference 檔），證明人工 review 對此類字元級 drift 沒有可靠檢出率。

### 規則落點問題

使用者全域規則（個人設定）不隨 repo 移動，非 Claude harness 或其他協作者看不到。依 ADR-007 規則 1，repo 級規則需要 Accepted ADR 作為權威來源 — 即本 ADR。

---

## Decision

### 1. 規則：repo 內 tracked 檔案的中文一律使用正體中文

適用所有 git tracked 的文字檔（文件、程式碼註解、腳本內字串）。英文內容不受影響。commit message 不在機械化掃描範圍（見「不在本 ADR 範圍」），但同樣適用此規則。

### 2. 機械化：`scripts/zh-lint.sh`，字表 vendored 自 OpenCC

```bash
bash scripts/zh-lint.sh   # 掃描全部 tracked 文字檔，發現簡體字元即 exit 1
```

- 字表：`scripts/data/opencc-STCharacters.txt`，自 OpenCC 專案（Apache-2.0）vendored 的完整簡繁對照表（4000+ 條），**不使用手寫清單**。
- 判定規則：字元是 OpenCC 對照表的 key、且其對應正體字集合**不含自身** → 視為簡體字元。（例：某些字 s→t 對應包含自己，屬簡繁同形，合法不攔。）
- 純 Python stdlib 實作，本機與 CI（ubuntu）皆無需安裝任何相依套件。
- 接入 `scripts/ci-checks.sh` 的 **fast 與 full** 兩模式（維持 fast ⊂ full 不變式）。

### 3. Variant 白名單

OpenCC 視為需轉換、但屬台灣標準或通行字形的字元，列入 lint 內明文白名單（初始集合：「群」「秘」）。擴充白名單是規則變更，**必須開新 ADR**，不得默契追加。

### 4. 行內豁免標記 `zh-lint:allow`

刻意引用違規字元的行（如 lessons 記錄事故原文），必須在**同一行**加上 `zh-lint:allow` 字樣（markdown 中可用 HTML 註解包裹使其不顯示）。豁免以行為粒度 — 不提供檔案級豁免。

### 不在本 ADR 範圍

- commit message 不做機械化掃描（git hook 可攔 `commit-msg`，但目前事故率低且訊息不進檔案內容；若再犯，開新 ADR 補上 `commit-msg` hook）。
- 不追溯改寫 git 歷史中的違規（歷史 commit 訊息與舊版本內容維持原樣）。
- 不規範英文文件的用語風格。
- OpenCC 字表升級不自動進行（見 Implementation Rules 4）。

---

## Rationale

### 為何用 vendored OpenCC 字表而不是手寫清單

手寫清單在同一天內兩度漏字（實證），且每次擴充都是一次「靠記憶」的賭注。OpenCC 字表由上游社群長期維護、涵蓋完整，vendored snapshot 讓檢驗結果可重現且離線可用。

### 為何 vendored 而不是依賴系統安裝的 opencc

本機（macOS）與 CI（ubuntu）都需要額外安裝步驟，任一環境漏裝就是一層靜默失效；純資料檔 + Python stdlib 沒有安裝面。字表是穩定資料，不需要跟隨上游即時更新。

### 為何白名單而不是直接改字表

字表維持與上游 byte-identical，升級時可直接 diff 驗證；台灣慣用字形的取捨是**本 repo 的規則**，屬於 lint 邏輯層，寫在 script 內並由本 ADR 管轄，兩層責任分離。

### 為何行級豁免而不是檔案級

檔案級豁免（如整份 lessons.md 排除）會讓該檔案未來的新增違規全部漏檢 — lessons.md 恰好是 executor 頻繁寫入的檔案。行級標記把豁免範圍縮到最小，且標記本身在 review diff 中可見。

---

## Consequences

### Positive

- 字元級 drift 首次有紅綠燈：任何模型（含 orchestrator 級）寫出簡體字，commit 當下即被攔下，不再依賴 review 檢出率。
- 首跑即清償既有欠債：8 處真實違規全數修正，repo 達成基線全綠。
- 規則落點進 repo（本 ADR），非 Claude harness 的協作者透過 `AGENTS.md` → 驗證矩陣路徑可發現此規則。

### Negative / Trade-offs

- 引用違規字元的合法場景需要記得加 `zh-lint:allow` 標記，多一步操作。
  - Mitigation: 忘記加標記的結果是 lint 紅燈 + 錯誤訊息直接提示標記寫法，屬 fail-safe 方向，不會靜默放行。
- Variant 白名單可能隨文件增長需要擴充（台灣慣用字形不只兩個）。
  - Mitigation: 擴充走 ADR 通道，單字元級 ADR 成本低；lint 錯誤訊息會精確指出字元與位置，判斷成本小。
- vendored 字表與上游可能隨時間偏離。
  - Mitigation: 字表內容是穩定的簡繁對應，變動率極低；升級程序在 Implementation Rules 4 明文化。

---

## Alternatives Considered

### Alternative A：手寫簡體字元清單

Rejected. 同一天內兩度漏字的實證；清單完整性依賴撰寫者記憶，與「規則只活在文件裡」同構，違反防線迴圈核心命題。

### Alternative B：依賴系統安裝的 OpenCC（brew / apt + python binding）

Rejected. 本機與 CI 都增加安裝步驟與版本矩陣；任一環境漏裝即靜默失效。純資料檔可攜性完勝，且此用例只需查表、不需轉換引擎。

### Alternative C：維持人工 review 掃描（orchestrator 責任）

Rejected. 同日三次事故、其中一次出自 orchestrator 本人；lint 首跑找到 8 處歷史漏網。人工檢出率實證不可靠，且 orchestrator 退場後此防線隨之消失。

### Alternative D：檔案級豁免清單（allowlist 檔案路徑）

Rejected. 粒度過寬 — 被豁免檔案的未來新增違規全部漏檢，而高頻寫入的 lessons.md 正是最需要持續檢驗的檔案。行級標記把豁免縮到最小可見單位。

---

## Implementation Rules

1. repo 內 tracked 檔案的中文一律使用正體中文；`scripts/zh-lint.sh` 於 `scripts/ci-checks.sh` 的 fast 與 full 模式強制執行，紅燈禁止 commit / push / merge。
2. 刻意引用簡體字元的行，必須於同一行加 `zh-lint:allow` 標記；不得使用檔案級豁免。
3. Variant 白名單（lint 內 `ACCEPTED_VARIANTS`）的任何增刪，必須先開新 ADR。
4. `scripts/data/opencc-STCharacters.txt` 為 vendored snapshot，升級必須：開新 ADR、與上游 diff 審視、升級後全 repo 重跑 `zh-lint.sh` 綠才可合入。
5. 驗收命令：

   ```bash
   bash scripts/zh-lint.sh                          # 預期 exit 0
   grep -n "zh_lint" scripts/ci-checks.sh           # 預期 fast 與 full 兩分支皆有呼叫
   ```

6. 任何提案修改 1–5，必須先開新 ADR。
