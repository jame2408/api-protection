---
date: 2026-06-13
type: decision
status: archived
---

# 架構規則依「檢驗對象在哪」選工具：型別圖 / 方法簽名 / 語法

**Context:** 第二批四條規則各有最適工具：(1) BC 隔離 = 型別依賴圖 → NetArchTest（IL 級）；(2) Repository/Handler 回傳型別、ILogger 注入、命名 = 型別/成員 metadata → reflection；(3) `new Failure(`、`cancel` 參數命名 = **method body 內的建構式呼叫 / 參數名稱**，型別圖與 reflection 都看不到 → 只能 grep 原始碼。硬把語法層級規則塞進 NetArchTest/reflection 會寫不出來或寫錯。
**Rule:** 機械化一條規則前先問「違規長在哪個層次」：型別依賴→NetArchTest；型別/成員 metadata→reflection；method body/語法→grep（`scripts/source-lint.sh`）或 Roslyn analyzer。grep 的好處是 cheap，可放進 pre-commit fast 模式即時擋。命名豁免（如 `new Failure(` 的 `FailureProvider.cs`）必須在 lint 內明文排除，不是默契。
**落地:** `LoggerBoundaryTests.cs` / `NamingConventionTests.cs`（reflection）+ `scripts/source-lint.sh`（grep，接進 `ci-checks.sh` fast+full）。Architecture.Tests 3→11 tests。
