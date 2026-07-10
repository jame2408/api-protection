# Checkpoint

> 唯一續接入口（`docs/adr/adr-013-content-tiering-and-injection-slimming.md` 決策 (c)）。欄位比照 `tasks/_templates/checkpoint.md`；歷史交接紀錄見 git log（本檔內容取代 `tasks/process-improvement-plan.md` §8.5 原文）。新 session 直接讀本檔即可接手，不需要先讀 plan 全文。

---

## 分支

`main` only（TBD 轉換已完成；分支紀律見 `docs/adr/adr-012-charter-amendments-external-adoption.md` 決策 (e)）。

## 已完成（含 commit hash）

> 本欄依「如何接上」條文只保留最近數項；更早紀錄（TBD 轉換、Phase I–K、Wave 1 全 10 場景、ADR-012～020 落地、防線機械化各包、lessons triage 一二輪）見 git log 與 `tasks/archive/`。

- **Wave 2 首場景「從 Active 狀態撤銷」Red→Green，13/46** — 首個完整垂直切片：ADR-020 Transactional Outbox 最小落地（`7c248bb`）→ RevokeKey slice（Domain/Handler 三 guard/endpoint）→ steps＋outbox row 斷言 — `02514f3`；revokedBy 債務（api-spec §3.2.8＋integration spec §6.1，待 auth slice）
- **ADR-021 共享狀態檔團隊尺度**：`tasks/lessons/` 一檔一教訓（新增=新檔、歸檔=改單行 frontmatter）＋session context glob 注入；checkpoint 分流與 bdd-progress 帳面生成化只定規格（觸發=第二常設寫者）— `1a7e5f3`
- **ADR-022 BDD 需求類型分流**：六類分流表定案；`Spec-change:` trailer gate 上線（矩陣 9g，逃生口 `ALLOW_FEATURE_MAINTENANCE=1`）；CLAUDE.md 凍結句限縮為 Discovery 管道 — `71a193c`
- Session 初始載入瘦身＋lessons triage 第三輪（2026-07-10）：claude.ai connectors 停用（使用者端）＋Context7 雙掛消除；Tessl 收納 `.claude/parked/`；plugins 專案層關閉；skill description 瘦身；triage：`untracked-adr-draft` 防線代記歸檔（矩陣 10a 補登、故意紅重演）＋`heredoc` 條瘦身，注入 Active 15→14 — `e08562e` `f9aba4f`
- GPT-5.6 外部回饋處置（2026-07-10 使用者裁決）：問題約六成認同；解方多數依制度凍結啟發式與既往裁決拒絕（分支模型=ADR-012 Alt E 已拒、流程量測=ADR-018 已裁同域）；落地三小項 — ci.yml required-check 註解勘誤對齊 ADR-012 (e)、本欄裁切至最近數項、todo.md 歸檔 pass（結案內容→`tasks/archive/todo-closed-2026-07-10.md`）；三個觸發制擱置項記入 todo「觸發制擱置項」段 — `c1fcaaa`
- **scenario「從 Rotating 狀態撤銷 — 同時清除輪替關聯」Red→Green，14/46** — design-doc T6 垂直切片：ApiKey 增 SuccessorKeyId/PredecessorKeyId＋`Revoke()` 清自身側＋`ClearPredecessorLink()`、handler 跨 aggregate 清 successor 側（缺列靜默跳過，非本場景範圍）、`AddRotationLinkColumns` migration、新 Given（EF CurrentValue seed Rotating 對）＋新 Then（DB 斷言雙向 null，wire 無關聯欄位）。自然紅 A（undefined steps）＋自然紅 B（清除斷言紅）→ 綠，orchestrator 親跑 full gate（19 passed/32 skipped、RevokeKeyHandler coverage 88%）放行；executor 零 blocker（99.9K tokens／47 calls／5.2 分）；spec 兩處精度瑕疵（測試計數外推錯誤、漏列 `dotnet tool restore` 前置）記入下輪範本注意 — `65899b5`
- **scenario「從 Locked 狀態撤銷」Red→Green，15/46** — 純 test-only 啟用（production 側 guard 本就放行 Locked）：新 Given 循 Rotating 的 EF CurrentValue seed 先例＋既有 When 疊加第二個 `[When]` attribute（Security Admin 措辭；actor 區分留待 auth slice 債務）。自然紅（undefined steps）→ 綠 20 passed/31 skipped → 故意紅（seed 改 Suspended 使 previousStatus 斷言紅）→ 還原回綠；orchestrator 親跑 FunctionalTests＋pre-push full gate 放行。executor 零 blocker（79.0K tokens／28 calls／3.2 分，較上輪 -21% tokens／-40% calls）；上輪兩處 spec 精度注意（計數勿外推、tool restore）已修正生效；本輪新增一處 spec 步驟順序瑕疵（ci-checks fast 排在帳面更新前，造成一次預期性 bdd-lint 中間紅）記入下輪注意 — `ee25317`
- **seed-helper 重構＋scenario「從 Suspended 狀態撤銷」Red→Green，16/46** — 兩 commit 串行：`CreateSeedKey` helper 消除 4 份 `ApiKey.Create` 樣板（SaveChanges 時機留在各 Given，Rotating 單次 SaveChanges 結構不變）— `ec49650`；Suspended 啟用（唯一新 step 為 Given，When/Then 全數既有匹配），自然紅→綠 21 passed/30 skipped→故意紅（seed 改 Active）→回綠 — `fba2072`。orchestrator 親跑 FunctionalTests＋pre-push full gate 放行。executor 零 blocker、零 friction（96.5K tokens／42 calls／4.0 分，含兩個 commit；單 commit 均攤較上輪再降）；spec 累積注意三條全數生效（計數命中、帳面先於 ci-checks、無 tool restore 誤列）
- **scenario「金鑰已在終態 — 拒絕撤銷」Red→Green，17/46＋analyzer 盲區發現** — 首個 failure-path：新 Given Expired（helper＋CurrentValue）＋新 Then「撤銷失敗」（鏡像 CreateApiKey 先例：RFC 9457 斷言＋場景文字→(status, errorCode) 對照表，預鎖 reason_empty 條目）；自然紅（2 undefined steps）→ 綠 22/29 → 故意紅（seed 改 Active，409 斷言收到 200 如預期紅）→ 回綠；`RevokeKeyHandler` coverage 88%→92%（終態 guard 分支）— `abbb415`。executor friction 申報 CA1310「CreateApiKeySteps 增量編譯快取未爆」，orchestrator 依 unverified_success 親驗**證偽其機制**（`--no-incremental` 全量 build 0 錯誤）並追到真根因＝editorconfig `generated_code = true` 手寫檔標記（詳待裁決欄）；executor 93.9K tokens／54 calls／5.7 分
- **analyzer 盲區修復（2026-07-10 使用者裁決）**：移除 `backend/.editorconfig` 兩個手寫檔的 `generated_code = true` 標記（`8922c47` 排版豁免的副作用＝整檔 analyzer 豁免）——production `CreateApiKeyEndpoint.cs` 經誘餌故意紅證明檢驗恢復、零既存違規 — `67c802e`；test `CreateApiKeySteps.cs` 自然紅 3 處（CA1310 ×1／CA1822 ×2 補 static）補修＋CS8669 ×2 歸零＋矩陣 19c 備註＋lesson 落地欄 — `63e6ee1`。editorconfig 僅剩 Migrations 工具產物段。executor 98.3K tokens／68 calls／6.0 分；friction 揭露「lesson 內寫自身 commit hash」自我指涉悖論，處置規範：改由後續 commit 引用（中繼 hash `f987efb` 已於 checkpoint commit 校正為 `63e6ee1`）
- **scenario「未提供撤銷原因 — 拒絕」Red→Green，18/46** — guard 2 啟用：新 When 以空 JSON 物件 POST（faithful 於「未提供」；executor 主動查證 `Program.cs` 無 `RespectNullableAnnotations`，STJ 缺欄位綁 null 命中 `IsNullOrWhiteSpace`），Given/Then 全既存（對照表條目 `abbb415` 預鎖直接兌現）。自然紅→綠 23/28→故意紅（payload 補原因，400 斷言收 200）→回綠；`RevokeKeyHandler` coverage 92%→**96%** — `5c9ebd4`。executor 零 blocker（88.4K tokens／43 calls／5.0 分）
- **ADR-023 跨 harness hook 與 skill 單一來源落地（2026-07-10）** — 第一層防線集中 `scripts/agent/hook.py`（五 action），`.claude/hooks/` 五檔退役；`.claude/settings.json`＋新 `.codex/hooks.json` 薄 wiring；9 個 project skills 以 `.agents/skills/` symlink 曝光（Tessl 項依 §9.3 D-2 維持 untracked）；驗證矩陣 12–15a／15b／16／23–23b 改登記共用核心。驗收：adr-lint 23 檔／fast gate／舊路徑 grep 0 命中全綠，另以模擬雙 harness payload 黑箱複驗 20 項（block=exit 2／allow=exit 0／secret scrub／marker 去重）全過，且本 session 活體驗證 session-context 注入＋去重 — `1a8e315`（lesson 先行 `22c9752`）
- **scenario「Secret Scanner 批次自動撤銷」真 Red→Green，19/46 — Wave 2（02_RevokeKey 全 7 場景）收官** — 兩契約缺口先經使用者裁決（api-spec 新 §3.2.9 內部批次端點，無 tenantId 全域 prefix 掃描／通知走 outbox 事件 `KeyLeakNotificationRequested` 含 audiences）；orchestrator 設計裁決＝批次 handler 組合復用 `IRevokeKeyHandler`（逐鍵委派白拿 guard／successor 清理／KeyRevoked，identity map 保證通知事件疊加在同一 tracked instance）。自然紅 A（undefined steps）＋自然紅 B（endpoint 未 Map，404）→ 綠 24/27；Architecture.Tests 14/14；`RevokeLeakedKeysHandler` coverage gate 自動納管 94.4% — `0072337`。executor 137.2K tokens／76 calls／8.5 分，申報兩處背離：(1) 編譯依賴使紅 B 改以「暫緩 Map 註冊」取得（紅仍為真跑）；(2) **spec 瑕疵**：orchestrator 漏查「觸發主動快取失效」Then 也讀 root `keyId`，executor 以 fallback（無 `keyId` 時取唯一 seeded key）擴修並留註解——spec 累積注意新增一條：復用既有 Then 前逐一核對該 step 讀取的 response 欄位在新 wire 形狀下存在

