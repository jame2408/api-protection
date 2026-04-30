---
name: code-review
description: |
  Technical code review workflow (警察/審計員角色).
  Triggered by: review, audit, security, bug finding keywords.
  - PR Mode: Contains GitHub PR ID or URL → Review GitHub PR
  - Self Mode: No PR specified → Review local changes
  This project uses GitHub only; commands from `vcs-platform-commands.ref.md`.

  Required capabilities for Phase 2.5 (Impact & Dependency Analysis):
  - Exact search in repo (e.g. grep)
  - Semantic search in repo (optional)
  - Read file contents
  - Run git + GitHub CLI commands (gh; may require network)
metadata:
  trigger: /code-review, /review, or "review" keywords
---

# Code Review Workflow

Act as a **Senior Technical Reviewer** (警察/審計員) providing actionable feedback.

> **角色定位**：專注於找出問題、安全漏洞、潛在 Bug，而非生成或重構程式碼。

---

## Phase 0: Environment Detection (首次執行必須)

> ⚠️ 在載入任何 Reference 前，**必須先確定 `{CONFIG_ROOT}` 的值**

### Step 0.1: 檢測設定目錄

檢查專案根目錄中存在哪個 AI 設定目錄：

| 檢查目錄 | 若存在則設定 |
|----------|-------------|
| `.claude/` | `{CONFIG_ROOT}` = `.claude` |
| `.cursor/` | `{CONFIG_ROOT}` = `.cursor` |
| `.gemini/` | `{CONFIG_ROOT}` = `.gemini` |
| `.github/copilot/` | `{CONFIG_ROOT}` = `.github/copilot` |
| `ai-config/` | `{CONFIG_ROOT}` = `ai-config` |

### Step 0.2: 執行偵測

使用你的檔案系統工具檢查哪些目錄存在，找到第一個符合的目錄後設定 `{CONFIG_ROOT}`。

> 💡 **快取機制**：在同一對話中，`{CONFIG_ROOT}` 只需偵測一次

### Step 0.3: Skill 路徑

| 變數 | 路徑 |
|------|------|
| `{SKILL_DIR}` | `{CONFIG_ROOT}/skills/code-review` |
| `{SKILL_DIR}/templates/` | Report 輸出格式範本 |

---

## Tool & Shell Compatibility (必讀一次)

> 這個 repo 目標是同時支援不同 agent runtime（Warp / Claude Code / Cursor...）以及不同 shell（PowerShell / bash/zsh）。
> 因此本 Skill 盡量用「能力描述」寫流程；具體工具/指令差異請依環境讀取下列 reference。

- Runtime tool mapping:
  - Warp: `{CONFIG_ROOT}/references/runtime/warp.ref.md`
  - Claude/Cursor (generic): `{CONFIG_ROOT}/references/runtime/claude-cursor.ref.md`
- Shell notes:
  - PowerShell: `{CONFIG_ROOT}/references/shell/powershell.ref.md`
  - bash/zsh: `{CONFIG_ROOT}/references/shell/bash.ref.md`

## Reference Documents (Auto-Discovery with Naming Convention)

References are loaded dynamically based on **file suffix**. See **Phase 3** for loading logic.

### Naming Convention

| Suffix | Behavior | Purpose |
|--------|----------|---------|
| `*.rule.md` | ✅ **Auto-load** | Code review 檢查規則 |
| `*.guide.md` | ❌ Skip | 開發指南、教學文件 |
| `*.ref.md` | ❌ Skip by default | 純參考資料（僅在 SKILL.md **明確點名** 時才讀取，例如 VCS 指令、posting、runtime/shell mapping） |

### VCS Platform Commands (PR Mode)

| File | Purpose |
|------|---------|
| `{CONFIG_ROOT}/references/vcs/vcs-platform-commands.ref.md` | GitHub `gh` 指令對照、URL/ID 提取規則。PR Mode 必讀。 |

### Directory Structure (Project Root Relative)

> ⚠️ 路徑從專案根目錄開始，不受 SKILL.md 存放位置影響

