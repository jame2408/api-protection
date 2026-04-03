---
name: coding-style
description: |
  Coding standards and style guidelines for code quality.
  Activated when: writing code, explaining code, or refactoring.
  Loads different references based on action type.
metadata:
  trigger: Activated when writing, explaining, or refactoring code
---

# Coding Style Workflow

Apply coding standards based on the current task context.

---

## Execution Overview

```
Phase 0: Environment Detection → 確定 {CONFIG_ROOT} 值
    ↓
Mode Detection → 判斷 Action Type (Generate/Explain/Refactor)
    ↓
Phase 1: Tech Stack Detection → 判斷專案技術棧
    ↓
Phase 2: Load References → 載入對應的參考文件
    ↓
Phase 3: Apply Standards → 套用規範生成/重構程式碼
```

---

## Phase 0: Environment Detection (首次執行必須)

> ⚠️ 在執行任何其他步驟前，**必須先確定 `{CONFIG_ROOT}` 的值**

### Step 0.1: 檢測設定目錄

**你必須**檢查專案根目錄中存在哪個 AI 設定目錄：

| 檢查目錄 | 若存在則設定 |
|----------|-------------|
| `.claude/` | `{CONFIG_ROOT}` = `.claude` |
| `.cursor/` | `{CONFIG_ROOT}` = `.cursor` |
| `.gemini/` | `{CONFIG_ROOT}` = `.gemini` |
| `.github/copilot/` | `{CONFIG_ROOT}` = `.github/copilot` |
| `ai-config/` | `{CONFIG_ROOT}` = `ai-config` |

### Step 0.2: 執行偵測

**方法 A：有檔案系統存取權限**

使用你的檔案系統工具檢查哪些目錄存在：
1. 列出專案根目錄的資料夾
2. 找到第一個符合上表的目錄
3. 設定 `{CONFIG_ROOT}` 為該目錄名稱

**方法 B：無檔案系統存取權限（Fallback）**

直接詢問使用者：
> 「請問你的 AI 設定目錄是哪個？（例如：`.claude`、`.cursor`、`.gemini`）」

### Step 0.3: 驗證目錄結構

確認 `{CONFIG_ROOT}/references/` 目錄存在：
- 若存在 → 繼續執行
- 若不存在 → 告知使用者：「找不到 `{CONFIG_ROOT}/references/` 目錄，請確認設定是否正確」

> 💡 **快取機制**：在同一對話中，`{CONFIG_ROOT}` 只需偵測一次，後續可重複使用

---

## Mode Detection (情境感知)

根據使用者請求的**動作類型**決定載入哪些文件：

| Action | Trigger Keywords | Load Files | Purpose |
|--------|------------------|------------|---------|
| **Generate** | 「寫」「建立」「新增」「實作」 | `*.guide.md` | 範例、命名規範 |
| **Explain** | 「解釋」「說明」「什麼是」 | `*.guide.md` + `*.ref.md` | 概念、原理 |
| **Refactor** | 「重構」「優化」「改善」 | `*.rule.md` + `*.guide.md` | 檢查規則、最佳實踐 |

### Decision Tree

**Step 1: Detect Action Type**
- IF request contains 「寫」「建立」「新增」「實作」「generate」「create」「implement」
  → Action = **Generate**
- ELSE IF request contains 「解釋」「說明」「什麼是」「explain」「what is」
  → Action = **Explain**
- ELSE IF request contains 「重構」「優化」「改善」「refactor」「improve」「optimize」
  → Action = **Refactor**
- ELSE → Action = **Generate** (default)

**Step 2: Detect Tech Stack**
→ Go to Phase 1

**Step 3: Load References**
→ Go to Phase 2 (based on Action + Tech Stack)

**Step 4: Apply Standards**
→ Go to Phase 3

---

## Phase 1: Detect Tech Stack (必須執行)

**你必須**檢查專案根目錄的檔案來判斷技術棧。

