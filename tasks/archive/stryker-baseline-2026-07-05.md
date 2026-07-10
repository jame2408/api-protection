# Stryker.NET 變異測試基線 — 2026-07-05（QA #2）

> Wave 1 收齊＋test-only 重構 pass 後量測。工具：dotnet-stryker 4.16.0（local manifest），
> 跑法：`bash scripts/mutation-test.sh <BC>`（on-demand，非 gate，使用者 2026-07-05 核准）。
> 報告原檔在 `backend/tests/FunctionalTests/StrykerOutput/`（gitignored，本檔為 survived 清單的持久化歸檔）。

## 總分

| BC | Mutation score | 總 mutants | Killed | Survived | NoCoverage | Ignored | CompileError | 耗時 |
|---|---|---|---|---|---|---|---|---|
| KeyLifecycle | **54.39%** | 69 | 31 | 19 | 7 | 10 | 2 | 1m42s |
| TenantManagement | **72.73%** | 13 | 8 | 2 | 1 | 2 | 0 | 47s |

對照：`CreateApiKeyHandler` line coverage 96.4%（ADR-014 gate）— coverage 與斷言品質的落差即本清單。

## A. 真缺口 — 高價值（建議優先處置，處置方式待使用者裁決）

1. **`KeyLifecycle/CreateApiKey/CreateApiKeyHandler.cs:66`** — Statement removal：整行刪掉 `await keyRepository.SaveAsync(apiKey, cancel);` 全套件照綠。成功場景的 Then 斷言了 response body 與 AccessPolicy 落庫，**唯獨沒斷言 ApiKey 本身落庫**。
2. **`KeyLifecycle/Domain/ApiKey.cs:62`** — Statement removal：刪掉 `key.AddDomainEvent(new KeyCreated(...))` 照綠。「系統產生 KeyCreated 事件」Then step 實際只驗 response body，事件是否真的發出無斷言。
3. **`TenantManagement/Application/ConsumerValidatorService.cs:24`** — Logical `&&`→`||`：場景「Consumer 不屬於該租戶」的 seed 是「consumer 不存在」，**「consumer 存在但屬另一租戶」的跨租戶隔離從未被測**；`||` 變異讓跨租戶 consumer 通過驗證也無場景抓到。場景名與 seed 語意不符。
4. **`KeyLifecycle/CreateApiKey/CreateApiKeyHandler.cs:47`** — Equality `>`→`>=`：到期上限精確邊界（恰好 +365 天應成功）無場景，現有場景只測遠超（5 年）與遠低（30 天）。

## B. 真缺口 — 低價值或需基礎設施

5. `CreateApiKeyHandler.cs:45` — `<=`→`<`：`ExpiresAt` 恰等於 `now` 的邊界；`DateTimeOffset.UtcNow` 直取，無時鐘抽象即不可決定性測試。
6. `ApiKey.cs:81`（×4 mutants）＋ `:84-85`（×3）：keyPrefix 生成（tenantAbbr 4 字截斷、envAbbr 映射）只被 `StartsWith("apk_")` 弱斷言覆蓋；tenantId < 4 字的分支無測試。
7. `CreateApiKeyEndpoint.cs:46` — 201 Created 的 Location URI 字串無斷言。

## C. 等價／無害變異（裁決不處置）

8. `ApiKey.cs:12-19`（×6）、`TenantManagement/Domain/Consumer.cs:7`：屬性初始器 `= string.Empty` 必被建構流程覆寫，不可觀測。
9. `KeyLifecycle/Http/ApiProblem.cs:46`：`"traceId"` 鍵名變異存活 — ASP.NET Core ProblemDetails 管線本身會補 traceId，顯式行屬冗餘防護。

## D. NoCoverage — 預期盲區（不處置，留待對應 wave）

10. `ApiKey.cs:85-86`：sandbox envAbbr 分支 — sandbox 場景屬後續 wave。
11. `ApiProblem.cs:63`：unknown-code 500 fallback — 防禦分支，所有現行 code 皆有映射。
12. `KeyLifecycleModule.cs:12,18`、`CreateApiKeyEndpoint.cs:19-20`、`TenantManagementModule.cs:11`：DI／endpoint 註冊等啟動期程式碼。

