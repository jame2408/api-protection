# api-protection-v2 閉環工程盤點報告（Loop Engineering Audit）

- **盤點日期**：2026-06-13
- **盤點範圍**：制度面材料（CLAUDE.md、docs/adr/、.claude/、scripts/、tasks/、backend/tests/）＋ 對程式碼抽樣驗證規則遵守度
- **方法**：loop-engineering skill Phase 1（盤點）＋ Phase 2（診斷）。**純分析，未修改任何規則 / 程式碼 / 測試**；本報告為新增交付物。

---

## 核心結論（TL;DR）

**這個專案是典型案例的反面。** 多數專案爛在外圈（規範演化、學習迴圈缺席），這裡的**規範演化迴圈是四環中最閉的**——ADR 單一通道、governance clause、adr-lint、pre-commit 串接，六份 ADR 全 Accepted、CLAUDE.md 與 references 高度自洽。規則本身是精品。

問題在**防線迴圈**：CLAUDE.md 與 ADR 宣告了十餘條架構硬規則（BC 隔離、Repository 不回 Result、ILogger 僅邊界、Service 必回 Result、禁 throw、禁 `new Failure(`、Failure shape 鎖定、`cancel` 命名、禁 BC-to-BC 引用），**這些規則目前的機械化檢驗數 = 0**。`backend/tests/Architecture.Tests/` 是空殼（只有 `obj/` 建置產物，零 `[Fact]`）。程式碼現在之所以乾淨（抽查 `new Failure(` 僅 `FailureProvider.cs` 工廠本身、`ILogger` 僅 `UnhandledExceptionMiddleware` 邊界、17 處 `CancellationToken cancel` 零違規），是因為剛做完一輪人工 hardening——**靠記憶力撐著，不是靠機器**。

`tasks/process-improvement-plan.md` 自己已經預言並開好藥方（防線 A：NetArchTest；防線 B：hooks），但**藥方大半未抓**。這份 plan 本身是一條巨大的、懸空的 lesson：寫了，沒落地。

一句話：**規則是精品、演化通道是模範，但守護這些規則的防線是一具空殼。半年後它會漂移，而 plan 早就算到了。**

---

## 1. 四迴圈健康度總覽

| 迴圈 | 狀態 | 一句話診斷 |
|---|---|---|
| 執行迴圈（內圈） | **半閉** | BDD kanban 紀律良好（`bdd-progress.md` 2/44、一次只拿掉一個 `@ignore`、進度與實作同 commit）。但「主幹永遠 Green」沒有任何機器背書——無 CI，suite 綠不綠全靠本機自律。 |
| 防線迴圈（三層） | **幾乎全開** | 對**架構規則**：三層皆空（無 editor/agent hook 攔截、Architecture.Tests 空殼、無 CI）。唯一存在的機械化是 adr-lint（範圍僅 `docs/adr/`）＋ dotnet format（`.editorconfig` 存在）。十餘條核心規則零檢驗。 |
| 規範演化迴圈（ADR） | **閉**（四環最佳） | ADR 單一通道 + 模板 + governance clause + adr-lint + pre-commit 串接齊備；6 份 ADR 全 Accepted、含「同步項目」。罕見的健康外圈。**唯一裂縫**：被拒絕的挑戰沒有獨立紀錄區（lessons 三類模板有，但實際 0 條）。 |
| 學習迴圈（外圈） | **開** | `tasks/lessons.md` 僅 1 條，且早於整輪 hardening。`process-improvement-plan.md` 是一份內容極厚的 retro，但它提的防線 A/B/C 大半未建——是「懸空 lesson」反模式的教科書案例：retro 很厚，沒有一條落地成檢驗。 |

---

## 2. 死規則清單（架構規則：規則精良，但檢驗為零）

逐條填防線盤點表。這裡的「死」不是規則爛，而是**零機械化檢驗**——違規不會讓任何東西變紅，純靠人/agent 記憶力。來源：CLAUDE.md「Verification Standards / Non-Negotiable Constraints」＋ ADR-003～006。

