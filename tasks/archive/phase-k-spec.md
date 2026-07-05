# Phase K 任務規格 — tasks/ 目錄歸檔（executor 級：Sonnet，小包）

> ADR-013 決策 (a) Tier 3 的落地：完成品移出工作目錄，降低掃目錄雜訊。純檔案搬移與收攏，一個 commit（`chore(archive):`）+ push + CI 確認。

## 規則

- 誠實申報；只做下列三項；**不改任何檔案內容語意**（搬移與收攏除外）。
- 搬移一律 `git mv`（保 blame）。

## 交付物

1. **已完成 phase specs 歸檔**：`tasks/phase-{a,b,c,e,f,g,h,i,j}-spec.md` → `tasks/archive/`（`git mv`）。本檔（phase-k-spec.md）完成後也一併移入。
2. **修指針**：全 repo `grep -rn "phase-[a-z]-spec" --include="*.md"` 逐一把指向舊路徑的引用改為 `tasks/archive/...`（歷史敘述行如 plan §8.2 的 commit 紀錄照改路徑即可，不改敘述文字）。`machinery-check` 只掃三份規範文件，其餘要靠這個 grep 自查 — 報告列出所有修改處。
3. **todo.md 收攏**：檔尾新增 `## Archived（已結案）` 區，把已標 ✅/~~刪除線~~ 的結案項整段搬入（內容原文不動）；未結案項留在原位。報告列出搬移的項次編號。

## 驗證

`bash scripts/ci-checks.sh fast` 綠（含 machinery-check）；`grep -rn "tasks/phase-[a-z]-spec" --include="*.md" .`（排除 archive/ 自身）0 命中舊路徑；commit + push + `gh run list --branch main` 最新 run 綠。