| Tech Stack | Reference Directory | Auto-Loaded |
|------------|---------------------|-------------|
| All | `{CONFIG_ROOT}/references/general/` | Only `*.rule.md` |
| .NET / C# | `{CONFIG_ROOT}/references/dotnet/` | Only `*.rule.md` |
| Node.js | `{CONFIG_ROOT}/references/nodejs/` | Only `*.rule.md` |
| Python | `{CONFIG_ROOT}/references/python/` | Only `*.rule.md` |

### Loading Order (重要)

1. **先載入通用規則** → `{CONFIG_ROOT}/references/general/*.rule.md`
2. **再載入特定技術規則** → `{CONFIG_ROOT}/references/{stack}/*.rule.md`
3. **覆蓋原則**：特定技術規則 **優先於** 通用規則（Override）

> 新增 `*.rule.md` 檔案即自動載入，`*.guide.md` 作為參考但不自動載入

---

## Mode Detection (觸發條件)

### 觸發關鍵字

> ⚠️ 包含以下任一關鍵字即觸發此 Skill

| 類別 | 中文關鍵字 | 英文關鍵字 |
|------|-----------|-----------|
| **審查類** | 「review」「審查」「檢查」「看一下」「幫我看」 | `review`, `check`, `audit` |
| **錯誤類** | 「找錯誤」「有沒有問題」「有問題嗎」「bug」 | `find bug`, `any issue`, `problem` |
| **安全類** | 「安全」「漏洞」「資安」 | `security`, `vulnerability` |
| **指令類** | `/code-review`、`/review` | (explicit commands) |

### 平台偵測（PR Mode 必須）

進入 PR Mode 前，從 `git remote get-url origin` 確認為 GitHub remote：
- 含 `github` → GitHub（使用 gh）
- 否則 → 不進入 PR Mode；fallback 到 Self Mode 或請使用者確認

> 指令對照表見 `{CONFIG_ROOT}/references/vcs/vcs-platform-commands.ref.md`

### 模式判定邏輯

```
Step 1: 檢查是否包含 PR 識別
  - 包含 GitHub URL (pull/123) → PR Mode
  - 包含 "PR" + 數字 (e.g., "PR#42") → PR Mode
  - 包含 `/code-review <number>` → PR Mode
  - 以上皆無 → Self Mode

Step 2: 確認觸發條件
  - 包含上述任一關鍵字 → 觸發 Code Review
  - 不包含任何關鍵字 → 不觸發此 Skill
```

### 模式對照表

| Trigger Example | Mode | Data Source |
|-----------------|------|-------------|
| `/code-review 249` | PR | GitHub PR #249 |
| `review PR 42` | PR | GitHub PR |
| `https://github.../pull/42` | PR | GitHub PR |
| `/code-review` 或 `/review` | Self | `git diff HEAD` |
| `幫我 review 這段程式` | Self | `git diff HEAD` |
| `檢查有沒有安全問題` | Self | `git diff HEAD` |
| `這段程式有 bug 嗎` | Self | `git diff HEAD` |
| `幫我看一下這段 code` | Self | `git diff HEAD` |

### 與 coding-style 的分工

| 情境 | 觸發 Skill | 原因 |
|------|-----------|------|
| 「幫我優化這段程式」 | coding-style (Refactor) | 優化 = 改善程式碼品質 |
| 「幫我 review 這段程式」 | **code-review** (Self) | review = 審查找問題 |
| 「這段程式有問題嗎」 | **code-review** (Self) | 問題 = 找錯誤 |
| 「寫一個 Service」 | coding-style (Generate) | 寫 = 生成程式碼 |
| 「解釋這段程式」 | coding-style (Explain) | 解釋 = 說明原理 |

---

### PR Review Mode

1. **確認 GitHub remote**：讀取 `vcs-platform-commands.ref.md`，從 git remote 確認為 GitHub
2. **Fetch 詳情與 diff**：執行 `gh pr view <ID>` 與 `gh pr diff <ID>`
3. **確保 workspace 一致**：執行 `gh pr checkout <ID>` 切換到 PR 的 source branch（詳見 Phase 2.5 Step 2.5.0）
4. **Impact & Dependency Analysis**：從 diff 抽出關鍵實體，在專案內搜尋引用/定義（不只審 diff）
5. Load language-specific references
6. Apply review rules to **diff + 影響面**
7. Output to chat
8. (Optional) Post findings to PR as comment（讀取 `code-review-posting-github.ref.md`）

