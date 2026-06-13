#!/usr/bin/env python3
"""PreToolUse hook — write-time guard for the architecture rules.

Blocks Edit/Write/MultiEdit on backend/src/**/*.cs when the incoming content
contains a forbidden pattern, giving feedback at the moment of writing — the
innermost defence-loop layer, earlier than pre-commit / CI.

The patterns mirror scripts/source-lint.sh and the ILogger architecture test
exactly, so write-time and commit/CI enforce the same rules with no drift. `throw`
is deliberately NOT checked: legitimate guard throws exist (Result.cs accessors,
Infrastructure config guards, contract argument validation), so a text-level
throw ban would false-positive — the "Handler returns Result" architecture test
covers the structural case instead.

Block mechanism: exit code 2 with an explanation on stderr (Claude reads it and
self-corrects). Exit 0 = allow. Malformed input never blocks.
"""
import json
import re
import sys


def main() -> None:
    try:
        data = json.load(sys.stdin)
    except Exception:
        sys.exit(0)

    if data.get("tool_name", "") not in ("Edit", "Write", "MultiEdit"):
        sys.exit(0)

    tool_input = data.get("tool_input") or {}
    path = tool_input.get("file_path", "") or ""

    # Guard production C# only.
    if "/backend/src/" not in path or not path.endswith(".cs"):
        sys.exit(0)

    # Text being introduced: Write.content / Edit.new_string / MultiEdit.edits[].new_string.
    chunks = [tool_input.get("content") or "", tool_input.get("new_string") or ""]
    chunks += [e.get("new_string") or "" for e in (tool_input.get("edits") or [])]
    text = "\n".join(c for c in chunks if c)
    if not text:
        sys.exit(0)

    fname = path.rsplit("/", 1)[-1]
    in_logger_zone = "/Domain/" in path or "/Application/" in path or fname.endswith("Handler.cs")

    violations = []

    if fname != "FailureProvider.cs" and re.search(r"\bnew\s+Failure\s*\(", text):
        violations.append("`new Failure(...)` → FailureProvider.CreateFailure(XFailureCodes.Foo)")

    if re.search(r'CreateFailure\("', text):
        violations.append('bare-string failure code → CreateFailure(XFailureCodes.Foo), not CreateFailure("...")')

    if re.search(r"CancellationToken\s+(cancellationToken|ct)\b", text):
        violations.append("CancellationToken parameter must be named `cancel` (not cancellationToken / ct)")

    if in_logger_zone and re.search(r"\bILogger\s*<", text):
        violations.append("ILogger must not be injected into Service/Domain/Handler — log at the boundary")

    if violations:
        lines = "\n".join("  - " + v for v in violations)
        print(
            f"[pre-tool-edit] blocked {fname} — write-time architecture guard:\n{lines}\n"
            "Fix the snippet. If the rule itself is wrong, challenge it via an ADR — do not bypass.",
            file=sys.stderr,
        )
        sys.exit(2)

    sys.exit(0)


if __name__ == "__main__":
    main()
