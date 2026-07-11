# AI Agent Engineering Kit 跨專案可攜化與分階段發佈

> Lead-in：本 ADR 將目前散落於單一 repo 的多模型協作、hooks、skills、驗證與交接機制，定義為可版本化的 Agent Engineering Kit。它同時固化三層內容邊界、installer 最終介面，以及「先人工 pilot 驗證、再自動化安裝與升級」的遷移路徑，避免把 `api-protection-v2` 的領域特例一起複製到其他專案。

---

## Status

Accepted (2026-07-11)

同步項目：`tasks/todo.md` 新增「Agent Engineering Kit 跨專案可攜化」開放工作流；本 ADR 接受時不搬移現有檔案、不建立 kit repository、不實作 installer。後續每個 rollout phase 的產物與驗證依 Decision §5 分開落地。

---

## Context

### 現況

本 repo 已具備一套可重複運作的 AI agent 開發 loop，但目前的可重用內容與專案知識共存於同一棵目錄：

- `docs/orchestration.md` 定義模型分級、executor contract、停止條件、checkpoint 與 token 紀律。
- `scripts/agent/hook.py`、`scripts/ci-checks.sh`、`scripts/failure-triage.sh` 與 `docs/verification-matrix.md` 形成「寫時回饋 → commit/push gate → phase-close learning loop」。
- `.claude/skills/`、`.agents/skills/` 與 `.claude/references/` 承載任務型 workflow 與技術棧規範。
- `CLAUDE.md`、Accepted ADR、`docs/design/`、BDD feature 與 progress files 同時承載 `api-protection-v2` 的架構與領域決策。

ADR-023 已裁定「同一 repo、不同 harness」共用單一 hook 核心與 skill 內容來源；ADR-012 的 Implementation Rule 1 亦明定，移植時不得弱化 `unverified_success` 的親自覆核義務。但既有 ADR 都沒有回答：

1. 哪些檔案可跨專案共用，哪些必須由目標專案擁有。
2. 新專案如何首次導入這套機制。
3. kit 改進後如何安全回灌，且不覆寫專案客製內容。
4. 如何證明「可攜」不是把目前 repo 複製一份後就宣告成功。

### 問題嚴重度

直接複製目前的 `.claude/`、`scripts/` 與文件會產生四種 drift：

- **知識 drift**：API Key domain ADR、BDD 規則與其他專案的行為混在一起，新 agent 無法判斷哪些是通用規則。
- **版本 drift**：複製後無 kit 版本或來源資訊，上游修復無法可靠回灌。
- **ownership drift**：不清楚檔案由 kit 還是專案維護，upgrade 只能覆寫或放棄更新。
- **驗證 drift**：installer 若在邊界尚未驗證前完成，只會把錯誤分類更快速地散播到所有專案。

### 易混淆概念釐清

| 概念 | 是什麼 | 本 ADR 是否規範 |
|---|---|---|
| 跨 harness parity | 同一專案內 Claude Code／Codex 共用行為 | ❌ 已由 ADR-023 裁決，本 ADR只重用 |
| 跨專案 portability | 將通用機制安裝、驗證、升級到不同 repo | ✅ |
| kit installer | `install`／`check`／`upgrade` 的正式遷移工具 | ✅ 目標介面；本 commit 不實作 |
| 人工 pilot | installer 前，依 manifest 與 runbook 導入第二個真實專案 | ✅ 第一個 rollout gate |
| Project Overlay | 目標專案自己的架構、domain、測試與流程差異 | ✅ ownership 邊界 |
| 全域使用者設定同步 | 修改 `~/.claude`、`$CODEX_HOME` 或其他機器設定 | ❌ |

### 不決定會發生什麼

若只建立 template repo，首次導入雖快，但之後沒有安全 upgrade；若立刻寫 installer，則目前尚未被辨識的 `api-protection-v2` 特例會被當成 Core 自動散播。兩者都無法同時滿足「可導入」與「可長期維護」。

---

## Decision

### 1. Kit 採 Core／Stack Profile／Project Overlay 三層