## 待驗證

- 一般互動式 Codex session 首次載入或 hook definition 變更後，仍須由使用者在 Codex `/hooks` 檢視並 trust（harness 安全邊界，repo 側無法代辦）。

## 已嘗試且失敗的方法

- sandbox 內首次 `scripts/ci-checks.sh fast` — `dotnet format` 的 restore operation failed；同指令在核可的可連線環境重跑後全綠，屬 sandbox network 限制，非程式失敗。
- machinery 故意紅首次以 zsh 唯讀變數 `status` 接 exit code，後續 restore trap 又拿到空 mode；已 `chmod 755 scripts/agent/hook.py` 恢復並重跑 machinery／fast gate 綠，教訓記於 `tasks/lessons/20260710-zsh-status-readonly-and-restore-trap.md`。

## 待裁決

- 跨全檔僅剩 Tessl 擱置項（`tasks/process-improvement-plan.md` §9.3 D-2）與 §8.3 低優先開環觀察（zh-lint 掃描範圍僅及 `git ls-files`），兩者皆非阻塞。

## 下一步（每項獨立可中斷；優先序供參，取捨由規格擁有者決定）

1. **A2 正式閉環（小項，test-only；使用者 2026-07-05 指示優先）**：CreateApiKey「系統產生 KeyCreated 事件」Then 依 ADR-020 §4 補 outbox row 斷言（取代 response-body 代理）＋重跑 `bash scripts/mutation-test.sh KeyLifecycle` 驗證 `ApiKey.cs` AddDomainEvent(KeyCreated) mutant 轉 killed、更新 stryker 歸檔。
2. **產品主線 Wave 3 起點**：27 個 `@ignore` 等待實作（backlog→progress 只能由使用者晉升）。下一個：`03_SuspendResumeKey.feature`「成功暫停金鑰」——**Wave 3 前置：基礎設施解鎖點「AuthToken 機制」尚未建立**（`tasks/bdd-progress.md` 解鎖表：Security Admin／Consumer／System 的 JWT）。開工前先勘查 03 檔場景是否真的依賴 actor 身分（若首場景不辨識 actor，可能先行；若需 AuthToken，屬新基礎設施＝架構決策，依 Working Agreement 進 Plan-first 求使用者核可）。派工一律用 `tasks/_templates/executor-spec.md`；spec 精度注意（累積）：測試計數勿外推（FunctionalTests dll = 46 場景＋5 hasher）、migration 需 `dotnet tool restore` 前置、帳面更新排在 ci-checks 之前、scratchpad 訊息檔名帶場景代號、**復用既有 Then 前逐一核對該 step 讀取的 response 欄位在新 wire 形狀下存在**。
3. **validation slice 前置合約已備**（ADR-017 Implementation Rule 6）：落地時必須帶 KeyHash 唯一索引 migration、`FixedTimeEquals` 複核、效能 smoke（P99 < 50ms／≥100 RPS）並同 commit 登記矩陣 — 效能無防線區在該點消除。todo #7 併發 guard 仍開放。
4. **小項**：todo #14–#18、#21–#24 housekeeping。
5. **觸發制（勿提前實作）**：checkpoint 分流（`tasks/checkpoints/<workstream>.md`）與 bdd-progress 帳面生成化，規格已定於 ADR-021 §2／§3，觸發條件「第二個常設寫者出現」成立時依規格執行，不需新開 ADR；其餘 CI 端 trailer 覆核／Discovery 解凍見 `tasks/todo.md`「觸發制擱置項」段。

