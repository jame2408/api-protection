# 金鑰儲存雜湊改用 HMAC-SHA256 + pepper，並固化驗證熱路徑合約

> Lead-in：終結「PRD 要求慢速 KDF（Argon2id/PBKDF2）vs 現況 BCrypt workFactor 4 vs 熱路徑 P99 < 50ms」三方矛盾。裁決依據實測基準：對高熵機器 token，慢速 KDF 防的威脅不存在，卻使熱路徑效能預算爆表；改用 HMAC-SHA256 + server-side pepper，同時把驗證熱路徑的查找方式、恆定時間比較、效能防線機械化時點一次固化。

---

## Status

Accepted (2026-07-05)

- 同步項目（同 commit）：`docs/design/prd.md` §5.2（R-STR-01 / R-STR-02 增補高熵機器 token 條款）、`docs/design/design-doc.md` 開放問題 Q1 回填、`docs/verification-matrix.md`（hasher 測試登記＋效能無防線區加註排定）、`tasks/todo.md` #5 結案與 #8 指針、production 與測試程式碼實作本體。

---

## Context

### 現況

三份權威來源對「金鑰怎麼存」互相矛盾：

- `docs/design/prd.md` 的「R-STR-01: 單向雜湊」要求「必須使用專為密碼儲存設計的慢速雜湊演算法（如 Argon2id 或 PBKDF2-SHA256）」；「R-STR-02: 獨立加鹽」要求每金鑰獨立隨機 salt。
- 實際程式碼（`ApiKey` 的 `GenerateKeyMaterial`）用的是 BCrypt，且 work factor 低到只剩測試速度：

```csharp
// 現況：既不在 PRD 允許清單內，強度參數也遠低於任何 2026 建議值
var keyHash = BCrypt.Net.BCrypt.HashPassword(rawKey, workFactor: 4);
```

- `CLAUDE.md` §4 Verification Standards 要求熱路徑「P99 < 50ms, throughput ≥ 100 RPS」；`docs/design/prd.md` 的「R-VAL-01: 恆定時間比較」要求驗證用恆定時間比較。

### 兩個決定性的事實

**(1) 金鑰識別碼不唯一，salted KDF 無法建索引查找。** `rawKey` 格式為 `apk_{tenant前4碼}_{env}_{隨機hex}_{checksum}`，prefix 在同租戶同環境的所有金鑰間共用。salted 雜湊（BCrypt/Argon2/PBKDF2 逐金鑰隨機鹽）不可能按雜湊值查索引 —— 驗證必須撈出同 prefix 的全部候選金鑰逐把 verify，熱路徑成本 = 單次雜湊成本 × 候選數（上限 `MaxActiveKeysPerConsumerEnv` = 10）。

**(2) 實測基準（2026-07-05，本機 Apple Silicon，Release build；絕對值依硬體浮動，數量級與相對比例是論證主體）：**

| 演算法（驗證單次成本） | median | ×10 候選（最壞） | vs P99 < 50ms |
|---|---|---|---|
| BCrypt workFactor=4（現況） | 0.88 ms | 8.8 ms | 過，但強度形同虛設 |
| BCrypt workFactor=12（2026 最低） | 219 ms | 2.2 s | 單次即爆 4 倍 |
| Argon2id（OWASP 最低 m=19MiB,t=2,p=1） | 44.3 ms | 443 ms | 單次即頂滿 |
| PBKDF2-SHA256 600k iter（OWASP 最低） | 85.6 ms | 856 ms | 單次即爆 |
| HMAC-SHA256 + pepper | 0.0003 ms | 0.003 ms | 餘裕 5 個數量級 |

Argon2id 另有記憶體維度：每次驗證吃 19 MiB，100 RPS 下等於 1.9 GB/s 的記憶體佔用churn —— 攻擊者拿無效金鑰打驗證端點即是現成的資源耗盡放大器（與 PRD「資源耗盡 (DDoS/Abuse)」威脅模型自相矛盾）。

### 為什麼慢速 KDF 在這裡是錯的工具

R-STR-01 的措辭（「專為**密碼**儲存設計」）暴露了它的隱含前提：輸入是低熵的人類密碼，慢速 KDF 用計算成本補熵不足。但本系統的金鑰是機器生成的隨機值（現況 96-bit，本 ADR 升至 128-bit），彩虹表與 GPU 暴力破解對 2^128 空間在任何雜湊速度下都不可行 —— 慢速 KDF 在此提供零額外安全，只帶來效能與 DoS 面的純損失。業界同型系統的既成實踐：GitHub PAT 以純 SHA-256 儲存、AWS secret key 以 HMAC 衍生驗證。

---

## Decision

### 1. 儲存雜湊：HMAC-SHA256 + server-side pepper

`KeyHash = Base64(HMACSHA256(pepper, UTF8(rawKey)))`。

