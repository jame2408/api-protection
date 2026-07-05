# Checkpoint

> 唯一續接入口（`docs/adr/adr-013-content-tiering-and-injection-slimming.md` 決策 (c)）。欄位比照 `tasks/_templates/checkpoint.md`；歷史交接紀錄見 git log（本檔內容取代 `tasks/process-improvement-plan.md` §8.5 原文）。新 session 直接讀本檔即可接手，不需要先讀 plan 全文。

---

## 分支

`main` only（TBD 轉換已完成，見「已完成」段）。

## 已完成（含 commit hash）

- TBD 分支轉換：`hardening/architecture-tests-mvp` 併入 main（衝突依裁決取 hardening 版）並退役（remote + local 皆刪除）— `5647b21`
- main 分支保護（required status check）解除，CI on main 綠（run 28725618658）
- Phase I P1 寫後語法驗證 hook — `d1ee08d`
- Phase I P2 機制自體健檢（`scripts/machinery-check.sh`）+ 合併後 gitignore 豁免修復 — `d756e50` `bb2bcfc`
- Phase I P3 ADR-012 憲章修訂（unverified_success 條款、並行派工規則、checkpoint 欄位、冷啟動 prompt、TBD 分支紀律） — `56ff07d`
- O-8（subagent 事實覆核未機械化）已由 ADR-012 決策 (a) 關閉
- Phase J：ADR-013 內容分級 — CLAUDE.md 197→111 行、注入改 Active-Rule 行（-56%）、本檔成為唯一續接入口 — `fb14f8b`
- Phase K：tasks/ 歸檔（phase specs → `tasks/archive/`、todo 結案項收攏、指針全修） — `ac8bdaa`
- 產品主線首戰：scenario「租戶狀態非 Active — 拒絕建立」以 orchestrator 寫 spec → executor 實作模式落地（slice 早已完整，僅移 `@ignore` + 進度檔同 commit），3/44 — `39b2ecc`
- Executor spec 範本 + lessons（故意紅義務、取證紀律、orchestrator 界線澄清）— `fee94c9`
- scenario「Consumer 不屬於該租戶 — 拒絕建立」Red→Green（step 補 seed Active tenant，production 未動），4/44，首次套用 executor-spec 範本 — `9101bff`
- QA #1 coverage gate：ADR-014（度量 = 全套件含 BDD、逐 `*Handler` 類 ≥ 80%）+ `scripts/coverage-check.sh` 接線 full gate，綠＋故意紅驗證過，矩陣無防線區再消一條 — `e94a381`
- ADR-015 依賴弱點 audit gate：`Microsoft.OpenApi` 弱點（GHSA-v5pm-xwqc-g5wc）以 CPM transitive pin 2.7.5 消除、NU1903/NU1904 升 build error，綠＋故意紅驗證過 — `94c22b7`

## 待驗證

- 無排定事項。

## 已嘗試且失敗的方法

- 無（本檔案為遷移產物，非任務執行紀錄；後續 session 使用本欄位記錄自己任務的失敗嘗試）。

## 待裁決

- 無排定事項；跨全檔僅剩 Tessl 擱置項（`tasks/process-improvement-plan.md` §9.3 D-2）與 §8.3 低優先開環觀察（zh-lint 掃描範圍僅及 `git ls-files`），兩者皆非阻塞，不需要立即裁決。

## 下一步（每項獨立可中斷；優先序供參，取捨由規格擁有者決定）

1. **產品主線**：40 個 `@ignore` BDD scenario 等待實作（backlog→progress 只能由使用者晉升）。下一個：`01_CreateApiKey.feature`「Active 金鑰數達到上限 — 拒絕建立」（handler guard 與 `GivenActiveKeyCount` step 皆已存在，可能是啟用型 → 依 executor-spec 範本適用「故意紅」欄）。派工一律用 `tasks/_templates/executor-spec.md`。
2. **hash 演算法 ADR**（todo #5）：驗證熱路徑實作前必須裁決（Argon2id / HMAC / BCrypt 續用），連帶 todo #7 併發 guard、#8 constant-time 比較。
3. **小項**：todo #14–#18、#21–#24 housekeeping。
4. **QA #2 變異測試（Stryker.NET）**：Wave 1（`01_CreateApiKey` 全 10 場景）全綠後啟動；範圍鎖 KeyLifecycle + TenantManagement，跑法為 on-demand script 或 CI 週期性 job，**非 gate**（使用者 2026-07-05 核准排程）。

## 工作區狀態警告

- `.agents/`、`.claude/skills/tessl__*`、`.mcp.json`、`.tessl/`、`tessl.json`：Tessl 相關，依 `tasks/process-improvement-plan.md` §9.3 D-2 裁決維持 untracked，不要 `git add`。
- 目錄歸檔（Tessl 相關 skill 目錄、`docs/arch-flow.html` 等可重產產物）另包處理，不在本檔範圍內處理。

## 如何接上

新 session 直接在 `main` 上工作：讀本檔即知全貌；`docs/orchestration.md` 是協調憲章（模型分級、executor 義務、全域停止條件），`tasks/process-improvement-plan.md` §1–§9 是歷史盤點紀錄（背景資料，非必讀）。`.claude/hooks/session-init.sh` 會自動注入 must-read 規則與 `tasks/lessons.md` Active 區教訓。每條新檢驗記得「綠＋故意紅」驗證；任務完成後回來更新本檔（覆寫「已完成」「下一步」等欄位為當下實況，不需保留歷史版本——歷史紀錄在 git log）。
