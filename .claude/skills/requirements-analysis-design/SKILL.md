---
name: requirements-analysis-design
description: >-
  DDD-based post-PRD design workflow: Context Integration Spec, Per-BC Detailed
  Design (Command/Guard/State/Event), Example Mapping, Specification by Example.
  Use when defining BC integration contracts, writing Aggregate behavior specs,
  or when the user says "detail design", "BC integration", "aggregate spec", or
  "example mapping". In this repo, new .feature scenario authoring is frozen —
  existing-scenario changes route via ADR-022, not this skill.
metadata:
  trigger: '"I need to design aggregate behavior", "/requirements-analysis-design", "/design", or "/bdd"'
---

# Requirements Analysis & Design Workflow

Guide the user through the **post-PRD design flow**: from defining BC integration contracts (Step 3) through Aggregate behavior specs (Step 4) to BDD scenarios (Step 5).

## Document Hierarchy (Context)

| Step | Document | Purpose | Covered By |
|------|----------|---------|------------|
| 1 | PRD | Define "what" and "why" | `doc-coauthoring` skill |
| 2 | High-level Design Doc | System boundaries, Context Map, ADR | `doc-coauthoring` skill |
| 3 | Context Integration Spec | BC-to-BC contracts | **This skill** |
| 4 | Per-BC Detailed Design | Aggregate behavior specs | **This skill** |
| 5 | Specification by Example | BDD scenarios (Given/When/Then) | **This skill** |

## Core Principles

1. **Layered expansion, no skipping.** Each step's input is the previous step's output.
2. **Technology-agnostic.** Steps 3-5 must not bind to any specific framework or tech stack.
3. **Bidirectional feedback.** When expanding to the next layer, check if the previous layer needs correction.

## Interaction Principles

### Language & Terminology (CRITICAL)

- **Primary language:** Traditional Chinese (Taiwan/TW).
- **Tech terms:** Keep proper nouns in English (e.g., "Aggregate", "Domain Event", "Guard").
- **Markdown protection:** All structured output (Command/Guard/State/Event, Gherkin, Payload Schema) MUST be wrapped in complete fenced code blocks. Always ensure fenced code blocks are complete and closed.
- **Terminology normalization:** After drafting Chinese content, apply the `deai-editor` skill for cross-strait terminology correction (e.g., 程式碼 not 代碼, 物件 not 對象, 介面 not 接口). Do NOT maintain a separate term list here — `deai-editor` is the single source of truth.

### Visualization

Proactively offer Mermaid diagrams when content is complex:
- **stateDiagram-v2** for Aggregate lifecycle (3+ states)
- **sequenceDiagram** for BC integration flows
- **flowchart** for decision logic

Diagram labels follow TW terminology rules. Variable names stay in English.

---

## Prerequisite Guardrail

**BEFORE entering Step 3**, verify the user has:

1. ✅ A **Context Map** with defined BC boundaries and relationships
2. ✅ At least one **ADR** for key architectural decisions
3. ✅ Clear **Bounded Context names** and their classification (Core / Supporting / Generic)

**If ANY prerequisite is missing:**

> 「目前缺少 [具體缺少的項目]。建議先使用 `doc-coauthoring` 技能完成高階設計文件（Step 2），再回到這裡繼續。是否要先進行高階設計？」

Do NOT proceed to Step 3 without prerequisites. This is a hard gate.

---

## Workflow Router

Determine the entry point based on user's current artifacts:

1. **If** Context Map + ADR are missing → Redirect to `doc-coauthoring` (Step 2).
2. **If** Context Integration Spec is missing → Start at Step 3.
3. **If** Per-BC Detailed Design is missing → Start at Step 4.
4. **If** Step 4 is complete → Start Example Mapping, then Step 5.

Ask the user which artifacts they already have if unclear.

---

## Execution Rhythm

Process **one BC at a time**, completing all steps before moving to the next:

