---
name: domain-discovery
description: >-
  Domain exploration workflow (grilling interview → event storming → domain
  modeling → handoff) that turns a vague one-line idea into the three
  prerequisites required by requirements-analysis-design: a Context Map, key
  architecture decision topics ready for ADR drafting, and Bounded Context
  names with Core/Supporting/Generic classification. Strictly
  human-in-the-loop: exploration questions can only be answered by the user;
  when no user answer is available it stops and reports the known/unknown
  boundary instead of assuming. Does not write PRDs or Design Docs (use
  doc-coauthoring), does not produce any downstream specification content,
  and does not do Step 3+ detailed design (use requirements-analysis-design).
  Use when starting domain exploration from a fuzzy idea, running event
  storming, doing domain modeling, or when the user asks where a Context Map
  should come from.
metadata:
  trigger: '"/domain-discovery", "domain exploration", "event storming", "domain modeling", "領域探索"'
---

# Domain Discovery（領域探索）

承接「一段模糊想法」，經 Grilling → Event Storming → Domain Modeling → 交棒四個 Phase，產出 `requirements-analysis-design` 的 Prerequisite Guardrail 所要求的三項前置物。本 skill 解決的斷鏈：下游 skill 進入 Step 3 前要求 Context Map、關鍵 ADR、BC 分類已就位，但先前沒有任何 skill 負責產出它們。

## Language & Terminology (CRITICAL)

- **Primary language:** Traditional Chinese (Taiwan/TW).
- **Tech terms:** Keep proper nouns in English (e.g., "Bounded Context", "Aggregate", "Domain Event", "Context Map", "hotspot").
- **Markdown protection:** All structured output (事件清單、Context Map mermaid、詞彙表) MUST be wrapped in complete fenced code blocks or Markdown tables. Always ensure fenced code blocks are complete and closed.
- **Terminology normalization:** After drafting Chinese content, apply the `deai-editor` skill for cross-strait terminology correction (e.g., 程式碼 not 代碼, 物件 not 對象, 介面 not 接口). Do NOT maintain a separate term list here — `deai-editor` is the single source of truth.

## HITL 契約（CRITICAL，凌駕所有 Phase 流程）

探索一旦自答就失效——grilling agent 替使用者作答，產出的模型反映的是模型的想像而非領域事實。因此：

1. **Phase A–C 的問題只能由使用者回答。** 本 skill 不得替使用者作答、不得以「合理假設」「業界慣例」「常見做法」推進任何未決點。
2. **無法取得使用者回答時（使用者未回應、離線執行、派工情境無人可答）：立即停止推進。** 停止時必須輸出「已知／未知」邊界：
   - **已知**：至今由使用者親口確認的事實（引用出處輪次或輸入原文）。
   - **未知**：所有待答問題與未決點，逐條列出，標記為 hotspot／open question。
3. **任何未決點未經使用者裁決前，禁止產出完整 Context Map、禁止定案 BC 邊界與分類。** 帶著未決點硬走到 Phase D＝本 skill 的頭號失敗模式。
4. 停止不是失敗——輸出「已知／未知」邊界＋待答問題清單就是本輪的合法產出，交下一輪 grilling 續接。

## 邊界（紅線，不可越）

1. **不寫 PRD／Design Doc**：那是 `doc-coauthoring` 的工作（Step 1–2 文件本體）。本 skill 只產探索工件與交棒清單。
2. **不產出任何 BDD 場景或 `.feature` 內容**：本 repo 的新場景產出已凍結，該凍結不歸本 skill 解；本 skill 的產出止於 domain model 層，不落到可執行規格。
3. **不做 Step 3 以後的工作**：Context Integration Spec、Per-BC Detailed Design、Specification by Example 全部交 `requirements-analysis-design`。

另一條 ADR 紅線：本 skill 只產「ADR 草案清單（題目＋張力）」，**不代寫 ADR 本文**——ADR 起草是協調者職責，且必須走 `docs/adr/_template.md`。