## 處置結果（同日執行，使用者裁決 A–D 全類）

重跑對照（commits `5efed80` Batch 1 test-only、`f2c1079` TimeProvider production-only、`d4542cb` 凍結時鐘＋邊界場景 test-only）：

| BC | 基線 → 處置後 | Killed | Survived | NoCoverage |
|---|---|---|---|---|
| KeyLifecycle | 54.39% → **73.68%** | 31→42 | 19→12 | 7→3 |
| TenantManagement | 72.73% → **81.82%** | 8→9 | 2→1 | 1→1 |

- **A1 落庫**（Handler:66）、**A3 跨租戶**（Validator:24）、**A4+B5 到期雙邊界**（Handler:45/47）、**B7 Location**（Endpoint:46）— 全數轉 killed。A4/B5 依賴 `FrozenTimeProvider` 凍結等值（解凍故意紅證實：解凍不翻轉判定、僅使等式邊界失效）。
- **A2（ApiKey.cs:62 AddDomainEvent）維持 survived — 裁決降級**：調查證實 domain event 無任何發佈管道（EF `Ignore(DomainEvents)`、無 interceptor/outbox/publisher，RabbitMQ 容器為未接線鷹架，「事件已發佈」Then step 實際只驗 HTTP body）。為殺 mutant 而建事件架構屬本末倒置；Wave 2 RevokeKey「觸發主動快取失效」必然引入事件基礎設施（需 ADR），屆時自然閉環。
- **B6 殘留**（ApiKey:81×2、84-85×2）：prefix 斷言已殺可殺者；殘留需 tenantId < 4 字的場景（8 字 tenant 下為等價變異），留待有短 tenant 需求的 wave。
- **C 類**（初始器 ×7、ApiProblem traceId）與 **D 類**（sandbox 分支、500 fallback、DI 註冊）：明文不處置，維持原裁決。KeyLifecycleModule/Endpoint 註冊行意外由新斷言連帶轉 killed。

## 附註

- CompileError ×2（`CreateApiKeyFailureCodes.cs`）：const 字串無法參與 Stryker 的 mutant switching，屬工具限制、無資訊量；錯誤碼字串的防線實際在 Then 映射表的 wire-format 逐字斷言（ADR-006）。
- 成本結論：兩 BC 全量合計 < 3 分鐘 — 遠低於預估，未來可考慮納入 CI 週期 job（另行裁決，維持非 gate）。

## 2026-07-10 A2 正式閉環

`ThenKeyCreatedEventIsPublished()` 已從 response-body 代理改為 ADR-020 outbox 真實斷言：先以本次 response 的 `keyId` 精確過濾 `EventType == "KeyCreated"` 與 `AggregateId == keyId.ToString()`，再逐一驗證 payload 的 `keyId`、`consumerId`、`tenantId`、`environment`、`scopes`（完整內容）、`keyPrefix`、`expiresAt`、`policyId` 八個欄位。

本輪指令：`bash scripts/mutation-test.sh KeyLifecycle --reporter json`。Stryker 4.16.0 終態原始 metrics：

| BC | Mutation score | 總 mutants | Killed | Survived | NoCoverage | Ignored | CompileError | 耗時 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| KeyLifecycle | **70.45%** | 112 | 62 | 12 | 14 | 20 | 4 | 1m52.3546182s |

最新 JSON report：`backend/tests/FunctionalTests/StrykerOutput/2026-07-10.20-54-46/reports/mutation-report.json`（gitignored，未入版控）。`ApiKey.cs` 的 `key.AddDomainEvent(new KeyCreated(...))` statement-removal mutant 唯一識別結果：

```json
{
  "id": "32",
  "location": {
    "start": { "line": 66, "column": 9 },
    "end": { "line": 76, "column": 34 }
  },
  "mutatorName": "Statement mutation",
  "replacement": ";",
  "status": "Killed",
  "killedBy": ["e3024e89-05c1-29e7-4caa-a4bf20491cf9"]
}
```

A2 因此由先前的 Survived 正式轉為 Killed。此 mutation test 仍是 on-demand 驗證，不是 CI gate。Production 無改動；Test 無額外重構，理由為單一斷言缺口閉環。