| # | 規則（出處） | 寫的當下 | commit 前 | review/CI | 現狀 | 風險 |
|---|---|---|---|---|---|---|
| 1 | BC 之間禁直接引用，只走 SharedKernel（CLAUDE.md, ADR-003） | ❌ | ❌ | ❌ | 抽查目前合規 | **極高 × 極高**（跨 BC 耦合最難回收） |
| 2 | Repository 不回 `Result<T,Failure>`（plan #20） | ❌ | ❌ | ❌ | 合規 | 高 × 中 |
| 3 | Service 必回 `Result<T,Failure>`，禁 throw 業務邏輯（CLAUDE.md, ADR-004） | ❌ | ❌ | ❌ | 合規 | 高 × 高 |
| 4 | ILogger 僅邊界（Endpoint/Middleware/Pipeline/BG），禁注入 Service/Domain/Handler（ADR-004） | ❌ | ❌ | ❌ | 合規（僅 `UnhandledExceptionMiddleware`） | 高 × 中 |
| 5 | 禁 `new Failure(`，一律 `FailureProvider.CreateFailure()`（CLAUDE.md） | ❌ | ❌ | ❌ | 合規（僅工廠內 1 處） | 中 × 低（grep 一行可擋） |
| 6 | `Failure` shape 鎖定，禁加欄位（ADR-004 §4） | ❌ | ❌ | ❌ | 合規 | 中 × 高（破壞 wire-format 穩定性） |
| 7 | `CancellationToken cancel` 命名（CLAUDE.md） | ❌ | ❌ | ❌ | 合規（17/17） | 中 × 低 |
| 8 | 禁 bare-string failure code，用常數（plan） | ❌ | ❌ | ❌ | 未驗 | 中 × 低 |
| 9 | Primary Constructor 為預設 DI（ADR-005） | ❌ | ❌ | ❌ | 合規 | 低 × 低 |
| 10 | Wire-format RFC 9457 / ApiKeyStatus PascalCase（ADR-006） | ❌ | ⚠️ | ❌ | 部分（見下） | 高 × 中 |
| 11 | Handler 單元測試覆蓋率 ≥ 80%（CLAUDE.md） | ❌ | ❌ | ❌ | 未量測 | 中 × 中 |

**統計**：CLAUDE.md / ADR 明列的可機械化架構規則 **11 條，機械化檢驗 0 條**（adr-lint 不檢查任何一條，它只檢查 ADR 文件結構本身）。

**活生生的 drift 證據**：`tasks/todo.md #35` — ADR-006 把生命週期字面值統一成 PascalCase，但 acceptance grep 的範圍漏了 `tasks/`，於是 `bdd-progress.md:36` 殘留一個 `ROTATING`。**這正是「acceptance command 範圍不全 → drift 從縫隙溜過」的實例**——規則對、人也努力了，但沒有持續性機器檢驗,單次 grep 的盲區就是漏洞。

---

## 3. 缺口診斷表

| 實踐 | 迴圈 | 斷在哪 | 證據 | 風險（頻率 × 成本） | 建議補法 |
|---|---|---|---|---|---|
| 11 條架構規則的守護 | 防線 | **無機械化** | Architecture.Tests 空殼（零 `[Fact]`）；無 NetArchTest 引用 | **極高** | 拉起 NetArchTest，先做 plan 點名的最高 ROI 三條：BC 隔離、Repository raw return、Service 必回 Result（含 `SharedKernel/Contracts` 豁免） |
| 任何檢驗的遠端執行 | 防線＋執行 | **無訊號** | 無 `.github/`、零 CI workflow | **極高** | 加 CI：`dotnet build` + `dotnet test`（含 Architecture.Tests）+ `dotnet format --verify-no-changes` + adr-lint。沒有 CI，所有本機檢驗都靠自律 |
| pre-commit hook 未安裝 | 防線 | 無訊號 | `.git/hooks/` 只有 sample；hook 存在於 `scripts/git-hooks/` 但未 install | 中 × 極低 | `scripts/install-git-hooks.sh` 跑一次；或在 CI 兜底（hook 可被 `--no-verify` 繞過，CI 才是硬門檻） |
| `new Failure(` / bare-string code / `cancel` 命名 | 防線 | 無機械化 | 規則存在、檢驗不存在 | 中 × 極低 | grep-based CI step 或 Roslyn analyzer；plan 已列為「新加」 |
| agent 寫碼當下的攔截 | 防線（第一層） | 無機械化 | plan 提的 `pre-tool-edit.py`（B2）不存在；`session-init.sh` 只注入 lessons,未注入 must-read 規則清單（B1 未做） | 中 × 低 | PreToolUse hook 攔 `new Failure(`/`throw`/`ILogger<` 於 Service 路徑；SessionStart 注入 must-read |
| review 抓到 → 回寫成檢驗 | 學習 | **無學習回寫** | lessons.md 僅 1 條且過時；plan 是厚 retro 但防線大半未落地 | 高 × 低 | 把 plan §2 防線 A 拆成可勾選 todo，每落地一條就在 lessons 記一條「意外發現→新檢驗」 |
| 被拒絕的規範挑戰紀錄 | 規範演化 | 無學習回寫 | lessons 三類模板齊備但 0 條；被拒挑戰無處沉澱 | 低 × 低 | 規範挑戰（無論接受/拒絕）都記一條 lesson，指向 ADR 或強化檢驗 |
| ADR acceptance command 範圍 | 防線 | 無機械化（範圍盲區） | todo #35 的 `ROTATING` 殘留 | 中 × 低 | acceptance grep 涵蓋全 repo（含 `tasks/`），或改成 CI 持續檢驗而非單次 grep |

