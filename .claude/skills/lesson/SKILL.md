---
name: lesson
description: >-
  Capture a lesson learned to its own file under tasks/lessons/ with
  structured format. Use when: a correction was made, a non-obvious decision
  was taken, a bug root cause was surprising, or a good approach was
  confirmed.
metadata:
  trigger: '"/lesson", "記錄教訓", "capture lesson", "記下來", "寫進 lessons"'
---

# Lesson Capture Workflow

從當前對話上下文擷取教訓，以結構化格式寫入 `tasks/lessons/` 下的新檔案（一檔一教訓，`docs/adr/adr-021-shared-state-files-team-scale.md`）。

---

## 執行流程

```
Step 1: 確認觸發類型
    ↓
Step 2: 從對話上下文萃取教訓內容
    ↓
Step 3: 新增 tasks/lessons/YYYYMMDD-kebab-slug.md
    ↓
Step 4: 確認寫入完成
```

---

## Step 1：確認觸發類型

判斷本次教訓屬於哪一類（可複選）：

| 類型 | 代號 | 說明 |
|------|------|------|
| 使用者糾正 | `[correction]` | 我做錯了，使用者指出來 |
| 自我修正 | `[self-fix]` | 我自己發現並修正了錯誤 |
| 架構決策 | `[decision]` | 選擇了某方案，有明確理由 |
| Bug 根因 | `[bug]` | 非顯而易見的 bug 根本原因 |
| 重複問題 | `[repeat]` | 同類問題第二次出現 |
| 確認有效 | `[confirmed]` | 非顯而易見的方法被確認正確 |

若使用者呼叫時附帶描述（例如 `/lesson 剛才 hook 的問題`），以該描述為主；否則從最近的對話自動判斷。

---

## Step 2：萃取教訓內容

從對話中萃取以下要素：

- **What happened**: 發生了什麼（一句話描述）
- **Why it matters**: 為什麼值得記錄（影響範圍）
- **Rule**: 未來應遵循的具體規則或做法

若資訊不足，詢問使用者補充說明，但只問一次，不反覆確認。

---

## Step 3：新增 tasks/lessons/YYYYMMDD-kebab-slug.md

新建一個檔案，檔名 `tasks/lessons/YYYYMMDD-kebab-slug.md`（日期=今天，slug=簡短英文
kebab-case，自訂但需與目錄內既有檔名不衝突）。新增教訓一律新增檔案，不修改既有
lesson 檔（歸檔是另一動作，見下）。frontmatter + 內文格式：

```markdown
---
date: YYYY-MM-DD
type: correction        # correction | decision | info
status: active          # active | archived — 新增教訓一律 active
---
# 標題（一句話）

**Context:** 簡述當時情境（1-2句）
**Rule:** 未來應遵循的具體做法（可執行，不模糊）
**落地:** 防線／檔案落點（若尚無機械化防線，填「本條 lesson」；歸檔時必填，指向接管的機械化 gate）
```

> **注入機制提醒**：`scripts/agent/hook.py` 的 `session-context` action 每次 session 只注入
> `status: active` 每條的「標題 + Rule 行」（Context、落地不注入）。Rule 必須自足、
> 可執行，讀者不看 Context 也要能照做。

> **歸檔**：判準見 `docs/adr/adr-013-content-tiering-and-injection-slimming.md`
> 決策 (b)（落地已成為機械化 gate 者歸檔）。歸檔動作 = 把該檔 frontmatter
> `status: active` 改為 `archived` 並補「**落地:**」欄，不移動或刪除檔案，不在此
> skill 的職責範圍內（由 triage 執行）。

範例（`tasks/lessons/20260403-hook-behavior-not-in-claude-md.md`）：

```markdown
---
date: 2026-04-03
type: correction
status: active
---
# Hook 自動化後不應在 CLAUDE.md 重複描述其行為

**Context:** 將 session-init 和 post-tool-observe hook 自動化後，CLAUDE.md 仍保留了描述 hook 運作的說明文字。
**Rule:** CLAUDE.md 只記錄「我需要主動判斷並執行」的規則。自動化由 hook 處理的事項不寫進 CLAUDE.md，避免重複與矛盾。
**落地:** 本條 lesson
```

---

## Step 4：確認完成

寫入後輸出一行確認：

```
✓ Lesson saved: tasks/lessons/YYYYMMDD-kebab-slug.md — [標題]
```

不需要重複列出完整內容，使用者可自行查閱 `tasks/lessons/`。