### 偵測邏輯

| 找到的檔案 | 判定為 |
|-----------|--------|
| `*.csproj` 或 `*.sln` | **.NET / C#** |
| `package.json` | **Node.js / TypeScript** |
| `requirements.txt` 或 `pyproject.toml` | **Python** |
| `go.mod` | **Go** |
| 以上皆無 | 使用 Fallback |

### 執行方式

**方法 A：有檔案系統存取權限**

使用你的檔案系統工具（Glob, LS, Read 等）檢查專案根目錄

**方法 B：無檔案系統存取權限（Fallback）**

詢問使用者：
> 「請問這個專案使用什麼技術棧？（例如：.NET/C#、Node.js、Python）」

**方法 C：從上下文推斷**

如果使用者已提供程式碼片段：
- 看到 `namespace`、`using`、`async Task` → .NET / C#
- 看到 `import`、`export`、`const` → Node.js / TypeScript
- 看到 `def`、`import`、`class:` → Python

---

## Phase 2: Load References (必須執行)

> ⚠️ 確保 Phase 0 已完成，`{CONFIG_ROOT}` 已有值

### Step 2.1: 定位目錄

| Detected Stack | General Directory | Specific Directory |
|----------------|-------------------|-------------------|
| **.NET / C#** | `{CONFIG_ROOT}/references/general/` | `{CONFIG_ROOT}/references/dotnet/` |
| **Node.js** | `{CONFIG_ROOT}/references/general/` | `{CONFIG_ROOT}/references/nodejs/` |
| **Python** | `{CONFIG_ROOT}/references/general/` | `{CONFIG_ROOT}/references/python/` |

### Step 2.2: 根據 Action 決定載入的檔案後綴

| Action | 載入後綴 | 原因 |
|--------|----------|------|
| **Generate** | `*.guide.md` only | 需要範例、命名規範 |
| **Explain** | `*.guide.md` + `*.ref.md` | 需要概念說明 |
| **Refactor** | `*.rule.md` + `*.guide.md` | 需要檢查規則 |

### Step 2.3: 執行載入 (CRITICAL)

**方法 A：有檔案系統存取權限**

1. **掃描 General 目錄**：列出 `{CONFIG_ROOT}/references/general/` 中的所有檔案
2. **掃描 Specific 目錄**：列出 `{CONFIG_ROOT}/references/{stack}/` 中的所有檔案
3. **過濾檔案**：根據 Action 決定的後綴過濾
4. **讀取檔案**：讀取過濾後的檔案內容
5. **套用順序**：先 General，後 Specific（Specific 可覆蓋 General）

**方法 B：無檔案系統存取權限（Fallback）**

詢問使用者：
> 「請提供 `{CONFIG_ROOT}/references/{stack}/` 目錄中的檔案列表，或直接貼上相關的規範內容」

### 載入範例

**範例：Generate + .NET（{CONFIG_ROOT} = `.claude`）**
```
Action = Generate
{CONFIG_ROOT} = .claude
→ 只載入 *.guide.md

執行：
1. 掃描 .claude/references/general/ → 找到 solid.rule.md (❌ skip)
2. 掃描 .claude/references/dotnet/ → 找到 naming.guide.md ✅, testing.guide.md ✅
3. 讀取 naming.guide.md, testing.guide.md
```

**範例：Refactor + .NET**
```
Action = Refactor
→ 載入 *.rule.md + *.guide.md

執行：
1. 掃描 .claude/references/general/ → 找到 solid.rule.md ✅
2. 掃描 .claude/references/dotnet/ → 找到所有 *.rule.md ✅ + *.guide.md ✅
3. 讀取所有符合條件的檔案
```

---

## Phase 3: Apply Standards (依情境執行)

> ⚠️ **重要**：本 Skill 不包含具體規則。所有規則來自 Phase 2 載入的 Reference 檔案。

### Generate Mode

