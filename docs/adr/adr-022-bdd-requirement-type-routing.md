# BDD 需求類型分流：既有行為變更、缺陷再現與行為移除的場景修訂流程

> Lead-in：現行 BDD 流程只定義「新功能且場景已預產」的 `@ignore` 啟用路徑；既有行為變更、缺陷修復、行為移除都沒有流程，且 `.feature` 凍結條款把合法的場景修訂一併擋死。本 ADR 建立需求類型分流表，補上場景修訂（spec-first、同 commit、`Spec-change:` trailer）與缺陷再現豁免，並機械化對應 gate。

---

## Status

Accepted (2026-07-06)

- 同步項目（與本 ADR 同 commit）：見 Decision §7。
- 關聯：共享狀態檔的團隊尺度處置屬 `docs/adr/adr-021-shared-state-files-team-scale.md`，不在本 ADR。

---

## Context

### 現況

`CLAUDE.md`「BDD Scenario Development Cycle」段開宗明義：

```
> Development phase only — `.feature` scenarios and API specs are already produced.
> Do not author new `.feature` files here.
```

`tasks/bdd-backlog.md` 檔頭進一步凍結：「`.feature` 產出目前凍結……`requirements-analysis-design` skill 的 Step 5 不得產出新場景檔」。整套機械化 gate（pre-commit 單移 `@ignore`、進度檔同 commit、`commit-msg` 的 `Refactor-assessment:` trailer、`scripts/bdd-lint.sh` 帳面一致性）全部以「移除 `@ignore` 啟用既有場景」為唯一觸發語意。

矛盾並排：

1. `tasks/bdd-backlog.md` 的格式欄自己就預留了「來源：[discovery session / **需求變更** / 其他]」— 制度預期需求變更會發生，但下游沒有任何流程承接：修改一個已通過場景的 Given/When/Then 不移除 `@ignore`，所有 gate 都不觸發，凍結條款卻又字面禁止動 `.feature`。
2. `CLAUDE.md` never-commit-red 的例外只涵蓋「confirming a scenario/its steps are unimplemented」— 行為變更必經的「舊實作不符新規格」的紅沒有明文歸屬。
3. 缺陷修復依「Autonomy Scope」屬自主處理，但若缺陷是外部可觀察行為，補一條再現場景會同時撞上凍結條款與「backlog → progress 只能由使用者晉升」兩道牆。

### 易混淆概念釐清

| 概念 | 是什麼 | 本 ADR 是否規範 |
|---|---|---|
| 新場景大量產出（Discovery 管道） | `requirements-analysis-design` Step 5 的批次場景生成 | ❌ 維持凍結，不動 |
| 既有場景修訂 | 已存在場景的 Given/When/Then/表格文字變更 | ✅ §2 |
| 缺陷再現場景 | 為重現既有缺陷即時新增的單一場景 | ✅ §3 |
| 場景刪除 | 行為移除導致的場景退場 | ✅ §4 |
| 純重構 / 非功能需求 | 不改變外部行為的程式碼整理；效能/安全門檻 | ❌ 現制已覆蓋（Refactor discipline／驗證矩陣），只入分流表指針 |

---

## Decision

### 1. 需求類型分流表

BDD 開發循環的入口從「找下一個 `@ignore`」升級為「先判型再走對應路徑」：

| 需求類型 | 規格動作 | 紅的形態 | 承載機制 |
|---|---|---|---|
| 新功能（場景已預產） | 使用者晉升 backlog → progress，移除 `@ignore` | Pending／自然紅；啟用型補故意紅 | 現制不變 |
| 新功能（場景不存在） | Discovery 管道（凍結中） | 同上 | 現制不變 |
| 既有行為變更 | 使用者裁決 → 修訂場景（spec-first） | 修訂後自然紅 = 舊實作不符新規格 | §2（新增） |
| 缺陷修復 | 先補再現（場景或測試）證紅 → 修 | 再現紅 | §3（新增） |
| 行為移除 | 使用者裁決 → 刪場景＋產碼同 commit | 無（刪後仍綠） | §4（新增） |
| 純重構 | 禁碰 `.feature` | 無 | Refactor discipline（現制） |
| 非功能 | 不進 BDD 看板 | gate 故意紅 | `docs/verification-matrix.md`（現制） |

### 2. 既有行為變更：spec-first、同 commit、`Spec-change:` trailer

1. **裁決權**：場景文字 = 規格本體，修訂意圖與修訂後文字必須經使用者核准（比照 backlog 晉升為使用者專屬動作）；執行（實際編輯 `.feature`）由 executor 依核准後規格進行。
2. **順序**：先修訂場景（含必要的 steps 調整）→ 實跑取得**自然紅**（舊實作不符新規格的測試輸出，記入 spec 回報）→ 修改 production → 綠 → refactor 評估。
3. **入庫**：場景修訂 + steps + production + 帳面更新**同一個 commit** — never-commit-red 完全不破例，紅只存在於工作區，以測試輸出取證。
4. **trailer**：該 commit 必須帶 `Spec-change: <一行裁決依據（誰核准、變更意圖）>` trailer。
5. **vacuous amendment 防呆**：場景修訂後若未經 production 變更即直接綠，表示修訂沒有改變可觀察行為（修訂無效或行為本已如此）— executor 必須停止回報，不得逕行 commit（對稱於啟用型場景的故意紅義務）。

