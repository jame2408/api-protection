---
name: backlog-decomposition
description: >-
  Decompose an existing batch of BDD scenarios (feature files or a candidate
  scenario list) into an implementation-wave proposal: BC-dependency-ordered
  wave table, infrastructure unlock points, filename number-prefix suggestions,
  and per-wave rationale. Output is a proposal document only — backlog →
  progress promotion and .feature renaming stay with the user. Use when
  ordering an existing scenario set into waves, re-evaluating wave order after
  scenario amendments, or when the user says "backlog decomposition",
  "wave 切分", "切 wave", or "場景排序".
metadata:
  trigger: '"/backlog-decomposition", "wave 切分", "切 wave", "場景排序提案"'
---

# Backlog Decomposition（場景 → Wave 切分）

把一批**既存**場景（feature 檔或候選場景清單）切分為有依賴根據的實作 wave 排序提案，並前置識別基礎設施解鎖點。本 skill 承載的是「場景批 → `tasks/bdd-progress.md` 形態的 wave 表＋解鎖點表」這段原本靠手工判斷的切分工作。

## Language & Terminology (CRITICAL)

- **Primary language:** Traditional Chinese (Taiwan/TW).
- **Tech terms:** Keep proper nouns in English (e.g., "Wave", "Bounded Context", "Domain Event", "Guard", "Migration").
- **Markdown protection:** All structured output (wave 表、解鎖點表、Gherkin 引文) MUST be wrapped in complete fenced code blocks or Markdown tables. Always ensure fenced code blocks are complete and closed.
- **Terminology normalization:** After drafting Chinese content, apply the `deai-editor` skill for cross-strait terminology correction (e.g., 程式碼 not 代碼, 物件 not 對象, 介面 not 接口). Do NOT maintain a separate term list here — `deai-editor` is the single source of truth.

## 邊界（紅線，不可越）

1. **不升格**：本 skill 的產出是**提案文件**；backlog → progress 的升格、插入位置決定、以及 `.feature` 檔名前綴的實際調整，一律由使用者執行。本 skill 不得直接修改 `tasks/bdd-progress.md`，也不得指示任何人直接修改它（CLAUDE.md 紅線「Only the user promotes backlog → progress」）。
2. **不產出場景、不改 `.feature` 內容**：輸入是**既存**場景集；本 skill 只排序與識別依賴，不新增、不改寫任何場景文字。檔名前綴調整屬升格步驟，同樣歸使用者。

## 輸入

| 項目 | 說明 | 缺少時 |
|------|------|--------|
| 一批既存場景 | `.feature` 檔或候選場景清單（含 Given/When/Then 骨架） | 必要；無場景即無事可做 |
| `docs/design/context-integration-spec.md` | BC 通訊矩陣與事件契約，判定生產者／消費者順序的依據 | 停下向使用者提問，不得腦補跨 BC 依賴 |
| `docs/detailed-design/` 對應 BC 檔 | Command/Guard/State/Event 規格，判定場景落在哪個 Command | 停下向使用者提問，不得腦補 BC 歸屬 |

**輸入不足即停**：依賴判定缺乏文件根據時，輸出「待確認」清單並提問，不得以猜測補洞。

## 產出（單一提案文件，四項俱備）

寫入派工方指定的提案檔路徑（未指定時寫 scratchpad）。四項缺一即不完整：

### 1. Wave 表

欄位對齊 `tasks/bdd-progress.md` 現行格式：

```markdown
| 文件 | Wave | Feature | 場景數 | 涉及 BC | 特殊需求 |
|------|------|---------|--------|---------|---------|
```

- 「涉及 BC」精確到 command 層級（如 `KeyLifecycle.RevokeKey`），首波打通全 stack 者列出所有觸及 BC。
- 「特殊需求」記 seed 需求、角色需求、時鐘需求等（如「Rotating 狀態需 seed」「System 角色」）。

### 2. 基礎設施解鎖點表

```markdown
| 時機 | 需要的基礎設施 |
|------|--------------|
| Wave N 開始前 | …… |
```

每個解鎖點必須列在**首個使用它的 wave 之前**（時機欄寫「Wave N 開始前」或「Wave N 某類場景前」）。

### 3. 檔名數字前綴建議

為每個 feature 檔建議數字前綴（`01_`、`02_`……），使**字母序＝wave 順序**。現行「找下一個場景」機制是 `grep -rn "@ignore" backend/tests/FunctionalTests/Features/ | sort | head -1`，依賴此不變式——前綴建議必須維持它。僅為建議；實際改名由使用者執行。

