# Roslyn analyzer gate — 語意層程式碼品質檢驗從 AI review 移交編譯器

> Lead-in：repo 的機械化防線集中在語法/結構層（grep、reflection、NetArchTest、命名 editorconfig），語意層品質規則（CancellationToken 傳播、rethrow 保留堆疊、字串文化、FluentAssertions 強制）實際全押在 AI review 上，違反協調憲章明文規則 (i)。本 ADR 啟用 .NET 內建 analyzer（latest-recommended、CA 警告升 error）+ BannedApiAnalyzers，一次清償 `docs/verification-matrix.md` 無防線區三條已登記債務，並明文劃定不機械化的邊界。

---

## Status

Accepted (2026-07-05)

同步項目（本 ADR 接受時必須同 commit 落地；測試側修正依 refactor 紀律得為前置獨立 commit）：
- `backend/Directory.Build.props`（`AnalysisLevel` + `CodeAnalysisTreatWarningsAsErrors`）
- `backend/.editorconfig`（規則降級項，每項附理由註解）
- `backend/Directory.Packages.props` + 各測試 `.csproj`（`Microsoft.CodeAnalysis.BannedApiAnalyzers` + `BannedSymbols.txt`）
- `docs/verification-matrix.md`（主表新增；無防線區三條移出、一條加註「裁決不機械化」）
- 既有違規修正（baseline 62 警告歸零：修正或 documented 降級）

---

## Context

### 現況

- 協調憲章明文規則 (i)（`docs/orchestration.md` §1）：「任何能寫成腳本／測試／lint 的檢驗，一律用腳本；AI review 只負責補機械化做不到的部分。」
- 實查（2026-07-05）：`backend/Directory.Build.props` 與 `backend/.editorconfig` 皆無 `AnalysisLevel`／`AnalysisMode` 設定——SDK 預設僅啟用最小 CA 子集且只是 warning；`.editorconfig` 僅一條 diagnostic 嚴重度（IDE1006）。語意層品質檢查實際由 AI review（`docs/verification-matrix.md` 主表第 20 項）獨力承擔。
- `docs/verification-matrix.md` 無防線區塊有三條明寫需 analyzer 級檢驗：`CancellationToken cancel` 傳播（CA2016 直接命中）、禁 `throw ex;`（CA2200 直接命中）、FluentAssertions 強制禁 `Assert.*`（BannedApiAnalyzers 直接命中）。
- Baseline sweep（`dotnet build /p:AnalysisLevel=latest-recommended --no-incremental`，2026-07-05，build 日誌重複輸出去重後）：31 個不重複位置 / 7 規則——CA1707×11（測試命名底線）、CA1310×6 + CA1311×5 + CA1304×5（字串操作未指定文化/比較方式）、CA1861×2（EF Migrations 工具產物）、CA1859×1、CA1848×1；CA2016 與 CA2200 零命中（現況乾淨，gate 屬防患型）。
- 使用者裁決觸發（2026-07-05）：「code analyzer 薄弱，不應該全部交給 AI 進行 code review」。

### 不決定會發生什麼

Wave 1 放量在即，每個新場景都會新增 Handler／step 程式碼；語意層違規（文化敏感字串比較、未傳播的 token）只會在 AI review 記得看的時候被抓，漏檢率隨程式碼量單調上升，事後清欠成本隨之增長——與 coverage gate（ADR-014）相同的時機論證。

---

## Decision

### 1. 啟用內建 analyzer：latest-recommended，CA 警告升 error

```xml
<!-- backend/Directory.Build.props -->
<!-- before：無 AnalysisLevel 設定（SDK 最小子集、僅 warning） -->
<!-- after： -->
<AnalysisLevel>latest-recommended</AnalysisLevel>
<CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>
```

CA 規則違規自此使 build 失敗，時機層與既有 build gate 相同（fast 的 format 段不涉及；full 的 build 段與 CI；另 `EnforceCodeStyleInBuild` 既有行為不變）。

### 2. 降級政策：documented-only

- baseline 31 處逐條處理：**修正優先**；確屬與 repo 慣例衝突或成本效益不成立者，才在 `backend/.editorconfig` 降級（`dotnet_diagnostic.CAxxxx.severity = none|suggestion`），**每條降級行必須緊鄰理由註解**（規則、範圍、理由）。
- 預授權降級（理由已審）：`CA1707`（測試專案——BDD step／測試方法底線命名是既定慣例，與 ADR-011 §3 test carve-out 同源）；`CA1848`（LoggerMessage source-gen——僅 Host/Infrastructure 邊界少量非熱路徑 log，儀式成本不成比例）。其餘規則不得未經評估就降級。
- 禁止專案級 `NoWarn` 打包關閉、禁止無註解降級（比照 ADR-015 §3 精神）。
- generated code（`*.feature.cs`、EF Migrations 目錄等工具產物）不受 CA 約束——以 `.editorconfig` `generated_code = true` 標記，屬正確分類而非降級。

### 3. BannedApiAnalyzers：FluentAssertions 強制機械化

測試專案引入 `Microsoft.CodeAnalysis.BannedApiAnalyzers` + `BannedSymbols.txt`：

```text
T:Xunit.Assert; 測試斷言一律用 FluentAssertions（CLAUDE.md §4），禁用 xunit.Assert
```

### 4. 明文不機械化的邊界

