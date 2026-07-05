# 命名規則機械化：`.editorconfig` `dotnet_naming_*` + `EnforceCodeStyleInBuild`

> `CLAUDE.md` 的命名規則（PascalCase 方法/型別、`_camelCase` 私有欄位、`Async` 後綴、`I` 介面前綴）過去只靠人工 review 遵守；驗證矩陣第 11 行同時記錄「`dotnet format` 權威來源模糊」——format gate 存在但只鎖 whitespace，命名規則從未被任何腳本檢驗過。本 ADR 把兩者一次解掉：命名規則落點在 `backend/.editorconfig` 的 `dotnet_naming_*`，並用 `EnforceCodeStyleInBuild` 把它從 IDE 建議升級成 build-time 錯誤。

---

## Status

Accepted (2026-07-04)

同步項目：`backend/.editorconfig`（新增 `dotnet_naming_*` 規則段）、`backend/tests/.editorconfig`（新增，Async 後綴豁免）、`backend/Directory.Build.props`（新增，`EnforceCodeStyleInBuild=true`）、`docs/verification-matrix.md`（主表第 11 行改指本 ADR；無防線區塊「命名慣例」行標✅移出）、`tasks/process-improvement-plan.md`（§8.2 增列 Phase F 行；§8.3「`dotnet format` 權威來源模糊」條目關閉）。首次全掃（`dotnet build` + `dotnet format --verify-no-changes`）**未發現既有違規**——repo 既有程式碼已自然符合本 ADR 制定的規則，無需任何 rename。

---

## Context

### 現況：規則存在，防線是空的

`CLAUDE.md`「Code Quality」段：

```
Naming conventions: PascalCase methods, `_camelCase` fields, `Async` suffix on async methods
```

`.claude/references/dotnet/naming.guide.md` §B 補上完整表格（Interface `IPascalCase`、Field private `_camelCase`、Field const `PascalCase` 等），是本專案命名規則細節的權威來源。但 `docs/verification-matrix.md` 無防線區塊如實記載：

```
命名慣例：一般 PascalCase 方法 / `_camelCase` 欄位 / `Async` 後綴 | `CLAUDE.md` §4「Code Quality」|
未追蹤——`NamingConventionTests.cs` 只鎖 `*Handler`/`*Repository`/`*FailureCodes` 後綴這三類，
未涵蓋一般方法/欄位命名；`backend/.editorconfig` 存在，僅含 2 檔 `generated_code` whitespace 豁免，
style/naming 規則未定義，權威來源仍為工具預設，無 analyzer 設定
```

同表主表第 11 行對 `dotnet format` 的描述同樣承認「權威來源模糊」——format gate（`scripts/ci-checks.sh` 的 `format_check()`）只驗證 whitespace/using 排序等工具預設，從未檢驗命名。

### 為何不能繼續空著

- `NamingConventionTests.cs`（Architecture.Tests）只鎖三類後綴命名（`*Handler`/`*Repository`/`*FailureCodes`），對「一般方法要 PascalCase」「私有欄位要底線前綴」這類全域規則完全無感——NetArchTest 的 reflection 檢查方式，天生不適合逐符號掃描命名慣例，勉強寫會是一堆脆弱的 regex-on-reflection。
- 這正是 `docs/verification-matrix.md` 「無防線區塊」存在的意義：規則寫在 `CLAUDE.md` 但沒有機械化，長期只能靠 review 記憶力撐著——而 `docs/adr/adr-009-traditional-chinese-and-zh-lint.md` 已經證明，同一天內任何等級的模型（含 orchestrator 本人）都可能在無防線的規則上犯規。

### 易混淆概念釐清