### Self Review Mode

1. Get local changes: `git diff HEAD` (包含 staged + unstaged 所有變更)
2. If no changes, inform user
3. **Impact & Dependency Analysis**：從 diff 抽出關鍵實體，在專案內搜尋引用/定義（不只審 diff）
4. Load language-specific references
5. Apply review rules to **diff + 影響面**
6. Output to chat only (不發布到任何地方)
7. 結尾詢問用戶是否需要執行 commit

### Decision Tree

**Step 1: Trigger Detection (是否觸發此 Skill)**
- IF request contains 審查類關鍵字（review、審查、檢查、看一下、幫我看）→ 觸發
- ELSE IF request contains 錯誤類關鍵字（bug、問題、錯誤）→ 觸發
- ELSE IF request contains 安全類關鍵字（安全、漏洞、security）→ 觸發
- ELSE IF request contains `/code-review` 或 `/review` → 觸發
- ELSE → **不觸發此 Skill，交由其他 Skill 處理**

**Step 2: Mode Detection (PR vs Self)**
- IF request contains GitHub URL (`pull/(\d+)`) → Extract ID → **PR Mode**
- ELSE IF request contains PR reference (`PR\s*#?\s*(\d+)`) → Extract ID → **PR Mode**
- ELSE IF request contains `/code-review <number>` → Extract ID → **PR Mode**
- ELSE → **Self Mode**

**Step 3A: PR Mode**
1. 讀取 `vcs-platform-commands.ref.md` 確認 GitHub remote
2. Clear proxy（如需）後執行 `gh pr view <ID>`、`gh pr diff <ID>`
3. IF PR is draft/WIP → STOP, inform user "PR is draft, skipping review"
4. ELSE → 執行 `gh pr checkout <ID>` 切換至 source branch（Phase 2.5 搜尋需本機一致）→ Go to Step 4

**Step 3B: Self Mode**

Run these commands to gather diff (使用 `HEAD` 同時包含 staged + unstaged):

```bash
# Get diff stats (staged + unstaged 所有變更)
git --no-pager diff --stat HEAD

# Get full diff for pattern analysis
git --no-pager diff HEAD

# Count changed files
git --no-pager diff --name-only HEAD | wc -l
```

- IF no changes detected → STOP, report "Nothing to review - no uncommitted changes found."
- ELSE → Go to Step 4

**Step 4: Impact Analysis + Load Checklist & Apply Review Rules**
1. **Phase 2.5 必做**：從 diff 抽出關鍵型別/常數/方法，在專案內搜尋引用與定義，鎖定影響面。
2. Detect project tech stack (check `*.csproj`, `package.json`, `requirements.txt`, etc.)
3. Load appropriate references (路徑從專案根目錄開始):
   - `.csproj` found →
     - 先載入 `{CONFIG_ROOT}/references/general/*.rule.md`
     - 再載入 `{CONFIG_ROOT}/references/dotnet/*.rule.md`（可覆蓋通用規則）
   - `package.json` found →
     - 先載入 `{CONFIG_ROOT}/references/general/*.rule.md`
     - 再載入 `{CONFIG_ROOT}/references/nodejs/*.rule.md`（如存在）
   - `requirements.txt` / `pyproject.toml` found →
     - 先載入 `{CONFIG_ROOT}/references/general/*.rule.md`
     - 再載入 `{CONFIG_ROOT}/references/python/*.rule.md`（如存在）
4. Apply checklist rules to the **diff + 影響面**（含 Phase 2.5 搜尋到的呼叫者/定義）。
5. Generate findings based on Confidence Scoring；僅依 diff 推論且未在專案內驗證者，不得標為確定 Bug。

**Step 5: Output**
- IF PR Mode:
  - Output review to chat (using PR Mode template)
  - IF user explicitly requests → 讀取 `{CONFIG_ROOT}/references/vcs/code-review-posting-github.ref.md` 並 Post comment
