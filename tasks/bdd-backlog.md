# BDD Scenario Backlog

新場景從 Discovery（`requirements-analysis-design` skill）產出後，先進此處等待排程。（Discovery 新場景產出目前凍結 — 見 CLAUDE.md「BDD Scenario Development Cycle」段；凍結解除前 `requirements-analysis-design` skill 的 Step 5 不得產出新場景檔。**範圍化解除逐案走 `docs/adr/adr-030-discovery-freeze-scoped-lift.md`**：判準為「API 契約已在 api-spec 定案但 `.feature` 從未產出」，目前已解除 Data Plane（api-spec §4），場景須可指回具體規格條目、不得發明規格外行為。既有場景修訂／缺陷再現／行為移除不受此凍結限制，走 `docs/adr/adr-022-bdd-requirement-type-routing.md` 分流）
只有用戶決定順序後，才將項目移入 `tasks/bdd-progress.md`。
Claude **不得自主將項目從 backlog 升格到 progress**。

## 看板流程

```
Discovery → bdd-backlog.md → (用戶決定順序) → bdd-progress.md → ✅ Done
```

## 升格步驟（由用戶執行）

1. 決定新場景在 `bdd-progress.md` 中的插入位置
2. 將項目從 backlog 移至 `bdd-progress.md` 對應位置
3. 在對應的 `.feature` 檔案加上 `@ignore` tag
4. 若插入位置在現有場景中間，確認 `.feature` 檔案命名前綴是否需要調整

## 格式

```
- [ ] **Scenario 名稱** (`FeatureFile.feature`)
      來源：[discovery session / 需求變更 / 其他]
      說明：簡要描述場景意圖
```

---

## 待排程項目

> 以下 9 條由 `docs/adr/adr-030-discovery-freeze-scoped-lift.md` 授權產出（Data Plane 範圍化解除），逐條可指回 `docs/design/api-spec.md` §4.1 的具體條目；已帶 `@ignore` 落入 `backend/tests/FunctionalTests/Features/DataPlane/07_ValidateKey.feature`，**等待使用者決定晉升順序**。實作前置：`docs/adr/adr-029-validation-funnel-execution-side.md`（漏斗在系統側）；第一刀直查 KL 主表、AP 側欄位（ipAllowlist／rateLimitConfig）留第二刀，故「來源 IP 不在白名單」一條建議排在第二刀。

- [ ] **成功驗證 Active 金鑰** (`07_ValidateKey.feature`)
      來源：ADR-030 spec-derived — api-spec §4.1 成功回應欄位表
      說明：通過全部五層，回應帶 keyId／tenantId／consumerId／environment／scopes／rateLimitConfig
- [ ] **Rotating 金鑰在寬限期內仍可驗證** (`07_ValidateKey.feature`)
      來源：ADR-030 spec-derived — api-spec §4.1 錯誤碼表 KEY_INACTIVE 列（「狀態非 Active 或 **Rotating**」的反面）
      說明：鎖住寬限期內舊金鑰仍可用，這是 C2／C9 輪替設計的核心承諾
- [ ] **金鑰格式不合法 — 拒絕驗證** (`07_ValidateKey.feature`)
      來源：ADR-030 spec-derived — api-spec §4.1 錯誤碼表 KEY_FORMAT_INVALID 列（Layer 1）
      說明：前綴／長度／checksum 不合法即擋在最便宜的一層
- [ ] **金鑰已暫停 — 拒絕驗證** (`07_ValidateKey.feature`)
      來源：ADR-030 spec-derived — api-spec §4.1 錯誤碼表 KEY_INACTIVE 列（Layer 2）
      說明：Suspended 是唯一不被其他錯誤碼涵蓋的非 Active／Rotating 狀態，用它固定 KEY_INACTIVE 語意
- [ ] **金鑰已過期 — 拒絕驗證** (`07_ValidateKey.feature`)
      來源：ADR-030 spec-derived — api-spec §4.1 錯誤碼表 KEY_EXPIRED 列（Layer 2）
      說明：與 KEY_INACTIVE 區分，讓 Gateway 能回報更精確的原因
- [ ] **金鑰已撤銷 — 拒絕驗證** (`07_ValidateKey.feature`)
      來源：ADR-030 spec-derived — api-spec §4.1 錯誤碼表 KEY_REVOKED 列（Layer 2）
      說明：撤銷語意不得被泛化成 KEY_INACTIVE，安全脈絡須保留
- [ ] **來源 IP 不在白名單 — 拒絕驗證** (`07_ValidateKey.feature`)
      來源：ADR-030 spec-derived — api-spec §4.1 錯誤碼表 IP_NOT_ALLOWED 列（Layer 3）
      說明：**需 AP 側 ipAllowlist**，建議排在第二刀（第一刀只投影／查 KL 側欄位）
- [ ] **金鑰雜湊不匹配 — 拒絕驗證且不區分不存在與錯誤** (`07_ValidateKey.feature`)
      來源：ADR-030 spec-derived — api-spec §4.1 錯誤碼表 KEY_NOT_FOUND 列（Layer 4）
      說明：規格明文「不區分『不存在』與『錯誤』以防列舉」，此條同時鎖住 `FixedTimeEquals` 比對路徑（ADR-017 Rule 6(b)）
- [ ] **Scope 不涵蓋請求的 Endpoint — 拒絕驗證** (`07_ValidateKey.feature`)
      來源：ADR-030 spec-derived — api-spec §4.1 錯誤碼表 SCOPE_INSUFFICIENT 列（Layer 5）
      說明：403 而非 401——金鑰有效但權限不足，對應 §4.1「401 代表『你是誰』失敗、403 代表『你不能』」
