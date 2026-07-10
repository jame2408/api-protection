# Loop Engineering 巡檢報告（2026-07-10，第二輪；首輪見 `loop-audit-2026-07-05.md`）

> 產出：`/loop-engineering` skill Phase 1–2（純分析）＋ Phase 3（D1–D4 裁決落地紀錄）。
> 性質：例行巡檢 — 產出物為缺口清單、待裁決點與裁決落地，非大批修復（合法節奏，非 hardening pass）。

---

## 1. 四迴圈健康度總覽

| 迴圈 | 狀態 | 一句話評語 |
|---|---|---|
| 執行迴圈 | **閉** | 抽查最近 12 commits：bdd-progress／矩陣／checkpoint 與實作全數同 commit；每場景自然紅→綠→故意紅→回綠有 checkpoint 逐條證據；bdd-lint 帳面一致性綠 |
| 防線迴圈 | **閉** | 三層皆實裝且活體驗證綠：hook.py 寫時攔截（hook-smoke 綠）、`core.hooksPath=scripts/git-hooks` 實裝、CI；六個 lint/check 腳本本次實跑全綠 |
| 規範演化迴圈 | **閉** | 規則文件近期變更全部對應 ADR commit；被拒挑戰有記錄（GPT-5.6 回饋處置、各 ADR Rejected 段、既往裁決反查 lesson）；governance clause 由 adr-lint 機械化 |
| 學習迴圈 | **半開（訊號到期）** | 機制健全，但常設觸發已成立未執行：active lessons = 16 ≥ 15（todo.md 常設觸發條款）；另兩條 active lesson 缺「落地:」欄 |

本次實跑證據：`adr-lint` / `bdd-lint` / `machinery-check` / `zh-lint` / `source-lint` / `hook-smoke` 全部 exit 0；`failure-triage` 40 筆記錄、3 個 REPEAT 簽名皆為 checkpoint 已處置項且計數未增，無新 REPEAT。

## 2. 死規則清單

**無新增死規則。** 矩陣「無防線區塊」7 條開放項逐條核對：每條均有明文裁決（使用者裁決不機械化 ×2、ADR-016 §4 裁決 ×2、本質不可機械化 ×1、ADR-017 已排定 ×2）或半防線誠實標注（矩陣反向同步、`--no-verify` 殘餘風險）。無「三欄全空且無裁決」的規則。

## 3. 缺口診斷表

| 實踐 | 所屬迴圈 | 斷在哪一段 | 證據 | 風險 | 建議補法 |
|---|---|---|---|---|---|
| Lessons triage 常設觸發到期 | 學習 | 訊號已亮、未回寫 | `tasks/lessons/` active = 16 ≥ 15（`tasks/todo.md`「Lessons triage 常設觸發」條款） | 中：session 注入量持續墊高，正是 token 經濟條款要防的 | 執行 triage 第四輪：可機械化者落地＋歸檔，目標降回 15 以下 |
| 兩條 active lesson 缺「落地:」欄 | 學習 | 格式 drift（非實質懸空） | `20260705-restore-tactic-depends-on-commit-status.md`（純程序紀律，依模板應填「本條 lesson」）；`20260705-spec-guard-chain-check.md`（實質已落地 executor-spec 模板，僅未用「落地:」欄位格式） | 低：其餘 14 條 active 均有落地欄，模板語意為必填 | 機械性補欄（2 行），可併入 triage 第四輪同 commit |
| todo #38 懸置 40 天 | 規範演化 | 決策點老化 | `tasks/todo.md` #38（ADR-002 §3 樹漏 `SharedKernel.Tests`，2026-05-31 提出，需裁決 erratum note vs new ADR） | 低：純文件勘誤，但 governance clause 卡住編輯路徑，只有使用者能解 | 一次性裁決結案（建議 erratum note — 附註不改決策本體，不觸發 governance clause 的「修改規則」語意；最終由使用者裁決） |

## 4. 反模式掃描

六項反模式（規則墳場／hardening 排程化／默契豁免／懸空 lessons／多處複寫／幽靈交付）逐項掃描：**均未命中**。豁免全數明文（`ALLOW_MULTI_IGNORE` / `ALLOW_FEATURE_MAINTENANCE` / `machinery-check:ignore` / `zh-lint:allow`）；CLAUDE.md 維持指針化；checkpoint 交接紀律嚴格。

## 5. 建議的前三個動作（依 ROI 排序）

