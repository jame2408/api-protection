# 憲章 token 經濟條款：四條反模式對策升為 §5 可打勾規則

> lesson「Token 經濟四個反模式」的落地欄自 2026-07-05 起承諾「納入下一個憲章修訂」但該修訂一直沒有發生 — 這是懸空 lesson（寫了未落地 = 學習迴圈開環）。本 ADR 把四條規則正式寫入 `docs/orchestration.md` §5，並一併收口同型的「spec 背景欄執行期值求證」lesson。

---

## Status

Accepted (2026-07-05)

同步項目（同 commit）：`docs/orchestration.md` §5（新增第 5–8 條與治理指針）、`tasks/lessons.md`（兩條 Active lesson 的落地欄收口）、`tasks/_templates/executor-spec.md`（背景欄補執行期值求證註記）。

---

## Context

### 現況

`tasks/lessons.md` Active 區「Token 經濟四個反模式」條目的落地欄：

```
落地: 本條 lesson；納入下一個憲章修訂（ADR-013 候選：token 經濟條款從原則升為可打勾規則）
— 在此之前由 §8.5 checkpoint 的「如何接上」段執行 session 重啟紀律。
```

該「下一個憲章修訂」至今未發生。`docs/orchestration.md` §5「Token 節約原則」現況只有 4 條原則層級條文（注入有上限、細節單一來源、續接靠 checkpoint、大範圍掃描派小型模型），不含 lesson 所載的四條可操作規則（任務包拆分門檻、resume 限制、session 壽命、limit 中斷處置）。2026-07-05 loop-engineering 盤點（`tasks/loop-audit-2026-07-05.md`，使用者裁決 Q4「併本輪」）判定此為懸空 lesson。

同型問題：lesson「Spec 背景欄的執行期值敘述必須讀宣告求證」的落地欄只指向 lesson 自身，`tasks/_templates/executor-spec.md` 的背景欄未載此義務 — 規則只活在注入層，範本使用者看不到。

### 不決定會發生什麼

lesson 注入依賴 Claude Code harness 的 `session-init.sh`；其他 harness 的協調者按冷啟動 prompt 只讀 checkpoint 與憲章，永遠看不到這四條規則。歷史已付過學費：單句「先繼續」+37% 用量、limit 恢復 +13% 用量。落地欄的承諾放著不兌現，「落地欄必填」的制度本身也會失去可信度。

---

## Decision

### 1. `docs/orchestration.md` §5 新增第 5–8 條（逐字）

```
5. **任務包單一階段**：executor 任務規格以單一階段為原則，預估超過約 50 次
   工具呼叫即拆包派發。
6. **resume 只限小型追問**：續行長任務一律新開 executor、以 spec／checkpoint
   銜接；不得 resume 大 transcript（整份 transcript 會以未快取輸入重讀計費）。
7. **協調者 session 以一個 Phase 為壽命上限**：Phase 落地即結束 session，
   下個 Phase 冷啟動接 checkpoint。
8. **limit／服務中斷視同 phase 邊界**：將達 limit 時最後一動是 checkpoint
   落盤；恢復一律開新 session 接 checkpoint；中斷的 executor 一律新開接手
   （讀 spec 與現有 git diff 續跑），不得 resume。
```

§5 標題下加治理指針一行（比照 §1.5／§6／§7 的既有慣例）：「第 5–8 條受 `docs/adr/adr-019-token-economy-charter-rules.md` 管轄，修改須先開新 ADR。」

### 2. executor-spec 範本背景欄補求證註記

`tasks/_templates/executor-spec.md`「背景（orchestrator 已核實的事實）」欄加一行註記：凡涉及執行期值（預設值、null 與否、初始狀態）的敘述，必須讀該欄位／屬性的宣告與初始化行求證，核實深度以「executor 可直接照抄判斷式」為準。

### 3. lessons 落地欄收口

- 「Token 經濟四個反模式」落地欄改指：`docs/orchestration.md` §5 第 5–8 條（本 ADR）。
- 「Spec 背景欄的執行期值敘述必須讀宣告求證」落地欄改指：`tasks/_templates/executor-spec.md` 背景欄註記（本 ADR）。
- 兩條 lesson **維持 Active 不歸檔**：ADR-013 決策 (b) 的歸檔判準是「機械化 gate 接管」，憲章條款與範本註記不是 gate。

