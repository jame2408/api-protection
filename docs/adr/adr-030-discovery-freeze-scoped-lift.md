# Discovery 凍結的範圍化解除：規格已產出但場景從未產出的 BC

> Discovery 凍結的正當性建立在「`.feature` scenarios and API specs are already produced」這句事實宣稱上。該宣稱對 Key Lifecycle 成立（50 條場景全綠），對 Data Plane 不成立——`api-spec.md` §4.1 完整定義了 `validate-key` 契約，但 `.feature` 一條都沒有。本 ADR 不解除凍結本身，而是明文承認凍結的前提從未涵蓋這類 BC，並以「逐案 ADR、嚴格由既有規格推導」的方式解除 Data Plane 一案。

---

## Status

Accepted (2026-07-26)

- 同步項目: `CLAUDE.md`「BDD Scenario Development Cycle」段的凍結句、`tasks/bdd-backlog.md` 檔頭的凍結敘述，兩處於同 commit 補上本 ADR 指針（見 Decision §6）。

---

## Context

### 現況

`CLAUDE.md`「BDD Scenario Development Cycle」段的凍結句：

```
> Development phase only — `.feature` scenarios and API specs are already produced.
> 凍結的是 Discovery 新場景產出；既有場景修訂、缺陷再現、行為移除走
> docs/adr/adr-022-bdd-requirement-type-routing.md 分流（§1 需求類型分流表）。
```

`tasks/bdd-backlog.md` 檔頭同義：「Discovery 新場景產出目前凍結……凍結解除前 `requirements-analysis-design` skill 的 Step 5 不得產出新場景檔」。

`docs/adr/adr-022-bdd-requirement-type-routing.md` §1 分流表把「新功能（場景不存在）」路由到「Discovery 管道（凍結中）」，其「明文不在本 ADR 範圍」節則寫「Discovery 凍結的解除條件 — 維持現狀，另案處理」。本 ADR 即為該「另案」。

### 問題嚴重度

1. **凍結的前提對 Data Plane 是偽的。** 凍結句宣稱場景與 API spec「皆已產出」。實測：`docs/bdd/` 有五份 Discovery Step 5 產出的場景規格（tenant-management、access-policy、key-lifecycle、monitoring-detection、audit-compliance），**唯獨沒有 Data Plane／validation 的對應檔**；`backend/tests/FunctionalTests/Features/` 只有 `KeyLifecycle/01–06`；`tasks/bdd-backlog.md` 待排程項目為空；而 `api-spec.md` §4「Data Plane API（Internal）」與 §5.6 對照表都完整定義了 `POST /api/internal/v1/validate-key`。**即：Data Plane 的場景在任何形態下都不存在，凍結句所稱的「已產出」對它不成立。**

   > **勘誤（2026-07-26 同日）**：本條原文曾寫「Discovery 當初只跑過 Key Lifecycle」——**該敘述有誤**。Discovery Step 5 涵蓋全部五個控制面 BC，其產出即 `docs/bdd/` 的五份規格（合計 121 條 Gherkin 場景）；未被涵蓋的只有 Data Plane。此誤述不影響本 ADR 的決定（Data Plane 確實從未有場景，範圍化解除仍然正當），但會誤導讀者以為 TM／AP／MD／Audit 也需解凍才能動工——**實際上那四個 BC 的場景早已產出，凍結管的是「產出新場景」，不是「把既有產出展開進 `.feature`」**，其待辦是走既有看板流程（`docs/bdd/` → `tasks/bdd-backlog.md` → 使用者晉升）。
2. **凍結條款是被「補寫」進來的，不是帶論證的封閉裁決。** 該敘述由 commit `be0152e`（`docs(consistency): 規範層衝突修繕 — 8 處對齊 + 2 條 follow-up 登記`）加入 `tasks/bdd-backlog.md` 檔頭，性質是把既有狀態文字化，未附解除條件。
3. **不處理則整條主線凍死。** validation slice 是 `docs/adr/adr-017-key-hash-hmac-and-hotpath-contract.md` Implementation Rule 6 三項承諾（`KeyHash` 唯一索引、`FixedTimeEquals` 複核、效能 smoke）的唯一兌現點；`docs/verification-matrix.md` 中兩條效能條目登記為「未追蹤」並指名由該 slice 兌現。凍結不動，這些都無限期懸空。

### 易混淆概念釐清