```
BC-1 (Core):       Step 4 → Example Mapping → Step 5 → Summary
BC-2 (Supporting):  Step 4 → Example Mapping → Step 5 → Summary
BC-3 (Supporting):  Step 4 → Example Mapping → Step 5 → Summary
...
```

Process strictly sequentially: complete Step 4, Example Mapping, and Step 5 for a single BC before initiating work on the next BC.

### State Checkpointing

After completing each BC's Step 4 + Step 5 cycle, generate an **Integration Context Summary** (≤50 words) using this template:

```
[BC 名稱]: 核心 Aggregate 為 [X]，關鍵不變式為 [Y]，
已確認與 [BC-B] 透過 [同步/非同步] 整合。
```

Carry all summaries forward as fixed context when working on subsequent BCs. Present the accumulated summaries at the start of each new BC.

---

## Step 3: Context Integration Spec

**Goal:** Define BC-to-BC contracts for parallel development.

**When to read:** Starting a new design that has a Context Map but no integration specs.

→ Read [context-integration-spec.md](references/context-integration-spec.md) for the full format template, worked example, and completion checklist.

### Quick Reference

For each BC pair on the Context Map, define:
- Trigger scenario
- Communication method (sync / async / mixed)
- Contract spec (Command or Event + Payload Schema)
- Failure handling (retry, degradation, idempotency)
- Data consistency model

### Completion Gate

Before proceeding to Step 4:
- [ ] All Context Map relationships have integration specs
- [ ] All async Events have Payload Schema
- [ ] All failure scenarios have handling strategies

---

## Step 4: Per-BC Detailed Design

**Goal:** Define Aggregate behaviors so developers can implement directly.

**When to read:** Working on a specific BC's internal design.

→ Read [per-bc-detailed-design.md](references/per-bc-detailed-design.md) for the mandatory format, worked example, and completion checklist.

### Quick Reference — Mandatory Format

Every Aggregate behavior uses this exact structure:

```
Command:  [命令名稱]
Guard:    [前置條件，用 AND/OR 連接]
State:    [從 → 到]
Event:    [Domain Event { 關鍵欄位 }]
```

### Completion Gate

Before proceeding to Example Mapping:
- [ ] All behaviors use Command/Guard/State/Event format
- [ ] Invariants are complete and consistent
- [ ] Contracts match Step 3 specs
- [ ] Integration Context Summary generated

---

## Step 4→5 Bridge: Example Mapping

**Goal:** Bridge inside-out design to outside-in BDD scenarios.

**When to read:** Transitioning from a BC's Step 4 design to Step 5 scenarios.

→ Read [example-mapping.md](references/example-mapping.md) for the 4-card system, workshop flow, and exit criteria.

### Quick Reference

For each Command:
1. 🟡 Place the Command (Story)
2. 🔵 List all Guards (Rules)
3. 🟢 Generate positive + negative Examples per Rule
4. 🔴 Capture Questions

### Exit Trigger

When all 🔵 Rules have 🟢 Examples (positive + negative) and no unresolved 🔴 Questions remain:

> 「所有 Rule 都已有正向與反向範例，且沒有未解決的問題。是否準備好將這些範例轉化為 Gherkin 格式（進入 Step 5）？」

---

## Step 5: Specification by Example

**Goal:** Convert Examples to executable BDD scenarios.

**When to read:** Converting Example Mapping output to Gherkin format.

→ Read [specification-by-example.md](references/specification-by-example.md) for the Gherkin format, derivation rules, and worked example.

### Quick Reference — Derivation

| Spec Field | → Gherkin |
|------------|-----------|
| Guard (pass) | `Given` |
| Guard (fail) | `Given` (negative scenario) |
| Command | `When` |
| State + Event | `Then` |

### Completion Gate

Before marking a BC as done:
- [ ] Every Guard has positive and negative scenarios
- [ ] Scenarios use domain language (readable by non-technical team)
- [ ] Scenarios are independent (no execution order dependency)
- [ ] All Gherkin wrapped in ` ```gherkin ` fenced code blocks
