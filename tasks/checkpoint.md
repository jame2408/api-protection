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
- ADR-016 Roslyn analyzer gate：latest-recommended + CA 升 error + BannedApiAnalyzers（禁 `Xunit.Assert`），baseline 31 處清償（修正/generated_code/documented 降級），無防線區再消三條（CA2016/CA2200/FluentAssertions）、`.Value` 條明文裁決不機械化 — `4e60c71` `7bb4053`
- scenario「Active 金鑰數達到上限 — 拒絕建立」Red→Green（`GivenActiveKeyCount` 補條件式 seed tenant+consumer，production 未動；guard 早已存在），故意紅（`>=`→`>` 使測試 422 vs 409 轉紅）orchestrator 親自重演確認，5/44 — `26a1160`
- lessons triage 機械化包（使用者核准全包）：source-lint 新增 MSBuild XML 合法性／bash 3.2 相容／CreateScope 禁令（Middleware、Program.cs 豁免）三段，各過綠＋故意紅（executor 與 orchestrator 各一輪）；四條 lesson 歸檔（Active 13→9，注入量下降）；矩陣登記 9a/9b/9c；todo 增設 triage 常設觸發（Active ≥ 15 或 phase 收尾） — `b179fb2`
- 全 repo 文件衝突掃描（三路平行探查 + orchestrator 逐條裁決）：規範層 8 處衝突修繕（di.rule.md §D 範例對齊 CreateScope lint、ILogger 枚舉三處統一三層版、matrix 失效引用全修、已機械化 pattern 標注、CLAUDE.md coverage 措辭對齊 ADR-014）— `be0152e`；自建雙 skill（bdd-vertical-slice／lesson）指針化對齊 2026-07 制度 — `f90bf3d`；執行面文件七項核對全相符、ADR 間無隱性衝突；ADR-004 第 6 類正名與 upstream skill 凍結 gate 記入 todo follow-up
- scenario「金鑰名稱在同 Consumer + Environment 下重複 — 拒絕建立」Red→Green（`GivenKeyNameAlreadyExists` 補條件式 seed，production 未動；guard 3 早已存在），自然紅（404 vs 409）＋故意紅（guard 反轉致 422）雙取證，executor 一次到位、orchestrator 親自重跑全套件放行，6/44 — `e70eeed`
- 架構防線 enrollment gap 修復：`BoundedContextIsolationTests` BC 名單改動態發現（掃 `backend/src/`，漏 ProjectReference 時 fail-loud 指路修法）+ guard Fact 鎖已知最小集合；故意紅 A（FakeBc 自動入列轉紅）與 B（KeyLifecycle→TenantManagement 違規偵測）雙取證；矩陣 13→14 同 commit — `a4094b3`
- ADR-017 hash 演算法裁決＋實作同 commit：HMAC-SHA256 + pepper（使用者裁決，實測基準 Argon2id 44ms/HMAC 0.0003ms + prefix 不唯一致 salted KDF 不可索引查找）、熵 96→128-bit、BCrypt 依賴全移除、`IApiKeyHasher`（Domain）/`HmacApiKeyHasher`（Infrastructure，pepper 缺值 fail-fast）、hasher 測試 5 項；PRD R-STR-01/02 高熵豁免、design-doc Q1 回填、矩陣 19d、todo #5 結案同批 — `abc71aa`
- scenario「指定的 Scope 不存在 — 拒絕建立」Red→Green（seed 落點裁決：「不存在」語意的 Given 維持 no-op，tenant/consumer seed 落 When step，抽 `SeedDefaultTenantAndConsumerIfMissingAsync` helper 供後續無 Given 場景複用；production 未動），自然紅＋故意紅雙取證，7/44 — `03a49f2`
- 新流程全關卡驗證輪：`ci-checks.sh full` 實跑綠（含 coverage gate）、pre-commit 兩條 staged guard 合成故意紅、`pre-tool-edit.py` 合成 payload 故意紅＋綠、failure-triage 無新 REPEAT、pre-push/CI 接線與遠端一致 — 全數「綠＋故意紅」雙證，無 repo 改動
- scenario「未指定任何 Scope — 拒絕建立」Red→Green（`WhenConsumerCreatesKeyWithEmptyScopes` 接 `SeedDefaultTenantAndConsumerIfMissingAsync`，production 未動；guard 4 前半早已存在），自然紅（404 vs 400）＋故意紅（guard 條件 `if(false)` 級聯落 guard 5 致 422）雙取證，orchestrator 親自以 pre-push full gate 放行（13 passed/36 skipped、coverage 96.4%），8/44 — `fcd8063`
- scenario「到期時間已過 — 拒絕建立」轉綠，9/44 — 首個真 blocker：佔位 scope `any:read` 從未註冊，guard 4b 先於 5a 短路成 422（executor 正確停止回報，未擴權）；裁決把 scope 註冊併入 seed helper 並改名 `SeedDefaultPreconditionsIfMissingAsync`（下一場景 guard 5b 同坑，一次修除）；故意紅（guard 5a 改 `if(false)` 致 201 vs 400）、7/44 與 8/44 同輪綠證無回歸；新 lesson「spec 須沿 guard 鏈核對請求形狀、佔位常值視同執行期值」（Active 12 條）— `c15d836`
- scenario「到期時間超過最大允許有效期 — 拒絕建立」Red→Green，**Wave 1 收齊**（01_CreateApiKey 全 10 場景綠），10/44 — 自然紅（404 vs 422）＋故意紅（guard 5b 改 `if(false)` 致 201 vs 422），production 未動，executor 零 friction 一次到位 — `a81e4b0`
- test-only 重構 pass（Wave 1 收齊後既定工作）：`CreateApiKeySteps.cs` seeding 三份收編（`GivenActiveKeyCount`／`GivenKeyNameAlreadyExists` 改呼叫 `SeedDefaultPreconditionsIfMissingAsync`，順帶拆除 `any:read` 未註冊潛在坑）＋九個 When 樣板抽 `PostCreateKeyAsync`，-117/+28，行為零改變（15 passed/34 skipped 前後一致）、regex 與 Then 區零觸碰 — `65a3df9`
- Stryker survived 處置包（使用者裁決 A–D 全執行）：Batch 1 斷言強化＋跨租戶 seed 正名 — `5efed80`；Batch 2 TimeProvider 注入（production-only）— `f2c1079`；Batch 3 FrozenTimeProvider＋到期精確邊界場景 ×2（44→46，12/46）— `d4542cb`；重跑對照 KeyLifecycle 54.39%→**73.68%**、TenantManagement 72.73%→**81.82%**，A1/A3/A4/B5/B7 全轉 killed；A2 裁決降級（事件無發佈管道，待 Wave 2 事件基礎設施 ADR）；C/D 明文不處置 — 全紀錄 `tasks/archive/stryker-baseline-2026-07-05.md`
- Loop engineering 閉環包（使用者裁決 Q1–Q5 全數落地）：ADR-018 failures triage 回饋化＋`observations.jsonl` 除役（`scripts/failure-triage.sh`，矩陣 19e）— `75a9433` `1017a2b`；ADR-019 token 經濟四條升 `docs/orchestration.md` §5 可打勾規則、兩條懸空 lesson 落地欄收口 — `3d6b884`；BDD 紀律機械化（`scripts/bdd-lint.sh` 帳面一致性入 fast/full/CI + pre-commit staged guard 單移 `@ignore`／進度檔同 commit，三面故意紅取證，矩陣 9d/9e，CLAUDE.md CRITICAL 條註記防線）— `c6dce1d`；矩陣無防線區正名（Guard 正負場景裁決不機械化、refactor 紀律／backlog 晉升權／矩陣同步義務誠實登記）— `f1bbfdc`；審計報告歸檔 `tasks/archive/loop-audit-2026-07-05.md`＋首次 phase 收尾 triage 處置＋zsh 等號展開 lesson — 本 commit

