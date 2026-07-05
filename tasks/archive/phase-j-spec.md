# Phase J 任務規格 — 內容分級（ADR-013）：注入瘦身、checkpoint 遷出、CLAUDE.md 再瘦（executor 級：Sonnet）

> 使用者裁決（2026-07-05）：Tier 0 每-session 固定成本過高，全包核准整理。本包三項工程與 ADR-013 耦合（同 commit 同步），一個 commit 完成；目錄歸檔另包（Phase K）。

## 角色與義務

誠實申報 Blockers；只碰本規格明列檔案；**不動 backend/**；完成後一個 commit（`governance(adr-013):`）+ push main + 確認 CI 綠。

## 先讀

`docs/adr/adr-008-*.md`（你要修訂其注入規則）、`docs/adr/adr-012-*.md`（你要修訂其冷啟動 prompt）、`.claude/hooks/session-init.sh`、`scripts/hook-smoke.sh`、`CLAUDE.md` 全文、`tasks/lessons.md` 全文、`tasks/process-improvement-plan.md` §8.5。

## 交付物

### 1. `docs/adr/adr-013-content-tiering-and-injection-slimming.md`

Decision 四項（本 ADR 是 ADR-008 規則 2 與 ADR-012 決策 (d) 的合法修訂通道，Context 明文銜接）：

- **(a) 四級載入分級制度**：Tier 0 每-session 自動載入（CLAUDE.md + session-init 注入，有預算意識）；Tier 1 任務型必讀（rule 檔 / Accepted ADR / api-spec，由 skill must-read 觸發）；Tier 2 按需指針鏈（憲章 / 矩陣 / design）；Tier 3 歸檔（git 史 / archive 目錄）。原則：內容進 Tier 0 的唯一資格是「全局且每 session 都需要」。
- **(b) lessons 分區 + 注入只讀 Active 的 Rule 行**：`tasks/lessons.md` 分 `## Active` 與 `## Archived（已機械化 — 防線代記）` 兩區；判準：落地已成為機械化 gate（測試/lint/hook）者歸檔。session-init 注入改為：Active 區每條的 `###` 標題行 + `**Rule:**` 行（不含 Context/落地），末尾計數指針。取代 ADR-008 規則 2 的「最後 8 條全文」。
- **(c) checkpoint 遷出**：`tasks/checkpoint.md`（新檔，內容自 plan §8.5 遷入、欄位比照 `tasks/_templates/checkpoint.md`）成為唯一續接入口；plan §8.5 改為三行指針（歷史紀錄保留在 git）。ADR-012 決策 (d) 的冷啟動 prompt 文字同步改指 `tasks/checkpoint.md`。
- **(d) CLAUDE.md 瘦身原則**：只保留「全局且每 session 需要」：指令、Brief、自治範圍、變更紀律、non-negotiable 短句版 + 指針。細則一律指針化。

### 2. 實作

- `tasks/lessons.md` 分區：現有條目逐一歸類（判準見 (b)；歸類清單寫進報告供 review — 拿不準的放 Active 並註明）。條目內容不改寫，只搬移分區。
- `.claude/hooks/session-init.sh`：注入邏輯改為 (b) 格式（仍以 `### [` 錨點切塊，只取 Active 區、只輸出標題+Rule 行）；marker 去重邏輯不動。
- `scripts/hook-smoke.sh` 同步：斷言改為「含 Active 區最後一條的標題 + 其 Rule 行片段、**不含**其 Context 行片段」；原有 (b)(c) 斷言保留。
- `tasks/checkpoint.md` 建立 + plan §8.5 改指針 + 全 repo grep `§8.5` 引用逐一改指（`CLAUDE.md` Brief、`docs/orchestration.md` §6、其他命中處）。
- `CLAUDE.md` 瘦身（目標 ~100 行，不可破壞既有語意）：
  - §4 Verification Standards：錯誤處理與程式碼品質細則壓成 3–4 行指針（細節權威在 `.claude/references/dotnet/*.rule.md`、ADR-003/004、`docs/verification-matrix.md`）；保留 DoD 的高層條目（BDD 過、架構測試過、證據要求）。
  - ADR 段的 7 項 review checklist 移入 `docs/adr/_template.md`（模板內新增「Review Checklist」註解區），CLAUDE.md 留一行指針。
  - BDD 段：保留 kanban 規則與未機械化的 constraints（一次一個 @ignore、Green 紀律），敘事壓縮。
  - 「Non-Negotiable Constraints」段保留但逐條加已機械化標注（例：`(架構測試強制)`）。

### 3. 驗證（全部貼報告）

- `bash scripts/hook-smoke.sh` 綠 + 故意紅（暫時把一條 Active 條目搬到 Archived 造成斷言目標消失 → 紅 → 還原綠）。
- 手動注入模擬輸出全文貼上（供 orchestrator 檢查格式與 token 量）。
- `bash scripts/ci-checks.sh fast` 綠（含 machinery-check — checkpoint 遷移後指針不得斷）。
- `adr-lint` 13 檔綠 + governance clause 故意紅。
- 報告附：CLAUDE.md 行數 before/after、注入內容行數 before/after、lessons 歸類清單。