| 概念 | 是什麼 | 本 ADR 是否解除 |
|---|---|---|
| Data Plane 場景產出 | 由 `api-spec.md` §4.1 逐列轉寫的驗證場景 | ✅ 解除 |
| 「AI 得自創需求」 | 產出規格未定義的行為場景 | ❌ 仍全面禁止 |
| backlog → progress 晉升權 | 誰決定實作順序 | ❌ 不變，仍為使用者專屬 |
| Discovery 管道全面解除 | `requirements-analysis-design` Step 5 恢復自由批次產出 | ❌ 維持凍結 |
| 其他 BC（如 role-management）的場景 | 尚未產出場景的其他 BC | ❌ 不自動適用，須逐案另開 ADR |
| upstream skill 的凍結 gate | `jame2408/agent-skills` 的機械化防線 | ❌ 既有 todo follow-up，不在此立案 |

### 不決定會發生什麼

凍結條款與事實長期不一致，會逼出兩種壞路徑：把驗證需求包裝成「缺陷再現」去擠 ADR-022 §3 的豁免（侵蝕該豁免的可信度），或在實作時默默新增 `.feature` 繞過條款（drift 復現）。兩者都比明文解除更糟。

---

## Decision

### 1. 解除範圍限於 Data Plane

授權為 `api-spec.md` §4「Data Plane API（Internal）」定義的端點產出 `.feature` 場景。其他尚未產出場景的 BC **不因本 ADR 解凍**。

### 2. 場景必須由既有規格推導，不得發明

每一條新場景都必須可指回 `api-spec.md` §4.1 的具體條目——請求欄位表、成功回應欄位表、或驗證失敗錯誤碼表的某一列（`KEY_FORMAT_INVALID`／`KEY_NOT_FOUND`／`KEY_INACTIVE`／`KEY_EXPIRED`／`KEY_REVOKED`／`IP_NOT_ALLOWED`／`SCOPE_INSUFFICIENT`）。**規格未定義的行為不得寫成場景**；若產出過程發現規格缺口，停止並回報，由使用者裁決補規格或縮範圍。

### 3. 排程權不變

新場景先進 `tasks/bdd-backlog.md`，帶 `@ignore` 落入 `.feature`；`backlog → progress` 的晉升仍為**使用者專屬動作**（`CLAUDE.md`「BDD Scenario Development Cycle」段既有規則，本 ADR 不改）。

### 4. 解除判準（可援引，但不自動適用）

本次解除的判準是：**該 BC 的 API 契約已在 `api-spec.md` 定案，但 `.feature` 從未產出**。符合同一判準的其他 BC 得援引本 ADR 的論證，但**仍須逐案另開 ADR 裁決**——判準本身不是自動解凍開關。

### 5. 不在本 ADR 範圍

Discovery 管道的全面解除、role-management 或其他 BC 的場景產出、upstream skill 的凍結 gate、以及 validation slice 的技術題（Validation Model 落地形態、效能門檻）——各自另案。

### 6. 本 ADR 接受時的同步項目（同 commit）

| 檔案 | 段落 | 改動 |
|---|---|---|
| `CLAUDE.md` | 「BDD Scenario Development Cycle」段的凍結句 | 於「凍結的是 Discovery 新場景產出」後補一句：範圍化解除逐案走本 ADR，目前已解除 Data Plane |
| `tasks/bdd-backlog.md` | 檔頭凍結敘述 | 同上補指針，措辭與 CLAUDE.md 對齊 |

---

## Rationale

### 為什麼是範圍化解除，不是全面解除

凍結真正在保護的是「AI 不得自創需求」與「使用者掌握排程權」。Data Plane 的場景不是發明出來的——`api-spec.md` §4.1 的錯誤碼表逐列標註了對應的驗證漏斗層，把它轉寫成 Gherkin 接近**轉錄**而非 Discovery。全面解除則會把保護一併拿掉，而 upstream skill 的凍結 gate 尚未落地（`tasks/todo.md` 既有 follow-up），等同無防線。

### 為什麼不走 ADR-022 §3 的缺陷再現豁免

§3 的豁免是給**事故驅動**的：先有缺陷、再補再現。validation 場景沒有對應缺陷——行為根本還不存在。把它包裝成缺陷再現是對制度說謊，且會稀釋該豁免日後的可信度。同型判斷已有先例：終態濾條的覆蓋缺口補場景時，trailer 誠實寫 `coverage-gap` 而非 `defect-repro`。

### 為什麼不無限期擱置

擱置在誠實性上沒問題，但代價是 ADR-017 Rule 6 的三項承諾與 `docs/verification-matrix.md` 的兩條效能條目永遠停在「未追蹤」。凍結的目的是防止規格漫延，不是凍結產品線；用它來擋一個規格早已定案的 BC，是把手段當成目的。

### 為什麼不機械化