### 4. 切分理由

每個 wave 一行 gist，說明依賴根據（引事件契約、guard、或基礎設施先後）。

## 切分準則（四條，逐條套用）

### 準則 1：BC 依賴與事件流排序

被消費事件的**生產者場景先行**；跨 BC 消費場景排在生產者 wave 之後。判定依據是 `context-integration-spec.md` 的事件契約（通訊矩陣＋Domain Event Payload 目錄），不是直覺。

- 例外——**seed 可繞過的狀態依賴不構成 wave 依賴**：若場景的 Given 前置狀態可由測試直接 seed（不需經過生產者 command 的 API 路徑），該場景不必排在生產者之後，但必須把 seed 需求記入 wave 表「特殊需求」欄（先例：`02_RevokeKey` 的「從 Rotating 狀態撤銷」直接 seed Rotating 狀態，得以排在 RotateKey 之前）。
- 建立整體 stack 的首個 happy path（第一次打通 API → Handler → DB → 事件全鏈）永遠是 Wave 1。

### 準則 2：基礎設施解鎖點前置識別（頭號失敗模式所在）

橫切需求（migration、認證 token、時鐘控制、外部 fake）必須被識別並明列於**首個使用它的 wave 之前**——**靜默吞掉任何一個橫切需求＝本 skill 的頭號失敗模式**。寧可多列並標註存疑，不得省略。

逐場景掃描 Given/When/Then 步驟文字，命中以下任一信號即產生對應解鎖點：

| 信號類型 | 步驟文字特徵 | 對應解鎖點 |
|----------|-------------|-----------|
| **時間信號** | 「當前時間已超過／尚未超過 ……」「N 小時後」「寬限期」「到期」「掃描」等任何需要**控制或推進測試時鐘**才能斷言的敘述 | 時鐘控制／時間推進基礎設施（如 FakeClock 實作 `ISystemClock` 並注入測試 host） |
| **角色信號** | 步驟區分操作者身分（Security Admin／System／Consumer）、或以身分差異為 guard（「權限不足」「僅限人為操作」「只有系統可以」） | 認證 token 機制（每種角色可簽發的 JWT） |
| **持久化信號** | 批次中的首個 wave 一律需要：DB schema 就位與測試間資料重置 | EF Core Migration＋Respawn（或等價物） |
| **外部系統信號** | 步驟涉及外部行為者或旁路系統（如 Secret Scanner、通知服務） | 對應的 fake／stub |

已知先例（golden trace）：Wave 1 前 EF Migration＋Respawn、Wave 3 前 AuthToken（ADR-024）、Wave 5 寬限期場景前 FakeClock（`ISystemClock`）。

檢核：每個信號命中都必須能在解鎖點表找到對應列；反向核對一次（表中每列也要能指回至少一個場景信號）。

### 準則 3：同 feature 聚 wave、happy path 先於 guard 負場景

- 同一 feature 檔的場景聚在同一個 wave；一檔含多個 command 群時可切成**相鄰**兩個 wave（先例：`05_RotateKey` 含 RotateKey 與 CompleteGracePeriod 兩群，切為 Wave 5+6）。
- wave 內部順序：happy path 首場景先行（建立垂直切片骨架），guard 負場景在後——負場景多為 test-only 啟用（先例：RevokeKey Wave 2 內部順序）。

### 準則 4：wave 大小以「單場景單 session」為前提

wave 是**排序單位不是派工單位**，切分不需顧慮 wave 內場景數上限；不要為了控制 wave 大小而拆散同 feature 的場景。

## 執行程序

1. **盤點場景**：逐檔列出場景標題＋Given/When/Then 骨架（不需全文）。
2. **對照 BC**：用 `docs/detailed-design/` 各 BC 檔的 Command 規格，標記每個場景落在哪個 BC 的哪個 command。
3. **標記事件流**：用 `context-integration-spec.md` 通訊矩陣，標記各場景生產／消費的 Domain Event 與跨 BC 呼叫。
4. **掃描橫切信號**：依準則 2 的信號表逐場景掃描，累積解鎖點清單。
5. **排 wave**：依準則 1 → 3 → 4 排序，準則 2 的解鎖點插入對應時機。
6. **產出提案文件**：四項產出俱備，附每 wave 一行切分理由；結尾聲明「本文件為提案；升格與檔名調整由使用者執行」。