所有候選內容在進入 kit 前必須被明確分類，不得依路徑名稱或 installer heuristic 猜測：

| 層級 | Ownership | 內容範例 | 更新方式 |
|---|---|---|---|
| **Core** | kit | orchestration contract、checkpoint／executor templates、harness-neutral hooks、failure triage、verification framework | kit release |
| **Stack Profile** | kit | .NET rules、analyzer／source lint、BDD workflow、語言或框架專用 gates | 選用的 profile release |
| **Project Overlay** | target repo | domain ADR、Context Map、API spec、BC 規則、專案測試指令、backlog／progress | 專案自行維護 |

Project Overlay 永遠不由 upgrade 覆寫。Core 與 Stack Profile 也不得引用特定 domain symbol、endpoint、feature 名稱或本 repo 專屬路徑，除非該內容已被抽象成 profile 的明文參數。

### 2. 建立獨立、可版本化的 kit repository

Kit 最終落於獨立 repository，與任何產品 repo 分離發版。其最低結構為：

```text
agent-engineering-kit/
├── manifest.yaml
├── core/
├── profiles/
│   ├── dotnet/
│   └── bdd/
├── adapters/
│   ├── claude/
│   └── codex/
├── templates/
├── fixtures/
└── adoption/
    └── manual-pilot.md
```

實際 repository 名稱、hosting provider、installer 實作語言與 package distribution channel 後置到 rollout Phase 2；本 ADR 只固定責任邊界與能力契約。

### 3. Manifest 是安裝清單與 ownership 的單一來源

Kit root manifest 至少記錄：

```yaml
kitVersion: 1.0.0
profiles:
  - core
  - dotnet
  - bdd
files:
  - source: core/docs/orchestration.md
    target: docs/orchestration.md
    owner: kit
  - target: CLAUDE.md
    owner: project
```

每個受 kit 管理的 target file 必須能追溯 source、kit version、profile 與 content checksum。安裝後的 target repo 以 lock file 保存實際版本與 baseline checksums；對應的 kit release 必須 immutable 且可取回完整 baseline content，checksum 只負責識別與驗證，不假裝能單獨完成三方比較。Manifest 與 lock file 的 schema 在 Phase 1 pilot 後定案，避免以假資料結構綁死實作。

### 4. 正式遷移介面為 `install`／`check`／`upgrade`

Installer 是最終交付物，不是永久非目標。其能力契約為：

```text
agent-kit install --profile core,dotnet,bdd
agent-kit check
agent-kit upgrade <target-version>
```

- `install`：依 manifest 安裝 kit-owned files、產生 harness adapters 與 lock file；Project Overlay 只建立空白／指針型 scaffold，不填入 domain 決策。
- `check`：驗證檔案存在、來源版本、checksum、harness wiring 與 profile gates；任何不一致 fail-loud。
- `upgrade`：以「舊版 baseline／目前 target／新版 source」做三方比較；未修改的 kit-owned file 可自動更新，已修改的檔案產生 diff 或 conflict 並停止，不得靜默覆寫。

`upgrade` 預設只產生 plan；實際套用必須是另一個明確動作。CLI 命名可在實作時調整，但三項能力與安全語意不得弱化。

### 5. Rollout 分五個可獨立驗收的 Phase

#### Phase 0：可攜性盤點

- 逐項分類現有 hooks、skills、rules、templates、gates 與文件為 Core／Profile／Overlay。
- 對每個 Core／Profile 候選執行反向搜尋，清除 domain symbol 與本 repo 專屬假設。
- 產出 inventory 與排除清單；不搬檔、不改產品行為。

**Exit gate**：每個候選項目都有 ownership、相依項與最小驗證方式；不存在「未分類但先複製」項目。

#### Phase 1：第二個真實專案人工 pilot

