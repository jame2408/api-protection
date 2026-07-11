# 上游管線地圖（Upstream Pipeline Map）

> 目的：把「多模型統一開發 loop」的上游段（模糊想法 → 排序完成的 backlog）鋪成一張低解析度地圖，供後任協調者接手實作與驗證。本檔是**索引不是倉庫**：決策細節住在各自的 spec / ADR / commit，此處只留一行 gist 與連結。體例借自 wayfinder 方法論的 map 結構——2026-07-11 分析裁決不整包引入該 skill，只留用「map-as-index」與「fog-of-war 開票判準」兩思路，本檔即其實例。

## Destination（終點）

上游管線與下游（`bdd-vertical-slice` 一段）同等成熟：每一階段有 skill 承載、有驗證設計（skill 等價物的「綠＋故意紅」）、任何常設大型模型僅依文件即可接手執行（`docs/orchestration.md` §1 明文規則 (ii)）。終點判定：兩個缺口 skill 依 spec 落地並通過驗證，既有兩 skill 的驗證設計自 fog 升格完成。

## Notes

- 背景：Fable 5（顧問級）2026-07-12 起不可用。本圖與兩份 spec 是其設計判斷的凝結；實作與驗證由後任協調者派 executor 依 spec 執行，不需要顧問級模型在場。
- 驗證哲學：skill 的「綠」＝回放 golden trace（本 repo 手工走過一次完整上游，產物俱在）或活體任務，產出與已知良好結果**結構等價**；「故意紅」＝餵入刻意不完整或含依賴倒置的輸入，skill 必須停下（HITL 提問或輸出解鎖點），不得腦補。
- 派工義務：一律用 `tasks/_templates/executor-spec.md`；`fork_turns=none` 時 spec 須明列 active lessons 讀取義務。

## 管線全景與缺口

| # | 階段 | 承載物 | 狀態 |
|---|------|--------|------|
| 0 | 模糊想法 → Context Map／BC 分類／關鍵 ADR | `domain-discovery` skill | **已落地**（`4edb8ad`，故意紅＋HITL 回放驗證通過） |
| 1–2 | PRD／High-level Design | `doc-coauthoring` skill | 存在，未經本 loop 驗證 |
| 3–5 | BC 契約 → 聚合規格 → BDD 場景 | `requirements-analysis-design` skill＋`docs/design-methodology.md` | 存在，未經本 loop 驗證；Step 5 受 Discovery 凍結 |
| 6 | 場景 → wave 切分＋基礎設施解鎖點 | `backlog-decomposition` skill | **已落地**（`5b7bf60`，回放＋故意紅驗證通過） |
| 7 | 場景 → 實作 | `bdd-vertical-slice`＋驗證矩陣 | 20/46 實戰淬煉，不屬本圖 |

Golden trace（回放驗證的對照基準）：`docs/design/prd.md` → `design-doc.md` → `context-integration-spec.md` → `docs/detailed-design/`（5 BC）→ `docs/bdd/`（5 BC）→ 46 場景 feature 檔 → `tasks/bdd-progress.md` wave 表＋解鎖點表。

## Decisions so far

- **視窗轉向 charting**（2026-07-11 使用者裁決）— Fable 5 剩餘視窗自 BDD 進度轉上游 charting；spec 範圍＝domain-discovery＋backlog-decomposition 至 executor 可實作精度；既有兩 skill 的驗證設計列圖不列規格。
- **wayfinder 不整包引入**（2026-07-11 分析裁決）— 階段錯配／制度凍結／依賴鏈缺失三理由；留用 map-as-index 與 fog-of-war 兩思路。
- [domain-discovery spec](specs/skill-domain-discovery.md) — 缺口 0 的 skill 規格；DoD 錨定 `requirements-analysis-design` 的 Prerequisite Guardrail 三項。
- [backlog-decomposition spec](specs/skill-backlog-decomposition.md) — 缺口 6 的 skill 規格；回放 46 場景 golden trace 驗證，可全 AFK，建議首個實作。
- **domain-discovery skill 落地**（2026-07-11，`4edb8ad`）— 兩缺口全數銷案：故意紅（無人可答輸入停在首問、輸出已知／未知邊界、零腦補）＋HITL 回放（六輪 grilling → 五 BC 邊界 5/5 等價；分類差異兩處可解釋且顯式留痕：Access Policy Core/Supporting 裁決變異、租戶粒度列 adr-topics 待決；旁證＝adr-topics #1 獨立重推導出 pending 的 hash 演算法 ADR 題目）。比對報告見 scratchpad `discovery-replay/comparison.md`（session 工件，結論已錄於此）。
- **backlog-decomposition skill 落地**（2026-07-11，`5b7bf60`）— 首個「spec→skill→回放驗證」全程走通的先例：回放 wave 表與 golden trace 逐列吻合、三解鎖點時點相同、故意紅（只餵 05_RotateKey 藏時鐘線索）一次命中；executor 加值兩處（準則 2 展開四類信號掃描表＋反向核對、準則 1 補 seed 例外）。

## Not yet specified（fog — 「能精確陳述問題」才升格為工作項，勿預切）

- 既有兩 skill（`doc-coauthoring`／`requirements-analysis-design`）的回放驗證設計 — 待兩缺口 skill 的驗證方法先被證明，沿用同型設計（使用者 2026-07-11 裁決：列圖不列規格）。
- Discovery 解凍規格 — 觸發與落點已登記 `tasks/todo.md` 觸發制擱置項（首個真新需求出現時另開 ADR；ADR-022 明文排除範圍）。
- `docs/design-methodology.md` 與 skill 內文的單一來源整併 — domain-discovery 落地時裁決，避免雙寫 drift。
- 上游 skill 的機械化防線（lint／hook）— 依制度凍結啟發式，skill 實跑出現觀察到的失敗才立法。

## Out of scope

- 整包引入 wayfinder 及其伴生 skill 鏈（/grilling、/prototype、tracker 設定）— 已裁決不引入，理由見 Decisions。
- 解凍 Discovery／授權新場景產出 — 兩 spec 的驗證一律走回放與活體 ADR，不觸碰凍結條款。
- 產品側 BDD 進度（revokedBy 小包、Wave 3 續）— 主線續接入口仍是 `tasks/checkpoint.md`，不進本圖。
