# Phase I 任務規格 — 外部借鏡採用（P1/P2/P3）+ TBD 分支轉換（executor 級：Sonnet）

> 依 `tasks/process-improvement-plan.md` §10（分析）與 §10.3（使用者裁決：P1+P2+P3 全採用、TBD 直進 main）。四個階段各自獨立 commit，依序執行，任一階段卡住即停該階段回報、續做下一個不受影響的階段。可重派指令包。

## 角色與義務

- 誠實申報到「Blockers / 不確定」節；每個階段的驗證輸出都要貼報告。
- 每階段一個 commit；訊息開頭見各階段。全部完成後 push（TBD 轉換完成後即 push main）。
- Subagent 自報成功不可信 — 你自己就是 executor，所有驗證親自跑。

## 階段 0 — TBD 分支轉換（commit 訊息 `chore(branch):`，git 操作為主）

1. 解除 main 保護：`gh api -X DELETE repos/jame2408/api-protection/branches/main/protection`（若 404 = 已無保護，記錄後續行）。
2. `git checkout main && git pull origin main`，然後 `git merge hardening/architecture-tests-mvp`。預期乾淨合併（分支內容是 main squash 前的超集）；**若有任何衝突 → 停此階段**，回報衝突檔案清單，不要自行解。
3. `git push origin main`（pre-push full gate 屬預期，數分鐘）。確認 push 後 CI 在 main 觸發並轉綠（`gh run list --branch main` + `gh run watch`）。
4. 刪除已併入的 hardening 分支：`git push origin --delete hardening/architecture-tests-mvp && git branch -d hardening/architecture-tests-mvp`。
5. 之後所有階段直接在 main 上 commit + push。

## 階段 1 — P1 寫後語法驗證 hook（commit 訊息 `governance(post-edit):`）

1. 新增 `.claude/hooks/post-edit-validate.sh`（PostToolUse，matcher `Edit|Write`）：從 hook payload 取 `tool_input.file_path`，依副檔名驗證：
   - `.sh` → `bash -n`
   - `.json` → `python3 -c "import json,sys; json.load(open(sys.argv[1]))"`
   - `.py` → `python3 -m py_compile`
   - `.props` / `.csproj` / `.targets` / `.xml` → 先以文字掃描拒絕含 `<!DOCTYPE` 或 `<!ENTITY` 的檔案（MSBuild 檔合法內容不會有；出現即判失敗，同時擋掉 XXE 與 billion-laughs，不引入 defusedxml 依賴），通過後用 stdlib `xml.etree.ElementTree.parse` 驗 well-formed。
   - 其他副檔名 → 直接 exit 0。檔案不存在（可能被刪）→ exit 0。
   - 驗證失敗 → **exit 2** 並在 stderr 印出「哪個檔案、哪類驗證、原始錯誤」（PostToolUse 無法回滾寫入，exit 2 的作用是把錯誤立即回饋給 agent 修正）。
2. `.claude/settings.json`：PostToolUse 陣列新增一個帶 `"matcher": "Edit|Write"` 的項目註冊本 hook（**不要動既有 post-tool-observe 項**）。使用者已於 §10.3 同意此 settings 變更。
3. 驗證（貼輸出）：對四種副檔名各做一次「壞檔案 → exit 2 + 錯誤訊息」與「好檔案 → exit 0」的手動管線測試（`echo '<payload json>' | bash .claude/hooks/post-edit-validate.sh`）。

## 階段 2 — P2 機制自體健檢（commit 訊息 `governance(machinery-check):`）