---

## 4. 建議的前三個動作（依 ROI 排序）

> 排序原則：先補「違規不會變紅」的最大缺口（防線），因為規則已經是精品、演化通道也健康——唯一缺的就是讓規則自動被守住。

### 動作 1：拉起 Architecture.Tests，機械化最高 ROI 的三條架構規則（plan #20）

`process-improvement-plan.md` 已點名最高 ROI 三項：**BC 隔離、Repository raw return、Service 必回 Result**（含 `SharedKernel/Contracts` 豁免）。NetArchTest 是現成工具，這三條擋下未來 ~80% 的 drift。**每條建好後必須實跑一次綠、再故意違規一次確認會紅**——空殼測試比沒有更糟，它給「有防線」的錯覺。

### 動作 2：建立 CI（這是所有檢驗的遠端兜底）

無 CI 是當前最大的系統性開環：Architecture.Tests 即使拉起來，沒有 CI 也只在本機自律時才跑。最小 CI：`dotnet build` + `dotnet test` + `dotnet format --verify-no-changes` + `scripts/adr-lint.sh`，設為 merge 硬門檻。順手把 `scripts/install-git-hooks.sh` 寫進 onboarding（或 README setup 第一步）。

### 動作 3：把 process-improvement-plan 從「懸空 retro」轉成「可勾選的落地佇列」＋ 重啟 lessons

plan §4 的 Phase 2/3 拆成 `tasks/todo.md` 可勾選項，每落地一條防線就在 `lessons.md` 補一條「意外發現 → 新檢驗」並指向該 commit。順手清掉 todo #35 的 `ROTATING` 殘留。這一步把學習迴圈從「寫了沒落地」接回閉環。

**Definition of Success**：Architecture.Tests 至少 4 條 NetArchTest 全綠且各有一次「故意紅」驗證；CI 在主幹永遠 exit 0（除明文豁免）；`grep -rn "ROTATING" tasks/` = 0；lessons.md 無懸空條目。

---

## 5. 待裁決問題清單（需要你的成本判斷）

1. **CI 平台**：專案目前無 `.github/`。要上 GitHub Actions，還是你的工作流在別處（GitLab / 本機 only）？這決定動作 2 的具體落地形式。
2. **Architecture.Tests 一次做幾條**：plan 建議「MVP 三條 → 完整 NetArchTest → Roslyn analyzer」分三批。先只做最高 ROI 三條（保守、可中斷），還是一次補滿 CLAUDE.md 全部 11 條？
3. **第一層 agent hook（B2 `pre-tool-edit.py`）要不要做**：plan §5 明列「不引入 Roslyn analyzer for primary constructor」，但對 `new Failure(`/`throw`/`ILogger` 的 PreToolUse 攔截未表態。值得做，還是先靠 NetArchTest + CI 兩層就夠？
4. **grep-based vs analyzer**：`new Failure(`、bare-string code、`cancel` 命名這類，要用便宜的 CI grep step，還是投資寫 Roslyn analyzer（反饋更早但成本高）？
5. **本報告的後續**：這份 audit 要不要併入 `process-improvement-plan.md` 作為「2026-06-13 複盤」段落，避免兩份 retro 各自演化（單一真相來源原則）？

