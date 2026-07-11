# Glossary — role-management discovery（Phase D）

> Ubiquitous language 詞彙表。左欄為對話與文件用語，中欄為模型名，語意以本表為準。

| 用語 | 模型名 | 語意 |
|---|---|---|
| 團隊 | `Team` | 租戶內的一等單位；同時具「提供方」（擁有服務與 scope、核准授權）與「使用方」（持別隊金鑰）兩面向（R8） |
| 團隊管理者 | Team Admin（`Team` 內的名單） | 唯一能代表團隊送出申請／核准別隊申請的成員（H4）；**不是** JWT 全局角色，是「人對團隊」的關係 |
| 授權關係／資格 | `AccessGrant` | 提供方核准後成立的持久關係：誰（使用方 team）可以用哪些 scope（H6：不分環境）；生命週期獨立於金鑰（R11） |
| 申請 | `AccessRequested` 事件 | 使用方團隊管理者向提供方申請使用其若干 API（R9） |
| 核准／拒絕 | `AccessApproved`／`AccessRejected` 事件 | 提供方團隊管理者對申請的裁決（R9） |
| 加開／修改授權 | `AccessGrantAmended` 事件 | 在既有授權上增補 scope（H2：修改制，仍需提供方核准動作） |
| 收回 | `AccessGrantRevoked` 事件 | 資安例外事件（R12）；觸發級聯撤銷（H3） |
| scope 快照 | snapshot | 金鑰建立當下複製 grant 的 scope 集合；授權後續變動不回寫既有金鑰（R6） |
| 重發 | 既有輪替（`KeyRotationInitiated`） | 授權變動後金鑰更新的手段：在同一 grant 下重建金鑰（R6＋R11），復用既有輪替與重疊期機制 |
| 級聯撤銷 | cascade revoke | 收回授權即撤銷其下所有金鑰（H3）；復用批次撤銷先例 |
| 環境 | environment（`ApiKey` 屬性） | 測試／正式；屬金鑰層屬性，授權不分環境（R4、H6） |
| 使用方投影 | `Consumer`（既有實體） | Team 的使用方面向在既有模型的落點，1:1 連結（H5 裁決）；既有金鑰模型零擾動 |
| （業務）API | scope（`resource:action`） | 消費方實際呼叫的 Data Plane API，以 scope 為授權單位（R2） |
| Scope Registry | 既有概念，歸屬調整 | scope 的註冊表；擁有者由全局 Service Owner 變為各團隊（R8） |
