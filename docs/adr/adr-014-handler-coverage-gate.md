# Handler coverage gate — DoD「≥ 80%」門檻機械化與度量解讀固化

> Lead-in：`CLAUDE.md` DoD 自 repo 初期即明載「unit coverage ≥ 80% for Handler code」，但 `docs/verification-matrix.md` 無防線區塊誠實登記此規則「未追蹤」。本 ADR 固化三件事：度量來源的解讀（全測試套件，含 BDD）、鎖定對象（concrete `*Handler` 類別，每類各自達標）、執行機制與時機層（coverlet + `scripts/coverage-check.sh`，full gate）。

---

## Status

Accepted (2026-07-05)

同步項目（本 ADR 接受時必須同 commit 落地）：
- `scripts/coverage-check.sh`（新增：cobertura 解析與門檻判定）
- `scripts/ci-checks.sh`（full 模式接線：測試段以 collector 收集、其後呼叫 coverage check）
- `docs/verification-matrix.md`（主表新增本檢驗一行；無防線區塊「Unit test coverage ≥ 80%（Handler code）」條標記已機械化並移出）
- `tasks/todo.md`（Coverage gate 計畫段勾選落地項）

---

## Context

### 現況

- `CLAUDE.md` §4 Verification Standards 明載：「unit coverage ≥ 80% for Handler code」。
- `docs/verification-matrix.md` 無防線區塊第一條登記：「未追蹤——`coverlet.collector` 僅作為測試 SDK 依賴出現於 `.csproj`，`scripts/ci-checks.sh` / `.github/workflows/ci.yml` 均無涵蓋率門檻或報表解析步驟」。
- `backend/Directory.Packages.props` 已集中管理 `coverlet.collector` 版本——收集能力存在，僅缺門檻判定。
- 基線實測（2026-07-05，BDD 場景 4/44 時點）：`CreateApiKeyHandler`（含 compiler-generated async state machine 併回母類）line coverage 89.1%（55 行中 49 行命中）；未命中的行全部是尚未解鎖場景對應的 guard 早退分支。

矛盾：規則存在多月而無任何機制驗證，違反 `docs/orchestration.md` 明文規則 (i)「驗證優先機械化」——這正是無防線區塊設立的目的：照實標注，等待逐條機械化。

### 易混淆概念釐清

| 概念 | 本 ADR 是否規範 |
|---|---|
| 「unit coverage」的度量來源 | ✅ 固化為「全測試套件（含 BDD FunctionalTests）對 Handler 類別的 line coverage」 |
| 門檻數值 80% | ❌ 沿用 `CLAUDE.md` DoD 既有值，本 ADR 不創設也不修改數值；改數值須開新 ADR 並同 commit 改 `CLAUDE.md` |
| 非 Handler 程式碼（Domain / Repository / Endpoint / Service）的 coverage 門檻 | ❌ 不設；該範圍的正確性由 BDD wire-contract 鎖定與架構測試承擔 |
| Mutation testing（測試殺傷力） | ❌ 不在本 ADR；依既定排程於 Wave 1 全綠後另議（`tasks/checkpoint.md`） |

### 不決定會發生什麼

DoD 的 coverage 條文將持續處於「規則存在、無人驗證」狀態；隨 BC 增加，Handler 數量增長，事後補門檻的成本（歷史欠帳盤點、逐一豁免談判）遠高於在只有一個 Handler 且基線已達標時上線。

---

## Decision

### 1. 度量來源 = 全測試套件（含 BDD）

「unit coverage ≥ 80% for Handler code」解讀為：`scripts/ci-checks.sh` full 的測試執行（unit + architecture + BDD functional）以 `XPlat Code Coverage` collector 收集，對 Handler 類別計算 line coverage。

```bash
# before：full gate 的測試段
dotnet test backend/ApiKeyManagement.slnx --configuration Release --no-build

# after：同一次執行附掛 collector（不另跑第二輪測試）
dotnet test backend/ApiKeyManagement.slnx --configuration Release --no-build \
  --collect:"XPlat Code Coverage" --results-directory "$COVERAGE_DIR"
```