### 3. 缺陷再現場景：豁免凍結與晉升排隊

缺陷驅動 = 事故驅動，符合制度凍結啟發式的既有精神：

1. 缺陷修復得**即時新增**再現場景，豁免 Discovery 凍結與「backlog → progress 使用者晉升」排隊。
2. 再現粒度優先序：外部可觀察行為 → 場景；純內部邏輯 → unit test（不進 `.feature`）。
3. 流程：再現先紅（取證）→ 修復 → 綠；再現 + 修復 + 帳面遞增**同 commit**，commit 帶 `Spec-change: defect-repro — <缺陷一句話描述>` trailer。
4. 場景總數（帳面分母）允許因此遞增；`scripts/bdd-lint.sh` 帳面一致性以當下 grep 實況為準。

### 4. 行為移除

1. 場景刪除為使用者專屬裁決（同 §2 裁決權）。
2. 場景刪除 + 對應 production 收斂 + 帳面遞減**同 commit**，commit 帶 `Spec-change:` trailer。
3. 刪除後全套件必須綠；帳面分母允許遞減。

### 5. 機械化 gate（鏡射 `Refactor-assessment:` 模式）

`scripts/git-hooks/commit-msg` 新增一條規則：**staged diff 觸及 `backend/tests/FunctionalTests/Features/**/*.feature`，且改動不是「純 `@ignore` tag 移除」時，commit message 必須帶 `Spec-change:` trailer**；缺 trailer 即拒絕 commit。

- 「純 `@ignore` tag 移除」走既有啟用路徑（`Refactor-assessment:` trailer 管轄），兩條 trailer 規則互斥觸發、可同時滿足。
- 逃生口比照既有慣例以環境變數明文豁免（僅限機械性整理，如 ADR-006 型全域正名），變數名由實作定案並登記 `docs/verification-matrix.md`。
- 上線必須「綠＋故意紅」三面取證：修訂無 trailer 被擋、修訂有 trailer 放行、純 `@ignore` 移除不誤傷。

### 6. 明文不在本 ADR 範圍

- Discovery 凍結的解除條件 — 維持現狀，另案處理。
- 純重構與非功能需求 — 現制已覆蓋，本 ADR 只在分流表放指針，不新增機制。
- upstream `jame2408/agent-skills` repo 的 `requirements-analysis-design` skill 凍結 gate — 既有 todo follow-up，不在此重複立案。

### 7. 本 ADR 接受時的同步項目（同 commit）

- `CLAUDE.md`「BDD Scenario Development Cycle」段：凍結句改為「凍結的是 Discovery 新場景產出；既有場景修訂、缺陷再現、行為移除走 `docs/adr/adr-022-bdd-requirement-type-routing.md` 分流」；Constraints 列表補 `Spec-change:` trailer 一行（註明 commit-msg hook 機械化）。
- `tasks/bdd-backlog.md` 檔頭：凍結敘述同步（凍結範圍限縮為 Discovery 管道）。
- `.claude/skills/bdd-vertical-slice/SKILL.md`：實作節奏前置補「先判需求類型」指針（指向本 ADR §1 分流表，不複寫表格）。
- `scripts/git-hooks/commit-msg`：§5 gate 實作。
- `docs/verification-matrix.md`：新 gate 登記（機制／時機／驗證者）。
- `tasks/_templates/executor-spec.md`：背景欄補「需求類型」一格（新功能啟用／行為變更／缺陷再現／行為移除）。

---

## Rationale

### 為什麼 spec-first 但同 commit，而不是把紅 commit 入庫

規格變更歷史兩 commit 更可讀，但代價是 `never commit red` 必須開新例外、`scripts/ci-checks.sh` 與 CI 防線要學會辨識「合法的紅」— 防線複雜度換取的只是敘事清晰。同 commit 方案下紅的證據以測試輸出留在 spec 回報與 commit message，防線零改動。使用者 2026-07-06 裁決採同 commit。

### 為什麼裁決權在使用者、執行權在 executor

場景文字就是規格本體，主權歸規格擁有者 — 這與「backlog → progress 只能由使用者晉升」是同一條原則的兩個切面。但執行面若要求使用者親手編輯 `.feature`，每次變更都被人力卡口；「使用者核准逐字內容、executor 落檔、orchestrator 驗收」保留主權又不阻塞流程，且 trailer 留下裁決出處可稽核。

### 為什麼缺陷再現豁免排隊