「場景是否由既有規格推導」是語意判斷，無法用 grep 斷言。既有機制已提供足夠約束：ADR-022 §5 的 `commit-msg` gate 會要求任何非純 `@ignore` 移除的 `.feature` 改動帶 `Spec-change:` trailer，該 trailer 即為裁決依據的留痕點；場景內容的正確性由 review 與 Decision §2 的「可指回具體規格條目」要求承擔。

---

## Consequences

### Positive

- 凍結條款與事實重新一致，不再需要臨場解讀「這算不算 Discovery」。
- validation slice 解除阻斷，ADR-017 Rule 6 與驗證矩陣兩條效能條目有了兌現路徑。
- 保留凍結的實質保護：規格外行為仍禁止產出，排程權仍在使用者手上。
- 為未來同型情況（規格已定、場景未產）留下可援引的判準與論證，但不打開自動解凍的後門。

### Negative / Trade-offs

- 「由既有規格推導」是語意判準，存在被寬鬆解讀的空間。
  - Mitigation: Decision §2 要求每條場景可指回 `api-spec.md` §4.1 的具體條目，並明訂發現規格缺口時**停止回報**而非自行補寫；`Spec-change:` trailer 留下裁決依據。
- 逐案 ADR 比一次全面解除累贅，未來每個同型 BC 都要再寫一份。
  - Mitigation: Decision §4 已把判準與論證固化，後續 ADR 可直接援引，增量成本僅為範圍宣告與同步項目。
- 解除後 `.feature` 新增的來源多了一條，`tasks/bdd-progress.md` 的分母會再次變動。
  - Mitigation: ADR-022 §3.4 已允許帳面分母依 grep 實況變動，`scripts/bdd-lint.sh` 以當下實況校驗，無需額外機制。

---

## Alternatives Considered

### Alternative A: 全面解除 Discovery 凍結

恢復 `requirements-analysis-design` Step 5 的批次場景產出。

Rejected. 一次拿掉「AI 不得自創需求」的保護，而 upstream skill 的凍結 gate 尚未落地（`tasks/todo.md` follow-up），解除後無任何防線；且本次需求只涉及一個規格早已定案的 BC，全面解除屬過度反應。

### Alternative B: 把驗證需求包裝成 ADR-022 §3 的缺陷再現

以「缺陷修復得即時新增再現場景」的豁免通道繞過凍結。

Rejected. 沒有缺陷可再現——行為尚未實作；§3 的豁免以事故驅動為前提，濫用會稀釋其可信度。同型情境已有誠實處理的先例（覆蓋缺口以 `coverage-gap` 而非 `defect-repro` 標記）。

### Alternative C: 維持凍結，validation slice 無限期擱置

不動制度，等未來某次全面解凍再說。

Rejected. ADR-017 Rule 6 的三項承諾與 `docs/verification-matrix.md` 兩條效能條目將永遠停在「未追蹤」；且凍結的目的是防規格漫延，不是凍結產品線，用它擋一個規格已定案的 BC 是把手段當目的。

### Alternative D: 不開 ADR，由 executor 在 slice 派工時自行判斷是否屬「規格推導」

把判斷權下放到執行層，省一份 ADR。

Rejected. 凍結條款是規範層文字，執行層無權自行認定豁免；且 `CLAUDE.md` 與 `tasks/bdd-backlog.md` 的措辭目前是全域禁止，不同步修訂就會出現「ADR 說可以、規範面說不行」的對打。裁決權在使用者、留痕在 ADR，是本 repo 既有的分工。

---

## Implementation Rules

1. `.feature` 場景產出的解除範圍僅限 `api-spec.md` §4「Data Plane API（Internal）」定義的端點；其他 BC 不得援本 ADR 直接產出場景。
2. 每條新場景必須可指回 `api-spec.md` §4.1 的具體條目（請求欄位表／成功回應欄位表／驗證失敗錯誤碼表某一列）；規格未定義的行為不得寫成場景。
3. 產出過程若發現規格缺口，停止並回報，不得自行補寫規格或推測行為。
4. 新場景一律先進 `tasks/bdd-backlog.md` 並帶 `@ignore` 落入 `.feature`；`backlog → progress` 晉升維持使用者專屬。
5. 新增場景的 commit 必須帶 `Spec-change:` trailer（`docs/adr/adr-022-bdd-requirement-type-routing.md` §5 的 `commit-msg` gate 已機械化強制），trailer 內容須寫明裁決依據為本 ADR。
6. Decision §6 的兩處同步項目必須與本 ADR 同 commit 落地。
7. **驗收**：

   ```bash
   # 凍結措辭已補上範圍化解除指針，不再是全域禁止的唯一敘述
   git --no-pager grep -n "adr-030" -- CLAUDE.md tasks/bdd-backlog.md
   # 預期：兩檔各至少 1 命中
   ```

8. 任何提案修改 1–7，必須先開新 ADR。
