# Claude Code 與 Codex 共用 hook 核心及 skill 單一來源

> 本 ADR 將原本綁在 `.claude/` 的第一層開發防線與 learning loop 抽成 harness-neutral 核心，讓 Claude Code 與 Codex 使用同一份行為實作。兩個 harness 只保留必要的事件 wiring 與 skill discovery symlink，避免複製後各自漂移。

---

## Status

Accepted (2026-07-10)

- 同步項目：見 Decision §6。
- 關聯：本工作滿足 `tasks/todo.md`「觸發制擱置項」的「第二個 harness 常態參與開發」觸發條件。

---

## Context

### 現況

repo 的規則與 loop 已有 harness-neutral 的中外層：

- `CLAUDE.md`、Accepted ADR、`.claude/references/` 是規則正典；`AGENTS.md` 是非 Claude Code harness 的薄入口。
- `tasks/lessons/`、`tasks/checkpoint.md` 承載跨 session 學習與續接。
- `scripts/git-hooks/`、`scripts/ci-checks.sh`、Architecture.Tests 與 BDD tests 在 commit／push／CI 時確定性執行。

但最內層仍綁定 Claude Code：

```text
.claude/settings.json
  -> .claude/hooks/session-init.sh
  -> .claude/hooks/pre-tool-edit.py
  -> .claude/hooks/pre-tool-bash.py
  -> .claude/hooks/post-edit-validate.sh
  -> .claude/hooks/post-tool-failure.sh
```

`docs/verification-matrix.md` 因此把寫時防線標為「限 Claude Code harness」，`AGENTS.md` 也要求其他 harness 以主動閱讀補償。這在 Codex 尚無 lifecycle hooks 時是正確描述；目前 Codex 已提供 project-local `UserPromptSubmit`、`PreToolUse`、`PostToolUse` 等事件，以及 `Edit`／`Write` 對 `apply_patch` 的 matcher alias，繼續維持 Claude-only 實作已不再合理。

另一個可攜性缺口是 skills。專案 workflow skills 的唯一內容目前位於 `.claude/skills/*/SKILL.md`；Codex 的 repo discovery 入口是 `.agents/skills/`，所以 Codex session 看不到 `bdd-vertical-slice`、`lesson`、`code-review` 等既有流程。

### Payload 差異

事件名稱相近不代表 payload 相同：

| 行為 | Claude Code payload | Codex payload |
|---|---|---|
| 編輯前 guard | `Edit`／`Write`／`MultiEdit`，路徑在 `file_path`，新增文字在 `content`／`new_string` | canonical tool 為 `apply_patch`，完整 patch 在 `tool_input.command` |
| 編輯後驗證 | 單一 `file_path` | patch 可能同時觸及多個檔案 |
| tool failure | `PostToolUseFailure` 直接提供 `error` | 無同名事件；僅能在 `PostToolUse` 有提供失敗結果時觀察 |
| session context | `UserPromptSubmit` stdout 注入 | `UserPromptSubmit` stdout 同樣加入 developer context |

若把現有 scripts 直接複製到 `.codex/hooks/`，edit guard 會因找不到 `file_path` 而靜默放行；若各寫一套 parser，規則 regex、secret scrubber 與 lesson 注入格式日後必然漂移。

### 不決定會發生什麼

- Claude Code 與 Codex 對同一段違規 C# 會在不同時間失敗，增加重工且削弱「換 harness 仍一致」的目標。
- 同一條 guard 修正需要改兩份 scripts 與兩套 smoke tests，review 無法證明兩者仍等價。
- Codex 會繞過 repo 既有 skills，自行重建 BDD／lesson／review 流程，形成第二份隱性規範。

---

## Decision

### 1. Hook 行為集中於單一 harness-neutral dispatcher

所有第一層 hook 行為集中到 `scripts/agent/hook.py`，以 action 參數選擇行為：

```bash
python3 scripts/agent/hook.py session-context
python3 scripts/agent/hook.py pre-tool-edit
python3 scripts/agent/hook.py pre-tool-bash
python3 scripts/agent/hook.py post-edit-validate
python3 scripts/agent/hook.py observe-tool-failure
```

`.claude/hooks/` 的五份實作退役，不保留 wrapper 或副本。共用 dispatcher 必須保留現行 fail-open 邊界：hook payload malformed 時不阻擋工作；確定命中高信心規則或 syntax error 時維持 `exit 2` 並在 stderr 提供可修正訊息。

### 2. Harness 設定只做薄 wiring

`.claude/settings.json` 保留既有 permissions，但 hooks 全部改指 `scripts/agent/hook.py`。新增 `.codex/hooks.json`，只描述 Codex event／matcher 到相同 action 的對應，不含規則 regex、lesson parser 或 syntax validation 邏輯。

