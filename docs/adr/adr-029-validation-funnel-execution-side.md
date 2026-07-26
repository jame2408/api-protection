# 驗證漏斗執行側與 pepper 邊界：漏斗一律在金鑰管理系統內執行

> 本 ADR 終結設計文件內部的架構矛盾：`api-spec.md` §4.1 描述「Gateway 呼叫 validate-key、系統回答 valid」，而 `design-doc.md` §4.7／§6.2.2／§8.3 描述「Gateway 持有 KeyValidationView（含 keyHash）並自行執行驗證漏斗」。ADR-017 把金鑰雜湊改為全域 pepper 的 HMAC 之後，後者需要把 pepper 發送到每個邊緣節點才可能成立。本 ADR 裁定漏斗一律在金鑰管理系統內執行，pepper 與 keyHash 不得離開系統邊界。

---

## Status

Accepted (2026-07-26)

- Supersedes: `docs/design/design-doc.md` §6.2.2「快取互動模式」的時序圖與「雜湊驗證與 Salt 策略」段落；§8.3「分層快取架構」表的 L1 內容欄。
- 同步項目: `docs/design/design-doc.md` §4.7、§6.2.2、§8.3 三處於同 commit 修訂（見 Decision §6）。

---

## Context

### 現況

兩套各自自洽、互不引用的架構敘述並存於設計文件。

**陣營一 — 系統側執行（`docs/design/api-spec.md` §4.1「POST /api/internal/v1/validate-key — 金鑰驗證」）**：Gateway 送出完整金鑰與驗證上下文，系統回答 `valid` 與 metadata：

```
{ "apiKey": "...", "sourceIp": "...", "requestedScope": "orders:read" }
→ { "valid": true, "keyId": "...", "scopes": [...], "rateLimitConfig": {...} }
```

失敗時回 `errorCode` 與 `httpStatusHint`，錯誤碼表逐列標註「對應驗證漏斗層」（Layer 1 格式檢查 … Layer 5 權限檢查）——漏斗判斷顯然發生在系統內。

**陣營二 — Gateway 側執行（`docs/design/design-doc.md`）**，三個章節一致：

- §4.7「Validation Read Model」的 `KeyValidationView` 欄位表把 `keyHash` 列為「第 4 層：雜湊驗證」所用。
- §6.2.2「快取互動模式」時序圖最後兩步為 `GW->>GW: 執行驗證漏斗`，並在其後的「雜湊驗證與 Salt 策略」段寫明：

  > 第 4 層雜湊驗證：KeyValidationView 中包含預計算的 `keyHash`（內含 Salt）。Gateway 將請求中的金鑰明文以相同演算法與內嵌的 Salt 重新計算雜湊後進行恆定時間比對。

- §8.3「快取策略」的分層快取表把 L1（Gateway / Sidecar / SDK 本地）與 L2（共享快取層）的「內容」都列為 `KeyValidationView`。

### 問題嚴重度

矛盾在 ADR-017 之後才變成安全問題，而非單純的文件不一致：

1. **ADR-017 抽掉了陣營二的地基。** `docs/adr/adr-017-key-hash-hmac-and-hotpath-contract.md` Implementation Rule 3 把雜湊定為 `Base64(HMACSHA256(pepper, UTF8(rawKey)))`，pepper 為**全域單一秘密**、取自組態並於啟動 fail-fast。§6.2.2 描述的 per-key 獨立鹽值與 `$algorithm$salt$hash` 內嵌格式**已不存在**。
2. **陣營二現在需要把 pepper 發到邊緣。** 沒有 pepper 就算不出 HMAC，Gateway 無從比對。而 pepper 是全域的：任一邊緣節點被攻破，攻擊者即可對**全體租戶**的金鑰做離線碰撞驗證——爆炸半徑由單一金鑰放大為整個系統。per-key salt 模型沒有這個性質（salt 內嵌於雜湊、驗證不需額外秘密），所以陣營二在當時是自洽的。
3. **不裁定就無法動工。** 兩套架構的場景觀察面不同（HTTP 回應 vs 邊緣快取狀態），BDD 場景寫不出來；`KeyHash` 唯一索引（ADR-017 Rule 6(a)）是否為熱路徑查找主鍵也取決於此。

### 易混淆概念釐清

| 概念 | 是什麼 | 本 ADR 是否規範 |
|---|---|---|
| 驗證漏斗執行側 | 五層檢查在哪個行程內跑 | ✅ |
| pepper 與 keyHash 的傳播邊界 | 這兩個值可以出現在哪裡 | ✅ |
| 邊緣快取的內容 | Gateway / Sidecar / SDK 快取什麼 | ✅ |
| Validation Model 的落地形態 | read model 投影 vs 熱路徑直查主表 | ❌ 另題（見 `docs/design/validation-slice-adr-topics.md` 題 3） |
| 效能門檻數字 | 5ms 設計目標 vs 50ms 驗收 gate | ❌ 另題（同上，題 4） |
| Discovery 凍結是否解除 | 驗證場景能否產出 | ❌ 另題（同上，題 0） |
| 部署拓撲 | 幾個實例、如何水平擴充 | ❌ 不規範 |

