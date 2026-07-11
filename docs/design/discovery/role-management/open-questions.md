# Open Questions — role-management discovery（Phase D）

> 未決事項清單：交下一輪 grilling，或明列為 fog。皆不阻塞本輪交棒（Guardrail 三項不依賴它們）。

| # | 未決點 | 出處 | 建議去向 |
|---|--------|------|---------|
| OQ1 | 拒絕申請後的流程：可否重申請、否決是否留痕 | events.md H1 | 下輪 grilling（進 requirements-analysis-design 的 Example Mapping 前補問即可） |
| OQ2 | grant 下建金鑰的操作權：僅團隊管理者，還是隊內成員皆可？ | Phase C 未探討（H4 只裁決了申請／核准權） | 下輪 grilling |
| OQ3 | 授權事件的通知機制（申請待核提醒、核准通知） | 未探討 | fog——有真實需求再開 |
| OQ4 | 存量遷移：既有 3 團隊已發的金鑰無 grant 引用，如何補掛？ | brownfield 現實，未探討 | ADR topic 1 落地時一併裁決 |
| OQ5 | design-doc Q6 人物誌對應（Consumer／Service Owner／Security Admin ↔ 五角色 ↔ 團隊管理者）仍未整體裁決 | design-doc §14 Q6＋本探索 R1 觀察 | ADR topic 2／3 覆蓋大半，殘餘部分屆時銷案 |
| OQ6 | api-spec §2.1 角色-前綴表與各 endpoint Authorization 欄不完全一致，何者權威 | 本探索 R1 輪 AFK 觀察（Control Plane 側，非本探索主軸） | 獨立勘誤項，交協調者列 housekeeping |
| OQ7 | 本能力的 BDD 場景產出需先解凍 Discovery（ADR-022 凍結中，解凍屬使用者裁決） | CLAUDE.md 凍結句 | 使用者裁決；解凍後走 requirements-analysis-design |
