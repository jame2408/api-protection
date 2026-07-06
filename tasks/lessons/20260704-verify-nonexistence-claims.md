---
date: 2026-07-04
type: correction
status: active
---

# 「不存在」的斷言也要機械化驗證 — 矩陣誤報 .editorconfig 不存在

**Context:** 驗證矩陣與 plan 宣稱「repo 無 .editorconfig」，實際 backend/.editorconfig 存在（executor 只查 repo root，orchestrator 抽驗也未抓到）。「存在性」核對清單只驗證了「列出的檔案存在」，沒驗證「宣稱不存在的東西真的不存在」。
**Rule:** 寫「X 不存在」的結論前，必須用遞迴搜尋驗證（如 find . -name 'X' 或 git ls-files '**/X'），不能只看單一目錄。
**落地:** 矩陣與 plan 勘誤（本commit）；本條 lesson。
