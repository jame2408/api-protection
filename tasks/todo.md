# Todo — 開放項登記簿

## Codex harness parity — 已結案（2026-07-10）

- `docs/adr/adr-023-cross-harness-hook-and-skill-parity.md` 定案：Claude Code／Codex 的第一層防線共用單一 `scripts/agent/hook.py`，兩份 harness config 只做薄 wiring。
- Codex `apply_patch` 與 Claude Edit／Write／MultiEdit payload 已正規化；session context、四個 C# guard、兩個 Bash guard、四類 syntax validation、failure scrubbing 都由 `scripts/hook-smoke.sh` 鎖定 parity。
- 9 個 tracked project skills 以 `.agents/skills` symlink 指回 `.claude/skills`；Codex `prompt-input` 實測全部可發現，內容仍只維護一份。
- `scripts/machinery-check.sh` fail-loud 驗兩份 wiring、dispatcher executable／py_compile、skill links 與 pointers；`scripts/ci-checks.sh fast` 全綠。Codex 原生 hook coverage／failure event 差異已在驗證矩陣明文保留，不宣稱完整 enforcement parity。

## Secret Scanner 批次自動撤銷 — 已結案（2026-07-10）

兩契約缺口經使用者裁決（內部批次端點 `POST /internal/security/leaked-keys`／outbox 通知事件 `KeyLeakNotificationRequested`），完整垂直切片落地 `0072337`（19/46，api-spec §3.2.9 同步）。過程紀錄見 `tasks/checkpoint.md` 已完成欄。

> 2026-07-10 歸檔 pass（GPT-5.6 回饋處置）：本檔原名「`.claude/` Hardening — Outstanding Work」，已結案內容（hardening 七 commit、ADR-004 acceptance、coverage gate 計畫、ADR-021/022 規劃段、multi-agent review 已結案項）移至 `tasks/archive/todo-closed-2026-07-10.md`。開放項沿用 2026-04-30 multi-agent review 原編號（checkpoint 與多份 ADR 以該編號引用，不重編）。

## Non-blocking follow-ups（已登記，未排程）

- Value-level secret redaction coverage could be extended for additional token shapes:
  - Slack tokens: `xoxb-` / `xoxp-`
  - GitLab personal access tokens: `glpat-`
  - AWS secret access keys require a separate design decision because they lack a stable prefix and may cause false positives.
- `scripts/agent/hook.py` `observe-tool-failure` writes `tool_use_id`, `duration_ms`, and `is_interrupt` to `failures.jsonl`, but lessons do not include those trace fields. Consider adding them only if failure-to-lesson traceability becomes useful.
- `scripts/agent/hook.py` `session-context` malformed JSON fallback：payload 無法解析時 `session_id` 為空，仍會保守注入 lessons；這是既有語意，只有需要更嚴格 hook failure semantics 時才重議。
- Lessons triage 常設觸發：`tasks/lessons/` 內 `status: active` 檔案數 ≥ 15、或 phase 收尾時，盤點 active 條目可否機械化（腳本/lint/gate），可機械化者落地後改該檔 frontmatter 為 `status: archived`（判準：`docs/adr/adr-013-content-tiering-and-injection-slimming.md` 決策 (b)；載體見 `docs/adr/adr-021-shared-state-files-team-scale.md`）。
- ADR-004 允許 ILogger 邊界清單第 6 類（Infrastructure ApiClient，`LoggerBoundaryTests` 與 `exceptions.rule.md` §B 已實質採用）待正名——掛到下一份錯誤處理相關 ADR，不單開。
- `requirements-analysis-design` skill 觸發詞與 `.feature` 凍結相撞——根治在 upstream `jame2408/agent-skills` repo 加凍結 gate，本地已在 `tasks/bdd-backlog.md` 檔頭布防；2026-07-10 起本地 SKILL.md description 已移除相撞觸發詞並明示改道 ADR-022。

## 觸發制擱置項（GPT-5.6 回饋處置 2026-07-10；觸發成立前勿動）

