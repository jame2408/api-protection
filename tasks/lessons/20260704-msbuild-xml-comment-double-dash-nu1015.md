---
date: 2026-07-04
type: info
status: archived
---

# Directory.Packages.props 內 XML 註解含 `--` 會被 MSBuild 靜默跳過（NU1015）

**Context:** 冷啟動 executor 落地 todo #36 時，props 檔首次寫入後 `dotnet restore` 全面 NU1015（找不到版本）。root cause 出乎意料：XML 註解內寫了 `--force`，而 XML 註解不得出現 `--`，整份檔案被判定 invalid 後**靜默跳過匯入**，不是 fail-fast 報錯。`-v:diag` 才看得到 "file being invalid"。
**Rule:** MSBuild props/targets 檔的 XML 註解內禁用雙連字號（含 CLI flag 範例如 `--force`）；遇到「集中設定像不存在一樣」的症狀，先用 `xml.dom.minidom.parse` 驗證檔案合法性再查其他方向。
**落地:** 防線＝`scripts/source-lint.sh` MSBuild XML 合法性段（本 commit，對 `git ls-files '*.props' '*.targets'` 逐檔跑 `xml.dom.minidom.parse`）；`backend/Directory.Packages.props` 註解已改寫為無 `--` 版本（commit `1dc717b`）。
