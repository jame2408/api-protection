---
date: 2026-07-05
type: correction
status: active
---

# 故意紅的還原手法取決於目標檔案是否已 commit — 未 commit 的檔案禁用 git checkout/restore 還原

**Context:** RevokeKey 場景故意紅後，orchestrator 以 `git checkout -- ApiKey.cs` 還原 mutation — 但該檔載有 P2 executor 未 commit 的 `Revoke()` 方法，checkout 恢復到 HEAD 把 mutation 與 executor 工作一併洗掉，被迫依規格重建並以測試證明等價。先前場景的同手法安全，純因當時 production 早已 commit — 手法的前提條件從未被明文。
**Rule:** 對「工作區有未 commit 改動」的檔案做暫時 mutation，還原一律走快照法：mutate 前 `cp` 原檔至 scratchpad，還原用 `cp` 覆寫回來；`git checkout/restore -- <file>` 只允許用於「該檔相對 HEAD 無未 commit 改動」的情境，動手前先 `git diff --stat <file>` 確認。