### 2. 鎖定對象 = concrete `*Handler` 類別，每類各自 ≥ 80%

- 判定集合與 `backend/tests/Architecture.Tests/HandlerResultReturnTests.cs` 的鎖定邏輯一致：concrete（非 abstract / 非 interface）、名稱以 `Handler` 結尾的類別。
- cobertura 報表中 compiler-generated async state machine（形如 `CreateApiKeyHandler/<HandleAsync>d__5`）併回母類合併計算。
- 逐類判定：任何一個 Handler 類別 line coverage 低於 80% 即整體失敗（exit 非零）並列出類名與數字；不採全體聚合平均（聚合會讓高覆蓋老 Handler 掩護零覆蓋新 Handler）。

```text
# 失敗輸出範例
[coverage-check] FAIL ApiKeyManagement.KeyLifecycle.RevokeKey.RevokeKeyHandler: 41.2% < 80%
```

### 3. 執行機制 = `scripts/coverage-check.sh`，時機層 = full gate

- 解析使用 python3 標準庫（`xml.etree.ElementTree`），零新增套件依賴；多份 cobertura 報表（每個測試專案一份）以 per-line max hits 合併後計算。
- 接入 `scripts/ci-checks.sh` **full** 模式，於測試段之後執行；**fast 模式不含**——Handler 覆蓋主要來自 FunctionalTests（需 Docker），fast 層本來就不跑測試。
- 門檻值 80 以常數形式寫在 `scripts/coverage-check.sh` 內，並以註解標注權威來源為 `CLAUDE.md` §4 與本 ADR。

### 4. 範圍邊界

本 ADR 不規範：門檻數值的未來調整（新 ADR）、branch coverage 或 mutation score（另議）、非 Handler 類別的門檻（不設）、`.github/workflows/ci.yml` 的獨立實作（CI 與本機共用 `ci-checks.sh`，接線一處即全域生效）。

---

## Rationale

### 為什麼度量來源選全套件而不是嚴格 unit-only

本專案是 BDD-first：Handler 程式碼只在實作 BDD 場景時誕生（`CLAUDE.md` BDD cycle），其覆蓋天生來自場景經 HTTP 打進來的路徑。嚴格 unit-only 解讀會要求為每個 Handler 另寫一套與場景邏輯重複的單元測試——增加維護面卻不增加行為驗證強度，且現況 unit-only 覆蓋為零，門檻上線即紅，違反「Green before commit」。DoD 條文的意圖是「Handler 邏輯被測試覆蓋」，全套件解讀忠於意圖。

### 為什麼逐類判定而不是聚合

聚合平均允許「一個 100% 的老 Handler + 一個 0% 的新 Handler」通過，恰好放過最需要攔截的情況。逐類判定讓每個新 Handler 出生即受約束——而 BDD cycle 紀律（場景與實作同 commit、never commit red）保證 Handler 出生時就有場景覆蓋，逐類門檻不會誤傷正常工作流。

### 為什麼放 full 不放 fast

fast 層（commit 前）不執行測試，無 cobertura 產物可判；強行加入等於把「跑全套測試 + Docker」塞進每次 commit，破壞 fast/full 分層的設計初衷。push 前與 CI 的 full gate 已是「行為驗證」的既定時機層，coverage 判定屬同類。

### 為什麼自寫解析腳本而不用現成工具

判定需求只有「逐 Handler 類、state machine 併回、比對 80」——python3 標準庫數十行可完成。引入 ReportGenerator 或 coverlet.msbuild 的 threshold 換來的是 assembly 級或報表級功能，均不提供「類別級、名稱過濾」的精度，卻增加依賴與版本維護面。

---

## Consequences

### Positive

- DoD coverage 條文從紙上規則變為可打勾的機械化 gate；`docs/verification-matrix.md` 無防線區塊再消一條。
- 新 BC 的每個 Handler 自第一天受同一門檻約束，無歷史欠帳談判。
- 基線 89.1% 上線即綠，且未命中行皆為 `@ignore` 場景的 guard 分支——隨場景解鎖，覆蓋單調上升。

