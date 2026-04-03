# Step 4 → Step 5 Bridge — Example Mapping

## Table of Contents

- [Problem](#problem)
- [The 4-Card System](#the-4-card-system)
- [Workshop Flow](#workshop-flow)
- [Derivation Rules](#derivation-rules)
- [Exit Criteria](#exit-criteria)
- [Converting to Gherkin](#converting-to-gherkin)

## Problem

Step 4 (internal design) is an **inside-out** perspective. Step 5 (BDD scenarios) is an **outside-in** perspective. Jumping directly from Step 4 to Step 5 causes teams to stall. Example Mapping (by Matt Wynne) bridges this gap.

## The 4-Card System

| Card | Color | Source | Content |
|------|-------|--------|---------|
| 🟡 Story | Yellow `#FFD700` | Step 4 Command | One Command per Story card |
| 🔵 Rule | Blue `#4A90D9` | Step 4 Guard / Invariant | Each Guard condition becomes a Rule |
| 🟢 Example | Green `#4CAF50` | Workshop discussion | Concrete scenario — draft BDD scenario |
| 🔴 Question | Red `#E53935` | Workshop discovery | Unresolved issues → return to design or PRD |

## Workshop Flow

For each Command from Step 4:

1. **Place the 🟡 Story card** — Write the Command name at the top.
2. **List all 🔵 Rule cards** — Extract every Guard and relevant invariant from the Command's spec.
3. **Generate 🟢 Example cards** — For each Rule, create:
   - At least one **positive example** (guard passes)
   - At least one **negative example** (guard fails)
   - Edge cases where applicable
4. **Capture 🔴 Question cards** — Record any ambiguity or missing business logic discovered during discussion.
5. **Iteration limit:** Generate no more than 3-5 Example cards per Rule to prevent context bloat. Ask for user validation before moving to the next Rule.

### Participant Roles

| Role | Responsibility |
|------|---------------|
| **Developer** (Required) | Brings Step 4 design knowledge |
| **PO / Domain Expert** (Required) | Confirms business correctness |
| **QA** (Recommended) | Identifies boundary/edge cases |

## Derivation Rules

Map Step 4 spec fields to BDD components:

```
Guard (🔵 Rule)     →  Given（前置條件的正向/反向設定）
Command (🟡 Story)  →  When（使用者操作）
State + Event       →  Then（預期結果）
```

### Example derivation from CreateApiKey:

**🔵 Rule:** 租戶金鑰數 < 上限

- **🟢 Positive:** Given 租戶目前有 5 把金鑰，上限為 10 → When 建立 → Then 成功
- **🟢 Negative:** Given 租戶目前有 10 把金鑰，上限為 10 → When 建立 → Then 失敗

**🔵 Rule:** 名稱在租戶內不重複

- **🟢 Positive:** Given 租戶內沒有同名金鑰 → When 建立 → Then 成功
- **🟢 Negative:** Given 租戶內已有同名金鑰 → When 建立 → Then 失敗

## Exit Criteria

The Example Mapping session for a Command is **complete** when ALL of the following are true:

1. **Coverage:** Every 🔵 Rule has at least one 🟢 positive and one 🟢 negative Example.
2. **Resolution:** No unresolved 🔴 Question cards remain (resolved or explicitly deferred with rationale).
3. **Edge cases:** Team has considered boundary values and multi-condition combinations.

When these conditions are met, proactively ask:

> 「所有 Rule 都已有正向與反向範例，且沒有未解決的問題。是否準備好將這些範例轉化為 Gherkin 格式（進入 Step 5）？」

**If 🔴 Questions remain unresolved:**
- List them explicitly.
- Ask: "這些問題需要先釐清才能繼續，還是可以先標記為 Open Question 並推進？"
- If deferred, record in the output as `> ⚠️ Open Question: [內容]`.

## Converting to Gherkin

Each 🟢 Example card maps to one Gherkin Scenario:

```
🟢 Example card                    Gherkin Scenario
─────────────────                  ─────────────────
Positive/Negative context    →     Given [前置條件]
The Command being tested     →     When  [操作]
Expected State + Event       →     Then  [預期結果]
```

See [specification-by-example.md](specification-by-example.md) for the full Gherkin format and worked examples.