- **`.Value` 前未檢查 `.IsFailure`**：需自寫 dataflow Roslyn analyzer，成本高於現階段效益——裁決**不機械化**，由 AI review（矩陣第 20 項）+ BDD 行為驗證承擔；無防線區保留該條並加註本裁決。
- **空 catch block**：內建 CA 無對應規則，不為單一規則引入 Sonar 全家桶；留在無防線區。
- 不引入 SonarAnalyzer／SecurityCodeScan 等第三方大型規則集（見 Alternatives A）。

---

## Rationale

### 為什麼是內建 NetAnalyzers 而不是第三方規則集

內建 analyzer 隨 SDK 版本演進、零套件依賴、與既有 `EnforceCodeStyleInBuild` 同一條 build 路徑；latest-recommended 是微軟維護的訊噪比已調校子集。62 個 baseline 警告的規模證明 recommended 級在本 repo 可一次清償；Sonar 級（數百規則）會直接製造豁免文化。

### 為什麼 CA 警告要升 error

與 ADR-015 同一論證：warning 不阻斷就等於依賴注意力，NU1903 漏報事故已實證這條路必然失敗。62→0 清償後，任何新違規即紅，無灰色地帶。

### 為什麼 `.Value`/`IsFailure` 不機械化

自寫 dataflow analyzer 是本 repo 至今最重的機械化投資（專案模板、測試、隨 Roslyn 版本維護），而該規則的實際違規會在 BDD 場景（錯誤路徑斷言）大機率顯形。依制度凍結啟發式（`tasks/lessons.md` 2026-07-05 [decision]），等出現一次真實漏網事故再評估。

---

## Consequences

### Positive

- 無防線區一次移出三條（CA2016／CA2200／FluentAssertions）；AI review 職責縮至真正需要語意判斷的部分（設計取捨、規格誤解、`.Value` 紀律）。
- 文化敏感字串比較（CA1310/1311/1304，本次 16 處）被消除——對中文 wire 字串做 `StartsWith` 這類真實風險自此在編譯期攔截。
- 防患型規則（CA2016 零命中即上線）在違規第一次出現時就攔，永無清欠期。

### Negative / Trade-offs

- SDK 升版可能引入新 recommended 規則，未改程式碼的 build 轉紅（與 ADR-015 的 advisory 時間炸彈同型）。
  - Mitigation: 期望行為（fail-loud）；處理循 Decision §2 降級政策，documented 降級是受控出口。
- 62 處既有違規的清償改動面偏大，混入行為風險（文化比較語意變更）。
  - Mitigation: 測試側修正獨立 commit（refactor 紀律）；全程 full gate + BDD 驗證；wire-contract 相關字串操作的修正需測試證據。
- BannedApiAnalyzers 是一個新套件依賴。
  - Mitigation: 微軟官方維護、僅測試專案引用、被 CPM 集中釘版與 ADR-015 弱點 gate 覆蓋。

---

## Alternatives Considered

### Alternative A: SonarAnalyzer.CSharp／SecurityCodeScan 全規則集

Rejected. 數百條規則的初始噪音會迫使大面積降級，訓練出「降級是常態」的文化（與 NU1901/02 不擋、Sonar 不引入同一論證）；且單為空 catch（S108）一條引整套，成本結構不成立。

### Alternative B: 維持 AI review 承擔語意層

Rejected. 直接違反協調憲章明文規則 (i)；本 ADR 的 Context 即是「規則存在但靠注意力」的又一實例，且 baseline 已實測出 62 處漏網。

### Alternative C: 自寫 Roslyn analyzer 覆蓋 `.Value`/`IsFailure` 與空 catch

Rejected. 最重的投資對最少的實際違規（兩者至今零事故）；違反制度凍結啟發式（事故驅動）。已明文列入 Decision §4 邊界，未來事故觸發時再議。

### Alternative D: `AnalysisMode=All` 全規則啟用

Rejected. All 級啟用數百條含高爭議規則，baseline 將從 62 膨脹一個數量級，清償期過長且降級清單會失控；latest-recommended 已覆蓋本次目標的全部三條債務。

---

## Implementation Rules

1. `backend/Directory.Build.props` 含 `AnalysisLevel` = `latest-recommended` 與 `CodeAnalysisTreatWarningsAsErrors` = `true`。
2. baseline 31 處歸零：修正、generated_code 分類、或 `.editorconfig` documented 降級；每條 `dotnet_diagnostic.CA*` 降級行緊鄰理由註解，降級集合不得超出 Decision §2 預授權清單加逐條評估過的項目。
3. 測試專案引用 `Microsoft.CodeAnalysis.BannedApiAnalyzers`（CPM 管理版本）+ `BannedSymbols.txt` 至少含 `T:Xunit.Assert`。
4. `docs/verification-matrix.md`：主表新增 analyzer gate 一行；無防線區移出 CA2016／CA2200／FluentAssertions 三條（比照既例標記已機械化）、`.Value`/`.IsFailure` 條加註「ADR-016 裁決不機械化」。
5. 新檢驗以「綠＋故意紅」驗證：綠 = full gate 通過且 build 零 CA 警告；故意紅 = 暫時加入一處 `throw ex;`（或 `Assert.True`）確認 build 以 error 失敗後移除。
6. **驗收**：

   ```bash
   grep -n "AnalysisLevel\|CodeAnalysisTreatWarningsAsErrors" backend/Directory.Build.props
   # 預期：兩行命中
   dotnet build backend/ApiKeyManagement.slnx -c Release --no-incremental 2>&1 | grep -c "warning CA"
   # 預期：0
   ```

7. 任何提案修改 1–6，必須先開新 ADR。