```text
.claude/settings.json ----\
                          > scripts/agent/hook.py
.codex/hooks.json --------/
```

Codex project hooks 受其 trust model 管理：首次啟用或 hook definition hash 改變後，使用者必須在 Codex `/hooks` 檢視並信任。這是 harness 安全邊界，不以 `--dangerously-bypass-hook-trust` 作為日常操作指引。

### 3. 在共用核心正規化 payload

`scripts/agent/hook.py` 先把 harness-specific payload 正規化為「受影響路徑 + 新增文字」再套用既有規則：

```text
Claude Edit/Write/MultiEdit -> [(file_path, introduced_text)]
Codex apply_patch           -> [(patch_path, added_lines)]
```

Codex patch parser 只檢查 `Add File`／`Update File`／`Move to` 的目標與實際 `+` 新增行，不掃 unchanged context，避免把既存合法文字誤判為本次新增違規。post-edit validator 對 patch 內所有仍存在的目標檔逐一驗證，不只取第一個檔案。

### 4. Skills 以 symlink 維持內容單一來源

`.claude/skills/*` 暫維持既有 canonical 位置；對每個包含 `SKILL.md` 的 project skill，在 `.agents/skills/<name>` 建立相對 symlink 指回 `.claude/skills/<name>`。Codex 官方支援 symlinked skill folder，因此不複製 `SKILL.md`。

```text
.agents/skills/lesson -> ../../.claude/skills/lesson
```

現有未追蹤 Tessl symlink 不屬本 ADR，不移動、不納入 parity 完整性檢查，也不加入本次變更。

### 5. Failure observation 採可驗證的最接近等價，不偽造完整 parity

Claude Code 的 `PostToolUseFailure` 繼續把任意 tool failure 送入共用 `observe-tool-failure`。Codex 以 `PostToolUse` 呼叫同一 action；共用核心只有在 response 明確提供失敗旗標或非零 exit code 時才寫入既有 `.claude/failures.jsonl`，成功或不明 payload 一律略過。

Codex 未提供 `PostToolUseFailure` 完整等價事件，因此不得以解析不穩定 transcript、改寫每個 shell command、或把成功結果猜成失敗來補齊。`docs/verification-matrix.md` 必須把這個 coverage 差異列為殘餘限制。

### 6. 本 ADR 接受時的同步項目

- `scripts/agent/hook.py`：單一 hook dispatcher 與兩種 payload normalizer。
- `.claude/settings.json`：Claude Code 薄 wiring。
- `.codex/hooks.json`：Codex 薄 wiring。
- `.claude/hooks/`：五份舊實作刪除。
- `.agents/skills/`：既有 project skills 的相對 symlink。
- `scripts/hook-smoke.sh`：Claude／Codex fixture parity、syntax validation 與 failure scrubbing 驗證。
- `scripts/machinery-check.sh`：兩份 wiring、dispatcher、skills symlink 與 pointer 自體健檢。
- `CLAUDE.md`、`AGENTS.md`、`tasks/lessons/_README.md`、`.claude/skills/lesson/SKILL.md`、`tasks/checkpoint.md`：舊 hook 路徑與 harness 限制指針同步。
- `docs/verification-matrix.md`：第一層防線改登記為共用核心，並標示 Codex failure observation 殘餘限制。
- `tasks/todo.md`：跨 harness 觸發項結案與計畫狀態更新。

### 7. 明文不在本 ADR 範圍

- 不改 `docs/orchestration.md` 的模型分級、executor contract、停止條件或 checkpoint schema。
- 不把 Claude Code permissions 自動翻譯成 Codex sandbox／approval policy；兩者安全模型不同，沿用各 harness 原生設定。
- 不搬移 `.claude/references/` 或重寫 `CLAUDE.md`；`AGENTS.md` 繼續作薄入口，規則內容仍只有一份。
- 不承諾 Codex 尚未攔截的 tool path 具有第一層防線；commit／push／CI gates 仍是完整 enforcement boundary。

---

## Rationale

### 為什麼是單一 dispatcher，而不是五支共用 scripts

五支 scripts 已經共享 payload parsing、repo root、錯誤輸出與測試需求；跨 harness 後這些共同邊界更多。單一 dispatcher 讓 parser、secret scrubber 與路徑解析只存在一份，同時讓 wiring 清楚顯示每個 event 對應哪個 action。action 仍保持獨立函式，不把五種行為混成單一路徑。

### 為什麼 canonical skills 暫留 `.claude/skills`

把全部 skill 目錄搬到 `.agents/skills` 再反向連給 Claude Code，會造成大量 rename、影響既有 Claude workflow 與歷史指針；目前只需要讓第二個 harness 可發現同一內容。相對 symlink 是最小改動，且由 machinery check 防止漏接新 skill。

