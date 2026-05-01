# Impact & Dependency Analysis — Detailed Reference

> This file is loaded by SKILL.md during Phase 2.5. It contains tool selection rules, step-by-step procedures, and worked examples.

## Tool Selection Rules

1. **Exact first**: When you know the symbol name (type/method/constant/field), use exact search (e.g. `grep`) to find all definitions and references.
2. **Read to verify**: For structural inferences (inheritance, DTO fields, mapping, constant values, method signatures), open the definition file directly — never infer from diff alone.
3. **Semantic is optional**: Use semantic search only when the exact name is unknown. It must not replace exact search.
4. **No evidence → no bug**: Without completing verification (exact search and/or file read), findings must be classified as Suggestion / Unconfirmed, not Bug.

## Step 2.5.0: Ensure Workspace Consistency (Precondition)

Phase 2.5 searches run against **local workspace** files. If the branch is wrong or outdated, analysis is unreliable.

- **PR Mode**: Run `gh pr checkout <ID>` and `git pull` if needed, before searching.
- **Self Mode**: Optionally remind user to confirm current branch.

## Step 2.5.1: Extract Key Entities from Diff

List all:
- Changed types / interfaces / class names
- Changed constants / enums / field names
- Changed public method / API signatures

## Step 2.5.2: Search for References & Dependencies

For each key entity, search the **full project** (not just diff):

| Direction | Purpose | Method |
|-----------|---------|--------|
| **Callers** | Do callers still assume old behavior/types/constants? | Exact search for method/type/constant name |
| **Callees** | Are called APIs/types consistently defined elsewhere? | Exact search for type/constant, then read definition file |
| **Shared types/constants** | Is the same constant/type used in unchanged files? | Exact search for all references; read files to confirm |
| **Inheritance** | Are base/derived field assumptions correct? | Read base/derived class files directly |

### Execution Sequence Example

Given diff shows "`ContentWithBannerResponse` removes `Color` field":

1. **Exact search** — find all references to `ContentWithBannerResponse`
2. **Read definitions** — open the type file and its base DTO to confirm whether `Color` moved to base or was removed entirely
3. **Exact search** — find all `.Color` usages to identify affected consumers
4. **Semantic search (optional)** — if inheritance/composition relationships are unclear

## Step 2.5.3: Classify Conclusions After Verification

- **Unverified inference** (e.g. "Color should have moved to Base" without confirming the actual file) → Suggestion / Unconfirmed only. Note: "Please verify against actual code structure."
- **Verified in project** (e.g. found multiple files using same constant, only one was updated) → May classify as Bug or firm Suggestion.

## Worked Examples

### Example 1: Constant Inconsistency

**Diff**: A constant changed from 300KB to 5MB.

1. Search constant name across project
2. If other files still reference old value → Bug: "Constant inconsistency"
3. If all references updated → No issue

### Example 2: Type Structure Verification

**Diff**: A Response type adds a `WebsiteService` field.

1. Read base DTO — confirm whether base already has the field
2. Read derived class — check for duplicate definition
3. Search mapping/builder/converter methods for the field
4. If base has it and mapping doesn't map it → Suggestion: "Missing mapping"

### Example 3: Method Signature Change Impact

**Diff**: `GetDailyWinnerListAsync` parameters changed from `keyWord` to `userId, cellPhoneLastThreeNumbers`.

1. Search all call sites for the method name
2. Read each caller to confirm parameters are updated
3. If any call site uses old parameters → Bug: "Call site not updated"
