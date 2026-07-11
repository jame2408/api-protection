# 角色型模型路由與 Codex Subagent Adapter

> Lead-in：本 ADR 修正現行協調憲章把「小／中／大型模型」直接當成可調度 executor、卻未證明各 harness 能選定模型的落差。未來 Core 只定義 explorer／executor／reviewer／orchestrator 的能力與責任；Codex adapter 再以 project-scoped custom agents 綁定模型、reasoning 與 sandbox，無法選模的 surface 必須明示 degraded mode。

---

## Status

Accepted (2026-07-11)

同步項目：`tasks/todo.md` 新增「角色型模型路由與 Codex Subagent Adapter」工作流。本 ADR 接受時不修改 `docs/orchestration.md`、`AGENTS.md` 或 `.codex/`；Decision §7 的 Phase 1–4 於未來分批實作，每批依其治理義務同步文件與驗證矩陣。

---

## Context

### 現況

`docs/orchestration.md`「模型分級路由表」直接以執行模型大小分配任務：

| 現行文字概念 | 任務 |
|---|---|
| 小型模型／子代理 | 大量讀取、掃 repo、摘要、read-back |
| 中型模型 | 實作、code review、修 bug |
| 大型模型 | 架構決策、規格裁決、跨規範衝突 |

這個意圖正確：高階 orchestrator 不該消耗 context 與成本親做明確規格下的雜事。但目前 Codex 實際狀態只完成一半：

