# tasks/archive — 完成品歸檔（ADR-013 決策 (a) Tier 3）

一檔一紀錄、扁平存放、**只進不改不搬**：多份 Accepted ADR 以 `tasks/archive/<檔名>` 逐字引用本目錄檔案，且 ADR-021 明文「歷史文件不回改」——分類由檔名前綴承載，不用子目錄。

| 前綴 | 性質 | 例 |
|---|---|---|
| `phase-*-spec` | 已執行完畢的 executor 派工規格（可重派指令包） | `phase-a-spec.md` |
| `loop-audit-*` | Loop engineering 巡檢報告（日期戳） | `loop-audit-2026-07-10.md` |
| `stryker-baseline-*` | 量測基線快照（日期戳） | `stryker-baseline-2026-07-05.md` |
| `todo-closed-*` | todo.md 歸檔 pass 移出的已結案內容（逐字保留，供舊編號引用） | `todo-closed-2026-07-10.md` |

新類型完成品：取一個能自我分類的前綴＋日期戳，直接放本目錄。
