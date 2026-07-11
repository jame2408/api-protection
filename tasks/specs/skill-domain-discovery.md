# Skill Spec：domain-discovery（領域探索）

> 上游地圖缺口 0 的實作規格（`tasks/upstream-map.md`）。目標讀者：後任協調者與 executor。本 spec 定義 skill 的邊界、流程、產出契約與驗證設計；SKILL.md 逐字內容由 executor 依此產生。

## 1. 定位與邊界

**解決的斷鏈**：`requirements-analysis-design` 的 Prerequisite Guardrail 要求進入 Step 3 前先有 (1) Context Map、(2) 至少一份關鍵 ADR、(3) BC 名稱與 Core／Supporting／Generic 分類——但 repo 內沒有任何 skill 負責產出這三項。本 skill 承接「一段模糊想法」，經領域探索產出它們。

**明確不做**（寫進 SKILL.md description，避免觸發詞相撞——先例：todo #26 `requirements-analysis-design` 觸發詞與 `.feature` 凍結相撞）：
- 不寫 PRD／Design Doc（交 `doc-coauthoring`）。
- 不產出任何 BDD 場景或 `.feature` 內容（Discovery 凍結不歸本 skill 解，勿在 description 使用 BDD／scenario 類觸發詞）。
- 不做 Step 3 以後的工作（交 `requirements-analysis-design`）。

## 2. 流程（SKILL.md 主體結構）

四個 Phase，嚴格順序，每 Phase 產出遞增的中間工件落檔（落點：使用者指定的設計目錄，預設 `docs/design/discovery/<effort-name>/`）：

- **Phase A — Grilling（HITL）**：一次只問一個問題，聚焦「業務事實與痛點」，禁止複合題。每輪把已確認事實增補進 `facts.md`。停止條件：連續兩輪沒有新事實推翻或增補既有模型。
- **Phase B — Event Storming（HITL）**：從 facts 盤點領域事件（過去式命名）、排時間軸、標記 hotspot（爭議／未知點）。產出 `events.md`（事件清單＋時間軸＋hotspot 清單）。
- **Phase C — Domain Modeling（HITL 裁決、AFK 起草）**：由事件聚類推導聚合候選與 BC 邊界，提出 Core／Supporting／Generic 分類**建議**，由使用者裁決定案。產出 `model.md`。
- **Phase D — 產出交棒（AFK）**：彙整為 (1) Context Map（mermaid，含 BC 關係）、(2) BC 清單＋分類、(3) ubiquitous language 詞彙表、(4) 關鍵架構決策的 ADR 草案清單（只列題目與張力，不代寫 ADR 本文——ADR 起草是協調者職責且須走 `docs/adr/_template.md`）、(5) 未決事項清單（交 grilling 下一輪或明列為 fog）。

## 3. HITL 契約（CRITICAL，寫進 SKILL.md）

- Phase A–C 的問題只能由使用者回答；**skill 不得替使用者作答、不得以「合理假設」推進**。無法取得回答時停下並輸出「已知／未知」邊界。
- 這是 wayfinder 分析留下的核心洞察：grilling agent 一旦自答，探索就失效。

## 4. Definition of Done

`requirements-analysis-design` 的 Prerequisite Guardrail 三項全數 ✅ ——即下游 skill 的守門條件就是本 skill 的驗收標準（不另立標準，避免雙寫）。

## 5. 驗證設計

- **綠（回放，HITL、一次性）**：輸入「API key 管理服務」一句話想法＋使用者臨場作答，跑完四 Phase，產出與 `docs/design/design-doc.md` 既有 Context Map／五 BC（KeyLifecycle、AccessPolicy、TenantManagement、AuditCompliance、MonitoringDetection）**結構等價**（BC 邊界與分類一致；命名與細節允許差異，差異需逐條可解釋）。
- **綠（活體，替代路徑）**：下一個真實設計任務（如 hash 演算法 ADR 的前置探索）以本 skill 開場，產出能直接餵進協調者的 ADR 起草。
- **故意紅**：餵入一段刻意含未決裁決的想法（如「金鑰要支援某種輪替，但輪替策略還沒想清楚」），skill 必須停在 Phase A 提問或把該點列入 hotspot／未決清單；若它自行生成完整 Context Map 並替使用者決定輪替策略＝紅。

## 6. 落點與機械化慣例

- `.claude/skills/domain-discovery/SKILL.md`＋`.agents/skills/domain-discovery` symlink（ADR-023 慣例）；`scripts/machinery-check.sh` 的 skill links 檢查須綠。
- SKILL.md 語言規範沿用 `requirements-analysis-design` 的 Language & Terminology 段（正體中文、術語英文、fenced block 完整性、`deai-editor` 為兩岸用語單一來源）。

## 7. 派工註記

- 用 `tasks/_templates/executor-spec.md` 派工；spec 明列 active lessons 讀取義務與取證指令（machinery-check、雙 harness skill 可發現性）。
- 實作順序建議：在 backlog-decomposition **之後**——後者可全 AFK 驗證，先證明「spec→skill→回放驗證」這條路走得通，再做需要 HITL 驗證的本件。