- IF Self Mode:
  - Output review to chat only (using Self Mode template)
  - DO NOT post anywhere external
  - IF no critical issues found → 詢問用戶「確認無誤後，是否需要我幫你執行 commit？」
  - IF has issues → 建議用戶修正後再 commit

---

## Review Philosophy

1. **Be a Technical Consultant, not a Process Robot** - Focus on catching real bugs, not formatting
2. **Verify Before Criticizing** - Check usage context before suggesting pattern changes
3. **Prioritize by Impact** - Security > Bugs > Performance > Style
4. **Respect Developer Intent** - Understand the "why" before questioning the "how"

### ⚠️ Diff-Only 不足：必須全局審視

**核心原則**：只審「差異 (diff)」會漏掉真正問題。未修改的程式碼中，**引用處、相依處、共用型別/常數** 往往隱藏問題。

| 審視範圍 | 說明 | 常見遺漏 |
|----------|------|----------|
| **僅看 diff** | 只掃變更行 | 假設「改 A 就只影響 A」，誤判結構或誤報 Bug |
| **全局審視** | Diff + 呼叫者 + 被呼叫者 + 共用定義 | 發現「改 A 導致 B 行為不一致」「常數只改一處、另一處沒改」 |

**參考**（業界做法）：
- **Google eng-practices**：["Look at the CL in broad context"](https://google.github.io/eng-practices/review/reviewer/looking-for.html#context) — 要看整個檔案、整個系統脈絡；只看變更的幾行容易誤判。
- **Design 優先**：["Do the interactions of various pieces of code make sense? Does it integrate well with the rest of your system?"](https://google.github.io/eng-practices/review/reviewer/looking-for.html#design)

**本 Skill 要求**：
- 在「下結論或寫成 Bug/Suggestion」之前，必須做 **Impact & Dependency Analysis**（見 Phase 2.5）。
- 若僅依 diff 推論出「應該怎樣、不應該怎樣」，而**未在專案內搜尋實際定義與引用**，該結論只能標為「建議」或「待確認」，不得標為確定 Bug。

---

## Phase 1: Triage (30 seconds)

**Quick assessment before deep review:**

| Change Size | Strategy |
|-------------|----------|
| **Small (<100 lines)** | Line-by-line review, focus on logic correctness |
| **Medium (100-500 lines)** | Review by file, focus on integration points |
| **Large (>500 lines)** | Architecture review first, then critical paths only |

**Skip review if (PR Mode):**
- PR is draft/WIP
- PR is automated (dependabot, renovate)
- PR is trivial (typo fix, comment update)
- You already reviewed this PR

**Skip review if (Self Mode):**
- No changes detected (`git diff` is empty)
- Only whitespace/formatting changes

---

## Phase 2: Context Gathering

### PR Mode
1. **Read the PR description** - Understand the intent
2. **Identify changed components** - Which layers are affected?
3. **Check for project guidelines** - `CONTRIBUTING.md`, `CLAUDE.md`(or `AGENT.md`, or `GEMINI.md`), `.editorconfig`
4. **Detect tech stack** - Check file extensions and project files

### Self Mode
1. **Run `git diff HEAD`** - Get all local changes (staged + unstaged)
2. **Identify changed files** - Which layers are affected?
3. **Check recent commit context** - What task is being worked on?

---

## Phase 2.5: Impact & Dependency Analysis（影響面與相依性，必做）

> ⚠️ **不做此階段，不得將任何「推論」當成確定 Bug 回報。** 僅依 diff 推論易造成誤判（例如假設某欄位已上提到 Base、實際程式碼並未如此）。

在套用規則 (Phase 3) 之前，必須先鎖定「變更的影響面」，避免只看差異行就下結論。

### Tool Selection Rules (跨模型一致性)

為了避免不同 agent / model 使用到較差的工具或跳過驗證，請遵守以下規則：

1. **Exact first**：當你已知明確 symbol（型別/方法/常數/欄位名）時，必須先用 exact search（例如 `grep`）找定義與所有引用。
2. **Read to verify**：凡是涉及結構推論（繼承、DTO 欄位、mapping、常數值、方法簽章），必須用 file read tool 直接打開定義檔案驗證；不得只依 diff 臆測。
3. **Semantic is optional**：semantic search tool 只用在「不知道精確名稱」或「需要找使用方式/模式」時；不得取代 exact search。
4. **No evidence → no bug**：若沒有完成 Phase 2.5 的查證（exact search +/or file read），不得將結論標為確定 Bug；只能標為 Suggestion / 待確認。

### Step 2.5.0: 確保本機 Workspace 與審查目標一致（前置條件）

> ⚠️ Phase 2.5 的搜尋（exact search / semantic search / read files）是針對**本機 workspace** 的實際檔案。若本機 branch 錯誤或版本落後，影響面分析會失真。

| 模式 | 前置動作 | 目的 |
|------|----------|------|
| **PR Mode** | 1. 執行 `gh pr checkout <ID>`<br>2. 確認該 branch 已與 remote 同步（必要時 `git pull`） | 確保搜尋的本機程式碼與 PR 實際變更一致 |
| **Self Mode** | 可選：提醒使用者確認當前 branch，若有需要可 `git pull --rebase` | 降低合併時與 remote 衝突的風險 |

**PR Mode 實務**：在執行 fetch view/diff 之後、Phase 2.5 搜尋之前，應先執行 `gh pr checkout <ID>`（見 `vcs-platform-commands.ref.md`），確保 workspace 處於該 PR 的 source branch 且為最新。

### Step 2.5.1: 從 Diff 抽出關鍵實體

從變更內容中列出：
- **變更的型別/介面/類別名稱**（例如 `ContentWithBannerResponse`、`BaseContent`）
- **變更的常數/列舉/欄位名稱**（例如 `IMAGE_MAX_BYTES`、`Color`）
- **變更的公開方法/API 簽章**（例如 `GetDailyWinnerListAsync`）

### Step 2.5.2: 在專案內搜尋「引用與相依」

對每個關鍵實體，在**專案程式碼**（非僅 diff）中執行搜尋：

| 搜尋方向 | 目的 | 做法 |
|----------|------|------|
| **誰呼叫這段程式** | 呼叫者是否仍假設舊行為、舊型別、舊常數？ | 用 exact search（例如 `grep`）找方法名/型別名/常數名 |
| **這段程式呼叫誰** | 被呼叫的 API/型別在別處是否也有一致定義？ | 從 diff 內出現的型別/常數名，先用 exact search 找定義，再用 file read tool 打開確認 |
| **共用型別/常數** | 同一常數、同一型別是否在「未變更檔案」中也有使用？定義是否只有一處？ | 用 exact search 找所有引用；必要時讀檔確認定義與引用處 |
| **繼承/實作關係** | Base 與衍生類的欄位是否真的如 diff 推測？ | 直接用 file read tool 打開 Base/Response 等檔案確認 |

> 具體使用哪個工具名稱，依你的 runtime 讀取 `{CONFIG_ROOT}/references/runtime/*.ref.md`。

### Step 2.5.2 實務執行指引

**核心原則**：必須查閱**本機端專案程式碼**（workspace 內的實際檔案），而非僅依 diff 推論。

**可用工具**：

1. **`grep`** - 精確字串搜尋
   - 用途：搜尋方法名、型別名、常數名在專案內的所有出現位置
   - 範例：
     ```bash
     grep -r "IMAGE_MAX_BYTES" src/
     grep -r "GetDailyWinnerListAsync" src/
     grep -r "class BaseContent" src/
     ```

2. **Semantic search tool（選用）** - 語意搜尋
   - 用途：當你不知道精確名稱、或要回答「這個方法/型別在哪裡被用到？」
   - 做法：用 runtime 的 semantic search tool 進行查詢（見 `{CONFIG_ROOT}/references/runtime/*.ref.md`）

3. **File read tool** - 讀取實際檔案內容
   - 用途：確認型別定義、繼承關係、欄位是否存在
   - 做法：直接打開檔案內容確認（不要只靠 diff 推論）

4. **File glob / pattern search tool（選用）** - 找相關檔案
   - 用途：當你只知道檔名 pattern（例如 `*Response.cs`）
   - 做法：用 runtime 的 glob 工具找檔，再用 file read tool 驗證

**執行順序範例**：

假設 diff 顯示「`ContentWithBannerResponse` 移除 `Color` 欄位」：

1. **Step 1（Exact search）**：搜尋 `ContentWithBannerResponse` 的所有引用（找 callers）
   ```bash
   grep -r "ContentWithBannerResponse" src/
   ```

2. **Step 2（Read definitions）**：用 file read tool 打開並確認「實際定義」
   - 打開 `ContentWithBannerResponse` 的檔案
   - 打開其 base / shared DTO（例如 `BaseContentResponse`）
   - 目標：確認 `Color` 是否真的移到 base，或是否完全移除

3. **Step 3（Exact search）**：搜尋 `.Color` 的所有使用處（找受影響的 consumers）
   ```bash
   grep -r "\.Color" src/
   ```

4. **Step 4（Optional: semantic search）**：若繼承/組合關係不明確，用 semantic search tool 詢問「型別關係 / 使用方式」

**驗證標準**：
- ✅ **已驗證**：已用 file read tool 或 `grep` 確認實際檔案內容 → 可標為 Bug
- ❌ **未驗證**：僅依 diff 推論，未查閱實際檔案 → 只能標為 Suggestion / 待確認

### Step 2.5.3: 驗證後再分類結論

- **僅在 diff 內看到、未在專案內驗證的「推論」**（例如「Color 應該已移到 Base」）→ 不得當成確定 Bug；若仍要提，標為 **Suggestion / 待確認**，並註明「請以實際程式碼結構為準」。
- **在專案內已確認**（例如搜到多處使用同一常數、且僅一處被改）→ 可列為 Bug 或明確 Suggestion。

### 範例（概念與實務）

#### 範例 1：常數不一致檢查

**Diff 顯示**：「某常數從 300KB 改為 5MB」

**實務執行**：
1. 用 `grep` 搜尋常數名：`grep -r "IMAGE_MAX_BYTES" src/`
2. 檢查搜尋結果：是否有多處定義或使用
3. 若發現其他檔案仍使用舊值 → 標為 Bug「常數不一致」
4. 若所有引用都已更新 → 不報此問題

#### 範例 2：型別結構驗證

**Diff 顯示**：「某 Response 新增欄位 WebsiteService」

**實務執行**：
1. 用 file read tool 讀取實際定義：
   - 打開 base DTO（例如 `BaseContentResponse`）→ 確認 base 是否已有 `WebsiteService`
   - 打開衍生類（例如 `ContentWithBannerResponse`）→ 確認是否重複定義
2. 用 `grep` 搜尋 mapping / builder / converter 方法：
   - `grep -r "MapContentWithBannerAsync\|MapContentWithTitleAsync" src/`
3. 用 file read tool 打開 mapping 方法內容，確認是否有把 `WebsiteService` 映射出去
4. 若 base 有、衍生類也有 → 可能是重複定義（需確認）
5. 若 base 有、但某個 mapping 沒映射 → 標為 Suggestion「缺映射」

#### 範例 3：方法簽章變更影響

**Diff 顯示**：「`GetDailyWinnerListAsync` 參數從 `keyWord` 改為 `userId, cellPhoneLastThreeNumbers`」

**實務執行**：
1. 用 `grep` 搜尋所有呼叫處：`grep -r "GetDailyWinnerListAsync" src/`
2. 用 file read tool 打開主要呼叫者（例如 Controller、Service）確認參數是否都已更新
3. 若發現未更新的呼叫處 → 標為 Bug「呼叫處未更新」

---

## Phase 3: Load Knowledge Base & Apply Rules (Auto-Discovery)

**Step 1: Detect Tech Stack**
Analyze project files to determine the stack:
- **.NET/C#**: Look for `*.csproj`, `.sln`, `appsettings.json`
- **Node.js**: Look for `package.json`, `tsconfig.json`
- **Python**: Look for `requirements.txt`, `pyproject.toml`

**Step 2: Dynamic Rule Loading (CRITICAL)**
Based on detected stack, you **MUST** perform auto-discovery with **naming convention filter**:

1. **Locate Directory**: 使用**專案根目錄相對路徑**（不受 SKILL.md 存放深度影響）:

   | Detected Stack | Step 1: General (先載入) | Step 2: Specific (後載入，可覆蓋) |
   |----------------|--------------------------|----------------------------------|
   | **.NET / C#** | `{CONFIG_ROOT}/references/general/` | `{CONFIG_ROOT}/references/dotnet/` |
   | **Node.js** | `{CONFIG_ROOT}/references/general/` | `{CONFIG_ROOT}/references/nodejs/` |
   | **Python** | `{CONFIG_ROOT}/references/general/` | `{CONFIG_ROOT}/references/python/` |

2. **Scan & Filter (依順序執行)**:
   - **Step A**: 先掃描 `general/` 目錄，讀取所有 `*.rule.md`
   - **Step B**: 再掃描特定技術目錄，讀取所有 `*.rule.md`
   - **FILTER** by naming convention:
     - ✅ `*.rule.md` → **MUST READ** (強制載入)
     - ❌ `*.guide.md` → Skip (參考用，不載入)
     - ❌ `*.ref.md` → Skip (純參考，不載入)
   - Treat these contents as your "Review Checklist"

3. **Override Rule**: 若 general 和 specific 有相同主題的規則，**specific 優先**

> ⚠️ **Self-Correction**: Only load `*.rule.md` files. If you see `new-check.rule.md`, you must read it. If you see `style-guide.guide.md`, skip it.

**Step 3: Execute Analysis**
Using the **loaded rules** from Step 2 as your checklist:
1. **Scan** the `git diff` content
2. **Match** patterns defined in the reference files (e.g., "Async Deadlock", "N+1 Query")
3. **Ignore** issues not present in the reference files unless they are obvious logical errors
4. **Categorize** findings into Security (Must Fix), Bugs (Must Fix), and Performance (Should Fix)

**Step 4: Verify Context (Anti-Hallucination)**
Before reporting an issue found in Step 3:
- **必須先完成 Phase 2.5**：對涉及「型別/常數/簽章」的結論，在專案內搜尋引用與定義，不得僅依 diff 推論。
- If the rule says "Check usage context", **在專案程式碼中搜尋 callers / 定義**，而非只在 diff 或「提供的片段」裡找。
- If the code context is insufficient (e.g., only seeing a method body), mark the finding as a "Suggestion" rather than a "Critical Issue".
- 若結論來自「假設結構」（例如「Color 已移到 Base」）但未在實際檔案中確認 → 降級為 Suggestion，並註明「請以實際程式碼為準」。

---

## Phase 4: Report

### Confidence Scoring

| Score | Meaning | Action |
|-------|---------|--------|
| **80-100** | Certain issue | Report as primary finding |
| **50-79** | Likely issue | Report in "Suggestions" section |
| **<50** | Uncertain | Do not report |

### Evidence Level (偏好：只對高風險議題附詳細證據)

- **High-risk issues**（建議附 evidence / 驗證步驟）：
  - Security findings
  - Public API / method signature changes
  - Shared constants / cross-module behavior changes
- **Other issues**：保持精簡描述即可（不要貼大量搜尋結果），但仍需完成 Phase 2.5 的驗證後才能下結論。

### Report Templates

輸出前**必須先讀取**對應 template 檔，依其結構產出：

| Mode | Template 路徑 |
|------|---------------|
| PR Mode | `{SKILL_DIR}/templates/report-pr-mode.md` |
| Self Mode | `{SKILL_DIR}/templates/report-self-mode.md` |

> `{SKILL_DIR}` = 本 SKILL 所在目錄（例如 `.claude/skills/code-review` 或 `{CONFIG_ROOT}/skills/code-review`）

---

**PR Mode 發布留言**：讀取 `{CONFIG_ROOT}/references/vcs/code-review-posting-github.ref.md`（見 `vcs-platform-commands.ref.md` 的 Posting Comments 區塊）。

---

## Anti-Patterns in This Skill

Things this skill intentionally **does NOT do**:

1. ❌ Nitpick formatting (leave to linters)
2. ❌ Suggest patterns without checking usage
3. ❌ Report issues outside the changed lines
4. ❌ Criticize code style preferences
5. ❌ Recommend rewrites for working code