| 概念 | 是什麼 | 本 ADR 是否規範 |
|---|---|---|
| `NamingConventionTests.cs` 三類後綴鎖定 | Handler/Repository/FailureCodes 的介面-實作命名對應 | ❌ 維持現狀，不重複 |
| 一般方法/型別/事件 PascalCase | 全域命名慣例 | ✅ 本 ADR |
| 私有欄位 `_camelCase` | 全域命名慣例 | ✅ 本 ADR |
| 介面 `I` 前綴 | 全域命名慣例 | ✅ 本 ADR |
| Async 方法 `Async` 後綴 | 全域命名慣例，`backend/tests` 有 carve-out | ✅ 本 ADR |
| `dotnet format` whitespace/using 排序 | 既有 format gate，本來就有 | ❌ 不動 |
| IDE 建議類分析規則（除命名外） | Analyzer 建議、程式碼簡化提示等 | ❌ 明文排除，見「不在範圍」 |

---

## Decision

命名規則機械化落點：`backend/.editorconfig` 的 `dotnet_naming_*` 規則（`severity=error`）+ `backend/Directory.Build.props` 開 `EnforceCodeStyleInBuild=true`，讓違規在 `dotnet build` 階段即失敗，而非只在 IDE 顯示建議燈泡。規則細節以 `.claude/references/dotnet/naming.guide.md` 為權威來源；本 ADR 只放機械化落點與決策，不重複規則本體。

### 1. 一般規則（`backend/.editorconfig`）

| 規則 | naming.guide.md 對應 | .editorconfig 機制 |
|---|---|---|
| 方法 / 屬性 / 型別 / 事件 → PascalCase | §B 表格 | `types_and_members_should_be_pascal_case` |
| 介面 → `I` 前綴 PascalCase | §B 表格 | `interfaces_should_be_i_prefixed` |
| 私有 instance/static 欄位 → `_camelCase` | §B 表格 | `private_fields_should_be_camel_case` |
| async 方法 → `Async` 後綴 | §B「Async 方法命名」 | `async_methods_should_have_async_suffix` |

```ini
# before：無 dotnet_naming_* 規則，違規只能靠 review 抓
# after（backend/.editorconfig 摘要）：
dotnet_naming_rule.async_methods_should_have_async_suffix.severity = error
dotnet_naming_rule.interfaces_should_be_i_prefixed.severity        = error
dotnet_naming_rule.private_fields_should_be_camel_case.severity    = error
dotnet_naming_rule.types_and_members_should_be_pascal_case.severity = error
```

### 2. 既有慣例的顯式承認：const / static readonly 欄位 → PascalCase

首讀 repo 既有程式碼（`ApiKey.cs` 的 `MaxActiveKeysPerConsumerEnv`、`ApiProblem.cs` 的 `Map`、`ArchitectureRules.cs` 的 `ProductionAssemblies`）發現：`private const` 與 `private static readonly` 欄位一律 PascalCase，視為「事實上的常數」，不落入私有欄位 `_camelCase` 規則。這與 `naming.guide.md` §B「Field (const) → PascalCase」一致，本 ADR 把它明文擴大到 `static readonly`（guide 原文只舉了 const 例子），因為既有程式碼已經一致地這樣做，機械化規則若不涵蓋會把既有正確程式碼誤判為違規。

```ini
# 具體 modifier 規則必須排在通用私有欄位規則之前（dotnet_naming 規則依檔案順序、
# 第一個符合者生效），否則 const / static readonly 私有欄位會被通用規則誤判。
dotnet_naming_symbols.constant_fields.required_modifiers        = const
dotnet_naming_symbols.static_readonly_fields.required_modifiers = static, readonly
```

### 3. `backend/tests/.editorconfig`：Async 後綴 carve-out

`async` 方法 `Async` 後綴規則**僅限 `backend/src`**。`backend/tests/.editorconfig` 覆寫該條 `severity=none`：

```ini
# backend/tests/.editorconfig（新增檔案，root=false，繼承 backend/.editorconfig 其餘規則）
[*.cs]
dotnet_naming_rule.async_methods_should_have_async_suffix.severity = none
```