## 待驗證

- 無排定事項。

## 已嘗試且失敗的方法

- 無（本檔案為遷移產物，非任務執行紀錄；後續 session 使用本欄位記錄自己任務的失敗嘗試）。

## 待裁決

- 跨全檔僅剩 Tessl 擱置項（`tasks/process-improvement-plan.md` §9.3 D-2）與 §8.3 低優先開環觀察（zh-lint 掃描範圍僅及 `git ls-files`），兩者皆非阻塞，不需要立即裁決。

## 下一步（每項獨立可中斷；優先序供參，取捨由規格擁有者決定）

1. **產品主線 Wave 2**：34 個 `@ignore` 等待實作（backlog→progress 只能由使用者晉升）。下一個：`02_RevokeKey.feature`「從 Active 狀態撤銷」（首個非 CreateApiKey 場景，涉及既有金鑰 seed 與撤銷 endpoint，spec 需先盤 RevokeKey slice 現況，依 lesson 沿 guard 鏈核對）。注意：RevokeKey 的「觸發主動快取失效」步驟需要事件基礎設施（outbox／dispatcher，**產品架構級 ADR**）— 調查已證實 domain event 目前無發佈管道（詳 `tasks/archive/stryker-baseline-2026-07-05.md` A2 段），Stryker A2 mutant（`ApiKey.cs:62`）屆時一併閉環。派工一律用 `tasks/_templates/executor-spec.md`。
2. **validation slice 前置合約已備**（ADR-017 Implementation Rule 6）：落地時必須帶 KeyHash 唯一索引 migration、`FixedTimeEquals` 複核、效能 smoke（P99 < 50ms／≥100 RPS）並同 commit 登記矩陣 — 效能無防線區在該點消除。todo #7 併發 guard 仍開放。
3. **小項**：todo #14–#18、#21–#24 housekeeping。

## 工作區狀態警告

- 2026-07-05 首次 failure triage（ADR-018 決策 §3）處置紀錄：`(eval):N: == not found` ×4 → 已轉 lesson（zsh 等號展開）；`(eval):cd:N: no such file or directory: backend` ×2 → 不轉，探索性 cwd 誤試、無制度性根因；`Exit code N` ×2 → 不轉，簽名過泛（多個不同指令的非零退出被摺疊）、無共同根因。
- `.agents/`、`.claude/skills/tessl__*`、`.mcp.json`、`.tessl/`、`tessl.json`：Tessl 相關，依 `tasks/process-improvement-plan.md` §9.3 D-2 裁決維持 untracked，不要 `git add`。
- 目錄歸檔（Tessl 相關 skill 目錄、`docs/arch-flow.html` 等可重產產物）另包處理，不在本檔範圍內處理。

## 如何接上

新 session 直接在 `main` 上工作：讀本檔即知全貌；`docs/orchestration.md` 是協調憲章（模型分級、executor 義務、全域停止條件），`tasks/process-improvement-plan.md` §1–§9 是歷史盤點紀錄（背景資料，非必讀）。`.claude/hooks/session-init.sh` 會自動注入 must-read 規則與 `tasks/lessons.md` Active 區教訓。每條新檢驗記得「綠＋故意紅」驗證；phase 收尾更新本檔前，先跑 `scripts/failure-triage.sh` 並處置 `REPEAT` 簽名（`docs/adr/adr-018-failure-triage-and-observations-retirement.md` 決策 §3）；任務完成後回來更新本檔（覆寫「已完成」「下一步」等欄位為當下實況，不需保留歷史版本——歷史紀錄在 git log）。