### 不在本 ADR 範圍

- 不機械化第 5–8 條（工具呼叫計數與 session 壽命在本 repo 腳本的掛載點之外，見 Rationale）。
- 不改 §5 既有第 1–4 條的文字。
- 不動 `session-init.sh` 注入邏輯與 ADR-013 的分區判準。

---

## Rationale

### 為何入憲章而不是留在 lesson 注入

lesson 注入是提醒層，不是規範落點；憲章明文規則 (ii) 要求協調者角色「僅依本文件即可接手」— 只有寫進 §5，非 Claude Code harness 的接手模型才拿得到這四條義務。且落地欄承諾了憲章修訂，不兌現即開環。

### 為何不機械化

「預估 50 次工具呼叫」是派工時的設計判斷，不是執行時可攔截的事件；executor 可能在任何 harness 執行，本 repo 的 hook 與腳本掛不到跨 agent 的呼叫計量。把判斷型規則偽裝成機械防線，比誠實登記為憲章義務更糟。

### 為何兩條 lesson 不趁勢歸檔

歸檔判準（ADR-013 決策 (b)）是「落地已成為機械化 gate」；憲章條款仍靠人／模型遵守。放寬判準是二階制度修訂，違反制度凍結啟發式（lessons [decision] 條目），且會讓 Archived 區「防線代記」的語意稀釋。

---

## Consequences

### Positive

- 懸空 lesson 清零：兩條落地欄都指向 repo 內可驗證的權威落點。
- token 紀律對任何 harness 的接手模型可見，不再依賴 Claude Code 專屬注入。
- 「落地欄承諾必兌現」的先例得到維護。

### Negative / Trade-offs

- lesson Rule 行與憲章條文形式上重複兩份。
  - Mitigation: 權威落點明確為憲章（落地欄指針指向 §5）；lesson Rule 行是 ADR-013 分層設計下的注入層摘要，本就允許。
- §5 條文增加，憲章閱讀成本上升。
  - Mitigation: 四條皆為粗體導語＋一句話，無敘事；相對於違規的實測代價（+37%／+13% 用量），閱讀成本可忽略。
- 第 5–8 條無機械化防線，仍可能被違反。
  - Mitigation: 誠實定位為憲章義務（判斷型），違規事實可由用量異常與 checkpoint 紀錄事後稽核；若再現重大違規事故，屆時依事故驅動原則評估更強對策。

---

## Alternatives Considered

### Alternative A：維持 lesson-only，落地欄改寫為「session-init 注入即防線」

Rejected. 注入是 Claude Code harness 專屬，違反憲章的 harness 中立原則；且這等於把「懸空」改名，不是收口。

### Alternative B：另開 `docs/token-policy.md` 專文

Rejected. §5 已是 token 節約規則的權威落點，另開檔案製造第二真相源，違反 SSOT（ADR-007 規則 5 精神）。

### Alternative C：機械化 — hook 計數工具呼叫、超過 50 即攔截

Rejected. executor 可在任意 harness 執行，本 repo hook 掛不到跨 agent 計量；且門檻是「預估後拆包」的派工判斷，不是執行時邊界，攔截語意本身不成立。

---

## Implementation Rules

1. `docs/orchestration.md` §5 含第 5–8 條與治理指針行。**驗收**：

   ```bash
   grep -n "任務包單一階段" docs/orchestration.md
   # 預期 1 命中
   ```

2. `tasks/lessons.md` 不再含「納入下一個憲章修訂」字樣，且兩條 lesson 落地欄分別指向 `docs/orchestration.md` §5 與 `tasks/_templates/executor-spec.md`。**驗收**：

   ```bash
   grep -n "納入下一個憲章修訂" tasks/lessons.md
   # 預期 0 命中
   ```

3. `tasks/_templates/executor-spec.md` 背景欄含執行期值求證註記。
4. 任何提案修改 1–3，必須先開新 ADR。
