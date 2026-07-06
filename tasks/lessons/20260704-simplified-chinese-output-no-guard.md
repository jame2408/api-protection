---
date: 2026-07-04
type: correction
status: archived
---

# Executor 產出含簡體字 — 「禁簡體」規則存在但無機械化防線

**Context:** Phase A executor（Sonnet 級）在 adr-007 Rationale 寫出「执行」。禁用簡體是全域層級規則，但 repo 內無明文、無 lint，任何 executor（尤其非 Claude harness）都可能重犯；本次靠 orchestrator review 的簡體字元掃描才攔下。<!-- zh-lint:allow：本行刻意引用違規字元 -->
**Rule:** Review executor 產出的中文文件時，必須跑一次簡體字元掃描；接受 executor 報告「驗證全綠」不等於內容合規 — 報告只覆蓋它被要求跑的檢查。
**落地:** adr-007 修正（commit `d8a006b`）→ 2026-07-04 同日完成機械化：`docs/adr/adr-009-traditional-chinese-and-zh-lint.md` + `scripts/zh-lint.sh`（OpenCC 字表，接入 ci-checks fast+full）。過程中手寫掃描清單兩度漏字、orchestrator 本人 commit message 也違規一次 — 證明此類字元級規則必須用完整字表機械化，人工檢出不可靠。