理由（carve-out 本身，非默契）：BDD step 與測試方法命名跟隨場景語意（例如 `ThenCreateFailsWithReason`、`WhenSomethingHappens`），加 `Async` 後綴會破壞可讀性且與 Reqnroll 慣例衝突；其餘命名規則（PascalCase / `_camelCase` / 介面前綴）在 `backend/tests` 依然強制。

### 4. 隱藏開關：`dotnet_diagnostic.IDE1006.severity`

**這是本 ADR 唯一非顯而易見的技術細節，必須記錄**：`dotnet_naming_rule.*.severity` 只決定「命名規則違規被回報時的嚴重度」，**不會單獨讓 `dotnet build` 失敗**。所有命名規則違規共用診斷碼 `IDE1006`，`dotnet build` 階段是否真的把它當 build error，取決於 `IDE1006` 這個診斷碼本身有沒有被明確開啟到 `error`。

實測驗證（見驗收）：只設定 4 條 `dotnet_naming_rule.*.severity = error` + `EnforceCodeStyleInBuild=true`，暫增違規類別後 `dotnet build` 仍然綠燈（0 錯誤），但 `dotnet format --verify-no-changes` 正確回報 `error IDE1006`。加上以下一行後，`dotnet build` 才正確轉紅：

```ini
# 沒有這行：dotnet build 對命名違規完全無感（IDE1006 預設不啟用於 build）；
# 有這行：dotnet build 才會真的失敗，形成本 ADR 標題承諾的「build-time 強制」。
dotnet_diagnostic.IDE1006.severity = error
```

`backend/tests/.editorconfig` 的 `severity=none` carve-out（決策 3）在此全域 `error` 之下仍然正確生效——已實測確認：`backend/tests` 底下缺 `Async` 後綴的方法不觸發任何診斷，但同一個檔案裡缺底線前綴的私有欄位依然觸發 `error IDE1006`。

### 5. Build-time 開關（`backend/Directory.Build.props`，新建）