---

## Decision

### 1. 驗證漏斗一律在金鑰管理系統內執行

`api-spec.md` §4.1 的 `POST /api/internal/v1/validate-key` 是驗證的**唯一觸發面**。五層漏斗（格式 → 狀態 → IP → 雜湊 → 權限）全部在系統行程內完成，Gateway / Sidecar / SDK 只送出請求並依回應中的 `valid`／`errorCode`／`httpStatusHint` 行動。

### 2. pepper 不得離開系統邊界

pepper 只從 `ApiKeyHashing:Pepper` 組態讀取（ADR-017 Rule 3），**不得**出現在任何 API 回應、事件 payload、快取內容或發送至邊緣節點的資料中。

### 3. keyHash 不得離開系統邊界

`validate-key` 的回應不含 `keyHash`（`api-spec.md` §4.1 的回應欄位表即為契約，本 ADR 不修改該表）。雜湊比對以 `CryptographicOperations.FixedTimeEquals` 在系統內完成（ADR-017 Rule 6(b)）。

### 4. 邊緣快取的內容改為 validate-key 的回應

L1（Gateway / Sidecar / SDK 本地）快取的是**驗證回應**，不是 `KeyValidationView`。主動快取失效機制不變：撤銷／鎖定／暫停事件仍以 Pub/Sub 廣播清除各節點快取（`docs/design/prd.md` 的主動快取失效風險段與 `design-doc.md` §8.3「安全事件的快取失效優先級」皆維持成立）。

### 5. KeyValidationView 是系統內部投影

`KeyValidationView`（含 `keyHash`）只存在於系統信任邊界內。§8.3 的 L2 共享快取層若部署於系統邊界內（如系統自有的 Redis），仍可存放該視圖；邊界外則不可。

### 6. 本 ADR 接受時的同步項目（同 commit）

| 檔案 | 段落 | 改動 |
|---|---|---|
| `docs/design/design-doc.md` | §4.7「Validation Read Model」 | 於視圖表後補一句：該視圖為系統內部投影，`keyHash` 不離開系統邊界，指向本 ADR |
| `docs/design/design-doc.md` | §6.2.2「快取互動模式」 | 時序圖改為 Gateway 呼叫 validate-key、漏斗在系統側執行；「雜湊驗證與 Salt 策略」段改寫為 ADR-017 的 HMAC + 全域 pepper 事實，並說明比對在系統內完成 |
| `docs/design/design-doc.md` | §8.3「分層快取架構」表 | L1 內容欄由 `KeyValidationView` 改為 `validate-key 回應（不含 keyHash）`；L2 列補註「須位於系統信任邊界內」 |

---

## Rationale

### 為什麼選系統側而不是 Gateway 側

pepper 的邊界是**安全屬性**，不是部署細節。ADR-017 選擇全域 pepper（而非 per-key salt）的整個前提，就是它只存在於系統內部一處；把它複製到 N 個邊緣節點，等於把「單一秘密」的風險模型改成「N 個持有者中最弱的那個」。這是對 ADR-017 既有決策的實質變更，若要走該路徑必須依其 Rule 8 先開新 ADR，而非在實作時默默發生。

其次，Gateway 側漏斗在本 repo **無法被場景驅動**：repo 內沒有真的 Gateway 產品可整合，行為端到端驗證不了，違反「無場景不建」的既有紀律。

### 為什麼不做混合分層

「Gateway 跑第 1／2／3／5 層、只有第 4 層回系統」看似兩全，但**合法流量必然走到第 4 層**——省下的往返只發生在垃圾流量上，而代價是兩份契約、兩處快取語意、兩個失效路徑。收益與複雜度錯配。

### 為什麼不回頭改用 per-key salt

那會讓 Gateway 無須 pepper 即可自行比對，表面上救活陣營二。但 ADR-017 的確定性雜湊正是 `KeyHash` 唯一索引（Rule 6(a)）與 O(1) 直接查找的前提；改回 per-key salt 後，查找必須先以 `keyPrefix` 縮小候選集再逐筆重算，熱路徑成本反而上升。且此舉直接推翻 ADR-017，須走其 Rule 8。

### 為什麼不機械化

pepper 是否外流無法用單一 grep 斷言（它可能經由組態注入、日誌、事件 payload 洩漏）。本 ADR 以 Implementation Rules 的 review checklist 與回應 DTO 的形狀約束承載，機械化留待 validation slice 落地時針對具體型別補靜態檢查。

---

## Consequences

### Positive

