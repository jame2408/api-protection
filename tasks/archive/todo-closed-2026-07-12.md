# todo.md 結案歸檔（2026-07-12 收尾清掃，ADR-028 首次執行）

> 自 `tasks/todo.md` 移出的已結案內容，逐字保留（依 `docs/adr/adr-028-knowledge-ledger-lifecycle-and-adr-index.md` 決策 §2）。開放項與原編號仍在 `tasks/todo.md`；更早的結案內容見 `tasks/archive/todo-closed-2026-07-10.md`。

## Codex harness parity — 已結案（2026-07-10）

- `docs/adr/adr-023-cross-harness-hook-and-skill-parity.md` 定案：Claude Code／Codex 的第一層防線共用單一 `scripts/agent/hook.py`，兩份 harness config 只做薄 wiring。
- Codex `apply_patch` 與 Claude Edit／Write／MultiEdit payload 已正規化；session context、四個 C# guard、兩個 Bash guard、四類 syntax validation、failure scrubbing 都由 `scripts/hook-smoke.sh` 鎖定 parity。
- 9 個 tracked project skills 以 `.agents/skills` symlink 指回 `.claude/skills`；Codex `prompt-input` 實測全部可發現，內容仍只維護一份。
- `scripts/machinery-check.sh` fail-loud 驗兩份 wiring、dispatcher executable／py_compile、skill links 與 pointers；`scripts/ci-checks.sh fast` 全綠。Codex 原生 hook coverage／failure event 差異已在驗證矩陣明文保留，不宣稱完整 enforcement parity。

## Secret Scanner 批次自動撤銷 — 已結案（2026-07-10）

兩契約缺口經使用者裁決（內部批次端點 `POST /internal/security/leaked-keys`／outbox 通知事件 `KeyLeakNotificationRequested`），完整垂直切片落地 `0072337`（19/46，api-spec §3.2.9 同步）。過程紀錄見 `tasks/checkpoint.md` 已完成欄。

## 觸發制擱置項 — 已由 ADR-023 關閉（原列於觸發制擱置項段）

- ~~跨 harness 共用規則 CLI：第一層 hook 邏輯抽成共用執行核心，各 harness 只做 adapter——觸發：第二個 harness 常態參與開發。~~ ✅ **2026-07-10 已由 ADR-023 與 `scripts/agent/hook.py` 關閉**；Claude Code／Codex config 皆為薄 wiring，skill 以 symlink 共用。

## Cross-doc consistency sweep (2026-05-31) — 全數結案

- [x] **38. ✅ 已結案（2026-07-10 使用者裁決：erratum note，不開新 ADR）** ADR-002 Status 段補 Erratum 註記 — 示意樹漏列 `SharedKernel.Tests`，樹為示意非決策本體，原文不改。