- Pilot target 必須是持續開發、具有自己的架構／測試／CI、且確實需要至少一個 Stack Profile 的非 toy repo；須與 `api-protection-v2` 屬不同 domain。空 repo、一次性 demo、或刻意複製本 repo 結構的 fixture 不算第二個真實專案。
- 依 manifest 草案與 `adoption/manual-pilot.md` 明列的檔案清單，由人或 executor 逐項複製／調整到一個真實目標專案。
- 人工建立 Project Overlay，記錄每個必須修改的 kit 候選內容與原因。
- 在目標專案執行 session cold start、hook smoke、fast/full gate 與 checkpoint handoff。

**Exit gate**：目標專案可獨立運作；所有為了 pilot 而修改的 kit-owned file 都已回饋成參數、profile 差異或移出 kit，沒有未記錄的 post-copy patch。若 pilot 結果顯示候選內容除 Core contract 外大多只有單一消費者，或導入仍依賴大量無法分類的手改，工作流停在此 Phase：將內容降回 Project Overlay，不啟動 installer MVP。

#### Phase 2：Installer MVP

- 實作 `install` 與 `check`，先不實作自動 upgrade。
- fixture 至少涵蓋空 repo、對應 stack repo、重複 install 與缺檔／壞 wiring 故意紅。
- 用 installer 重建 Phase 1 的目標專案導入結果，比對人工 pilot 的結構與 gate 結果。

**Exit gate**：重複 install 具冪等性；`check` 能抓到刻意刪除檔案、竄改 managed file 與 wiring 斷裂。

#### Phase 3：Upgrade 與衝突保護

- 實作版本選擇、baseline checksum、dry-run plan 與三方比較。
- 驗證「未修改自動升級」「專案已修改停止並產生 diff」「Project Overlay 永不覆寫」三條主路徑。

**Exit gate**：以至少一個舊版 fixture 完成跨版本升級；故意修改 managed file 時 upgrade 必須停下，不得以新版覆蓋。

#### Phase 4：雙專案驗收與 1.0 發版

- 在至少兩個技術或 domain 不同的真實專案執行 install、check、upgrade。
- 彙整共同 post-install patch；若仍有未被 manifest/profile/overlay 解釋的 patch，不得發 1.0。
- 產出版本政策、相容性聲明、release notes 與 rollback runbook。

**Exit gate**：兩個專案皆能從同一 release 導入並通過各自 gates，且 upgrade 不覆寫 Project Overlay，才可標示 kit 1.0。

### 6. Harness adapter 保持薄層，完整 enforcement 留在 repo gates

Core 提供 harness-neutral 行為；Claude Code、Codex 或未來 harness 只保留事件 wiring、payload normalization 與 discovery adapter。不能跨 harness 保證的 tool interception 必須明列殘餘限制，完整規則仍由 target repo 的 commit／push／CI gates 承擔，沿用 ADR-023 Decision §7。

### 7. Kit-owned 修復先回 upstream，再以 release 下游同步

下游專案發現 kit-owned bug 時，修復先進 kit repository、加入 fixture／故意紅、發布新版本，再由下游 `upgrade` 取得。只有 production incident 的緊急 hotfix 可暫時修改 managed file，但 lock/check 必須顯示 drift，且後續仍須回補 upstream；不得把下游 patch 當成永久 fork。

### 8. 明文不在本 ADR 範圍

- 本 commit 不建立 kit repository、不移動現有檔案、不實作 installer。
- 不將本 repo 的 domain ADR、API spec、feature、BDD progress 或產品效能目標列為 Core。
- 不修改使用者全域 harness 設定，不繞過 Codex hook trust 或其他 harness 安全邊界。
- 不保證所有專案採用相同 branch、release、BDD 或測試策略；這些差異屬 Profile 或 Project Overlay。
- 不建立遠端管理服務、中央 policy server 或自動修改所有下游 repo 的 bot。

### 9. 本 ADR 接受時的同步項目

- `tasks/todo.md`：新增本工作流與 Phase 0–4 狀態；所有 Phase 初始為未排程。
- `CLAUDE.md`、`docs/orchestration.md`、`docs/verification-matrix.md`：本次不改。既有 repo 行為未變；等 installer 或新 gate 實際落地時再依各文件治理規則同步。

