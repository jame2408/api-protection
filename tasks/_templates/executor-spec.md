# Executor Spec 範本

> 用途：orchestrator 派工給 executor 的任務規格標準格式（`docs/orchestration.md` §2 Executor Contract 的派工端配套）。
> 原則：能機械化的驗證一律由 spec 給死可執行指令與取證方式，不讓 executor 自行判斷「怎麼證明」；回報要求精確事實（路徑、指令輸出原文），不接受概括摘要。
> 使用方式：複製下列欄位逐欄填寫。回報欄位的定義單一來源在 `tasks/_templates/checkpoint.md`，此處只放指針不複寫。

---

## 任務

<一句話任務目標>

## 背景（orchestrator 已核實的事實）

- **需求類型**：<新功能啟用／既有行為變更／缺陷再現／行為移除／純重構／非功能 — 分流見 `docs/adr/adr-022-bdd-requirement-type-routing.md` §1>
- <事實 1（含檔案位置或指令輸出）— executor 勿重複調查>

> 凡涉及執行期值（預設值、null 與否、初始狀態）的敘述，必須讀該欄位／屬性的宣告與初始化行求證；核實深度以「executor 可直接照抄判斷式」為準。
> 凡啟用「測試後段 guard」的 BDD 場景：本欄必須列出 handler guard 順序，並逐一核對該場景請求形狀（URL、payload 常值）與 seed 狀態能通過目標 guard 之前的每一道 guard；佔位常值（如 `"any:read"`）視同執行期值求證。
> 凡列舉型敘述（caller 數、樣板出現點清單、「恰 N 處」、**「落點唯一」「只有一處」等存在性數量斷言**）：必須以 `grep -n` 對全檔／全 repo 取證計數後才可寫入，不得憑部分閱讀列舉——漏列會迫使 executor 在「照 spec 留孤兒引用致編譯失敗」與「自行擴大範圍」之間二選一（2026-07-12 重構 pass 實例：spec 列 5 個 caller、實為 7；2026-07-26 ADR-029 實例：spec 寫「grep 確認落點唯一」實為 2 處，其中一處早已是目標格式）。
> 凡場景有**多個 Given 疊加在同一個 alias 上**：spec 必須明示後續 Given 的組合方式為「**覆寫既有實體**」（tracked 重查後設 `CurrentValue`），並禁止「再呼叫一次 seed helper」——後者會留孤兒列並把 alias 字典改指新列，丟棄前一個 Given 已建立的狀態與關聯，使場景在**錯誤前提下變綠**（2026-07-26 Wave 8 實例：spec 只講單一 Given 內的 seed、未交代組合方式，executor 選了重新 seed，會讓下一條 Rotating 場景實際驗證一把 Active 金鑰）。
> 凡 BDD 派工宣稱「新 step」：每一條 step 文字必須逐字 grep `backend/tests/FunctionalTests/Steps/` 證明無既有 binding 才可列為新增——同文 pattern 在新檔重複宣告是 Reqnroll ambiguous binding，且既有實作的語意可能與新場景衝突（2026-07-12 C9 實例：「觸發主動快取失效」已存在於 RevokeKeySteps 且硬讀 ResponseBody，spec 誤列新 step 致紅 A 計數不符觸發停止）。

## 允許改動的檔案集（嚴格限定）

- <檔案 1 — 允許的變更範圍>

> 需要改動檔案集以外的檔案 = 觸發全域停止條件（範圍超出），停止該部分並回報，不得自行擴大。

## 步驟（含取證指令）

1. <步驟 — 凡驗證類步驟，直接給完整可執行指令與要擷取的輸出行，例：`dotnet test backend/tests/FunctionalTests/ --logger "console;verbosity=detailed"`，回報需含目標場景行與總結行原文>

> **取證指令本身也是 spec 的一部分，寫入前須確認在本 repo 真的可用**——指令錯誤會讓 executor 誤判結果或白跑一輪。已知三個坑（皆實測）：(a) `--filter "FullyQualifiedName~<英文名>"` **無效**，Reqnroll 產生的 Feature class 名是中文（如 `輪替金鑰Feature`、`金鑰驗證DataPlaneFeature`）——要過濾就用中文名，否則不加 filter 跑全量再 grep；(b) 測試**總結行格式依 logger 而異**：不帶 `--logger` 是 `Passed!  - Failed: N, Passed: N, ...`，帶 `--logger "console;verbosity=detailed"` 是 `Total tests: N` / `Passed: N`，且 Failed／Skipped 為 0 時該兩行**完全不印**；(c) 取證 grep **勿用 `head -N` 截斷多條件 OR 輸出**——xUnit 平行輸出交錯會漏抓，改導全量到檔再逐段核對。

## 故意紅（適用時必填）

<若本任務的測試未經 Red 直接 Green（例如啟用型場景：slice 已存在、移除 `@ignore` 即綠），必須指定 mutation 驗證：暫時破壞哪個 guard 或斷言期望值 → 跑哪個指令取得紅的原始輸出 → 還原 → 再跑確認回綠。紅與綠兩份輸出都列入回報，且還原後 `git diff` 須確認 production 檔案無殘留改動。不適用時寫「不適用：<理由>」，不得留空。>

> **mutation 選點紀律**：必須挑「**只讓本場景紅、姊妹場景仍綠**」的點，並要求回報同時列出兩者輸出——否則證明的只是共用路徑活著，不是本場景的獨立鑑別力。挑點方法：找**只服務本場景語意**的述詞（如查詢加一條該場景專屬的排除條件），避開服務所有姊妹場景的泛型段落（如共用的 previousStatus capture）。**多條 AND 述詞的防線，一次只移除一條**——一次移除多條只能證明合取被守住，分不出哪條測試守哪條防線（2026-07-26 終態濾條實例：M1 只移除 `!= Revoked`、M2 只移除 `!= Expired`，各自精確命中對應場景）。

## 停止條件

- `docs/orchestration.md` §3 全域停止條件（連續失敗 3 次、規格模糊、範圍超出、context 將耗盡）。
- <本任務特有停止條件，例：測試紅且原因指向檔案集外 → 記錄診斷作為 blocker 回報，不得自行修 production code>

## 重構評估（BDD scenario 派工必填）

> 對應 `.claude/skills/bdd-vertical-slice/SKILL.md` 步驟 9 / Refactor Checklist。兩側各自獨立判斷、各自獨立 pass（先一種、確認 Green、再另一種，絕不混改）：

- **Production 側**（`backend/src/`）：需要重構的具體項目清單，或明確寫「無，理由：<原因>」。
- **Test 側**（`backend/tests/`）：需要重構的具體項目清單，或明確寫「無，理由：<原因>」。

判斷結果須寫入 enablement commit message 的 `Refactor-assessment:` trailer（`scripts/git-hooks/commit-msg` 機械化強制，staged net `@ignore` 移除 ≥ 1 時觸發）；「不重構」也是判斷，必須留痕，不得省略。

## 回報格式

1. checkpoint 欄位（定義見 `tasks/_templates/checkpoint.md`，不複寫）。
2. 證據原文：spec「步驟」與「故意紅」欄指定的每條取證指令之關鍵輸出行。
3. **非 blocker 的不順與繞路**：指令重跑、輸出出乎意料、規格歧義或錯字造成的遲疑、多餘的查找等，逐條列實際發生的事（含指令與現象）；沒有就寫「無」，不得省略本欄。