## 工作區狀態警告

- 2026-07-10 Codex harness parity 收尾 failure triage：三個 REPEAT 仍為既有簽名（`== not found` ×4／`cd backend` ×2／`Exit code N` ×3）；前兩者維持既有處置，`Exit code N` 雖增一筆仍摺疊多個不同指令、無共同根因，不轉 lesson／todo。`== not found` 與 heredoc 現由矩陣 23/23a 的共用 Claude/Codex hook 接管。批次落地（`1a8e315`）後複跑：三簽名計數未增，維持處置。
- 2026-07-05 首次 failure triage（ADR-018 決策 §3）處置紀錄：`(eval):N: == not found` ×4 → 已轉 lesson（zsh 等號展開）；`(eval):cd:N: no such file or directory: backend` ×2 → 不轉，探索性 cwd 誤試、無制度性根因；`Exit code N` ×2 → 不轉，簽名過泛（多個不同指令的非零退出被摺疊）、無共同根因。
- `.agents/skills/tessl__*`、`.tessl/`、`tessl.json`、`.claude/parked/`：既有 Tessl 相關 untracked 項，依 `tasks/process-improvement-plan.md` §9.3 D-2 裁決維持 untracked，不要 `git add`。2026-07-10 起 tessl MCP（原 `.mcp.json`）與 5 個 `tessl__*` skills 收納至 `.claude/parked/`（降低 session 初始載入）。
- 目錄歸檔（Tessl 相關 skill 目錄、`docs/arch-flow.html` 等可重產產物）另包處理，不在本檔範圍內處理。

## 如何接上

新 session 直接在 `main` 上工作：讀本檔即知全貌；`docs/orchestration.md` 是協調憲章，`tasks/process-improvement-plan.md` §1–§9 是歷史盤點紀錄（非必讀）。Claude Code／Codex 由各自 config 呼叫 `scripts/agent/hook.py` `session-context`，自動注入 must-read 與 `tasks/lessons/` active 教訓；Codex hook hash 變更後先在 `/hooks` trust。每條新檢驗記得「綠＋故意紅」；phase 收尾更新本檔前先跑 `scripts/failure-triage.sh` 並處置 REPEAT。
