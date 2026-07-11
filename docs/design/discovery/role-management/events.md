# Events — role-management discovery（Phase B）

> 來源：`facts.md` R1–R12。事件過去式命名；標 ★ 者為本探索新增概念的事件，未標者為既有模型已有（或可直接沿用）的事件。hotspot 裁決屬使用者，未裁決前保留。

## 事件清單

| # | 事件 | 說明 | 出處 |
|---|------|------|------|
| E1 | ★ `TeamRegistered` | 團隊在（單一）租戶內註冊為一等單位 | R10 |
| E2 | `ScopeRegistered` | 團隊為自己的服務註冊 scope（既有 Scope Registry 概念，**歸屬單位由全局 Service Owner 變為各團隊**） | R8＋design-doc §5 |
| E3 | ★ `AccessRequested` | 使用方團隊向提供方團隊申請使用其若干 API（scope 清單） | R9 |
| E4 | ★ `AccessApproved` | 提供方核准 → 持久授權關係（grant）成立 | R9, R11 |
| E5 | ★ `AccessRejected` | 提供方拒絕申請 | R9 反面（流程未探討，見 H1） |
| E6 | `KeyCreated` | 使用方在授權資格下建立金鑰（測試／正式各一把），scope 為授權內容的**快照** | R4, R6, R11 |
| E7 | ★ `AccessGrantAmended` | 授權內容變動（如新 API 上線加開）——形狀未裁決，見 H2 | R5 |
| E8 | `KeyRotationInitiated`／`KeyRotated` | 授權變動後重發金鑰（既有輪替機制天然承載「重發」語意，含重疊期） | R6 |
| E9 | ★ `AccessGrantRevoked` | 資安事件時收回授權（例外事件，非常態流程） | R12 |
| E10 | `KeyRevoked` | 既有事件；與 E9 的級聯關係未裁決，見 H3 | R12 |

## 時間軸（正常流＋兩條變動流）

```text
建置期   E1 TeamRegistered (A、B 各自) → E2 ScopeRegistered (A 註冊其 API)
授權流   E3 AccessRequested (B→A) → E4 AccessApproved ─→ 授權關係成立
                                  └→ E5 AccessRejected [H1]
用鑰流   E6 KeyCreated ×2（測試/正式，scope 快照）→ …日常呼叫（Data Plane 驗證，既有）…
變動流   新 API 上線: E2 → [H2: E7 AccessGrantAmended 或 重走 E3→E4] → E8 KeyRotated（重發）
資安流   E9 AccessGrantRevoked → [H3: E10 KeyRevoked 級聯?]
```

## Hotspots

| # | 爭議／未知 | 狀態 |
|---|-----------|------|
| H1 | 拒絕申請後的流程（可否重申請、要不要留否決紀錄） | **未決** — 列入 open questions |
| H2 | 既有授權加開 API 的流程 | **已裁決（2026-07-11）**：修改既有授權（E7 `AccessGrantAmended` 成立，變更仍需提供方核准動作），不重走申請流程 |
| H3 | 授權收回時金鑰是否級聯撤銷 | **已裁決（2026-07-11）**：級聯撤銷——收回授權即撤銷其下所有金鑰（E9→E10 因果成立；與 design-doc Q8 同向） |
| H4 | 團隊內部誰能代表團隊申請／核准 | **已裁決（2026-07-11）**：團隊管理者——每隊有指定管理者，僅管理者能申請／核准（團隊內角色分層成立） |
| H5 | 「團隊」與既有 `Consumer` 實體的關係 | **未決** — Phase C 建模主裁決點，由 AFK 草案提建議 |
| H6 | 授權關係是否區分環境 | **已裁決（2026-07-11）**：不分環境——一份授權涵蓋測試／正式，環境屬金鑰層屬性 |
