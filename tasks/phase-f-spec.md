# Phase F 任務規格 — 命名規則機械化（ADR-011：.editorconfig + EnforceCodeStyleInBuild）（executor 級：Sonnet）

> 使用者已裁決：「擴充並強制」— 把 CLAUDE.md 的命名規則（PascalCase 方法/型別、`_camelCase` 私有欄位、`Async` 後綴、`I` 介面前綴）寫成 `dotnet_naming_*` 規則升 error，同時解決驗證矩陣第 11 行「format 權威來源模糊」。可重派指令包。

## 角色與義務

- 誠實申報任何不確定到「Blockers / 不確定」節；規格模糊 → 停該項回報。
- 允許最後執行一個 commit（訊息開頭 `governance(adr-011):`）；commit 前 `git status` 確認只含本任務檔案。
- 不動 `backend/src` 與 `backend/tests` 的 `.cs` 內容 — **除非**首次全掃出現既有命名違規需要修正（見交付物 4；修正僅限重新命名，禁止行為變更）。

## 先讀（依序）

1. `backend/.editorconfig`（現況：僅 2 檔 `generated_code` whitespace 豁免 — 必須保留）
2. `.claude/references/dotnet/naming.guide.md`（規則細節的權威來源，ADR-011 引用它）
3. `docs/adr/_template.md` + `scripts/adr-lint.sh`
4. `scripts/ci-checks.sh`（了解 format gate 與 build 怎麼跑）
5. `docs/verification-matrix.md` 主表第 11 行與無防線區塊「命名慣例」行（你要同 commit 更新）

## 交付物

1. **`docs/adr/adr-011-naming-rules-editorconfig-enforcement.md`**：
   - Decision：命名規則機械化落點 = `backend/.editorconfig` 的 `dotnet_naming_*` 規則（severity=error）+ `backend/Directory.Build.props` 開 `EnforceCodeStyleInBuild=true`（build 期強制）。規則集（以 `naming.guide.md` 為權威來源，ADR 只放指針與機械化決策）：
     - 方法 / 屬性 / 型別 / 事件 → PascalCase
     - 私有 instance/static 欄位 → `_camelCase`（底線前綴 + camelCase）
     - 介面 → `I` 前綴 PascalCase
     - `async` 方法 → `Async` 後綴，**僅限 `backend/src`**：`backend/tests/.editorconfig` 覆寫該條 severity=none（BDD step 與測試方法命名跟隨場景語意，不加後綴 — 在 ADR 明文為 carve-out，理由與豁免寫進規則本身，不是默契）
   - 不在範圍：不引入其他 style/analyzer 規則（IDE 建議類全部不動）；不改 whitespace 豁免。
2. **`backend/.editorconfig` 擴充**（保留既有 `generated_code` 段）+ **`backend/tests/.editorconfig`**（Async 後綴豁免）+ **`backend/Directory.Build.props`**（`EnforceCodeStyleInBuild`；若檔案不存在則新建，注意不要干擾既有 `Directory.Packages.props`）。
3. **既有程式碼首次全掃**：`dotnet build` + `dotnet format --verify-no-changes` 全跑；若有既有違規，逐一重新命名修正（純 rename，用 IDE 級語意等價修改；有任何不確定的 rename → 停，列 Blockers）。
4. **驗證矩陣同 commit 更新**：第 11 行（權威來源改指 ADR-011 + naming.guide.md）；無防線區塊「命名慣例」行比照「禁簡體」前例劃線標✅移出（一般 PascalCase/`_camelCase`/`Async` 後綴已機械化；若 dotnet_naming 無法覆蓋的子項如 `Async` 後綴之測試豁免，如實註記）。
5. **`tasks/process-improvement-plan.md`**：§8.2 增列 Phase F 行；§8.3「`dotnet format` 權威來源模糊」條目標✅關閉（由 ADR-011）。

## 驗收（全部輸出貼報告）

- `bash scripts/adr-lint.sh` 綠 + 故意紅（governance clause 暫刪）驗證。
- **故意紅驗證（核心）**：在 `backend/src` 暫增一個含 `private string badName;`（無底線）與 `public async Task Foo()`（無 Async 後綴）的暫存類 → `dotnet build` 必須紅、`dotnet format --verify-no-changes` 行為記錄下來（紅或不紅都如實回報 — 兩個 gate 至少一個要紅）→ 刪除暫存類 → 綠。
- `bash scripts/ci-checks.sh full` 全綠（含 build+test；Docker 需可用）。
- `bash scripts/zh-lint.sh` 綠。
- 最終報告：交付清單 + 全部驗證輸出 + 既有違規修正清單（若有）+ Blockers/不確定。