```xml
<Project>
  <PropertyGroup>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

新建檔案，與既有 `backend/Directory.Packages.props`（中央套件版本管理，`docs/adr` 未涉及本 ADR 範圍）互不干擾——兩者是 MSBuild 不同機制（`Directory.Build.props` 是一般專案屬性匯入點，`Directory.Packages.props` 是 `ManagePackageVersionsCentrally` 專用檔），SDK 對兩者的匯入邏輯彼此獨立。

### 不在範圍

- 不引入其他 style/analyzer 規則（`IDE00xx` 系列的程式碼簡化建議、`var` 偏好等 IDE 建議類，全部不動；`EnforceCodeStyleInBuild=true` 只是讓「已設定 severity 的規則」在 build 期生效，不會無端把所有預設 IDE 建議一併轉紅——因為未設定 `dotnet_diagnostic.IDE0xxx.severity` 的規則維持其預設 severity，通常是 `suggestion`，不影響 build exit code）。
- 不改既有 `generated_code = true` whitespace 豁免（`CreateApiKeyEndpoint.cs` / `CreateApiKeySteps.cs`）。
- 不涵蓋 enum 成員、public/internal 可變欄位等 `naming.guide.md` 未明確列出的命名子項——維持「無防線」誠實標註優於過度擴張規則面。
- 不處理 `NamingConventionTests.cs` 既有三類後綴鎖定——兩套機制目標不同（後綴鎖定管介面-實作對應，本 ADR 管全域大小寫慣例），不合併、不取代。

---

## Rationale

### 為何選 `.editorconfig` + `EnforceCodeStyleInBuild`，不擴充 `NamingConventionTests.cs`

NetArchTest 靠 reflection 檢查型別/成員，天生看得到「型別 T 實作了 IFoo，是否命名為 Foo」這種**結構關係**，但看不清楚「這個私有欄位的原始碼字面是否以底線開頭」——reflection 拿到的是編譯後的 metadata name，寫 regex-on-reflection 逐一比對命名慣例的 ROI 低且脆弱（例如無法區分 record 的 primary constructor 參數捕獲欄位與手寫欄位）。`.editorconfig` 的 `dotnet_naming_*` 是 Roslyn 原生機制，直接在原始碼語法樹層級運作，維護成本遠低於自製 reflection 規則。

### 為何不機械化「不擴張到其他 analyzer」

CLAUDE.md 與 `naming.guide.md` 目前只要求命名慣例四類子規則，其餘 style（`var` 偏好、括號風格等）不是 CLAUDE.md 明文規則，貿然開啟等於把「風格偏好」升級成「強制規則」，超出使用者裁決範圍（見任務規格「擴充並強制」的裁決文字，只針對命名）。

### 為何 `dotnet_diagnostic.IDE1006.severity` 需要獨立記錄而非視為隱含細節

這是本 ADR 起草過程中實測發現的真實陷阱：直覺上「每條 naming rule 都設 `severity=error`」應該足夠讓 build 失敗，但 Roslyn 的實作把「規則要用什麼 severity 顯示」與「IDE1006 這個診斷碼本身開不開」分成兩層。若不明文記錄，未來任何人（含下一個 executor）新增 naming rule 時很可能重複踩坑、誤以為規則沒生效是規則寫錯，而非少了這一行全域開關。

---

## Consequences

### Positive

- 命名慣例從「無防線」轉為「build-time 強制」，`docs/verification-matrix.md` 無防線區塊少一條待辦。
- 驗證矩陣主表第 11 行的「格式權威來源模糊」一併解決——命名規則現在明確指向本 ADR + `naming.guide.md`，whitespace 規則維持工具預設（本就不在模糊指控範圍內，模糊的是命名，不是 whitespace）。
- 首次全掃 0 違規，證明過去純靠 review 遵守的命名慣例實際執行率相當高——機械化是把既有良好實踐鎖住，而非追討大量既有欠債。

### Negative / Trade-offs

- `dotnet_diagnostic.IDE1006.severity = error` 是全域開關，任何未來新增的 `dotnet_naming_rule`（即使 severity 設 `warning` 或 `suggestion`）只要符合 IDE1006 診斷碼，都會被這行拉到 error。
  - Mitigation: 目前四條規則全部刻意設計成 `error`（見決策 1），沒有「想要 warning 級」的命名規則；若未來新增需要更低 severity 的命名規則，屬於規則變更，本就要走 Implementation Rules 的治理條款開新 ADR。
- 新工程師或協作者不熟悉 `.editorconfig` `dotnet_naming_*` 語法，除錯命名規則生效與否時心智負擔較高（不像 architecture test 有明確斷言訊息）。
  - Mitigation: `dotnet build` 的錯誤訊息本身已含檔案位置與具體規則描述（「遺漏前置詞: '_'」「遺漏尾碼: 'Async'」），加上本 ADR 決策 4 已把隱藏開關明文化，除錯路徑清楚。
- `backend/tests/.editorconfig` 的 carve-out 只豁免 Async 後綴，若未來測試程式碼有其他需要豁免的命名子項，需要逐條加豁免、不能整檔關閉。
  - Mitigation: 這是刻意設計（比照 ADR-009 行級豁免優於檔案級豁免的精神）——`backend/tests` 仍應遵守 PascalCase/`_camelCase`/介面前綴，只有 Async 後綴因 BDD 語意衝突而豁免，維持豁免範圍最小化。

---

## Alternatives Considered

### Alternative A：擴充 `NamingConventionTests.cs`（NetArchTest reflection 規則）

Rejected. Reflection 只看得到 metadata name，看不到原始碼層級的字面前綴/後綴語意；要用 regex 比對 reflection 拿到的名稱字串本身可行，但無法區分「手寫欄位」與「primary constructor 捕獲的編譯器合成欄位」，會產生大量誤報或漏報。`.editorconfig` 是 Roslyn 原生語法樹層機制，職責更貼合。

### Alternative B：只寫文件規則，不機械化（維持現狀）

Rejected. `docs/verification-matrix.md` 已明文記載這是「無防線」規則；`docs/adr/adr-009-*.md` 已用同一天三次違規事故證明「規則存在但無機械化」對任何等級模型都不可靠。使用者裁決明確要求「擴充並強制」。

### Alternative C：用 Roslyn Analyzer 套件（如 StyleCop.Analyzers）取代 `.editorconfig` naming rules

Rejected. StyleCop 等第三方 analyzer 引入新的套件相依與規則集維護面（版本升級、規則集 baseline），且其命名規則粒度與本專案 `naming.guide.md` 的既有慣例（如 `I` 前綴、`Async` 後綴、const 例外）不完全對齊，需要額外抑制/覆寫其他不相關規則。`.editorconfig` 原生機制零額外相依，且已被 `EnforceCodeStyleInBuild` 官方支援。

### Alternative D：把命名規則寫成 `scripts/source-lint.sh` 的新 grep pattern

Rejected. `source-lint.sh` 現有三個 pattern（`new Failure(`、bare-string code、`cancel` 命名）都是「特定 API 呼叫」的語法層級 grep，命名慣例則需要區分符號種類（方法/欄位/型別/介面）與修飾詞（`private`/`const`/`static readonly`/`async`），grep 沒有語法樹資訊，無法可靠做到（例如區分字串常值裡出現的 `private string` 字樣 vs. 真正的欄位宣告）。Roslyn 原生 naming rule 引擎正是為此設計。

### Alternative E：全域強制 `EnableNETAnalyzers` + 完整分析等級（而非只開命名規則）

Rejected. 會連帶引入大量與本 ADR 無關的程式碼品質/效能/安全性 analyzer 規則，一次性造成大量既有程式碼需要修正或抑制，超出使用者裁決的「擴充並強制**命名規則**」範圍，且與「不在範圍」明文排除的 IDE 建議類規則直接衝突。

---

## Implementation Rules

1. `backend/.editorconfig` 的 `dotnet_naming_*` 規則（`async_methods_should_have_async_suffix` / `interfaces_should_be_i_prefixed` / `constant_fields_should_be_pascal_case` / `static_readonly_fields_should_be_pascal_case` / `private_fields_should_be_camel_case` / `types_and_members_should_be_pascal_case`）severity 一律 `error`，且 `dotnet_diagnostic.IDE1006.severity = error` 必須同時存在——缺其一則 `dotnet build` 對命名違規無感（見決策 4）。
2. `backend/Directory.Build.props` 的 `EnforceCodeStyleInBuild` 必須為 `true`；不得移到單一 `.csproj` 內用條件式關閉個別專案。
3. `backend/tests/.editorconfig` 只能豁免 `async_methods_should_have_async_suffix` 一條規則；新增其他豁免屬於規則變更，須先開新 ADR。
4. 既有 `generated_code = true` whitespace 豁免（`CreateApiKeyEndpoint.cs` / `CreateApiKeySteps.cs`）不受本 ADR 影響，維持原樣。
5. 規則細節（各符號分類的大小寫/前綴/後綴定義）以 `.claude/references/dotnet/naming.guide.md` 為權威來源；本 ADR 只管機械化落點，不重複規則本體——`naming.guide.md` 修改若導致與本 ADR 決策 1/2/3 的 `.editorconfig` 內容不一致，須同 commit 修正 `.editorconfig`。
6. **驗收**：

   ```bash
   dotnet build backend/ApiKeyManagement.slnx
   # 預期 0 錯誤

   dotnet format backend/ApiKeyManagement.slnx --verify-no-changes
   # 預期 exit 0

   grep -n "dotnet_diagnostic.IDE1006.severity" backend/.editorconfig
   # 預期 1 命中，值為 error

   grep -n "async_methods_should_have_async_suffix.severity = none" backend/tests/.editorconfig
   # 預期 1 命中
   ```

7. 任何提案修改 1–6，必須先開新 ADR。