- ~~跨 harness 共用規則 CLI：第一層 hook 邏輯抽成共用執行核心，各 harness 只做 adapter——觸發：第二個 harness 常態參與開發。~~ ✅ **2026-07-10 已由 ADR-023 與 `scripts/agent/hook.py` 關閉**；Claude Code／Codex config 皆為薄 wiring，skill 以 symlink 共用。
- commit trailer／staged 紀律的 CI 端覆核：CI 對 base..head 逐 commit 驗 `Refactor-assessment:`／`Spec-change:`／進度檔同 commit——**觸發：首次觀察到 `--no-verify` 或未裝 hook 的繞過事故**（矩陣 9d/9f/9g 殘餘風險現況為知情接受）。
- Discovery 管道解凍：`requirements-analysis-design` skill 正式接回 BDD backlog（Example Mapping → 候選場景 → 使用者核准晉升）——**觸發：首個「repo 內無既有場景的真新需求」出現**；解除規格另開 ADR（ADR-022 明文排除範圍）。

## Multi-agent review (2026-04-30) — 開放項（原編號保留）

A 4-agent parallel review (security / architecture / tests / `.claude` consistency) was run against `backend/`, `.claude/`, `docs/`. Findings cross-referenced against `docs/design/prd.md`, `docs/design/api-spec.md`, `docs/design/design-doc.md`, and current BDD scenarios. 標記：🐞 real drift、🏗️ scaffolding（spec/BDD 已規劃）、☁️ out-of-scope。已結案項（#1–3、5、6、9–13、19、20、35–37、39）與歷史行動序列見 `tasks/archive/todo-closed-2026-07-10.md`。

### A. Wire-format / contract drift vs `api-spec.md` 🐞

4. **`VALIDATION_ERROR` prefix matches too loosely** (`CreateApiKeyEndpoint.cs:49`, `CreateApiKeyFailureCodes.cs:11`). Tighten to `"VALIDATION_ERROR:"` so unrelated codes starting with the same letters can't accidentally fall into 400.

### B. Security: PRD-mandated invariants not yet honoured 🐞

7. **Active-key-count guard is TOCTOU under concurrency** (`CreateApiKeyHandler.cs:23-32`). BDD `01_CreateApiKey.feature:38` covers the happy-path "limit reached" case but not the race. Add unique partial index or `IsolationLevel.Serializable` + treat `DbUpdateException` (unique violation) as the limit/duplicate failure.
8. **Constant-time comparison reminder for the future validation hot-path** (`prd.md` R-VAL-01). Not yet implemented; flag in the validation slice when it lands. Use `CryptographicOperations.FixedTimeEquals` over byte arrays — never `string ==`（ADR-017 Implementation Rule 6 已固化此義務）。

### C. Architecture / code drift 🐞

14. **`ITenantQueryContext` registration location.** Currently `InfrastructureModule.cs:25`. Per BC-isolation principle the abstraction belongs to TenantManagement; register in `TenantManagementModule` (Infrastructure can expose a small `AddAppDbContext` helper for the BC module to call). Or document the placement.
15. **`ScopeRegistryEntry` record declared inside `AppDbContext.cs:28`.** Should live in `KeyLifecycle/Domain/` (where `IScopeRegistry` lives) so Infrastructure isn't owning a domain record.
16. **`ScopeRegistryService` named as `*Service` but is a Repository** (`Infrastructure/Persistence/Repositories/ScopeRegistryService.cs`). Rename to `ScopeRegistryRepository` for naming consistency with `ApiKeyRepository`, `AccessPolicyRepository`.
17. **Anaemic `ApiKey` aggregate.** All invariants (limit, name uniqueness, scope existence, expiry) live in the Handler; `ApiKey.Create` only constructs the entity. At least the intrinsic invariants (`expiresAt > now`, `scopes.Any()`) should move into the aggregate constructor. Cross-aggregate guards (count, name uniqueness) stay in the Handler — they need DB access.
18. **`Result<TValue, TError>` double implicit conversion is a latent footgun.** No call site uses `Result<X, X>` today, but if anyone ever returns `Result<string, string>` the conversion is ambiguous. Consider explicit `Result.Ok(value)` / `Result.Fail(err)` factories or constraining `TError` to `Failure` everywhere.