## 工件落點

預設 `docs/design/discovery/<effort-name>/`；**派工方指定其他路徑時（如 scratchpad），以指定路徑為準**。每 Phase 的中間工件遞增落檔於同一目錄。

## 流程（四 Phase，嚴格順序，不可跳）

### Phase A — Grilling（HITL）

- **一次只問一個問題**，聚焦「業務事實與痛點」，禁止複合題（一題包多問即違規）。
- 每輪把使用者已確認的事實增補進 `facts.md`；輸入中已明示「還沒想清楚」「還在吵」的點，直接登記為未決點，不得代答。
- **停止條件：連續兩輪沒有新事實推翻或增補既有模型** → 進 Phase B。
- 無人可答 → 依 HITL 契約第 2 條停下，輸出「已知／未知」邊界後結束本輪。

### Phase B — Event Storming（HITL）

- 從 `facts.md` 盤點領域事件（**過去式命名**，如 `KeyRotated`）、排時間軸、標記 hotspot（爭議／未知點）。
- 產出 `events.md`：事件清單＋時間軸＋hotspot 清單。
- hotspot 的裁決仍屬使用者；取不到回答時保留為 hotspot，不得自行裁決。

### Phase C — Domain Modeling（AFK 起草、HITL 裁決）

- **AFK 起草**：由事件聚類推導 Aggregate 候選與 BC 邊界，提出 Core／Supporting／Generic 分類**建議**。
- **HITL 裁決**：分類與邊界由使用者裁決定案；未裁決前一切標記為「建議」，不得當作定案帶進 Phase D。
- 產出 `model.md`。

### Phase D — 產出交棒（AFK）

前提：Phase A–C 完成，且未決點已由使用者裁決、或明列於未決清單。五項產出：

| # | 產出 | 落檔 |
|---|------|------|
| 1 | Context Map（mermaid，含 BC 關係） | `context-map.md` |
| 2 | BC 清單＋Core／Supporting／Generic 分類 | `context-map.md`（同檔） |
| 3 | Ubiquitous language 詞彙表 | `glossary.md` |
| 4 | 關鍵架構決策的 ADR 草案清單——**只列題目與張力，不代寫 ADR 本文** | `adr-topics.md` |
| 5 | 未決事項清單（交 grilling 下一輪，或明列為 fog） | `open-questions.md` |

## Definition of Done

`requirements-analysis-design` SKILL.md 的 **Prerequisite Guardrail** 三項全數 ✅——下游 skill 的守門條件就是本 skill 的驗收標準，不另立清單、不複寫條文（單一來源在該 SKILL.md）。其中「至少一份關鍵 ADR」由協調者依本 skill 交付的 `adr-topics.md` 起草補齊，本 skill 的義務止於題目＋張力清單。

## 執行程序

1. **確認工件落點**：派工方指定路徑優先，否則用預設 `docs/design/discovery/<effort-name>/`。
2. **登記初始輸入**：把輸入想法原文存入 `facts.md` 開頭；輸入中已明示未決的點（「還沒想清楚」「還在吵」）直接登記為未決點。
3. **Phase A grilling 迴圈**：一次一問 → 使用者作答 → 增補 `facts.md`；連續兩輪無新事實即出場。**無人可答時依 HITL 契約停下**，輸出「已知／未知」邊界並結束。
4. **Phase B**：盤點事件、排時間軸、標 hotspot → `events.md`。
5. **Phase C**：AFK 起草模型建議 → 使用者裁決 → `model.md`。
6. **Phase D**：五項產出俱備落檔；`adr-topics.md` 只含題目＋張力。
7. **對照 DoD**：Prerequisite Guardrail 三項逐項核對；缺任何一項、或仍有未裁決 hotspot → 不得宣告完成，改輸出「已知／未知」邊界與 `open-questions.md`，回報停止點。
