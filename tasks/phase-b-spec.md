# Phase B 任務規格 — 學習迴圈機械層重設計（executor 級：Sonnet）

> 設計已定案於 `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md`（Accepted）— 先完整讀它，本規格只補實作細節與邊界。可重派指令包。

## 角色與義務

- 誠實申報任何不確定、卡住、規格模糊之處到最終報告「Blockers / 不確定」節；不要腦補繞過。
- **禁止 commit**。orchestrator review 後才 commit。
- 只碰下列「交付物」列出的檔案。特別是：**不要動** `tasks/process-improvement-plan.md`、`tasks/lessons.md`、`CLAUDE.md`、`tasks/todo.md`、`backend/`（另一 executor 與 orchestrator 正在並行作業）。

## 先讀（依序）

1. `docs/adr/adr-008-learning-loop-injection-and-pending-lessons.md`（全文 — 你的設計規格）
2. `.claude/hooks/session-init.sh`、`.claude/hooks/post-tool-observe.sh`、`.claude/hooks/post-tool-failure.sh`（現況）
3. `scripts/ci-checks.sh`（fast 模式結構，你要接入冒煙測試）

## 交付物（全依 ADR-008 Decision §1–§4）

1. **`.claude/hooks/session-init.sh` 重寫**：
   - session_id 從 hook payload 取（`json.loads(stdin)["session_id"]`，經環境變數傳遞給 python，比照現檔的防注入寫法）；與 marker 檔內容相同 → `exit 0`。
   - marker 路徑：`MARKER_FILE="${SESSION_INIT_MARKER:-$PROJECT_ROOT/.claude/session-init.marker}"` — env 可覆寫是給冒煙測試用的，正常執行用預設。
   - payload 無 session_id 或解析失敗 → **保守行為：照常注入**（寧可多注入一次，不可靜默失效），並不更新 marker。
   - lessons 抽取：以 `### [` 為條目錨點切塊，取檔案中**最後 8 塊**（現行慣例：新條目往檔尾加；不要重排既有條目）；結尾加一行「（完整 N 條見 tasks/lessons.md）」，N 為實際條目總數。
   - 移除 pending-lessons 計數段；must-read 段文字不變。
2. **`.claude/hooks/post-tool-observe.sh` / `post-tool-failure.sh`**：移除所有 `pending-lessons.jsonl` 寫入邏輯；observations / failures 記錄與 secret scrubbing **一行都不要動**。
3. **`scripts/hook-smoke.sh`（新增）**：不依賴真實 marker（用 `mktemp` + `SESSION_INIT_MARKER` 覆寫），斷言：
   (a) 新 session_id → 輸出含「必讀規範」且含 `tasks/lessons.md` 最後一條的標題文字；
   (b) 同 session_id 再跑 → 輸出為空、exit 0；
   (c) payload 缺 session_id → 仍輸出 must-read（保守注入）。
   任一斷言失敗 → 非零 exit + 明確錯誤訊息。
4. **`scripts/ci-checks.sh`**：fast 模式加入 `bash scripts/hook-smoke.sh`（放 adr-lint 之後、source-lint 之前或之後皆可，維持現有輸出風格 `[ci-checks] ...`）。
5. **`.gitignore`**：在既有 `.claude/*.jsonl` 行附近加 `.claude/*.marker`。
6. **刪除 `.claude/pending-lessons.jsonl`**（gitignored 本機檔；ADR-008 已記錄其最終 triage 結論）。

## 驗收（全部要做，輸出貼進報告）

- `bash scripts/hook-smoke.sh` 綠。
- **故意紅驗證**：暫時把 session-init.sh 的 lessons 抽取錨點改壞（如 `### [` 改成 `#### [`）→ 跑 hook-smoke 確認紅；還原 → 確認綠。兩次輸出都貼報告。
- `bash scripts/ci-checks.sh fast` 綠（含新接入的冒煙測試）。
- `grep -rn 'pending-lessons' .claude/hooks/ scripts/` → 0 命中（ADR-008 規則 3 驗收）。
- 實際模擬一次注入：`echo '{"session_id":"smoke-manual-1"}' | SESSION_INIT_MARKER=$(mktemp) bash .claude/hooks/session-init.sh` 輸出貼報告（人工可讀性檢查用）。
- 最終報告：交付清單 + 上述全部輸出 + Blockers/不確定。
