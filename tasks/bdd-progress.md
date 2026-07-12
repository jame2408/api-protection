# BDD 場景實作進度

> 場景定義與順序以 `tests/FunctionalTests/Features/KeyLifecycle/` 內的 `.feature` 文件為準。
> 未實作場景標記 `@ignore`；`.feature` 文件已用數字前綴排序，字母序 = Wave 順序。

---

## 如何找到下一個場景

每次開始作業，依序執行：

```bash
# 1. 找出下一個待實作場景（行號需數值排序：純字典序會把同檔的 13 行排在 5 行前）
grep -rn "@ignore" backend/tests/FunctionalTests/Features/ | sort -t: -k1,1 -k2,2n | head -1

# 2. 確認整體進度
grep -rc "@ignore" backend/tests/FunctionalTests/Features/
```

第一個輸出即為本次目標場景。移除該場景的 `@ignore` tag，進入 Red-Green 循環。

---

## 目前進度

**已通過：** 29 / 46  
**下一個：** `04_LockUnlockKey.feature` — 非 System 角色嘗試鎖定 — 拒絕

---

## Wave 概覽與實作順序

| 文件 | Wave | Feature | 場景數 | 涉及 BC | 特殊需求 |
|------|------|---------|--------|---------|---------|
| `01_CreateApiKey.feature` | 1 | 建立 API 金鑰 | 12 | TenantManagement、KeyLifecycle、AccessPolicy | 整體 stack 首次通 |
| `02_RevokeKey.feature` | 2 | 撤銷金鑰 | 7 | KeyLifecycle.RevokeKey | Rotating 狀態需 seed |
| `03_SuspendResumeKey.feature` | 3 | 暫停與恢復金鑰 | 8 | KeyLifecycle.SuspendKey、ResumeKey | AuthToken 機制（Wave 3 前須建立） |
| `04_LockUnlockKey.feature` | 4 | 鎖定與解鎖金鑰 | 6 | KeyLifecycle.LockKey、UnlockKey | System 角色 |
| `05_RotateKey.feature` | 5+6 | 輪替金鑰 + 完成寬限期 | 7 | KeyLifecycle.RotateKey、CompleteGracePeriodJob | FakeClock（Wave 5 後段） |
| `06_ExpireKey.feature` | 7 | 金鑰到期處理 | 6 | KeyLifecycle.ExpireKeyJob | FakeClock |

---

## 基礎設施解鎖點

| 時機 | 需要的基礎設施 |
|------|--------------|
| Wave 1 開始前 | EF Core Migration、Respawn 初始化 |
| Wave 3 開始前 | AuthToken 機制（Security Admin / Consumer / System 的 JWT）— **已建立**（ADR-024 Phase 2） |
| Wave 5 寬限期場景前 | FakeClock 實作（`ISystemClock`）並注入 WebApplicationFactory |
