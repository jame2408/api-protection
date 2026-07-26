# ADR Topics — Validation Slice（設計輪產出，2026-07-26）

> 只列題目＋張力＋建議，不代寫 ADR 本文（ADR 起草走 `docs/adr/_template.md`）。順序＝建議的裁決順序。
> 產出脈絡：使用者指示開 validation slice；協調者設計輪盤點權威輸入（`docs/design/api-spec.md` §4／§5.6、`docs/design/design-doc.md` §4.7／§6.2.1／§6.2.2、`docs/design/context-integration-spec.md` I7／I8、`docs/adr/adr-017-*.md` Implementation Rules、`docs/verification-matrix.md`）後，發現三處實質矛盾與一項流程阻斷，均需使用者裁決才能進實作。

## 0. 前置阻斷：本 slice 目前無法進入 BDD 路徑

**事實**（已機械化取證）：
- `backend/tests/FunctionalTests/Features/` 只有 `KeyLifecycle/01–06`，**無任何驗證場景**。
- `tasks/bdd-backlog.md`「待排程項目」為空。
- `backend/src/` 無 Validation BC；`grep FixedTimeEquals|ValidateKey|KeyValidationView` 零命中。
- `docs/adr/adr-022-bdd-requirement-type-routing.md` §1 把「新功能（場景不存在）」路由到 **Discovery 管道（凍結中）**；§6 明列凍結解除條件「維持現狀，另案處理」。

**含意**：題 1–4 全部裁決完，仍不能派工寫產品碼——沒有場景可驅動紅綠。解凍與否是獨立於下列技術題的**流程裁決**，且依 §6 屬「另案」，可能需要自己的 ADR 或一次明文裁決。

**建議**：技術題（1–4）先裁決，讓解凍後的場景產出有明確的規格基礎；解凍裁決可與題 1 的結論一起做，因為漏斗執行側決定了場景的觀察面（是 HTTP 回應，還是 read model 狀態）。

---

## 1. 驗證漏斗執行在哪一側？（阻斷級，決定整個切片形狀）

**張力**：兩份權威文件描述兩種架構。

- `api-spec.md` §4.1：Gateway 呼叫 `POST /api/internal/v1/validate-key`，送 `apiKey`／`sourceIp`／`requestedScope`，收 `valid` ＋ metadata ＋ `errorCode`／`httpStatusHint`。→ **本系統執行漏斗**。
- `design-doc.md` §6.2.2 時序圖：Gateway 查快取的 `KeyValidationView`，未命中才向 KM 取資料，然後 **Gateway 自己執行驗證漏斗**（明文寫「Gateway 將請求中的金鑰明文以相同演算法……重新計算雜湊後進行恆定時間比對」）。→ **Gateway 執行漏斗**。

兩者對三件事的含意完全相反：**pepper 邊界**、**P99 由誰承擔**、**快取失效語意落在哪裡**。

**關鍵新事實**（見題 2）：ADR-017 已把雜湊改為**全域 pepper** 的 HMAC。Gateway 側自行比對雜湊，等同於**把全域 pepper 發送到每一個 Gateway／Sidecar／SDK 節點**——pepper 一旦外流，攻擊者可離線對全體租戶的金鑰做碰撞驗證，爆炸半徑是全域而非單鑰。舊的 per-key salt 模型沒有這個問題（salt 內嵌於雜湊、無額外秘密），所以 §6.2.2 的設計在當時是自洽的，是 ADR-017 之後才失效。

**建議**：**採 api-spec §4.1，漏斗在本系統側執行**；Gateway 只快取 validate-key 的**回應**，不持有 `keyHash`、不持有 pepper。同步項目：改寫 design-doc §6.2.2 的時序圖與 Salt 段落（見題 2）。

**若裁決採 Gateway 側**：必須先解決 pepper 分發與輪替（等同新增一個密鑰管理子題），且與 ADR-017 Rule 3「pepper 取自組態、啟動 fail-fast」的單一持有者假設衝突——那會是一份新 ADR 修改 ADR-017，走其 Rule 8 governance clause。

---

## 2. design-doc §6.2.2 的 Salt 敘述已被 ADR-017 廢止（文件勘誤，但含技術後果）