```csharp
// before（BCrypt，隨機鹽 → 不可索引查找）
var keyHash = BCrypt.Net.BCrypt.HashPassword(rawKey, workFactor: 4);

// after（HMAC + pepper，確定性 → 可唯一索引查找）
var keyHash = hasher.ComputeHash(rawKey);   // IApiKeyHasher（KeyLifecycle.Domain）
```

- 抽象 `IApiKeyHasher` 放 `KeyLifecycle.Domain`（比照 `IApiKeyRepository`），實作 `HmacApiKeyHasher` 放 Infrastructure。
- pepper 為 256-bit（Base64 設定值，解碼後 ≥ 32 bytes），來源 `ApiKeyHashing:Pepper` 組態（環境變數 / secret store），**永不入 DB、永不入版控的 production 設定**；options validation 於啟動時 fail-fast（缺值或過短即拒絕啟動）。開發/測試環境使用明顯標示的假 pepper（`appsettings.Development.json` / WebApplicationFactory），不屬秘密。

### 2. 金鑰熵下限升至 128-bit

`GenerateKeyMaterial` 隨機體由 12 bytes（96-bit）升至 16 bytes（128-bit）。快速雜湊路線的安全性完全立足於輸入熵，128-bit 是本 ADR 的安全前提，列為硬性下限：**任何降低金鑰隨機體熵的提案，必須先開新 ADR**。

### 3. 驗證熱路徑合約（未來 validation slice 的既定設計，本 ADR 先固化）

1. 確定性雜湊 → 對 `KeyHash` 建**唯一索引**，等值查找 O(1)（migration 隨 validation slice 落地，不在本 ADR 實作範圍）。
2. 取回列後以 `CryptographicOperations.FixedTimeEquals` 複核候選雜湊與儲存值（滿足 R-VAL-01；禁止 `string ==`）。
3. validation slice 的 DoD 必須包含**可執行的效能 smoke 檢驗**（P99 < 50ms、≥ 100 RPS），並同 commit 登記入 `docs/verification-matrix.md` —— 消除矩陣「無防線區塊」的兩條效能規則。

### 4. 邊界（不在本 ADR 範圍）

- 若未來出現**人類選擇的低熵秘密**（使用者密碼、可自訂 token），R-STR-01 的慢速 KDF 要求原樣適用 —— 本 ADR 的豁免嚴格限於「系統生成、熵 ≥ 128-bit 的機器 token」。
- pepper 的輪替機制（雙 pepper 過渡期）：目前無資料遷移需求（無 production 資料），輪替設計留待實際需要時開 ADR。
- todo #7 併發 TOCTOU guard：與雜湊選型無關，不在此裁決。

---

## Rationale

### 為什麼選 HMAC 而不是 PRD 明文的 Argon2id

安全上等效（對 128-bit 熵輸入，兩者都不可暴力破解；HMAC 的 pepper 另補「DB 外洩但組態未洩」情境的縱深），效能上差 5 個數量級且唯一能支撐索引查找。Argon2id 的所有優勢（memory-hard、抗 GPU）只在輸入熵不足時有意義。詳見 Alternatives。

### 為什麼要 pepper 而不是純 SHA-256

純快速雜湊已足以護 128-bit 熵，但 pepper 成本近零（一次 HMAC vs 一次 SHA-256），換到兩件事：(1) DB dump 單獨外洩時，攻擊者連驗證候選金鑰的能力都沒有；(2) 對未來可能的低熵金鑰格式回歸（如人為縮短）提供緩衝。棄 per-key salt（R-STR-02 原文）的理由：salt 防的是預計算/彩虹表，128-bit 隨機輸入本身就是不可預計算的 —— 金鑰即自己的 salt；而 salt 會破壞確定性查找，pepper 不會。

### 為什麼熵升級與演算法決策綁在同一份 ADR

快速雜湊的安全論證以熵為前提，兩者分開裁決會出現「先接受 HMAC、後有人降熵」的組合漏洞。綁定後，熵下限受本 ADR 治理條款保護。

### 為什麼效能防線機械化延至 validation slice

效能規則（P99/RPS）約束的是**驗證端點**，該端點尚未實作 —— 現在能機械化的只有微基準（已做，證據在 Context），端到端防線必須等被測物存在。本 ADR 把「validation slice DoD 含 perf smoke + 矩陣登記」寫成 Implementation Rule，使缺口有規範錨點而非依賴記憶。

---

## Consequences

### Positive

- 驗證熱路徑成本從「44–219 ms × 候選數」降至微秒級，P99 < 50ms 預算從不可能變成餘裕充足。
- `KeyHash` 可建唯一索引，驗證查找 O(1)，同時天然消除雜湊碰撞歧義。
- 消除 DoS 放大面（無 memory-hard 計算可被濫用）。
- BCrypt 依賴（含 Infrastructure 的殭屍引用）整包移除，供應鏈面縮小。
- PRD 與程式碼的矛盾正式終結，矩陣效能無防線區取得排定時點。

### Negative / Trade-offs

