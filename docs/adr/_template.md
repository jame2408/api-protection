# <一句話標題：這個 ADR 解什麼問題>

> Lead-in：1–2 句 abstract，寫給未來搜尋這份 ADR 的人。說明本 ADR 終結了什麼矛盾、明文化了什麼既成事實、或固化了什麼決策。

---

## Status

Accepted (YYYY-MM-DD)

<!--
可選補充項（按需保留）：
- Supersedes: 指向被取代的 ADR 或文件段落（用穩定錨點：檔案 + 段落標題 / symbol / 內容 quote，不要用 file:line — 程式碼一動行號就漂掉）。
- Superseded by: 若日後被取代，回填新 ADR。
- 同步項目: 列出本 ADR 接受時必須在「同 commit」一起改的檔案，例如 CLAUDE.md / rule.md / 範例。讓未來的人知道這份 ADR 的影響面已經落地。
-->

---

## Context

### 現況

引用實際程式碼、設計文件、CLAUDE.md 段落，把矛盾並排擺出來。**引用要用穩定錨點，不要用 `file:line`**（程式碼一改行號就漂掉）：

- 檔案 + 段落標題：`CLAUDE.md` 的「Error Handling」段
- 檔案 + symbol：`SharedKernel/Domain/Failure.cs` 的 `record Failure`
- 直接 quote 該行內容，由讀者搜尋

```csharp
// 現況程式碼（直接 quote，不寫行號）
```

```
// 設計文件 / CLAUDE.md 既有寫法
```

說明矛盾為何不能繼續存在。

### 問題嚴重度（可選）

當決策牽涉 contract drift、命名違規、未來必踩的坑、跨檔案同步成本時，列點說明嚴重度。讓讀者三秒判斷優先級。

### 易混淆概念釐清（可選）

當決策容易被過度解讀或縮限解讀時，用表格 / 列表把「本 ADR 規範什麼、不規範什麼」明寫，避免後續 review 反覆爭議。

| 概念 | 是什麼 | 本 ADR 是否規範 |
|---|---|---|
| ... | ... | ✅ / ❌ |

### 不決定會發生什麼（可選）

當決策的價值是「阻止 drift 重演」而非「解決當下問題」時，明寫「若不固化，未來會出現什麼成本」。

---

## Decision

以 §1 / §2 / §3 編號，必要時用 §3.1 / §3.2 / §2a 拆出子規則。每條決策應：

1. 直接陳述規則本身（祈使句，不繞彎）。
2. 附最小 code 範例 — before / after 對比優先。
3. 主動劃定「不在本 ADR 範圍」的邊界，避免被過度解讀。

### 1. <第一條決策>

```csharp
// before / after 對比
```

### 2. <第二條決策>

…

### N. 本 ADR 接受時的同步項目（可選）

當決策需要 reference docs / CLAUDE.md / 範例同步修改時，列出**必須在同 commit 一起改**的檔案清單，並對每個檔案寫明改動內容。讓接受 ADR 的 PR 自帶驗收清單。

---

## Rationale

回答「為什麼是這個、不是別的」。Rationale 不是 Decision 的重複，而是補論證強度。常用子標題：

### 為什麼選 X 而不是 Y

### 為什麼不擴張到 Z

### 為什麼不機械化 / 不加 analyzer / 不寫架構測試

> 提示：每個替代方案的論證會在 Alternatives Considered 細寫；Rationale 主要承擔「正面論證」與「範圍邊界論證」。

---

## Consequences

### Positive

- …

### Negative / Trade-offs

每條 Negative 必須配 `Mitigation: ...`。沒有 mitigation 等於承認決策有缺口。

- <trade-off 1>
  - Mitigation: <如何緩解>
- <trade-off 2>
  - Mitigation: <如何緩解>

---

## Alternatives Considered

每個替代方案明寫 `Rejected.` 與理由。寧可寫滿，也不要省略 — 這節是未來「為什麼不 XXX」的標準回應。

### Alternative A: <方案名稱>

```csharp
// 必要時附 code 樣本
```

Rejected. <理由：成本、破壞既有 contract、違反某條原則…>

### Alternative B: …

Rejected. …

---

## Implementation Rules

可逐條打勾驗收的清單。本節是 ADR 的「測試」 — 不是論述、不是背景，是工程合約。

1. <規則 1：祈使句，可機械驗證或可 review checklist 化>
2. <規則 2>
3. …
N. **驗收**：列出 grep / 架構測試 / functional test 的歸零條件（若適用）。例如：

   ```bash
   git --no-pager grep -n -E '<pattern>' -- <paths> ':!docs/adr/<this-adr>.md'
   # 預期 0 命中
   ```

N+1. 任何提案修改 1–N，必須先開新 ADR。

<!--
治理條款（最後一條）是必須項。它阻擋「悄悄改 rule.md 範例 → 範例 drift → 結論 drift」的惡性循環。
-->
