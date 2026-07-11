# Agent Surface Capability Matrix

> ADR-026 Phase 0 產物（`docs/adr/adr-026-role-based-model-routing-and-codex-subagents.md` Decision §7 Phase 0）。本檔只登記各 harness surface 的 subagent／role／model／sandbox 能力與證據，並判定 routing capability mode（mode 定義見 ADR-026 Decision §4）；**路由規則權威在 `docs/orchestration.md`，本檔不承載路由決策**。
>
> 盤點日：2026-07-11。re-audit 觸發：harness 大版本更新、新 surface 投入使用、或 adapter 檔案（`.codex/agents/`、`.claude/agents/`）新增／變更。

## 判定總表

| Surface | 使用中 | Role selection | Per-role model / reasoning | Per-role sandbox | 併發／巢狀 | Capability mode | 證據 |
|---|---|---|---|---|---|---|---|
| Claude Code CLI | ✅（本 audit session 即此 surface） | ✅ Agent tool `subagent_type`＋`.claude/agents/*.md` frontmatter 定義自訂角色 | ✅ 呼叫時 `model` 參數可逐次指定，agent 定義 frontmatter 可綁 model／reasoning（解析順序：env var → 逐次參數 → frontmatter → 主對話模型） | ✅ agent 定義 `tools` allowlist（內建 Explore 型即無 Edit／Write）＋`isolation: worktree` | 子代理無 Agent tool → 巢狀深度 1；可平行派發 | **model-routed** | 本 session Agent tool schema 機械觀察（2026-07-11）：`model` 參數存在且逐次可選；內建 read-only agent 型存在；三 capability class 最小 eval 通過（見下方綁定紀錄） |
| Codex CLI（互動） | ✅（使用者日常操作） | ✅ project-scoped `.codex/agents/*.toml`（必填 `name`／`description`／`developer_instructions`） | ✅ per-agent `model`、`model_reasoning_effort`（省略時繼承 parent session） | ✅ per-agent `sandbox_mode` | `agents.max_threads` 預設 6、`agents.max_depth` 預設 1（本 repo 憲章併發上限 4 更嚴，以憲章為準） | **能力＝model-routed；現況運行＝role-only**（repo 尚無 `.codex/agents/`，Phase 2 落地並活體驗證前不得宣稱成本分級） | 官方手冊 Custom agents／Multi-agent operations 節（2026-07-11 fetch，`developers.openai.com/codex/codex-manual.md`）；`find .codex -type f` 僅 `hooks.json` |
| Codex managed collaboration surface（`spawn_agent`） | ✅（2026-07-11 revokedBy 回補包實測委派） | ❌ 介面只收 task name／message／forked turns | ❌ 無 model 參數；session contract 明示所有 agents 能力相同 | ❌ 無 per-agent 設定 | 平行 thread、fork turns 可用 | **role-only** | ADR-026 Context「現況」節登記之 2026-07-11 session 機械觀察 |
| Codex app／IDE extension | ❌ 本 repo 尚未投入使用 | 官方文件同 CLI（project-scoped `.codex/agents/` 跨 surface 共用；IDE 有 background-agent panel） | 同 CLI | 同 CLI | 同 CLI | 能力暫依官方文件歸 model-routed；**投入使用時須補該 surface 機械證據後才可宣稱** | 官方手冊 Surface support 節（2026-07-11 fetch） |
| Gemini CLI | ❌ 不可用 | — | — | — | — | 不列級（服務端 `IneligibleTierError`，無可用 model list） | 2026-07-11 ADR-025 session 實測（`tasks/checkpoint.md`「已嘗試且失敗的方法」欄） |

## 判定原則（指針，不複寫規則）

- 無 runtime per-agent model 證據的 session 一律標示 **role-only**；不得以 prompt 指派、agent 自述或 root model 設定推論 child model（ADR-026 Implementation Rule 5）。
- Codex 官方另有內建 fallback agents（`default`／`worker`／`explorer`），不等於本 repo 的角色契約；角色責任與輸出契約以 `docs/orchestration.md` §1 為準。

## Claude adapter 綁定與 eval 紀錄（Phase 4 部分提前，2026-07-11 使用者裁決）

`.claude/agents/{explorer,executor,reviewer}.md` 已建立（官方推薦形態：project-scoped、進版控、`tools` allowlist 限權、`model` frontmatter 綁定；官方文件另明文建議以 project agent 覆寫內建 Explore 綁低成本模型）。綁定依 ADR-026 Rule 6 先過最小 capability-class eval（內建 agent 型＋逐次 `model` 參數執行，orchestrator 親驗）：

| Capability class | 綁定 | Eval fixture 與結果（2026-07-11） |
|---|---|---|
| `fast-read` | explorer → `haiku` | 取證 coverage gate 門檻：路徑／行號／逐字原文全對（orchestrator grep 複驗吻合） |
| `standard-code` | executor → `sonnet` | scratchpad off-by-one fixture 真 Red→Green（orchestrator 親跑測試 3/3 OK） |
| `deep-review` | reviewer → `opus`＋`effort: high` | 三個 seeded 違規（throw／`CancellationToken.None`／ILogger）全中＋一條合理加值 finding；零寫入（fixture 檔未變動） |
| `deep-orchestration` | 主 session，不建 child role | ADR-026 Decision §3 角色預設 |

補充明示（Rule 4 capability report 義務）：explorer／reviewer 的 Write／Edit 為 `tools` allowlist **機械阻斷**；Bash 保留供唯讀查詢（`git log`／`git grep`），其寫入禁令為 agent 檔內**指示層**防線。錯誤 role name fail-loud 已實測（spawn 未定義 `explorer` 得明確錯誤含可用 agent 清單）。2026-07-11 重啟後 session 活體複驗：三角色均出現在 Agent tool 可用清單（discovery 成立）；explorer 寫檔故意紅——Write 遭 allowlist 機械拒絕（`Write exists but is not enabled in this context`）、Edit 不在其工具定義、probe 檔以遞迴搜尋確認未落地。

## 缺口登記

| 缺口 | 對應 Phase |
|---|---|
| `.codex/agents/` 三角色 TOML 未建立；Codex session 現況只能宣稱 role-only | ADR-026 Phase 2（exit gate 需 Codex session 活體驗證 discovery／fail-loud／read-only） |
| Phase 4 其餘項（跨 harness fixture role assignment 對齊、ADR-025 kit 收錄） | 須等 Phase 2–3 exit gate |
| Codex app／IDE 無使用中機械證據 | 投入使用時 re-audit |
