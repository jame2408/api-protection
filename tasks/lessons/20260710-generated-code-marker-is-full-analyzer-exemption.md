---
date: 2026-07-10
type: info
status: active
---

# editorconfig `generated_code = true` 是整檔 analyzer 豁免，不只是排版豁免

**Context:** `8922c47`（ADR-016 之前）為保留兩個手寫檔的欄位對齊排版、繞過 hardcoded 的 `dotnet format whitespace`，將 `CreateApiKeyEndpoint.cs`（production）與 `CreateApiKeySteps.cs` 標為 `generated_code = true`。ADR-016（`7bb4053`）把 CA 規則升為 build error 時，此標記的語意副作用放大：Roslyn 對 generated code **跳過全部 analyzer 規則**（CA2016 CancellationToken、CA1310 文化敏感比較等）且改變 nullable 語境（CS8669），baseline sweep 因此漏掃這兩檔——`CreateApiKeySteps.cs` 至今留有一處裸 `StartsWith`（鏡像到新檔立即被 build 擋下，兩檔行為不一致才暴露盲區）。
**Rule:** `generated_code = true` 只能用於真正的工具產物（如 EF Migrations）；手寫檔案要豁免排版檢查不得借用此標記——排版偏好與 analyzer 覆蓋衝突時，捨排版。升級任何 analyzer gate 前，先 grep `.editorconfig` 盤點既有 `generated_code` 標記是否涵蓋手寫檔。
**落地:** 待裁決（`tasks/checkpoint.md` 待裁決欄 2026-07-10 項）：移除兩個手寫檔標記＋放棄欄位對齊＋補修暴露出的 CA 違規。