backlog 晉升排隊的目的（使用者控制實作順序）對缺陷不成立 — 缺陷已在生產行為中存在，排隊只是延長已知錯誤的存活時間。Autonomy Scope 本就授權 bug 自主處理；補場景只是把「修復必須先再現」的既有紀律延伸到場景級。使用者 2026-07-06 裁決豁免。

### 為什麼只加一條 trailer 而不是新看板

`tasks/bdd-backlog.md` 的來源欄已能承載「需求變更」條目的排程需求；變更的稽核需求由 trailer + git log 滿足。再開第二看板是重複的制度形態，違反制度凍結啟發式。

---

## Consequences

### Positive

- 六類需求各有明文路徑，「改既有功能怎麼走」不再靠臨場解讀凍結條款。
- 場景修訂有機械化 gate 兜底：任何非啟用型 `.feature` 改動都留下裁決出處。
- 缺陷修復獲得場景級迴歸保護，不再被凍結條款逼進 unit test。

### Negative / Trade-offs

- commit-msg hook 規則增多，觸發語意（`@ignore` 移除 vs 其他 `.feature` 改動）有誤判風險。
  - Mitigation: 兩規則以 staged diff 形狀互斥判定，上線含三面故意紅取證（§5）；誤判案例出現時依 failure-triage 迴路處置。
- 「使用者核准逐字場景文字」在變更頻繁時可能成為節流點。
  - Mitigation: 核准粒度是單場景文字（分鐘級 review）；若實測成為瓶頸，屬裁決習慣調整，依治理條款開新 ADR 再議。
- 缺陷再現豁免可能被濫用為「繞過晉升排隊的後門」（把新功能包裝成缺陷）。
  - Mitigation: trailer 必須以 `defect-repro —` 開頭並附缺陷描述，git log 可稽核；orchestrator 驗收時核對再現紅的證據 — 沒有「修復前必紅」證據的即非缺陷。

---

## Alternatives Considered

### Alternative A: 行為變更重新走 `@ignore` 路徑（修訂場景後重標 `@ignore` 再啟用）

Rejected. `@ignore` 的既定語意是「尚未實作」，挪用到「已實作但規格變更」會汙染帳面（已通過計數瞬間倒退）與既有 gate 語意（單移 `@ignore` 檢查、`Refactor-assessment:` 觸發條件），為了複用機制而扭曲語意是反向工程。

### Alternative B: spec-first 兩 commit（場景修訂先入庫，庫內短暫紅）

Rejected. 使用者 2026-07-06 裁決不採。需在 never-commit-red（CLAUDE.md CRITICAL 條）與 `scripts/ci-checks.sh`／CI 上開「合法紅」例外，主防線複雜度上升；收益（規格變更歷史獨立成 commit）可由同 commit 的 `Spec-change:` trailer + diff 內 `.feature` 變更等價提供。

### Alternative C: 缺陷再現一律用 unit/integration test，`.feature` 絕對凍結

Rejected. 使用者 2026-07-06 裁決不採。外部可觀察行為的缺陷失去場景級迴歸保護，且製造「行為規格在 `.feature`、但部分行為的真相在 unit test」的雙源分裂，違反場景庫作為行為 SSOT 的定位。

### Alternative D: 為需求變更另開獨立看板（change-request kanban）

Rejected. `tasks/bdd-backlog.md` 來源欄已預留「需求變更」，排程需求可由既有看板承載；新看板是重複制度形態，違反制度凍結啟發式（解決制度問題優先用裁決習慣，不新增制度形態）。

---

## Implementation Rules

1. 任何 `.feature` 場景修訂（非純 `@ignore` 移除）前，必須取得使用者對修訂後逐字文字的核准；commit 帶 `Spec-change:` trailer 記載裁決依據。
2. 行為變更 commit 必須同時包含：場景修訂、steps／production 變更、帳面更新 — 不得拆 commit；工作區自然紅的測試輸出必須留存於 spec 回報。
3. 場景修訂後未動 production 即綠者，一律停止回報（vacuous amendment），不得 commit。
4. 缺陷再現場景免排隊即時新增，但 trailer 必須以 `defect-repro —` 開頭且附缺陷一句話描述；修復 commit 必含「修復前紅」的取證。
5. 場景刪除為使用者專屬裁決；刪除、production 收斂、帳面遞減同 commit。
6. `commit-msg` hook 的 `Spec-change:` 規則上線必須三面取證（無 trailer 被擋／有 trailer 放行／純 `@ignore` 移除不誤傷），並同 commit 登記 `docs/verification-matrix.md`。
7. **驗收**：

   ```bash
   # 凍結措辭已限縮：CLAUDE.md 與 bdd-backlog.md 不再全域禁止 .feature 變更
   git --no-pager grep -n 'Do not author new `.feature` files here' -- CLAUDE.md
   # 預期 0 命中（改為指向本 ADR 分流的新措辭）
   ```

8. 任何提案修改 1–N，必須先開新 ADR。