1. **Lessons triage 第四輪**（觸發已成立；預估 20–40 分鐘）：盤點 16 條 active，可機械化者落地後改 `status: archived`；順手補齊第 3 節兩條缺欄。
2. **todo #38 裁決結案**（預估 5 分鐘裁決＋一個小 commit）：erratum note vs new ADR 二選一。
3. **無第三項** — 制度凍結啟發式下，本次巡檢未發現需要新防線的事故；下一份制度預算應讓位產品側（Wave 3 / AuthToken 勘查），與既有裁決一致。

## 6. 待裁決問題清單（2026-07-10 全數裁決收束）

| # | 問題 | 裁決（使用者採建議） | 落地 |
|---|---|---|---|
| D1 | Triage 第四輪何時執行？ | 立即執行＋根因「為何未觸發」 | 第 7 節三 commit |
| D2 | 「active lesson 必含落地欄」要不要加 lint？ | 不加 — 手工補欄結案；同類缺欄再現（重複出現）才機械化 | `e3c8a78` |
| D3 | todo #38：erratum note 還是 new ADR？ | erratum note — 樹為 illustrative 非決策條文 | `d3cc221`（ADR-002 Status Erratum＋todo #38 結案） |
| D4 | 觸發門檻 15 = 習慣型地板 15，條款永久到期 | 門檻調升 20（不動被拒過的歸檔判準） | todo 條款／failure-triage／checkpoint 儀式句／矩陣 19e 四落點同 commit，兩分支複驗 |

## 7. Phase 3 執行紀錄（2026-07-10 使用者裁決 D1 立即觸發後補記）

**觸發未燃根因（使用者追問「為什麼達到 15 沒觸發」的答案）**：條款斷在「無訊號」段 — (1) `hook.py` `session-context` 計算並顯示 active 計數，但從不與 15 比對；(2) 條款本體只活在 `tasks/todo.md`「Non-blocking follow-ups」散文區；(3) phase 收尾儀式（checkpoint「如何接上」）只點名 failure-triage，未指向本條款。時間軸實證：第三輪 triage（07-10 10:15，非條款自燃，搭「載入瘦身」便車）後，12:14 `99566c4` 越線至 15、16:43 `22c9752` 至 16，其後四次 checkpoint 更新（99566c4／72fb678／96b3312／3ca3f4c／437fa2b）無一觸發。

**落地三 commit**：
- `e3c8a78` — 兩條 active lesson 補「落地:」欄（第 3 節格式 drift 修復）。
- `ba0106c` — source-lint 新增 `bad_generated` 段（generated_code 限 Migrations 區段，矩陣 9h，綠＋故意紅驗證）；lesson `20260710-generated-code-marker` 防線代記歸檔。
- `51a9157` — failure-triage 報表補 `report_lessons_count` 門檻判定（訊號落點避開 session-context——ADR-008 Rule 6 需新 ADR；三分支驗證＋ADR-018 Rule 2 鎖定行為複驗不變）；checkpoint「如何接上」儀式句與矩陣 19e 同 commit 登記。

**Triage 第四輪盤點結論（16 條全數處置）**：歸檔 1（generated-code，gate 接管）；補欄 2；維持 active 13 — 其中習慣型 8（orchestrator 越位／不存在斷言／grep 反查／token 預算／凍結啟發式／既往裁決反查／restore 快照法／zsh status）、範本承載 4（executor-spec 取證欄／執行期值／guard 鏈／故意紅欄，ADR-019 Alternatives 裁決「非 gate 不歸檔」）、憲章承載 1（token 經濟反模式，orchestration §5）、部分機械化餘行為紀律 2（heredoc 餘兩段、ca-prefix 前瞻程序）。現況 active = 15。

**結構性發現 → D4（新增待裁決，已登記 checkpoint）**：不可歸檔地板（15 條）＝門檻（15），條款將永久顯示到期。選項：(a) 門檻調高至 20；(b) 拿掉數字門檻改 phase 收尾例行盤點；(c) 重提歸檔判準擴充（原拒：ADR-019「二階制度修訂＋稀釋防線代記」；新事證：地板＝門檻失去鑑別度）。

## 8. Delivery Manifest

- 目標 artifact：本檔 — 寫入成功；裁決全數收束後歸檔至 `tasks/archive/loop-audit-2026-07-10.md`（比照首輪命名慣例）。
- 完整性：Phase 1（盤點）＋ Phase 2（診斷）＋ Phase 3（D1–D4 全數裁決並落地）完成；checkpoint 已同步落帳。
- 未使用 fallback；無待裁決餘項。
- 落地 commits：`e3c8a78`／`ba0106c`／`51a9157`／`22f3cac`（D1 批次）＋ `d3cc221`（D3）＋ `3545f8e`（D4）。
