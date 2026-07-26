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

> **2026-07-26 使用者晉升第一批**：Data Plane 9 條中的 8 條已晉升為 **Wave 8**，順序即 `07_ValidateKey.feature` 檔內順序（首條為「成功驗證 Active 金鑰」），詳見 `tasks/bdd-progress.md`。剩餘 1 條留 Wave 9，因其需要 AccessPolicy 側 `ipAllowlist`，而 2026-07-26 題 3 裁決第一刀只查 KeyLifecycle 側欄位。

- [ ] **來源 IP 不在白名單 — 拒絕驗證** (`07_ValidateKey.feature`，置於檔尾)
      來源：ADR-030 spec-derived — api-spec §4.1 錯誤碼表 IP_NOT_ALLOWED 列（Layer 3）
      說明：**需 AP 側 ipAllowlist**；Wave 8 期間漏斗第 3 層走「無白名單即放行」的既有語意，本條隨 AP 側欄位可讀時晉升 Wave 9
