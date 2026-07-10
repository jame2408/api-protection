---
date: 2026-07-10
type: info
status: active
---
# JwtBearer 的 MapInboundClaims 預設 true — 讀原始 claim 名前必須顯式關閉

**Context:** ADR-024 Phase 2 落地 JwtBearer 後，orchestrator review 發現 `Actor.FromClaims` 的 `FindFirst("sub")` 與 `RoleClaimType="role"` 是 latent bug：`JwtBearerOptions.MapInboundClaims` 預設 true（Microsoft Learn aspnetcore-10.0 文件核實），驗證時會把 `sub`/`role` 改名成 XML-schema URI，讀原始名的程式碼靜默落空。本波測試不可能抓到——`FromClaims` 尚無任何消費者。修正見 `3ef9d23`。
**Rule:** 任何讀原始 JWT claim 名（`sub`/`role`/`name`）的程式碼，其認證管線必須已設 `options.MapInboundClaims = false`；且新增「本波無消費者」的基礎設施 API 時，須在派工 spec 或 checkpoint 明記首個消費者的驗證點，不得默認「編譯過＝可用」。
**落地:** 本條 lesson