---

## Rationale

### 為什麼先人工 pilot，installer 仍是正式目標

人工 pilot 是 boundary discovery，不是長期 distribution。它讓第二個真實專案暴露隱藏的路徑、domain 與工具假設；installer 則解決長期版本、重複導入與升級問題。先 pilot 可避免自動化錯誤分類，完成後仍必須進入 installer，否則 kit 只是一份會漂移的範本。

### 為什麼不違反制度機制事故驅動的凍結裁決

本 ADR 不是替目前 repo 追加新的內部治理防線，而是回應使用者明確提出的跨專案遷移需求；需求本身已超出單 repo 現制能處理的邊界。即使如此，installer 仍不能只憑預想直接開工：Phase 1 以第二個真實專案提供實證，且設有「共用性不足即停止」條件。這延續制度凍結的核心精神——沒有觀察到的跨專案摩擦，不自動化成新機制。

### 為什麼是三層而不是「通用／專案」兩層

.NET analyzer、Reqnroll BDD 與測試 gate 可跨多個 .NET 專案重用，卻不適用於其他技術棧。把它們塞進 Core 會讓 Core 綁死 .NET；全部留在 Project Overlay 又會讓每個 .NET 專案重複維護。Stack Profile 是兩者之間必要的變異點。

### 為什麼需要獨立 repository 與 lock file

獨立 repository 讓 kit 有自己的 release、fixtures 與 change history，不受產品 backlog 牽動。lock file 則回答每個下游「目前裝了什麼、來源是哪版、哪些 managed files 已 drift」，是安全 upgrade 的必要 baseline。

### 為什麼 upgrade 必須三方比較

單看目前 target 與新版 source，無法判斷差異來自專案客製還是上游更新。保存舊版 baseline 才能區分「未修改，可自動更新」與「雙方都改過，必須人工裁決」，避免靜默資料損失。

### 為什麼不把 portability 寫成 plugin-only

Skills／plugins 能攜帶 agent instructions，但本機制還包含 repo 文件、git hooks、CI gates、checkpoint、lessons 與 project-owned ADR。只打包 skill 會讓最重要的 enforcement 與交接狀態留在人工複製範圍，無法完成端到端遷移。

---

## Consequences

### Positive

- 通用 workflow、技術棧規則與 domain 知識各有明確 ownership。
- 新專案有可重現的首次導入與 fail-loud 健檢，不再依賴「記得複製哪些檔案」。
- Kit 修復可透過版本與 upgrade 回灌，不需要逐 repo 手工同步。
- Pilot 與 fixtures 讓「可攜性」有跨專案證據，而不是單 repo 自我驗證。
- Project Overlay 不被 installer 接管，保留每個專案的架構與規格自主權。

### Negative / Trade-offs

- Phase 0–1 在 installer 出現前仍需人工導入一次，短期速度較慢。
  - Mitigation: 人工步驟本身成為 `manual-pilot.md` 與 manifest 的輸入；只付一次 discovery 成本，不把它當永久流程。
- Kit repository、版本政策、fixtures 與 installer 形成新的維護產品。
  - Mitigation: 只在兩個真實專案證明共用價值後發 1.0；無第二個消費者的內容留在 Project Overlay，不升格進 kit。
- 三方 upgrade 比單純覆寫複雜，可能需要人工解 conflict。
  - Mitigation: upgrade 預設 dry-run，未修改檔案才自動套用；衝突明確停止比靜默覆寫安全。
- Profile 組合可能產生相依與順序問題。
  - Mitigation: manifest 顯式宣告 profile dependencies，`check` 對缺少或不相容組合 fail-loud；不靠安裝順序猜測。
- Downstream emergency patch 會短暫造成 managed-file drift。
  - Mitigation: lock/check 顯示 drift，並要求 upstream fix＋release 收口；不得將 drift 靜默吸收成永久 fork。

---

## Alternatives Considered

### Alternative A：直接複製目前 `.claude/`、`.agents/`、`scripts/` 與文件