當你在**生成程式碼**時：

1. **確認已載入指南**：確保已讀取 `*.guide.md` 檔案
2. **套用命名規範**：依據載入的 `naming.guide.md` 命名
3. **套用專案慣例**：檢查 `CLAUDE.md` 是否有專案特定規範
4. **生成程式碼**：依據載入的指南生成符合規範的程式碼

### Explain Mode

當你在**解釋程式碼**時：

1. **確認已載入概念文件**：確保已讀取 `*.guide.md` + `*.ref.md`
2. **引用原理**：解釋時引用載入文件中的原則（如 SOLID、DRY）
3. **提供範例**：用載入的 guide 中的範例輔助說明

### Refactor Mode

當你在**重構程式碼**時：

1. **確認已載入檢查規則**：確保已讀取 `*.rule.md`
2. **識別問題**：依據載入的規則檢查 Code Smells
3. **套用最佳實踐**：依據載入的 `*.guide.md` 重構
4. **驗證**：確保重構後符合載入的規範

---

## Quick Reference: SOLID Principles

> 完整內容請參考 `{CONFIG_ROOT}/references/general/solid.rule.md`

| Principle | Description | Violation Sign |
|-----------|-------------|----------------|
| **S**ingle Responsibility | One class, one reason to change | Class doing too many things |
| **O**pen/Closed | Open for extension, closed for modification | Modifying existing code for new features |
| **L**iskov Substitution | Subtypes must be substitutable | Overridden methods changing behavior unexpectedly |
| **I**nterface Segregation | Many specific interfaces > one general | Implementing unused interface methods |
| **D**ependency Inversion | Depend on abstractions, not concretions | `new` keyword in business logic |

---

## Quick Reference: Other Principles

| Principle | Description |
|-----------|-------------|
| **DRY** | Don't Repeat Yourself - Extract common logic |
| **KISS** | Keep It Simple, Stupid - Avoid over-engineering |
| **YAGNI** | You Aren't Gonna Need It - Don't add unused features |
| **Fail Fast** | Validate early, throw exceptions immediately |
| **Composition over Inheritance** | Prefer has-a over is-a relationships |

---

## Reference File Index

### Naming Convention

| Suffix | Auto-load When | Purpose |
|--------|----------------|---------|
| `*.guide.md` | Generate, Explain, Refactor | 開發指南、範例 |
| `*.rule.md` | Refactor only | 檢查規則 |
| `*.ref.md` | Explain only | 純參考資料 |

### File Discovery

> 檔案清單由 Phase 2 動態發現，無需在此維護靜態列表。
> 新增檔案時只需放入正確目錄並使用正確後綴即可。

---

## Integration with Other Skills

| Skill | Relationship |
|-------|--------------|
| **[code-review](../code-review/SKILL.md)** | code-review 專注於 MR diff 檢查，使用 `*.rule.md` |
| **coding-style** (this) | 日常開發時套用，依情境載入不同文件 |

---

## Language Support

Currently supported:
- ✅ .NET / C#

Planned:
- 🔲 TypeScript/JavaScript
- 🔲 Python
- 🔲 Go

---

## Capability Matrix

本 Skill 在不同環境下的行為：

| 能力 | IDE Agent (Cursor/VSCode) | Chat Agent (Web UI) |
|------|---------------------------|---------------------|
| Phase 0: 偵測 CONFIG_ROOT | ✅ 自動檢測目錄 | ⚠️ 需詢問使用者 |
| Phase 1: 偵測 Tech Stack | ✅ 自動檢測檔案 | ⚠️ 需詢問或從程式碼推斷 |
| Phase 2: 載入 References | ✅ 自動讀取檔案 | ⚠️ 需使用者貼上內容 |
| Phase 3: 套用規範 | ✅ 完整執行 | ✅ 完整執行 |

> Chat-based Agent 無法存取檔案系統時，會自動切換到 Fallback 模式詢問使用者