### Negative / Trade-offs

- BDD（整合層級）覆蓋計入「unit coverage」，line coverage 高不代表斷言強度足夠（測到 ≠ 測對）。
  - Mitigation: 測試殺傷力維度由已排程的 mutation testing（Stryker.NET，Wave 1 全綠後）補上；過渡期由 executor-spec 範本的「故意紅」義務承擔單點驗證。
- collector 附掛使 full gate 測試段變慢。
  - Mitigation: 只在 full 層收集；coverlet collector 對本 repo 規模的額外開銷為秒級，且不新增第二輪測試執行。
- 未來若出現「合法但無場景覆蓋」的 Handler（如純技術性 job handler），門檻會誤紅。
  - Mitigation: 屆時依治理條款開新 ADR 明文豁免該類別，禁止在腳本內悄悄加白名單。

---

## Alternatives Considered

### Alternative A: coverlet.msbuild 的 `/p:Threshold=80`

Rejected. 門檻施加於整個被收集 assembly（或以 module 為單位），無「僅 `*Handler` 類別」的過濾精度；為湊門檻會被迫把 Domain / Infrastructure 全拉進度量範圍，偏離 DoD 條文的鎖定對象。

### Alternative B: ReportGenerator 產報表 + 解析其輸出

Rejected. 增加一個工具鏈依賴與版本維護面，而其提供的 HTML / 摘要功能非判定所需；判定所需的 class 級數字直接來自 cobertura XML，python3 標準庫即可取得。

### Alternative C: 嚴格 unit-only 度量（只計 unit test 專案的覆蓋）

Rejected. 與 BDD-first 工作流矛盾：現況 Handler 的 unit-only 覆蓋為零，門檻上線即紅；補齊的唯一路徑是為每個 Handler 撰寫與 BDD 場景重複的單元測試，增加維護成本而不增加行為驗證強度。

### Alternative D: 不機械化，依賴 review 時人工判斷

Rejected. 違反 `docs/orchestration.md` 明文規則 (i)「任何能寫成腳本／測試／lint 的檢驗，一律用腳本」；本規則已在無防線區塊掛帳多月，正證明人工判斷不會自發生效。

---

## Implementation Rules

1. `scripts/coverage-check.sh` 存在、可執行（`chmod +x`）、`bash -n` 通過；門檻常數 `80` 寫於腳本內並註明權威來源（`CLAUDE.md` §4 + 本 ADR）。
2. `scripts/ci-checks.sh` full 模式：測試段附掛 `--collect:"XPlat Code Coverage"` 收集至暫存目錄（每次執行前清空），其後呼叫 `scripts/coverage-check.sh`；fast 模式不得呼叫。
3. `scripts/coverage-check.sh` 對每個 concrete `*Handler` 類別（cobertura 中 `/<...>d__N` state machine 併回母類）計算 line coverage；任何一類低於門檻 → exit 非零，輸出格式含類名與百分比；無任何 Handler 報表資料時必須 fail-loud，不得靜默通過。
4. `docs/verification-matrix.md` 主表新增本檢驗一行（機制 = `scripts/coverage-check.sh`，時機層 = push 前 / CI，執行者 = 腳本），無防線區塊對應條目比照既例標記「✅ 已機械化」並移出；與本 ADR 同 commit。
5. 新檢驗以「綠＋故意紅」驗證：綠 = `scripts/ci-checks.sh full` 通過且輸出含 coverage 段；故意紅 = 門檻暫調至高於基線的值（如 95）重跑判定取得失敗輸出後還原。兩份輸出留存於落地 commit 的驗證紀錄（checkpoint 或 commit message）。
6. **驗收**：

   ```bash
   grep -n "coverage-check" scripts/ci-checks.sh
   # 預期：僅 full 段命中
   bash -n scripts/coverage-check.sh && echo OK
   # 預期：OK
   ```

7. 任何提案修改 1–6，必須先開新 ADR。