Rejected. 首次導入看似最快，但沒有 ownership、版本或 upgrade baseline，且會把 domain 規則一起帶走；複製後每個專案立即成為獨立 fork。

### Alternative B：只提供 template repository

Rejected. Template 適合建立新 repo，不能安全更新既有 repo，也無法區分 template-owned 與 project-owned files；它可作 Phase 1 的輔助產物，但不是正式 distribution。

### Alternative C：以 Git submodule 或 subtree 嵌入整套機制

Rejected. Submodule 對固定子目錄有效，但本機制的檔案需落在 repo root、`.claude/`、`.agents/`、`scripts/` 與 `docs/` 多個 discovery 路徑；symlink／wrapper 會增加跨平台與 harness 摩擦。Subtree 又會回到 merge ownership 不明的問題。

### Alternative D：只發佈 skills／plugin

Rejected. 無法完整承載 git hooks、CI gate、checkpoint、verification matrix 與 Project Overlay scaffold；只能解決 instructions discovery，不能完成開發 loop 遷移。

### Alternative E：先完成 installer，再找第二個專案驗證

Rejected. Installer 的 manifest、參數與 ownership schema 會在只有單一樣本時被過早固定；第二專案揭露的差異將迫使 installer 大改，或被錯誤地塞進 Project Overlay。

### Alternative F：永久維持人工 runbook，不實作 installer

Rejected. 無法可靠追蹤版本、驗證 drift 或回灌修復；runbook 是 Phase 1 的探索工具，不是長期 distribution mechanism。

### Alternative G：所有內容只分 Core 與 Project Overlay

Rejected. 會迫使 Core 綁定 .NET／BDD，或讓所有技術棧規則在各專案重複；兩種結果都與可攜化目標矛盾。

---

## Implementation Rules

1. 任何內容進入 kit 前，必須明列為 Core、某一 Stack Profile 或 Project Overlay；未分類內容不得由 installer 管理。
2. Core 與 Stack Profile 不得含 `api-protection-v2` 的 domain symbol、endpoint、feature、BC 名稱或產品規格；Phase 0 必須以反向搜尋提供零命中或逐項豁免證據。
3. Phase 1 必須使用第二個真實專案，不得只用空 fixture 取代；pilot 的所有 post-copy patch 必須逐項歸因並回饋分類。
4. Installer 最終必須提供 install、check、upgrade 三項能力；install/check 未通過 Phase 2 exit gate 前不得宣稱可重複導入，upgrade 未通過 Phase 3 exit gate 前不得宣稱可維護升級。
5. Upgrade 必須能從 immutable kit release 取回舊版 baseline content 並做三方比較；lock checksum 只用於識別／驗證。已修改的 managed file 與任何 Project Overlay file 都不得被靜默覆寫。
6. Kit-owned bug 必須回 upstream、附綠與故意紅 fixture、發版後再由下游 upgrade 收口；緊急 downstream patch 必須保持可被 check 偵測的 drift。
7. Harness adapter 只能承載 wiring、payload normalization 與 discovery；完整 enforcement boundary 必須保留在 target repo 的 commit／push／CI gates。
8. ADR-012 的 `unverified_success` 義務與 ADR-023 的跨 harness 殘餘限制在移植後仍適用，不得因 installer 自動化而弱化。
9. Phase 1 target 必須符合 Decision §5 的非 toy／不同 domain／自有 CI 判準；pilot 若命中「共用性不足」停止條件，不得啟動 Phase 2。Kit 1.0 前必須有兩個真實專案完成 install、check、upgrade，且不存在未由 manifest／profile／overlay 解釋的共同 post-install patch。
10. 本 ADR 接受 commit 的驗收：

    ```bash
    scripts/adr-lint.sh
    # 預期 25 file(s) passed

    grep -n "Agent Engineering Kit 跨專案可攜化" tasks/todo.md
    # 預期至少 1 命中
    ```

11. 任何提案修改 1–10，必須先開新 ADR。
