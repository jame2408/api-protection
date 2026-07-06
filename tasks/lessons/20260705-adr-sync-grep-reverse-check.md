---
date: 2026-07-05
type: correction
status: active
---

# ADR 改寫被引用文字時，同步項目須 grep 反查「逐字引用者」

**Context:** 全 repo 衝突掃描發現 `docs/verification-matrix.md` 有 5+ 處逐字引用 ADR-013 瘦身前的舊 CLAUDE.md §4 文字（grep 全數 0 命中），另 checklist 本體位置說法錯誤 — 根因是 ADR-013 改寫 CLAUDE.md 時，「同步項目」清單只列了被改的檔案，沒列「逐字引用被改文字的文件」。同型還有：新 lint 上線（source-lint CreateScope 段）未反查 rule 檔既有範例是否會被攔（di.rule.md §D ✅ 範例直接違規）。
**Rule:** 任何修改「會被其他文件逐字引用的文字」（CLAUDE.md 條文、ADR 決策句、lint 豁免範圍）時，同步項目清單必須先 grep 反查引用者（含 `docs/verification-matrix.md` 與 `.claude/references/`）並列入同 commit；新 lint 上線前反查 rule 檔的 ✅ 範例是否落在禁令內。
**落地:** 本次修繕 commit `be0152e`；本條 lesson。
