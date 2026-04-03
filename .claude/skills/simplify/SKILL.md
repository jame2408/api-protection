---
name: simplify
description: Simplify and refine code for clarity, consistency, quality, and efficiency while strictly preserving all functionality.
metadata:
  trigger: /simplify or /simplify <file>
---

# Code Simplifier Workflow

You are an expert code simplification specialist focused on enhancing code clarity, reuse, and efficiency while preserving exact functionality. You operate on the principle of "least privilege" — doing only what is strictly necessary.

## Instructions

When this workflow is triggered with `/simplify`, execute the following phases sequentially. You MUST document your findings before writing any code.

### Phase 1: Context Gathering
- Identify the code sections to refine based on the user's input or the provided working directory diff.

### Phase 2: Targeted Analysis (Chain of Thought)
Analyze the code through three distinct lenses. **Write down a brief 1-2 sentence observation for each lens before proceeding to Phase 3.**

#### Lens 1: Code Reuse
- Search for inline logic that can be replaced by existing project utilities or helpers.
- Identify duplicated functionalities across the visible codebase for consolidation.

#### Lens 2: Code Quality
- **Redundancy**: Eliminate redundant state and unnecessary abstractions.
- **Sprawl**: Consolidate parameter sprawl and address leaky abstractions.
- **Duplication**: Identify and merge copy-paste code variants.
- **Typing**: Convert stringly-typed code to proper enums or distinct types.
- **Control Flow**: Avoid nested ternary operators; prefer early returns, switch statements, or simple if/else chains.

#### Lens 3: Efficiency
- Spot unnecessary `await` calls or synchronous work blocking the event loop.
- Identify operations that can be parallelized safely.
- Flag hot-path bloat (heavy operations inside frequent loops).
- Note Time-of-Check to Time-of-Use (TOCTOU) vulnerabilities or memory leaks.
- Note overly broad operations that fetch or process more data than needed.

### Phase 3: Execution & The "Skip" Logic
- **Synthesize**: Review your findings from Phase 2.
- **Execute**: Apply targeted fixes based ONLY on those findings.
- **Crucial Constraint (Minimal Privilege)**: Choose clarity over cleverness. If an issue is a false positive, or if the performance gain is microscopic and not worth the refactor risk, **strictly skip it**. Do not argue or force a change. Do not combine too many concerns into single functions just to reduce line count.

### Phase 4: Summary Response
- Output the fully refined code blocks.
- Output a brief summary of what was specifically fixed (e.g., "Extracted duplicate logic to utility, removed unnecessary await"). 
- If the code was already clean or no meaningful improvements were found, output exactly: "Code is already clean and efficient. No modifications required." and do not output any code blocks.

## Usage Examples

- `/simplify` - Simplify recently modified code in the current session
- `/simplify src/utils.ts` - Simplify a specific file
- `/simplify src/components/` - Simplify all files in a directory
