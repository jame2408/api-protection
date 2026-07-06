# BDD Scenario Backlog

新場景從 Discovery（`requirements-analysis-design` skill）產出後，先進此處等待排程。（Discovery 新場景產出目前凍結 — 見 CLAUDE.md「BDD Scenario Development Cycle」段；凍結解除前 `requirements-analysis-design` skill 的 Step 5 不得產出新場景檔。既有場景修訂／缺陷再現／行為移除不受此凍結限制，走 `docs/adr/adr-022-bdd-requirement-type-routing.md` 分流）
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

（目前為空）
