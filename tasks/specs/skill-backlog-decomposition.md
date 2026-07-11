# Skill Spec：backlog-decomposition（場景 → wave 切分）

> 上游地圖缺口 6 的實作規格（`tasks/upstream-map.md`）。目標讀者：後任協調者與 executor。可全 AFK 驗證（golden trace 俱在 repo），**建議作為首個實作對象**，先證明「spec→skill→回放驗證」路徑可行。

## 1. 定位與邊界

**解決的斷鏈**：46 場景到 `tasks/bdd-progress.md` wave 表＋基礎設施解鎖點表的切分是手工判斷，無 skill 承載；下次有一批新場景（Discovery 解凍後）將無章可循。

**明確不做**：
- **不升格**：backlog → progress 的升格與插入位置仍由使用者執行（CLAUDE.md 紅線「Only the user promotes backlog → progress」不變；本 skill 產出的是**建議排序**，落檔為提案文件，不直接改 `bdd-progress.md`）。
- 不產出場景、不改 `.feature` 內容（檔名前綴調整屬升格步驟，仍歸使用者）。

## 2. 輸入／輸出契約

**輸入**：一批場景（feature 檔或候選場景清單）＋`docs/design/context-integration-spec.md`＋`docs/detailed-design/` 對應 BC 檔。

**輸出**（單一提案文件）：
1. **Wave 表**：欄位對齊 `tasks/bdd-progress.md` 現行格式——文件／Wave／Feature／場景數／涉及 BC／特殊需求。
2. **基礎設施解鎖點表**：欄位「時點（哪個 Wave 開始前）／需建立的基礎設施」。
3. **檔名數字前綴建議**：使字母序＝wave 順序（現行機制：`grep @ignore | sort | head -1` 找下一場景，依賴此不變式）。
4. **切分理由**：每個 wave 一行 gist，說明依賴根據。

## 3. 切分準則（從 golden trace 逆向提煉，寫進 SKILL.md）

1. **BC 依賴與事件流排序**：被消費事件的生產者場景先行；跨 BC 消費場景排在生產者 wave 之後（依 `context-integration-spec.md` 的事件契約判定）。
2. **基礎設施解鎖點前置識別**：橫切需求（migration、認證 token、時鐘控制、外部 fake）必須被識別並明列於**首個使用它的 wave 之前**——靜默吞掉＝本 skill 的頭號失敗模式。已知先例：Wave 1 前 EF Migration＋Respawn、Wave 3 前 AuthToken（ADR-024）、Wave 5 前 FakeClock（`ISystemClock`）。
3. **同 feature 聚 wave、happy path 先於 guard 負場景**：首場景建立垂直切片骨架，負場景多為 test-only 啟用（先例：RevokeKey Wave 2 內部順序）。
4. **wave 大小以「單場景單 session」為前提**：wave 是排序單位不是派工單位，切分不需顧慮 wave 內場景數上限。

## 4. 驗證設計（全 AFK）

- **綠（回放）**：以 46 場景 feature 檔＋兩份設計文件為輸入跑 skill，產出與現行 `tasks/bdd-progress.md` wave 表**結構比對**：BC 依賴序不得矛盾；三個已知解鎖點（Migration／AuthToken／FakeClock）為**最低命中集**，漏任一＝紅；wave 歸屬差異允許，但需逐條給出可解釋理由（skill 可以切得比手工更好，不可漏依賴）。
- **故意紅**：只餵入 `05_RotateKey.feature`（寬限期場景依賴時鐘控制）而不提供任何時鐘基礎設施線索，skill 未輸出 FakeClock 類解鎖點＝紅；同法可用 AuthToken（餵入含 actor 斷言的場景子集）做第二軸。
- 取證：回放與故意紅的完整輸出存 scratchpad 並附於派工回報，比對結論逐條列明。

## 5. 落點與機械化慣例

- `.claude/skills/backlog-decomposition/SKILL.md`＋`.agents/skills/` symlink（ADR-023 慣例）；`scripts/machinery-check.sh` skill links 須綠。
- 語言規範沿用 `requirements-analysis-design` 的 Language & Terminology 段。
- description 觸發詞避開「新場景產出」語彙（凍結條款不歸本 skill；輸入是**既存**場景集）。

## 6. 派工註記

- 用 `tasks/_templates/executor-spec.md` 派工；明列 active lessons 讀取義務。
- 單一 executor session 可完成（skill 撰寫＋回放＋故意紅）；預算參考近期 slice（~100K tokens）。