### D. Test infrastructure 🐞

21. **`ApiKeyManagementWebApplicationFactory.cs:28-29` uses `Environment.SetEnvironmentVariable(...)`** — process-wide leakage; parallel test classes will see last-write-wins. Switch to `builder.UseSetting(...)` or `ConfigureAppConfiguration`.
22. **`TestHooks.cs:103-105` opens a fresh `NpgsqlConnection` per `ResetAsync`** instead of reusing the long-lived checkpoint connection at line 69. Pure overhead.
23. **Testcontainers don't use `.WithReuse(true)`** — each local dev iteration pays the full container startup. Add reuse for dev productivity (CI behaviour unchanged).
24. **No test data builders in `TestInfrastructure/`.** As more BCs come online each step file will repeat seed code. Add a `TenantBuilder` / `ApiKeyBuilder` etc. before the next BDD wave.

### E. Scaffolding — already in spec / BDD plan, no separate action 🏗️

25. **No auth on `CreateApiKeyEndpoint`** — `api-spec.md` §2.1 fully defines JWT roles (`PlatformAdmin`, `TenantAdmin`, `Consumer`, `SecurityAdmin`) and IDOR cross-validation rules. Implementation will land with the auth middleware slice; flagged here so reviewers don't re-raise.
26. **No FluentValidation / `Request` charset & length checks.** `api-spec.md` §3.2.1 lists all validation rules; partial Handler guards exist (`scopes_empty`, `expires_at_past`, `expires_at_exceeds_max`). Will be replaced by FluentValidation when input-validation slice lands.
27. **`Audit` and `Monitoring` BC projects are empty shells.** `design-doc.md` §3.2 defines both as Supporting Domain. BDD scenarios in `02_RevokeKey.feature` ... `06_ExpireKey.feature` are mostly `@ignore`d. They land per BDD wave.
28. **`Features/02–06/*.feature` step definitions don't exist yet.** Will be added one scenario at a time per the `@ignore`-driven BDD cycle.

### F. Out of this service's scope ☁️

29. **HTTPS redirect / HSTS** — `prd.md` R-SEC-04 mandates HTTPS but `design-doc.md` §6.1 places TLS termination at the API Gateway. This service runs behind it.
30. **Per-tenant rate limiting and burst control** — `design-doc.md` §6.3 and §7.2.2 explicitly assign rate limiting / quota enforcement to the API Gateway. `RateLimitConfig` is part of `AccessPolicy` but enforcement happens at the Gateway. Do **not** add `AddRateLimiter` to this service.
31. **CORS** — Gateway boundary concern; not configured here intentionally.

### G. Already logged elsewhere (kept here for cross-reference) ℹ️

32. Slack `xoxb-`/`xoxp-`, GitLab `glpat-` token shapes not yet covered by hook redaction. (See Non-blocking follow-ups above.)
33. `observe-tool-failure` trace fields (`tool_use_id`, `duration_ms`, `is_interrupt`) not mirrored in lesson records. (See Non-blocking follow-ups above.)
34. `session-context` malformed-payload fallback semantics. (See Non-blocking follow-ups above.)

## Cross-doc consistency sweep (2026-05-31) — 開放項

- [ ] **38. (P3, doc erratum — needs mechanism decision)** ADR-002 §3 `tests/` tree omits `SharedKernel.Tests` (present in `.slnx`). Illustrative tree, low impact, but doc trails reality.
  - ⚠️ #38 touches an Accepted ADR body → governance clause "任何提案修改 1–N 必須先開新 ADR" applies. Decide **erratum note vs new ADR** before editing; do **not** silently edit ADR bodies.
