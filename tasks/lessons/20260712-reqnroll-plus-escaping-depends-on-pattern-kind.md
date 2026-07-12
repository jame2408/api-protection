---
date: 2026-07-12
type: correction
status: active
---

# Reqnroll step attribute 的 `+` 跳脫取決於 pattern 判型 — 含 `(.*)` 捕獲群組判為 Regex 須 `\+`，無則為 Cucumber Expression 用字面 `+`

**Context:** 場景 34（`9e0e432`）friction 記錄「`\+` 使 binding registry 全滅，改字面 `+` 復原」，orchestrator 據此在 38/48 spec 寫成通則「`+` 一律字面、勿跳脫」。executor 依指示落 Given「同一 Consumer \+ Environment 下已有 "(.*)" 狀態為 Rotating」的字面 `+` 版後，紅 B 持續 undefined；追查 Reqnroll 原始碼 `CucumberExpressionDetector.cs`（`CommonRegexStepDefPatterns = @"(\([^\)]+[\*\+]\)|\.\*)"`）確認：attribute 含 `(.*)` 這類 regex 語法時整條 pattern 判定為 **Regex**——此時未跳脫的 `+` 是 quantifier（套在前一空白字元上）不匹配字面 `+`，須 `\+`；而 34 的案例**無**捕獲群組、判定為 **Cucumber Expression**——該語法只允許跳脫 `{ } ( ) \ /`，`\+` 反而非法致整個 registry 失效。兩案例情境相反，單一通則兩邊都會踩。

**Rule:** Step attribute 文字含字面 `+`（或其他 regex 特殊字元）時，先判 pattern 屬哪一型再決定跳脫：(1) 含 `(.*)`／量詞括號群組 → Regex 型，特殊字元須 `\` 跳脫；(2) 純文字或僅 `{string}` 佔位 → Cucumber Expression 型，只允許跳脫 `{ } ( ) \ /`，其餘字元一律字面。派工 spec 引用先例 friction 為指示前，須核對先例與本案的 pattern 型別是否相同。

**落地:** 本條 lesson（機械化候選：可在 hook.py pre-tool-edit 攔 Cucumber Expression 型 attribute 內的非法跳脫，但誤報面待評估——維持習慣承載，復發再議）。