- Codex 現行產品支援 subagent workflow、平行 agent thread、custom agents，以及 per-agent `model`、`model_reasoning_effort`、`sandbox_mode` 等設定（官方手冊：[Multi-agent operations](https://developers.openai.com/codex/codex-manual.md#multi-agent-operations)／[Custom agents](https://developers.openai.com/codex/codex-manual.md#custom-agents)）。
- 本 repo 只有 `.codex/hooks.json`，遞迴搜尋不存在 `.codex/agents/*.toml`；因此沒有 project-scoped Codex role→model binding。
- 本次 Codex session 的 callable `spawn_agent` 介面只接收 task name、message、forked turns，不提供 role／model 參數；session contract 亦明示所有 agents 能力相同。它能隔離 context 與平行工作，但不能證明成本分級。
- 實際執行 `revokedBy` 回補包時，orchestrator 已成功把實作交給 subagent，再親自 review 與重跑 full gate；這證明 executor contract 適合 Codex，也同時證明「有委派」不等於「委派給較便宜模型」。

ADR-012 已裁定 executor 宣稱一律是 `unverified_success`，協調者必須親跑確定性 gate；ADR-023 已裁定 Claude Code／Codex 共用 hook 與 skill 內容來源；ADR-025 已將 harness adapter 與 Core／Stack Profile／Project Overlay 分層。這三項均保留，本 ADR 只補足角色與模型路由之間的 adapter。

### 問題嚴重度

- **成本誤報**：文件宣稱小型模型處理掃描，但 runtime 可能仍啟動與 root 相同的高階模型。
- **能力誤配**：只按「大小」命名，未定義 explorer、executor、reviewer 的權限與輸出契約。
- **harness coupling**：Core charter 若硬編 Codex 或 Claude 的 model slug，會隨產品與 entitlement 變動而 stale。
- **安全邊界模糊**：read-heavy explorer 與 reviewer 若繼承 write 權限，錯誤委派仍可能修改工作區。
- **觸發不確定**：官方 Codex 支援由直接 prompt、`AGENTS.md` 或 skill instruction 觸發 subagent；目前薄入口只指向協調文件，沒有 Codex adapter 的可驗證 role discovery 與 dispatch smoke。

### 易混淆概念釐清

| 概念 | 保證什麼 | 本 ADR 如何處理 |
|---|---|---|
| 工作委派 | 主 agent 不親做所有細節 | 所有 subagent-capable mode 都可提供 |
| Context 隔離 | 掃描／log 不污染主 thread | 所有 subagent-capable mode 都可提供 |
| 角色路由 | 任務交給 explorer／executor／reviewer | Core contract＋adapter |
| 模型成本分級 | 不同角色實際使用不同模型／reasoning | 只有 runtime 能選模且有證據時才能宣稱 |
| 平行化 | 獨立任務同時執行 | 仍受檔案集不相交與 concurrency cap 約束 |
| Script-first | 能機械化的工作不用模型 | 優先於所有 agent 路由，規則不變 |

### 不決定會發生什麼

若保留現行文字，Claude surface 可能真的做到模型分級，Codex surface 卻只做到同模型委派，兩者都被同一份 checkpoint 稱為「依路由表完成」。成本與能力宣稱失去可稽核性，也會讓 ADR-025 將錯誤假設攜帶到其他專案。

---

## Decision

### 1. Core charter 改以角色與能力路由，不直接指定模型大小

`docs/orchestration.md` 未來改為下列邏輯順序：

| 優先序 | Role | 適用任務 | 必要能力／輸出 |
|---:|---|---|---|
| 0 | **Script** | lint、format、build、test、grep 可確定判定 | 原始輸出與 exit code；不啟動模型 |
| 1 | **Explorer** | 大量讀取、repo 掃描、事實取證、log 分類 | 精確路徑／symbol／原文；不做最終綜合 |
| 2 | **Executor** | 依明確 spec 實作、修 bug、局部重構 | diff、Red／Green、friction、checkpoint |
| 3 | **Reviewer** | security、design、dependency impact、executor semantic review | findings＋證據；不自行擴 scope 修復 |
| 4 | **Orchestrator** | 規格、架構、跨規範裁決、使用者互動、最終 gate | 決策、派工 spec、綜合與放行 |

Core 只規範「任務需要什麼能力」，不得寫死某家供應商的 model slug、intelligence label 或 entitlement。

### 2. Orchestrator 必須優先委派，但不得委派裁決與最終驗證

符合以下任一條件時，orchestrator 應委派給對應 role：

- 任務已有完整 spec，可由 executor 直接實作。
- 需要讀取／比對大量檔案，輸出可被限制為精確事實。
- review 可與主線工作獨立執行，且不改動相同檔案。
- 中間輸出會顯著污染主 thread context。

以下工作不得因有 subagent 而下放：

- 需求與 business decision。
- 架構／ADR 的最終裁決。
- 多 agent 結果的綜合判斷。
- `unverified_success` 升級為 verified 的確定性 gate。
- commit／push 放行與對使用者的最終承諾。

### 3. Codex adapter 使用 project-scoped custom agents

未來新增以下 project-owned adapter files：

```text
.codex/agents/
├── explorer.toml
├── executor.toml
└── reviewer.toml
```

每個 agent file 至少定義 `name`、`description`、`developer_instructions`，並依 runtime 能力選擇性設定：

- `model`
- `model_reasoning_effort`
- `sandbox_mode`
- `mcp_servers`
- `skills.config`

角色預設：

- Explorer：偏速度／成本效率，read-only，專注取證。
- Executor：主力 coding 能力，workspace write，受 executor contract 與允許檔案集限制。
- Reviewer：較高 reasoning，read-only，只產 findings。
- Orchestrator：主 session 設定，不建立可被任意 spawn 的 child role。

實際 model slug 不寫進本 ADR；由 Codex adapter profile 依當時官方可用模型、帳號 entitlement 與 eval 結果決定。

### 4. 每個 surface 必須宣告 routing capability mode

Adapter 啟動或驗收時將 surface 分為三種：

| Mode | 能力 | 可宣稱內容 |
|---|---|---|
| **model-routed** | subagent＋role selection＋per-role model/reasoning binding | 工作委派、角色路由、模型成本分級 |
| **role-only** | subagent 可用，但所有 child 使用同模型或 runtime 不暴露 model selection | 工作委派、context 隔離、角色 instructions；不得宣稱成本分級 |
| **single-agent** | 無 subagent surface | Script-first；其餘由 orchestrator 執行並明示限制 |

任何無法取得 runtime 模型證據的 session，預設降級為 `role-only`；不得用 agent 自述「我是某模型」當證據。

### 5. Model binding 是 adapter policy，不是 Core governance

Codex adapter 以 capability class 選模，而不是把特定版本寫進 charter：

| Capability class | Role | 選擇原則 |
|---|---|---|
| `fast-read` | Explorer | read-heavy eval 達標後選速度／成本效率較佳者 |
| `standard-code` | Executor | coding、tool use、Red→Green 與 follow-through 達標者 |
| `deep-review` | Reviewer | 複雜邏輯、安全與 edge cases 達標者 |
| `deep-orchestration` | Orchestrator | ambiguity、規格裁決與跨文件綜合達標者 |

模型或 reasoning 變更必須先跑各 capability class 的 eval；model slug 可在 adapter config 更新，不需修改 Core charter。若變更會改角色責任或驗收標準，則必須開新 ADR。

### 6. 委派觸發由薄入口與 task skills 指向唯一 charter

`AGENTS.md` 維持薄入口，不複寫路由表；未來只補一行可操作指針，要求 Codex 依 `docs/orchestration.md` 的 Role Routing 主動委派。BDD、code review、coding-style 等 skills 在需要特定 role 時同樣只引用 charter 與 agent name，不複寫模型設定。

這使官方 Codex 的「直接 prompt／applicable AGENTS.md／skill instruction」三種觸發路徑都有 repo 內明確入口，同時維持 routing rules 的單一來源。

### 7. 分五個 Phase 實作

#### Phase 0：Capability audit

- 盤點 Codex app、CLI、IDE 與目前 managed collaboration surface 是否能選 agent role、model、reasoning、sandbox。
- 對每個 surface 記錄可觀察的 thread metadata 與 concurrency／nesting限制。
- 產出 capability matrix，不修改 routing。

**Exit gate**：每個實際使用中的 surface 都被歸類為 model-routed／role-only／single-agent，且有機械輸出或官方文件證據。

#### Phase 1：Core charter 角色化

- 以本 ADR Decision §1–2 改寫 `docs/orchestration.md` 路由表與委派邊界。
- 更新 `AGENTS.md` 薄指針、`docs/orchestration.md`「Token 節約原則」的大範圍掃描條目、`docs/user-guide.md`、`docs/verification-matrix.md` 與其他非歷史逐字引用者；既有 ADR 本文保留歷史原文。
- 反查 Accepted ADR Alternatives，避免重提已拒絕方案。

**Exit gate**：Core 文件零 model slug／供應商 intelligence label；逐字引用同步完成；charter lint／pointer checks 全綠。

#### Phase 2：Codex custom agents

- 新增 explorer／executor／reviewer TOML。
- 先以 capability class 綁定設定；若當前 callable surface 不支援 role selection，保留 agent files 並將該 surface 標示 role-only，不造假成功。
- Explorer／Reviewer 驗證 read-only；Executor 驗證允許檔案集與 checkpoint contract。

**Exit gate**：Codex 可 discovery 三角色；錯誤 role name fail-loud；read-only roles 的故意寫入被拒絕；runtime capability mode 被明確輸出。

#### Phase 3：Routing replay 與故意紅

- 回放至少三種既有任務：大範圍 repo 掃描→Explorer、明確 BDD／bug spec→Executor、security／semantic diff→Reviewer。
- 故意紅：要求 Explorer 寫檔、要求 Reviewer直接修復、讓無選模 surface 宣稱 model-routed；三者都必須被拒絕或降級標示。
- Orchestrator 對 executor 結果親跑 gate，證明 `unverified_success` 未因多代理而弱化。

**Exit gate**：三條正向 replay 與三條故意紅都有原始證據；token／latency 只作觀測，不以單次數據宣稱節省。

#### Phase 4：Claude adapter 對齊與 ADR-025 kit 收錄

- 將 Claude 既有調度對映到同一組 role names，保留其原生 model selection 實作。
- 把 Core role contract 與 Codex／Claude adapters 納入 ADR-025 Phase 0 inventory。
- 跨 harness 比較的是角色責任與 gate，不強求相同 model slug。

**Exit gate**：兩個 harness 對相同 fixture 產生相同 role assignment 與停止條件；surface 能力差異在 adapter report 明示。

### 8. 明文不在本 ADR 範圍

- 本 commit 不建立 `.codex/agents/`、不修改 global `~/.codex/config.toml`、不切換目前模型。
- 不把 subagent token 消耗視為必然節省；平行 agent 可能增加總 tokens，只在 latency、context 隔離或角色成本 eval 有證據時宣稱收益。
- 不提高預設 agent nesting depth；recursive fan-out 須有獨立事證與新 ADR。
- 不建立外部 orchestration server，也不以 shell 啟動未受目前 session contract 管理的背景 Codex process 來繞過 capability mode。
- 不修改 ADR-012 的 verified gate、ADR-023 的 hook boundary 或 ADR-025 的 kit ownership。

### 9. 本 ADR 接受時的同步項目

- `tasks/todo.md`：新增 Phase 0–4，全部標示未排程。
- `docs/orchestration.md`、`AGENTS.md`、`.codex/agents/`、`docs/verification-matrix.md`：留待對應 implementation Phase，同批同步，不在本 commit 預寫完成態。

---

## Rationale

### 為什麼 Core 應定義角色，不定義模型大小

「小型」與「大型」是供應商、版本與 entitlement 相依的相對詞；Explorer 的真正需求是快速讀取與精確取證，Reviewer 的真正需求是深度推理與唯讀。用能力定義角色可以跨 Claude、Codex 與未來 harness 共用，也能以 eval 替換模型而不改治理語意。

### 為什麼有 subagent 還需要 custom agent files

單純 spawn 只隔離 thread，不能保證模型、reasoning、sandbox 與 instructions。Project-scoped agent files 讓 role binding 可版本控制、review 與跨 session 重現；prompt-only 指派無法提供相同的可稽核性。

### 為什麼 role-only 是正式降級模式

某些 managed surface 可能暴露 subagent 卻不暴露 per-agent model selection。完全禁用會放棄 context 隔離與平行化收益；假裝 model-routed 又會成本誤報。Role-only 誠實保留可用能力，並把缺口留給 adapter 而不是污染 Core contract。

### 為什麼模型名稱不寫進 ADR

模型可用性與建議會變，且不同帳號 entitlement 不同。ADR 應固定決策邊界與驗收標準；model slug 是依 eval 可替換的 adapter configuration。只有責任或安全語意改變才需要新 ADR。

### 為什麼 Reviewer 與 Explorer 預設 read-only

兩者的輸出契約是事實與 findings，不是修改。權限與責任一致可降低錯誤 prompt 或 scope creep 的影響；需要修復時由 orchestrator 另派 Executor，保留 review／implementation separation。

---

## Consequences

### Positive

- 高階 orchestrator 可聚焦裁決、綜合與 gate，明確 spec 的執行工作有正式去向。
- Codex 模型成本路由從口頭意圖升級為可版本控制的 adapter。
- 無法選模的 surface 不再被誤報為成本分級完成。
- Role names 可跨 harness 與 ADR-025 kit 重用，不綁供應商 model taxonomy。
- Explorer／Reviewer read-only 降低平行工作對共享 worktree 的風險。

### Negative / Trade-offs

- Custom agents、capability matrix 與 eval 增加設定與維護成本。
  - Mitigation: 只維護三個 child roles；model binding 集中 adapter，不在 skills／charter 重複。
- Subagents 可能增加總 token 使用量，即使 root context 更乾淨。
  - Mitigation: Script-first、單一 bounded task、禁止為可序列完成的小事投機平行；以 replay 觀測後再調整觸發閾值。
- 不同 Codex surface 可能呈現不同 capability mode。
  - Mitigation: 啟動／驗收時輸出 mode；無證據一律 role-only，不追求虛假的表面一致。
- Read-only Reviewer 發現簡單問題後不能直接修復，增加一次派工 round trip。
  - Mitigation: 維持 reviewer independence；orchestrator 可將精確 finding 轉為小型 Executor spec，避免 review 與自我證明混在一起。
- Model upgrade 可能使既有 capability class eval 失效。
  - Mitigation: 先跑 eval 再改 adapter binding；保留上一個通過版本作 rollback。

---

## Alternatives Considered

### Alternative A：維持小／中／大型模型路由表，只為 Codex 補註解

Rejected. 問題不是缺註解，而是 Core 把 adapter 能力當成既成事實；補註解仍無法定義 role permissions、degraded mode 與可驗證輸出。

### Alternative B：所有 subagent 永遠繼承 root 模型

Rejected. 可取得 context 隔離，但放棄使用者明確要求的成本／能力分級；對官方已支援 custom agent model binding 的 surface 也構成不必要限制。

### Alternative C：不使用 subagent，由最高階 orchestrator 執行全部工作

Rejected. 大量掃描、logs 與明確 spec 實作會污染主 context、提高成本，且違反本 repo 已驗證有效的 orchestrator／executor separation。

### Alternative D：只靠派工 prompt 寫「你是 explorer／executor／reviewer」

Rejected. Prompt 可改行為但不能可靠固定 model、reasoning、sandbox 或 discovery；跨 session 無法證明使用相同設定。

### Alternative E：在 Core charter 硬編目前推薦的 Codex model slugs

Rejected. 會把 harness、版本與 entitlement 變動帶入治理核心；任何模型更新都需修改憲章與開 ADR，違反 ADR-025 adapter 分層。

### Alternative F：建立中央遠端 orchestrator service 統一選模

Rejected. 引入認證、queue、remote state、成本追蹤與新的 failure domain；目前 project-scoped custom agents 已足以驗證需求，沒有事故支持更重機制。

### Alternative G：無法選模的 surface 禁止使用 subagent

Rejected. 會失去 context 隔離、平行取證與 executor contract 的收益；role-only 已能誠實保留這些能力而不誤報成本分級。

---

## Implementation Rules

1. `docs/orchestration.md` 的核心路由只能使用 Script／Explorer／Executor／Reviewer／Orchestrator 角色與能力描述，不得含供應商 model slug 或未定義的「小／中／大型」執行者。
2. Orchestrator 必須保留 business／架構裁決、跨 agent 綜合、確定性 gate 與 commit／push 放行；任何 adapter 不得將這些責任交給 child agent。
3. Codex project adapter 只能在 `.codex/agents/` 定義 explorer／executor／reviewer；全域 `~/.codex/agents/` 不得成為本 repo 可重現性的必要前提。
4. Explorer 與 Reviewer 預設 read-only；若特定 surface 無法提供 per-agent sandbox，capability report 必須明示，且派工 spec 仍禁止寫入。
5. Runtime 無 per-agent model 證據時一律標示 role-only；不得以 prompt、agent 自述或 root model 設定推論 child model。
6. Model binding 必須透過 capability-class eval 後更新 adapter；不得在 Core charter、skills 或 task templates 複寫 model slug。
7. 每個角色至少有一條正向 replay 與一條責任越界故意紅；Executor 結果仍須由 Orchestrator 親跑確定性 gate。
8. `AGENTS.md` 與 skills 只放 Role Routing 指針與必要 agent name，不複寫路由表或模型設定。
9. ADR-025 kit 收錄時，Core role contract 與各 harness adapter 分開打包；不同 surface 的 capability mode 必須可被 `check` 顯示。
10. 本 ADR 接受 commit 的驗收：

    ```bash
    scripts/adr-lint.sh
    # 預期 26 file(s) passed

    grep -n "角色型模型路由與 Codex Subagent Adapter" tasks/todo.md
    # 預期至少 1 命中
    ```

11. 任何提案修改 1–10，必須先開新 ADR。