**張力**：§6.2.2 原文寫「KeyHash 的 Salt 採用 **per-key 獨立鹽值**……Salt 不會以獨立欄位傳輸，而是內嵌在雜湊值中（如 `$algorithm$salt$hash` 格式）」。ADR-017 Rule 3 實際落地為 `Base64(HMACSHA256(pepper, UTF8(rawKey)))`——**無 per-key salt、無 `$…$` 分段格式、且輸出對同一輸入是確定性的**。

**後果不只是文字**：
- 確定性雜湊 → 可以**用 hash 直接查**（O(1) 索引查找），這正是 ADR-017 Rule 6(a) 要求 `KeyHash` 唯一索引的前提。
- per-key salt → 必須先以 `keyPrefix` 縮小候選集，再逐筆重算比對（無法用單一索引命中）。

兩者導出的查找策略、索引設計、以及漏斗第 1 層（格式／prefix）的職責都不同。

**建議**：本題不需要新 ADR（ADR-017 已裁決演算法），只需**在 validation slice 的第一個 commit 同步修訂 design-doc §6.2.2**，並明載查找策略為「以 KeyHash 唯一索引直接命中」。請使用者確認此修訂授權。

---

## 3. Validation Model 的落地形態

**張力**：`context-integration-spec.md` I7 定義 Validation Model 為 KL＋AP 的**事件投影**（`KeyValidationView`，投影規則表已列 8 個事件與各自是否需主動快取失效）；但 repo 內既無該 BC、也無任何投影消費者（outbox 目前只有寫入端）。

選項：
- **(a) 真的建 read model**（新資料表＋投影 handler 訂閱 outbox）：貼合 I7 契約，熱路徑不打主表；代價是要處理投影落後與重放，且需要先有 outbox 消費機制（目前不存在）。
- **(b) 驗證路徑直接查 KL／AP 主表**：最小可行、強一致；但違反 I7 契約，且把極高頻流量打進控制面主庫。

**建議**：走 (a)，但**第一刀只投影 KL 側欄位**（`keyPrefix`／`lifecycleStatus`／`keyHash`／`scopes`／`environment`／`tenantId`），AP 側（`ipAllowlist`／`rateLimitConfig`）留第二刀——對應漏斗第 3 層可先以「無白名單即放行」的既有語意通過，第 5 層 scope 檢查由 KL 側 `scopes` 即可完成。這樣第一刀就能端到端跑通並量測效能，而不必同時把 AP 投影也做完。

**併案**：I7 投影規則表**未列 `KeyGracePeriodExpired`**（2026-07-12 場景 40/48 發現並登記，見 `tasks/checkpoint.md` 待裁決欄），而該事件語意上與 `KeyRevoked` 同級（寬限期結束＝舊金鑰立即失效）。本題裁決時一併補列，缺口即可銷案。

---

## 4. 效能門檻：5ms 還是 50ms？量在哪裡？

**張力**：`api-spec.md` §4 開頭寫「目標 p99 < 5ms（含快取命中）」；`CLAUDE.md` §4 與 ADR-017 Rule 6(c) 寫 **P99 < 50ms、≥ 100 RPS**，且 `docs/verification-matrix.md` 第 99／100 行已把這兩條登記為「**未追蹤**——repo 內無負載測試腳本或效能基準測試」，並指名由 validation slice 兌現。

兩個數字本身不衝突（5ms 是設計目標、50ms 是驗收 gate），但**perf smoke 以何者為準、在什麼環境量測**必須先定，否則寫出來的 smoke 只是裝飾。

**建議**：
- gate 用 **P99 < 50ms／≥ 100 RPS**（ADR-017 已裁決，不重開）；
- api-spec §4 的「5ms」改為明確標示為**設計目標（非驗收門檻）**，避免後人誤把它當 gate；
- smoke 跑在既有 Testcontainers 環境，**回報時必須附環境描述**（本機 vs CI），並在矩陣登記中註明數字對環境敏感。

---

## 裁決後才會解鎖的事

1. 題 1 決定場景的觀察面（HTTP 回應 vs read model 狀態）→ 場景才寫得出來 → 題 0 的解凍裁決才有標的。
2. 題 3 決定第一刀範圍 → 才能估切片大小與 migration 內容（`KeyHash` 唯一索引屬 ADR-017 Rule 6(a)，與 read model 表分屬兩件事）。
3. 題 2、題 4 的同步項目（design-doc §6.2.2、api-spec §4 措辭、verification-matrix 第 99／100 行）依 CLAUDE.md ADR 紀律，須與對應實作**同 commit** 落地。