---

## 6. 交付狀態（Delivery Manifest）

- 目標 artifact：`tasks/loop-engineering-audit.md`（本檔）
- 狀態：**written**（成功寫入）
- 完整性：complete — Phase 1 盤點 + Phase 2 診斷完成
- 未動範圍：未修改任何規則 / 程式碼 / 測試 / 既有 ADR（盤點為唯讀）
- 待裁決：5 項（見 §5），需擁有者決定後才進 Phase 3（建環）
- 恢復方式：下一個 session 讀本檔 §4–§5 即可接續；建環前先回答 §5 的 CI 平台與範圍問題

---

## 7. Phase 3 建環進度（Resume Checkpoint）

擁有者裁決（§5）：CI 用 **GitHub Actions**；架構檢驗**先做最高 ROI 三條**（保守批次）。

### 已完成（綠＋故意紅驗證）
- [x] **動作 1 — Architecture.Tests 拉起，三條規則機械化**（plan #20 MVP 批次）
  - BC 隔離：`BoundedContextIsolationTests.cs`（NetArchTest，5 BC 配對，綠；故意紅確認會擋）
  - Repository raw return：`RepositoryReturnTypeTests.cs`（reflection，綠；故意紅點名 4 個 repo 方法）
  - Handler 必回 Result：`HandlerResultReturnTests.cs`（reflection，鎖定 `*Handler`，綠；故意紅點名 `CreateApiKeyHandler.HandleAsync`）
  - 套件：`NetArchTest.Rules 1.3.2` + `FluentAssertions 7.2.0`（合規 <8.0.0）
  - 全套件 Green：SharedKernel 6 + Architecture 7 + Functional 2 通過/42 略過（Docker 起後 BDD 通過，確認非迴歸）
- [x] **動作 2 — CI**：`.github/workflows/ci.yml`（restore→build→format verify→test→adr-lint）

### 本機防線已上線（單一真相來源）
- [x] **正典閘 `scripts/ci-checks.sh`**（單一腳本，兩模式，子集不可能漂移）：
  - `fast` = format + adr-lint（**pre-commit**，~7 秒，免 Docker）
  - `full` = restore + build + test + format + adr-lint（**pre-push** 與 `ci.yml`，需 Docker）
  - 不變式：pre-push 與 CI 都跑 `full`，故「pre-push 過 == CI 過」；fast 是快速預警子集。
- [x] pre-commit（fast）+ pre-push（full）hook 已安裝（`core.hooksPath = scripts/git-hooks/`），兩模式皆實跑驗證綠。
- [x] 全套件 Green 經 `full` 端到端確認（需 Docker 起，供 Testcontainers）。

### 待驗證（需 GitHub，環境限制非缺陷）
- [ ] CI 首次實跑：本專案**尚未上 GitHub**，`ci.yml` 已就緒但休眠，待 push 後確認綠（與本機跑同一支 `ci-checks.sh`，預期一致）
- [ ] `.github/workflows/ci.yml` 設為 main 的 required status check（需 repo 設定權限，push 後人工於 GitHub 操作）

### 待裁決 / 後續（§5 剩餘 + plan 其餘批次）
- [ ] 三條以外的 8 條 CLAUDE.md 架構規則是否續做（§5.2；plan 第二批「完整 NetArchTest」）
- [ ] `new Failure(` / bare-string code / `cancel` 命名：grep CI step vs Roslyn analyzer（§5.4）
- [ ] 第一層 agent PreToolUse hook（`pre-tool-edit.py`）是否做（§5.3）
- [ ] 本報告是否併入 `process-improvement-plan.md`（§5.5，單一真相來源）
- [ ] todo #19（FluentAssertions 8.9.0 違反 <8.0.0）、#35（`ROTATING` 殘留）順手清

### 交付狀態
- 本次新增/修改：3 個測試檔 + `ArchitectureRules.cs` helper + `Architecture.Tests.csproj`（加 2 套件）+ `.github/workflows/ci.yml` + `tasks/lessons.md`（2 條，皆有落地欄位）
- 未動：任何 production code、既有 ADR、既有規則文件（`naming.guide.md` 早已寫「Architecture tests enforce this via NetArchTest」——本次讓該句從願望變成真檢驗，無需改文件）
- 尚未 commit（等你過目；本機全綠）