- pepper 與 keyHash 的傳播邊界收斂為「系統內部」，攻擊面不隨 Gateway 節點數擴張。
- 驗證行為只有一份實作，天然滿足 ADR-05「統一介面確保不同模式下的驗證行為一致」。
- 場景觀察面明確（HTTP 回應），validation slice 的第一刀可端到端測試與量測。
- `KeyHash` 唯一索引（ADR-017 Rule 6(a)）確立為熱路徑查找主鍵，索引設計不再懸而未決。

### Negative / Trade-offs

- 每個驗證請求多一次網路往返，進入延遲預算。
  - Mitigation: 邊緣快取 validate-key 回應（Decision §4）吸收重複請求；驗收門檻以 ADR-017 Rule 6(c) 的 P99 < 50ms／≥ 100 RPS 為準，`api-spec.md` §4 的 5ms 屬設計目標，其定位由另題裁決。
- 推翻設計文件既有敘述，讀者可能引用到舊段落。
  - Mitigation: Decision §6 的同步項目在同 commit 改完三處，並在 Status 欄以 Supersedes 標明被取代的段落。
- 金鑰管理系統成為驗證路徑上的必經節點，可用性要求提高。
  - Mitigation: 本 ADR 不規範部署拓撲；水平擴充與系統邊界內的 L2 共享快取皆不受限制，`design-doc.md` §8.3 的 Fail-safe 機制（強制回源、TTL 自動縮短、Circuit Breaker）仍適用。

---

## Alternatives Considered

### Alternative A: 維持 design-doc 原架構，Gateway 側執行漏斗

Gateway 持有 `KeyValidationView`（含 `keyHash`）與 pepper，本地完成五層檢查；系統只在快取未命中時提供資料。

Rejected. 需要把全域 pepper 分發到每一個 Gateway / Sidecar / SDK 節點，爆炸半徑由單一金鑰放大為全體租戶；且此舉實質變更 ADR-017 Rule 3 的單一持有者假設，須先依其 Rule 8 開新 ADR 並補上 pepper 分發與輪替設計。此外，repo 內無 Gateway 產品可整合，該架構的行為無法用場景驅動驗證。

### Alternative B: 混合分層 —— Gateway 跑第 1／2／3／5 層，第 4 層回系統

pepper 留在系統內，Gateway 以快取視圖完成不需秘密的檢查。

Rejected. 合法流量必然走到第 4 層，網路往返省不掉；省下的僅是格式或狀態明顯無效的垃圾流量。代價是兩份契約（視圖查詢 + 雜湊驗證）、兩處快取失效語意、以及漏斗被切成兩半後難以保證層序一致。收益與複雜度錯配。

### Alternative C: 回頭改用 per-key salt，讓 Gateway 無須 pepper 即可比對

恢復 `$algorithm$salt$hash` 形態，salt 內嵌於雜湊值，驗證不需額外秘密。

Rejected. 直接推翻 ADR-017 Rule 3，須走其 Rule 8；且確定性雜湊是 Rule 6(a) `KeyHash` 唯一索引與 O(1) 查找的前提，改回 per-key salt 後熱路徑必須先以 `keyPrefix` 縮小候選集再逐筆重算，效能成本上升，與本 slice 的效能目標背道而馳。

### Alternative D: 不裁定，讓實作時再決定

先實作 `validate-key` 端點，邊緣快取形態留給未來整合 Gateway 時再說。

Rejected. 兩套架構的場景觀察面不同，不裁定則場景寫不出來、BDD 紅綠無從驅動；且「實作時再決定」正是本 repo 反覆記取的 drift 來源——設計文件與實作各自演化，最後由後人承擔考古成本。

---

## Implementation Rules

1. 驗證漏斗五層全部在金鑰管理系統行程內執行；`POST /api/internal/v1/validate-key` 為唯一驗證觸發面。
2. pepper 只從 `ApiKeyHashing:Pepper` 組態讀取，不得出現在 API 回應、事件 payload、邊緣快取內容或任何送出系統邊界的資料中。
3. `validate-key` 的回應不得包含 `keyHash`；雜湊比對以 `CryptographicOperations.FixedTimeEquals` 在系統內完成（禁 `string ==`，ADR-017 Rule 6(b)）。
4. 邊緣快取的內容為 validate-key 回應；`KeyValidationView`（含 `keyHash`）只得存在於系統信任邊界內。
5. Decision §6 的三處 design-doc 同步項目必須與本 ADR 同 commit 落地。
6. **驗收**（validation slice 落地時執行；本 ADR 接受時尚無驗證程式碼，故此條在該 slice 的 DoD 內兌現）：

   ```bash
   # 驗證回應型別不得帶出 keyHash
   git --no-pager grep -n -E 'KeyHash|keyHash' -- backend/src/*/Validate* ':!docs'
   # 預期：僅出現在系統內部比對路徑，不得出現在回應 DTO 的屬性宣告
   ```

7. 任何提案修改 1–6，必須先開新 ADR。
