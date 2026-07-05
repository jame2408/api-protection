# Loop Engineering 缺口報告（2026-07-05）

> 觸發：使用者 review 判定「還不是完全閉環——仍有機械化缺口、文字規則假裝成防線、jsonl 只是記錄不是學習」。本檔為 Phase 1（盤點）＋ Phase 2（診斷）產出；**Phase 3 動手前需使用者逐條裁決 §5**。取證時點：7/44 場景、`scripts/ci-checks.sh fast` 全綠、commit `173528f`。

---

## 1. 四迴圈健康度總覽

| 迴圈 | 狀態 | 一句話判定 |
|---|---|---|
| 執行迴圈 | **閉** | Red→Green＋故意紅義務已成慣例；近 5 個 scenario commit 全部與 `tasks/bdd-progress.md` 同 commit（git log 實查）；`@ignore` 餘 37 與帳面 7/44 一致；fast checks 現況全綠。 |
| 防線迴圈 | **大致閉，餘 6 個紅燈** | 三層皆存在（pre-tool hook / pre-commit fast / pre-push+CI full），fast ⊂ full 不變式成立，machinery-check 自體健檢在線。紅燈見 §2、§3。 |
| 規範演化迴圈 | **閉** | ADR 唯一通道＋governance clause＋adr-lint；被拒挑戰有紀錄（如 `.Value` 條明文裁決不機械化）；豁免明文（lint 內排除清單）。 |
| 學習迴圈 | **半開** | lessons 端閉環（Active/Archived 分區、注入、triage 常設觸發、落地欄）；但 (a) jsonl 通道 write-only：訊號有、決策/落地/回寫三段全缺；(b) 兩條 Active lesson 落地懸空。 |

## 2. 死規則與半防線清單（文字假裝成防線）

依「違規頻率 × 修復成本」排序：

| # | 規則 | 現況 | 假裝程度 |
|---|---|---|---|
| D1 | 「NEVER 一次移除多於一個 `@ignore`」 | CLAUDE.md 列為 **CRITICAL**，自承「人工紀律，無機械化防線」 | 高——CRITICAL 級卻是四條 Non-Negotiable 中唯一零防線者 |
| D2 | 「`bdd-progress.md` 更新必須與實作同 commit」 | 純文字；近 5 commit 靠手守住（實查合規），佇列說謊風險仍在 | 高——是執行迴圈「佇列不說謊」的關鍵不變式 |
| D3 | 「每個 Guard 須有正＋負場景」 | 矩陣無防線區明列「未追蹤」，無 `.feature` 比對腳本 | 中——矩陣已誠實標注，未假裝；但也從未裁決「做或不做」 |
| D4 | Refactor 紀律（production-only / test-only 不混改） | 純文字；合法例外多（契約變更、介面改名同 commit），naive lint 必誤報 | 低——機械化成本高於效益，候選「明文裁決不機械化」 |
| D5 | 「backlog→progress 只能由使用者晉升」 | 純文字；「誰改的」本質不可機械化 | 低——只能靠 review，建議明文承認 |
| D6 | 矩陣同步義務（機制異動同 commit 登記） | machinery-check 只驗「矩陣指的檔案存在」，不驗反向「新機制有登記」 | 中——半防線；漏登記靠人記 |

已排定、非本次缺口：效能 P99/RPS 兩條（ADR-017 Rule 6 綁 validation slice DoD）；「commit 層不跑測試」為 Docker 成本下的明文設計（push/CI 補齊），非假防線。

## 3. jsonl 診斷（使用者第三點：記錄 ≠ 學習）

- **`.claude/failures.jsonl`**（24 筆）：ADR-008 明文定為 pure forensic，且 Implementation Rule 5 **禁止**注入機制讀取。訊號在收，但無任何 triage 消費者——迴圈斷在「決策」段。實際樣本已顯示可學習的重複 pattern（如 zsh 下 `echo ===` 被解析成 `== not found`，三筆同型）。
- **`.claude/observations.jsonl`**（3,134 筆，無界成長）：記錄**每一次**工具呼叫含完整檔案內容，零讀者、零輪替。與 harness transcript 重複，純付成本。
- 升級為回饋迴圈需動 ADR-008 的定位條款 → 依 governance clause 走新 ADR（或修訂），不可默改。
- **張力申報**：lessons [decision]「制度凍結——新機制只能事故驅動」。本報告視使用者本次 review 為觸發事故；但 §5 各項是否過「觀察到的失敗」門檻，由使用者逐條認定。

## 4. 建議動作（ROI 排序，Phase 3 候選）

1. **jsonl 回饋化（小 ADR 修訂 ADR-008）**：`observations.jsonl` 除役（或 gitignore＋定期清空）；`failures.jsonl` 保留＋新增 `scripts/failure-triage.sh`（按 tool×error 簽名分組、報重複 pattern），phase 收尾／checkpoint 落盤時跑一次，重複 pattern 依既有 lesson 流程落地。工作量：ADR 修訂＋約 40 行腳本＋checkpoint 模板一欄。直接回應「記錄≠學習」。
2. **BDD 紀律兩條機械化（D1＋D2，一段 lint）**：pre-commit 檢查 staged diff——(a) `.feature` 檔被移除的 `@ignore` 行數 ≤ 1（多於 1 需行內豁免標記，對應「相同 step definitions」例外）；(b) 有移除 `@ignore` 時 `tasks/bdd-progress.md` 必在同一 staged set。約 30 行，入 `source-lint.sh` 或獨立 `bdd-lint.sh` 接 fast；綠＋故意紅驗證；矩陣同 commit 登記。
3. **殘餘假防線正名（零腳本）**：D3 裁決「機械化或明文不機械化」（比照 `.Value` 條寫進矩陣）；D4/D5 在矩陣無防線區補「裁決不機械化」行；D6 若不擴 machinery-check 就明文承認半防線。順手收兩條懸空 lesson 落地（見 §5 Q4）。

## 5. 待裁決（Phase 3 開工前逐條決定）

- **Q1 jsonl 處置**：(a) 建議案＝observations 除役＋failures triage 化（動作 1）；(b) 兩者皆除役，誠實承認無此迴圈；(c) 維持現狀。
- **Q2 D1/D2 機械化**：做（動作 2）或不做？制度凍結張力點：兩條至今**零觀察違規**，機械化屬「CRITICAL 卻裸奔」的預防性投資——是否過事故門檻由你認定。
- **Q3 D3 Guard 正負場景**：寫 `.feature` 比對腳本（中成本）或明文裁決不機械化（比照 `.Value`）？
- **Q4 懸空 lesson 收口**：token 經濟條款「納入下一個憲章修訂」至今未修——現在併入本輪 ADR 一次收掉，或改寫落地欄為「session-init 注入即防線」？（另一條「spec 背景欄求證」同型，落地欄僅指向 lesson 自身。）
- **Q5 報告本檔去留**：裁決落地後本檔應歸檔至 `tasks/archive/` 或刪除（結論屆時已活在 ADR／矩陣／lint 內，依 SSOT 不留第二真相源）。

## 6. Delivery Manifest

- 目標 artifact：本檔（`tasks/loop-audit-2026-07-05.md`）——已成功寫入，內容完整。
- 同批：`tasks/checkpoint.md` 待裁決欄補一行指針。
- 未 commit：依 Working Agreement，等使用者裁決 §5 後由後續 session 一併處理。
- 恢復方式：新 session 讀 `tasks/checkpoint.md` → 本檔 §5。
