# ADR Topics — role-management discovery（Phase D）

> 只列題目＋張力，不代寫 ADR 本文（ADR 起草是協調者職責，走 `docs/adr/_template.md`）。順序＝建議的裁決順序。

## 1. Team Access BC 的落地形態與 SharedKernel 契約

**張力**：金鑰建立 guard 需要跨 BC 查詢 grant（Active？scope ⊆ grant？），但架構鐵律禁止 BC 直接互引（`BoundedContextIsolationTests`）。介面形狀（同步查詢如 `IAccessPolicyService` 先例 vs 投影複本）、失敗語意（grant 不存在／已收回時的 Failure code）、與既有 `AccessPolicy` BC 的職責切線（scope 快照存哪邊）都需裁決。

## 2. 團隊管理者的授權模型 vs ADR-024 封閉角色集

**張力**：「團隊管理者」是人對團隊的**關係**（存於 `Team` aggregate），不是 JWT 全局角色；但現行授權判斷全部走 endpoint role policy（token claim）。「操作者是否為該隊管理者」的判斷落點（endpoint policy 做不到 → handler guard？新 authorization requirement？）、與 ADR-024「403 由 policy、業務拒絕由 guard」分工原則的對齊方式需裁決。此題直接影響 Control Plane 的授權骨架。

## 3. Scope Registry 歸屬遷移（全局 Service Owner → 各團隊）

**張力**：design-doc §5 既有立場是 Service Owner 維護全局 Registry、孤兒 scope 發警告不自動改；R8 裁決後 scope 有 owning team。遷移語意（既有 scope 誰接手）、design-doc Q6 的人物誌對應（Service Owner ↔ 團隊管理者 ↔ TenantAdmin）、以及「移除 scope」與「收回授權」（H3 級聯）兩種語意的邊界需一併裁決。

## 4. AccessGrantRevoked 級聯撤銷的一致性語意

**張力**：級聯走 outbox 事件（最終一致，先例 Secret Scanner），但收回授權只因資安（R12）——資安場景對「收回生效延遲」的容忍度、與撤銷既有的主動快取失效 SLO（design-doc Q7 尚未定 SLO）如何銜接需裁決；亦呼應 design-doc Q8（Consumer 停權級聯）既有開放問題，可考慮同 ADR 併決。
