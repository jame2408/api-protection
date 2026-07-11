# Model — role-management discovery（Phase C）

> AFK 起草的建議案，**全部標記為「建議」，未經使用者裁決不得視為定案**。來源：`facts.md` R1–R13＋`events.md` H2/H3/H4/H6 裁決。

## Aggregate 候選（事件聚類推導）

### 1.（★新）`Team` — 團隊

- **聚的事件**：E1 `TeamRegistered`；隊內管理者指派（H4 裁決衍生，事件待細部設計）。
- **持有**：團隊識別、團隊管理者名單（H4：僅管理者能代表團隊申請／核准）、所屬租戶（R10：全公司單租戶）。
- **不變量候選**：至少一名管理者；管理者才能發起／核准授權操作。

### 2.（★新）`AccessGrant` — 授權關係

- **聚的事件**：E3 `AccessRequested` → E4 `AccessApproved`／E5 `AccessRejected` → E7 `AccessGrantAmended`（H2：修改制，變更仍需提供方核准）→ E9 `AccessGrantRevoked`（R12：僅資安例外）。
- **持有**：提供方 teamId、使用方 teamId、scope 清單（H6：不分環境）、狀態（Requested／Active／Revoked）。
- **不變量候選**：scope 必須是提供方已註冊的 scope（E2）；狀態轉移僅限上列事件序；核准／修改／收回的操作者必須是提供方團隊管理者（H4）。
- **生命週期獨立於金鑰**（R11）：金鑰在 Active 授權下建立；授權收回 → 級聯撤銷其下金鑰（H3）。

### 3.（既有，微擴）`ApiKey` — 金鑰（KeyLifecycle BC）

- 建立時**引用 grantId** 並**快照** scope（R6／R11：快照語意，授權後續變動不回寫）；帶環境屬性（R4，H6：環境在金鑰層）。
- 建立 guard 新增：scope ⊆ 該 grant 當下的 scope 集合；grant 須為 Active。
- 級聯撤銷（H3）復用既有先例：`RevokeLeakedKeysHandler` 的批次撤銷組合模式＋outbox 事件驅動。

### 4.（既有，歸屬調整）Scope Registry

- E2 `ScopeRegistered` 概念不變；**歸屬單位由「全局 Service Owner」變為「各團隊」**（R8）——每個 scope 有 owning team。

## H5 建議案：「團隊」與既有 `Consumer` 的關係

**建議：一實體兩面向，`Team` 為新 aggregate，既有 `Consumer` 作為 Team 的使用方投影（1:1 連結）。**

- 理由：既有 KeyLifecycle 的金鑰掛在 `consumerId` 下（R7 已確認團隊的使用方面向＝Consumer），保留 Consumer 可讓既有金鑰模型與 46 場景零擾動；Team 承載新增的提供方面向（服務歸屬、管理者、核准權）與跨隊關係。
- 替代案（拆兩實體：Provider 與 Consumer 各自獨立）被建議排除：3 團隊規模下引入雙實體同步成本，且 R8 明示同一團隊天然兼具兩面向。

## BC 邊界與分類建議

| BC | 內容 | 分類建議 | 理由 |
|---|---|---|---|
| （★新）**Team Access** | `Team`＋`AccessGrant`＋申請→核准工作流 | **Supporting** | 是金鑰發放的前置賦能，非本產品的差異化核心（核心仍是金鑰保護與驗證）；但為本需求的主體 |
| KeyLifecycle（既有） | `ApiKey` 微擴（grantId 引用＋快照 guard＋級聯撤銷訂閱） | Core（不變） | 邊界不動，只加一道建立 guard 與一個事件訂閱 |
| Tenant Management（既有） | 不變（單租戶裁決下，租戶管理面不受影響） | Generic（不變，依 design-doc §3.1） | — |
| AccessPolicy（既有） | 不變（Data Plane 驗證時的 scope 判斷來源仍是金鑰快照） | 不變 | — |

## Context Map 關係建議

- KeyLifecycle → Team Access：**Customer/Supplier**（金鑰建立時經 SharedKernel 介面驗證 grant，先例：`IAccessPolicyService`）。
- Team Access → KeyLifecycle：**事件驅動**（`AccessGrantRevoked` 經 outbox → KeyLifecycle 級聯撤銷，先例：Secret Scanner 批次撤銷）。
- Team Access → Tenant：**Conformist**（沿用租戶／Consumer 身分模型，先例：既有 Key Lifecycle → Tenant）。

## Control Plane 接縫（回應探索起點的「角色」提問）

「團隊管理者」（H4）**不是**新的全局 JWT role——它是「某人對某團隊」的關係，不適合塞進 ADR-024 的封閉角色集（PlatformAdmin／SecurityAdmin／TenantAdmin／Consumer／System）。授權判斷=「操作者是否為該 team 的管理者」，屬 Team aggregate 的資料，不屬 token claim。此張力列入 adr-topics。