- 偏離 PRD R-STR-01 明文（慢速 KDF）。
  - Mitigation: PRD 同 commit 增補「高熵機器 token 豁免」條款並回指本 ADR；豁免範圍以「系統生成 + 熵 ≥ 128-bit」嚴格限定，低熵秘密不適用。
- pepper 成為單點秘密：組態洩漏 + DB 洩漏同時發生時，退化為純快速雜湊。
  - Mitigation: 退化後的底線仍是 128-bit 熵不可暴力破解（安全不繫於 pepper 保密）；pepper 只作縱深，且啟動期 fail-fast 強制其存在與長度。
- 確定性雜湊使「同一 rawKey 重複入庫」可被觀察（相同雜湊值）。
  - Mitigation: rawKey 為 128-bit 隨機生成，碰撞機率可忽略；唯一索引（validation slice）將此情境轉為顯式錯誤。
- `ApiKey.Create` 簽章增加 hasher 參數，呼叫面（Handler、測試 seed steps）需同步修改。
  - Mitigation: 呼叫面已盤點僅 3 處（`CreateApiKeyHandler`、`CreateApiKeySteps` 兩個 Given）；一次 commit 內完成，架構測試與 BDD 全套件把關。

---

## Alternatives Considered

### Alternative A: Argon2id（PRD 原文首選）

Rejected. 實測 OWASP 最低參數單次 44.3 ms —— 未計候選掃描即頂滿 P99 預算，×10 候選爆 9 倍；每驗證 19 MiB 記憶體在 100 RPS 下構成自帶的資源耗盡放大器，與 PRD 自己的 DoS 威脅模型衝突；salted 特性使唯一索引查找不可能。其 memory-hard 優勢只對低熵輸入有意義，對 128-bit 隨機 token 是純成本。

### Alternative B: PBKDF2-SHA256（PRD 原文次選）

Rejected. 實測 OWASP 最低 600k 迭代單次 85.6 ms，效能問題同 A；GPU 抗性又劣於 Argon2id —— 在「符合 PRD 措辭」以外沒有任何一項勝過其他選項。

### Alternative C: BCrypt 續用並升 work factor

Rejected. 改動最小（調一個參數），但 workFactor=12 實測 219 ms 完全不可行；降參數則回到現況的形同虛設。且 BCrypt 本就不在 PRD 允許清單，續用同樣要付「開 ADR 記偏差」的成本，卻拿不到 HMAC 的索引查找與效能。72-byte 輸入截斷特性是額外的隱性風險。

### Alternative D: 純 SHA-256（無 pepper，GitHub PAT 模式）

Rejected. 對 128-bit 熵輸入安全性已足，且少管理一個秘密。但 pepper 的邊際成本趨近零（同為單次雜湊運算），換得 DB 單獨外洩情境的縱深與熵回歸緩衝；「組態多一條 + 啟動驗證」的複雜度換這兩層是划算的。若未來 pepper 管理被證明是實際負擔，以新 ADR 降級到本選項。

---

## Implementation Rules

1. `ApiKey` 的 `GenerateKeyMaterial` 隨機體使用 16 bytes（128-bit）；金鑰格式其餘部分（prefix、checksum）不變。
2. 新增 `IApiKeyHasher`（`KeyLifecycle.Domain`，唯一方法 `string ComputeHash(string rawKey)`）；`ApiKey.Create` 以參數接收之，內部不得出現具體雜湊演算法。
3. `HmacApiKeyHasher`（Infrastructure）實作 `Base64(HMACSHA256(pepper, UTF8(rawKey)))`；pepper 取自 `ApiKeyHashing:Pepper` 組態（Base64，解碼後 ≥ 32 bytes），以 options validation 在啟動時 fail-fast。
4. BCrypt 依賴全移除（`KeyLifecycle`、Infrastructure 的 PackageReference 與 `Directory.Packages.props` 的 PackageVersion；props 內純歷史 changelog 註解不在此限）。**驗收**：

   ```bash
   git --no-pager grep -n "BCrypt" -- backend/src backend/tests
   git --no-pager grep -n 'Include="BCrypt' -- backend
   # 兩者皆預期 0 命中
   ```

5. 測試義務：hasher 確定性（同輸入同 pepper → 同輸出）、pepper 敏感性（異 pepper → 異輸出）、輸出形狀（Base64 32 bytes）以 integration test 鎖定；啟動 fail-fast 與所有新測試各過一次故意紅。
6. validation slice 落地時必須：(a) `KeyHash` 唯一索引 migration；(b) `CryptographicOperations.FixedTimeEquals` 複核（禁 `string ==`）；(c) 效能 smoke 檢驗（P99 < 50ms、≥ 100 RPS）同 commit 登記入 `docs/verification-matrix.md`。
7. 金鑰隨機體熵 ≥ 128-bit 為硬性下限；豁免僅及「系統生成的機器 token」，人類選擇的低熵秘密仍適用 PRD R-STR-01 慢速 KDF 原文。
8. 任何提案修改 1–7，必須先開新 ADR。