1. 新增 `scripts/machinery-check.sh`，檢查項目（任一失敗 → 非零 exit + 明確訊息，**fail-loud，禁止 `if [ -f ]` 靜默跳過** — 這正是外部 repo 的反面教材，見 §10.1 末行）：
   - `.claude/settings.json` 與 `.mcp.json`（若存在）JSON 合法。
   - `.claude/settings.json` hooks 段引用的每個腳本檔案存在、可執行、`bash -n`（.sh）或 `py_compile`（.py）通過。
   - `.claude/hooks/*.sh` 與 `scripts/*.sh` 全部 `bash -n` 通過。
   - 指針完整性：從 `CLAUDE.md`、`docs/orchestration.md`、`docs/verification-matrix.md` 抽出反引號包裹、符合 `^(docs|scripts|tasks|backend|\.claude|\.github)/.+\.(md|sh|py|yml|cs|csproj|json|txt)$` 的路徑，逐一確認存在（glob 與含 `*` 的樣式跳過）；行內含 `machinery-check:ignore` 標記者豁免。缺失即紅。
2. 接入 `scripts/ci-checks.sh` fast 與 full（比照 hook_smoke / zh_lint 模式，維持 fast ⊂ full）。
3. 驗證（貼輸出）：綠一次；故意紅兩種（暫時 `chmod -x` 一個 hook → 紅；在矩陣暫加一行指向不存在檔案 → 紅；皆還原後綠）。

## 階段 3 — P3 ADR-012 憲章修訂（commit 訊息 `governance(adr-012):`）

1. `docs/adr/adr-012-charter-amendments-external-adoption.md`：本 ADR 是 `docs/orchestration.md` 與 `tasks/_templates/checkpoint.md` 修改的合法通道（依 ADR-007 規則 4），Decision 五項：
   - (a) **unverified_success 條款**（orchestration.md §2 追加）：subagent／executor 自報成功一律視為中間態；orchestrator 必須親跑確定性檢查（測試、lint、grep、檔案存在性）後才升級為已驗證；確定性 gate 不得經 subagent 中介轉述。此條款關閉 §9.2 O-8。
   - (b) **並行派工規則**（orchestration.md §1 或新 §1.5）：並行 executor 的檔案集必須不相交；觸碰 build 產物鏈（csproj/props/editorconfig/src）的任務不得與跑 build gate 的任務並行，串行或用 worktree 隔離；同時並行數上限 4；executor 之間不直接通訊、不自行重試已失敗的他人任務。
   - (c) **checkpoint 模板加欄**：`tasks/_templates/checkpoint.md` 在「待驗證」之後加「已嘗試且失敗的方法」欄（含每項的失敗原因一句話），防止下個 executor 重試死路。
   - (d) **冷啟動標準 prompt**（orchestration.md 新小節）：給使用者/協調者的固定開場文字（讀 `tasks/process-improvement-plan.md` §8.5 checkpoint + 本憲章，依 checkpoint 接手）。
   - (e) **TBD 分支紀律**：main 直進；本機 pre-push full gate 為主防線（與 CI 同一支 `ci-checks.sh`）；CI 於 push-to-main 後為驗證訊號，紅了視同 build 壞掉最高優先修復；不再使用長命功能分支；required status check 已解除（階段 0）。
   - 出處註記：(a)(b)(c)(d) 借鏡 `zeuikli/claude-code-workspace`（§10.1），(e) 為使用者裁決。
2. 同步實作 (a)(b)(d) 的 orchestration.md 編輯與 (c) 的模板編輯（同 commit）。
3. `docs/verification-matrix.md` 同 commit 更新：新增 P1 hook 行（寫的當下層、限 Claude Code harness）與 machinery-check 行（commit 前/push 前/CI）；O-8 相關的第 21 行補 unverified_success 指針。
4. `tasks/process-improvement-plan.md`：§8.2 增列 Phase I 行；§9.2 O-8 標 ✅ 由 ADR-012(a) 關閉；§8.5 更新（分支已轉 main、殘項清單）。
5. 驗證：`bash scripts/adr-lint.sh` 綠 + governance clause 故意紅；`bash scripts/ci-checks.sh fast` 綠（此時含 machinery-check）；`bash scripts/zh-lint.sh` 綠。

## 最終報告

各階段：commit hash、驗證輸出（含全部故意紅）、CI on main 結果連結；Blockers/不確定。