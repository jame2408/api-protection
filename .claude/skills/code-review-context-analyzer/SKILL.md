---
name: code-review-context-analyzer
description: Analyze GitHub PR code diffs to generate intent summaries, logic walkthroughs, and Mermaid architecture diagrams. Use when reviewing pull requests, examining git diffs, or when the user asks for a code review context report.
metadata:
  trigger: '"Please analyze this diff" or when reviewing PRs'
---

# Code Review Context Analyzer

**Core Philosophy**: Move beyond line-by-line syntax comparison to intent-level understanding. Transform raw diffs into high-dimensional, instantly digestible information.

- **De-noise**: Filter out formatting changes and trivial log modifications; focus on logic flow.
- **Holistic view**: Assume every single-file change may trigger a butterfly effect; always resolve cross-file dependencies.
- **Visual-first**: Prefer diagrams over text to reduce cognitive load.

## Execution Process

When triggered by a PR review request, execute these steps in order:

### Step 1: Ingest & Retrieve

1. Read the Git diff (use `git diff`, `gh pr diff`, or the provided diff).
2. **Expand the context window**:
   - Use file-reading and search tools to fetch full definitions of modified functions and their callers/callees — even unchanged surrounding code matters.
   - **Fallback**: If unable to retrieve source beyond the diff (tool unavailable, permission denied, or file not found), analyze strictly within the provided diff and prepend the output with: `> ⚠️ 分析範圍僅限 Diff 內容 (Analysis scope limited to diff context)`
3. Identify all affected files and modules.

### Step 2: Analyze & Infer

1. Compare the logical structure between base and head commits.
2. Infer architectural impact:
   - New API endpoints introduced?
   - Database schema changes?
   - Interface/contract modifications?
   - Dependency additions or removals?
3. Classify change type: refactor, feature, bugfix, config, or mixed.

### Step 3: Asset Generation

Generate two types of assets:

**Text assets:**
- High-level intent summary (Why + How)
- Ordered reading guide (logic-flow order, not alphabetical)

**Graph assets (conditional):**
- Mermaid.js sequence diagrams or flowcharts showing module interactions

### Step 4: Synthesis

Combine text and diagrams into the standardized output format below.

## Decision Logic

Apply these rules dynamically during analysis:

| Condition | Action |
|-----------|--------|
| Change touches **1 file** with no external deps | **Skip** architecture diagram (too trivial) |
| Change touches **3+ modules** or modifies a core interface | **Force** sequence diagram generation |
| Diff exceeds **1000 lines** | Switch to **module-level summary** mode (summarize by folder/module, not per-function) |
| Generated summary references a file not in the diff | **Self-correct**: re-check the file list and regenerate |
| Module-level summary is active AND a module contains security-sensitive changes (auth, crypto, SQL, permission) | **Flag** the module as `⚠️ 需深入審查 (Requires drill-down)` |

## Output Format

Produce a structured Markdown block following this template:

```markdown
### 🐇 Code Review Context Report

**⚠️ 安全與風險提示 (Security & Risk Flags)**
*(Only include this section when Decision Logic flags security-sensitive modules. Omit entirely otherwise.)*
- 🔴 **[Module Name]**: Contains [auth/crypto/SQL/permission] changes — requires focused drill-down review.

**📝 變更摘要 (Change Summary)**
[1-3 sentences describing the developer's intent (Why) and implementation strategy (How).]
- **核心變更 (Core Changes)**: [Key modified components and what changed]
- **影響範圍 (Impact Scope)**: [List of affected modules/files beyond the direct changes]

**🗺️ 邏輯導覽 (Logic Walkthrough)**
[Ordered reading guide based on logical execution flow]
1. Start with `[file]`: [reason to read first]
2. Then `[file]`: [what to look for]
3. Finally `[file]`: [what to verify]

**📊 架構視覺化 (Architecture Visualization)**
[Mermaid diagram — only if decision logic requires it]
```

## Language Protocol

- **Reasoning and analysis**: Always in English internally.
- **Output report**: Use Traditional Chinese labels with English technical terms preserved.
- Format: `中文描述 (English Term)` for bilingual clarity.
- **Mermaid Diagrams**: Use English or short logical terms for all node labels and edge annotations. Avoid CJK characters inside diagram syntax to prevent cross-renderer rendering failures. Use the surrounding text summary for detailed Chinese explanations.

## Quality Checklist

Before delivering the report, verify:

- [ ] Summary reflects intent, not just line-by-line translation
- [ ] Reading guide follows logic flow, not file alphabetical order
- [ ] All referenced files actually exist in the diff
- [ ] Diagram accurately represents the data/control flow
- [ ] No hallucinated file names or function names

## Additional Resources

- For detailed output examples, see [examples.md](examples.md)
