---
date: 2026-07-05
type: info
status: active
---

# CodeAnalysisTreatWarningsAsErrors 只升級 CA 前綴 — 其他 analyzer 診斷需顯式 severity=error

**Context:** ADR-016 故意紅驗證時發現：`Xunit.Assert` 的 banned-symbol 違規只出 `warning RS0030`、不擋 build——`CodeAnalysisTreatWarningsAsErrors` 語意上僅提升 `CA*` 前綴診斷，RS／xUnit／第三方 analyzer ID 不在覆蓋範圍。沒做故意紅就會上線一個不會咬人的 gate。
**Rule:** 引入非 CA 前綴的 analyzer 規則時，阻斷性必須以 `.editorconfig` 的 `dotnet_diagnostic.<ID>.severity = error` 顯式設定，且一律用故意紅證明真的會使 build 失敗；不得假設 TreatWarningsAsErrors 類屬性已涵蓋。
**落地:** `backend/tests/.editorconfig` RS0030 段（commit `7bb4053`）；本條 lesson。
