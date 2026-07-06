---
date: 2026-06-13
type: info
status: archived
---

# NetArchTest 查不到方法回傳型別；FunctionalTests 需要 Docker

**Context:** (1) NetArchTest 的 fluent API 只做 IL 級 dependency 檢查（BC 隔離可用），但無法斷言「方法回傳 `Result<,>`」；Repository/Handler 回傳型別規則改用 reflection 測試。(2) 全套件 `dotnet test` 本機跑會有 2 個 BDD 場景失敗，根因是 Testcontainers 需要 Docker（`DockerUnavailableException`），非迴歸——本機沒開 Docker 時無法驗證 BDD，但 GitHub Actions 的 ubuntu runner 內建 Docker 會綠。
**Rule:** 架構規則「dependency 用 NetArchTest、回傳型別用 reflection」分工；判斷「suite 是否 Green」要先排除 Docker/Testcontainers 這類環境因素，別誤判成迴歸。
**落地:** reflection 測試 `RepositoryReturnTypeTests.cs` / `HandlerResultReturnTests.cs`；CI `.github/workflows/ci.yml` 用 `ubuntu-latest`（Docker 預裝）並於註解說明。
