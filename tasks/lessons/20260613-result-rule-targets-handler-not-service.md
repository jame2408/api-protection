---
date: 2026-06-13
type: decision
status: archived
---

# 「Service 必回 Result」架構規則應鎖定 `*Handler`，不是 `*Service`

**Context:** 建 NetArchTest 防線時，原打算對所有 `*Service` 類別斷言「必回 `Result<T,Failure>`」。實查發現三個具體 `*Service` 全是跨 BC contract 實作或 infra：`AccessPolicyService` 實作 `SharedKernel.Contracts.IAccessPolicyService` 回 `Task<Guid>`、`ConsumerValidatorService` 實作 `IConsumerValidator` 回 `Task<ConsumerValidationResult>`、`ScopeRegistryService` 在 Infrastructure。naive 規則會誤紅這些合法程式碼——那是一條寫錯的檢驗，比沒有更糟。BC 內部真正的 use-case 單位是 `*Handler`（`CreateApiKeyHandler.HandleAsync` 回 `Result`）。
**Rule:** 「Service/Handler 必回 Result」的機械化檢驗鎖定 concrete `*Handler` 類別的 public async 方法；跨 BC contract（`SharedKernel/Contracts`，由 `*Service` 實作）依 exceptions.rule.md「跨 BC Contract 例外」豁免，自然繞開不需特例。寫架構檢驗前先實查目標型別的真實形狀，別照規則字面套。
**落地:** `backend/tests/Architecture.Tests/HandlerResultReturnTests.cs`（已綠＋故意紅驗證，點名 `CreateApiKeyHandler.HandleAsync`）。
