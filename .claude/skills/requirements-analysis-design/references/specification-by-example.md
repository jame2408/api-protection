# Step 5 — Specification by Example (BDD 場景)

## Table of Contents

- [Purpose](#purpose)
- [Triple Role](#triple-role)
- [Gherkin Format](#gherkin-format)
- [Derivation from Step 4](#derivation-from-step-4)
- [Worked Example](#worked-example)
- [Writing Guidelines](#writing-guidelines)
- [Completion Checklist](#completion-checklist)

## Purpose

Unify three concerns into one artifact:

1. **Usage Scenario** — how users interact with the system
2. **Acceptance Criteria** — definition of done
3. **Executable Spec** — directly translatable to BDD test framework

## Triple Role

```
Specification by Example
├── 使用情境：使用者怎麼用
├── 驗收條件：怎麼算做完
└── 可執行規格：直接變成 BDD 測試
```

## Gherkin Format

Always output in a fenced code block with `gherkin` language tag:

```gherkin
Feature: [功能名稱]

  Scenario: [場景描述]
    Given [前置條件 — 來自 Guard 的正向/反向設定]
    When  [操作 — 來自 Command]
    Then  [預期結果 — 來自 State 變更 + Event]
```

### Format Rules

- **CRITICAL:** NEVER translate Gherkin keywords (`Feature`, `Scenario`, `Given`, `When`, `Then`, `And`) into Chinese. They must remain in English for test parser compatibility.
- **Feature** = one per Command (or closely related Command group)
- **Scenario** = one per 🟢 Example card from Example Mapping
- Use `And` for multiple Given/Then conditions
- Use domain language, not technical jargon — non-technical team members must be able to read it
- Keep scenarios independent — no scenario should depend on another's execution

## Derivation from Step 4

| Step 4 Spec Field | Gherkin Element | Derivation |
|-------------------|-----------------|------------|
| Guard (positive) | `Given` | Set up conditions where guard passes |
| Guard (negative) | `Given` | Set up conditions where guard fails |
| Command | `When` | The user action |
| State change | `Then` | Assert new state |
| Domain Event | `Then` / `And` | Assert event emission with key fields |

For each Guard condition, generate **at minimum**:
- 1 scenario where the guard passes (happy path)
- 1 scenario where the guard fails (error path)

## Worked Example

**Step 4 Input:**

```
Command:  CreateApiKey
Guard:    租戶金鑰數 < 上限 AND 名稱在租戶內不重複
State:    → PendingActivation
Event:    ApiKeyCreated { keyId, tenantId, name, scope, createdAt }
```

**Step 5 Output:**

```gherkin
Feature: 建立 API Key

  Scenario: 成功建立金鑰
    Given 租戶目前有 5 把金鑰，上限為 10
    And   租戶內沒有名為 "my-service-key" 的金鑰
    When  租戶建立名為 "my-service-key" 的金鑰
    Then  金鑰狀態為 PendingActivation
    And   產生 ApiKeyCreated 事件

  Scenario: 超過金鑰數量上限
    Given 租戶目前有 10 把金鑰，上限為 10
    When  租戶建立新金鑰
    Then  建立失敗，錯誤原因為「超過金鑰上限」

  Scenario: 金鑰名稱重複
    Given 租戶內已有名為 "my-service-key" 的金鑰
    When  租戶建立名為 "my-service-key" 的金鑰
    Then  建立失敗，錯誤原因為「金鑰名稱重複」
```

**Notice:**
- Guard `租戶金鑰數 < 上限` → 2 scenarios (pass: 5/10, fail: 10/10)
- Guard `名稱在租戶內不重複` → 2 scenarios (pass: covered in success, fail: dedicated scenario)
- The success scenario combines both guards passing

## Writing Guidelines

1. **Domain language only** — Write in terms the PO understands, not implementation details.
2. **Concrete values** — Use "5 把金鑰，上限為 10", not "金鑰數未達上限".
3. **One behavior per scenario** — Test one rule violation per failure scenario.
4. **Consistent terminology** — Use the same terms as Step 4 (Command names, State names, Event names).
5. **Markdown protection** — Always wrap Gherkin output in ` ```gherkin ` fenced code blocks.

## Completion Checklist

- [ ] 每個 Guard 條件都有正向和反向場景
- [ ] 場景使用領域語言，非技術人員可讀
- [ ] 場景可直接用於 BDD 測試框架（Cucumber / SpecFlow / Behave）
- [ ] 場景之間互相獨立，不依賴執行順序
- [ ] 所有 Gherkin 輸出都包裝在 fenced code block 中
