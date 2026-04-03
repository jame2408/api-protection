---
name: lesson
description: >-
  Capture a lesson learned to tasks/lessons.md with structured format.
  Use when: a correction was made, a non-obvious decision was taken, a bug root
  cause was surprising, or a good approach was confirmed.
metadata:
  trigger: '"/lesson", "記錄教訓", "capture lesson", "記下來", "寫進 lessons"'
---

# Lesson Capture Workflow

從當前對話上下文擷取教訓，以結構化格式寫入 `tasks/lessons.md`。

---

## 執行流程

```
Step 1: 確認觸發類型
    ↓
Step 2: 從對話上下文萃取教訓內容
    ↓
Step 3: 格式化並附加至 tasks/lessons.md
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

## Step 3：寫入 tasks/lessons.md

附加至 `tasks/lessons.md` 的格式：

```markdown
### [類型代號] 標題（一句話）
**Date:** YYYY-MM-DD
**Context:** 簡述當時情境（1-2句）
**Rule:** 未來應遵循的具體做法（可執行，不模糊）
```

範例：

```markdown
### [correction] Hook 自動化後不應在 CLAUDE.md 重複描述其行為
**Date:** 2026-04-03
**Context:** 將 session-init 和 post-tool-observe hook 自動化後，CLAUDE.md 仍保留了描述 hook 運作的說明文字。
**Rule:** CLAUDE.md 只記錄「我需要主動判斷並執行」的規則。自動化由 hook 處理的事項不寫進 CLAUDE.md，避免重複與矛盾。
```

---

## Step 4：確認完成

寫入後輸出一行確認：

```
✓ Lesson saved: [標題]
```

不需要重複列出完整內容，使用者可自行查閱 tasks/lessons.md。