### 為什麼不追求字面上的百分之百 hook coverage

第一層 hook 是加速失敗回饋，不是完整安全邊界。Codex 官方明示 `PreToolUse`／`PostToolUse` 仍只攔部分 shell、`apply_patch` 與 MCP 路徑；透過 transcript 或 command rewriting 補洞會依賴不穩定格式並改變 tool 語意。完整一致性繼續由既有 git／CI gates 保證，第一層只在 harness 原生可觀察面提供相同行為。

---

## Consequences

### Positive

- 規則 regex、lesson injection、syntax validation、failure scrubbing 各只有一份實作。
- Claude Code 與 Codex 對相同 edit／Bash payload 得到相同 block 結果與訊息。
- Codex 可直接使用既有 BDD、lesson、review 等 skills，無第二份 `SKILL.md`。
- machinery check 與 parity fixtures 會在 wiring 或 payload parser 漂移時 fail-loud。

### Negative / Trade-offs

- 單一 dispatcher 檔案比任一舊 hook 大，修改時需要辨識 action 邊界。
  - Mitigation: 每個 action 使用獨立函式，shared helper 限 payload／path／scrubbing；smoke test 依 action 分段。
- Codex project hook 第一次使用及 definition 變更後需要人工 trust。
  - Mitigation: `AGENTS.md` 提供 `/hooks` 指引；machinery check 驗 wiring，但不繞過 Codex 的 trust 邊界。
- Codex failure observation 與 tool interception 不是 Claude Code 的完整超集合。
  - Mitigation: 驗證矩陣明文標示殘餘限制；完整規則仍由 fast/full gates 執行，不把第一層當唯一防線。
- `.agents/skills/` 會同時包含 tracked project symlink 與既有未追蹤 Tessl symlink。
  - Mitigation: machinery check 只枚舉 `.claude/skills/*/SKILL.md` 對應項，不接管或誤納 Tessl 路徑；提交時明確 stage project symlink。

---

## Alternatives Considered

### Alternative A: 複製 `.claude/hooks` 到 `.codex/hooks`

Rejected. edit payload 不同，直接複製會靜默放行；分別修成兩套後，regex、scrubber 與 lesson 格式需要雙重維護，違反單一來源目標。

### Alternative B: 保留 Claude-only 第一層，只靠 `AGENTS.md` 主動提醒 Codex

Rejected. 這是目前狀態，只能把失敗延後到 pre-commit／CI；既然 Codex 已提供對應 lifecycle events，繼續接受不同回饋速度沒有合理收益。

### Alternative C: 把所有 rules 與 skills 搬到新 `.agent/` 目錄

Rejected. 大量 rename 會擴大 blast radius、改動既有 Claude Code discovery 與文件指針；hook 核心與 skill symlink 已能取得單一來源，不需為命名中立支付遷移成本。

### Alternative D: 產生兩份 harness scripts／config

Rejected. generated file 可減少手改，但仍需生成器、drift check 與產物 review；hook payload 本來就能在 runtime 正規化，單一 dispatcher 更直接。兩份薄 config 是 harness 原生格式差異，沒有必要再引入 code generation。

---

## Implementation Rules

1. 所有第一層 hook 行為只能實作於 `scripts/agent/hook.py`；`.claude/` 與 `.codex/` 不得新增同規則的 script 副本。
2. Claude Code 與 Codex wiring 必須涵蓋 `session-context`、`pre-tool-edit`、`pre-tool-bash`、`post-edit-validate`、`observe-tool-failure` 五個 action，並由 `scripts/machinery-check.sh` fail-loud 驗證。
3. Edit guard 必須以 Claude 與 Codex fixtures 對同一個四項違規取得相同 `exit 2`，且 Codex 只掃 patch 新增行。
4. Bash guard、session injection、post-edit syntax validation 與 secret scrubbing 必須各有兩種 harness payload 的 smoke coverage；不適用的 payload 差異須在 test 名稱或註解明示。
5. 每個 `.claude/skills/*/SKILL.md` 必須有 `.agents/skills/<name>` 相對 symlink 指回同一目錄；額外第三方 skills 不納入此反向完整性條件。
6. `docs/verification-matrix.md` 必須登記共用第一層與 Codex failure／tool coverage 殘餘限制，不得宣稱完全 enforcement parity。
7. **驗收**：

   ```bash
   scripts/adr-lint.sh
   scripts/hook-smoke.sh
   scripts/machinery-check.sh
   scripts/ci-checks.sh fast
   git --no-pager grep -n '\.claude/hooks/' -- \
     CLAUDE.md AGENTS.md docs/verification-matrix.md tasks/lessons .claude/skills
   # 預期 0 命中
   ```

8. 任何提案修改 1–7，必須先開新 ADR。
