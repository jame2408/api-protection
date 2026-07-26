---
date: 2026-07-04
type: correction
status: active
---

# 「不存在」的斷言也要機械化驗證 — 矩陣誤報 .editorconfig 不存在

**Context:** 驗證矩陣與 plan 宣稱「repo 無 .editorconfig」，實際 backend/.editorconfig 存在（executor 只查 repo root，orchestrator 抽驗也未抓到）。「存在性」核對清單只驗證了「列出的檔案存在」，沒驗證「宣稱不存在的東西真的不存在」。
**Context 追加（2026-07-26，同型第二例）:** 使用者中斷了一次 executor 派工，orchestrator 未跑 `git status` 就向使用者宣稱「executor 一步都沒跑，工作區停在 <hash>」。實際上該次中斷已留下部分寫入（feature 的 `@ignore` 已移除、steps 檔已含新 Given／helper／When 擴充，唯獨新 Then 未落地），下一輪 executor 開工才發現，須先 `git stash` 取乾淨基線才能產生可信的紅 A 計數。**中斷不等於零副作用**：subagent 在取消前已完成的檔案寫入會留在工作區。
**Rule:** 寫「X 不存在」「沒有改動」「乾淨」這類**否定斷言**前，必須先跑機械化驗證再說——檔案存在性用遞迴搜尋（`find . -name 'X'`／`git ls-files '**/X'`），工作區狀態用 `git status --short` 與 `git diff --stat`。「我沒有下令做 X」不是「X 沒有發生」的證據；被中斷、失敗、或部分完成的操作都可能已留下副作用。
**落地:** 矩陣與 plan 勘誤（`20260704`）；2026-07-26 追加工作區斷言條款與中斷副作用案例（場景 47/48 輪）。
