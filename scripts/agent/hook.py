#!/usr/bin/env python3
"""Shared Claude Code/Codex lifecycle hook dispatcher.

Harness configs only select an action. This file owns payload normalization and
all hook behavior so guard rules, lesson injection, syntax validation, and
failure scrubbing cannot drift between harnesses.
"""

from __future__ import annotations

import json
import os
import py_compile
import re
import subprocess
import sys
import xml.etree.ElementTree as ElementTree
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable


REPO_ROOT = Path(__file__).resolve().parents[2]
LESSONS_DIR = REPO_ROOT / "tasks" / "lessons"

_SINGLE_QUOTED = re.compile(r"'[^']*'")
_DOUBLE_QUOTED = re.compile(r'"(?:[^"\\]|\\.)*"')
_ARITHMETIC = re.compile(r"\$\(\([^)]*\)\)")
_HEREDOC = re.compile(r"(?<!<)<<-?(?!<)")
_ZSH_EQUALS_TOKEN = re.compile(r"^=.+")
_ZSH_STATUS_ASSIGN = re.compile(
    r"(?:^|[;&|({\n]\s*)(?:export\s+|local\s+|typeset\s+|readonly\s+)?status=",
    re.MULTILINE,
)

_PATCH_FILE = re.compile(r"^\*\*\* (Add|Update|Delete) File: (.+)$")
_PATCH_MOVE = re.compile(r"^\*\*\* Move to: (.+)$")
_BACKEND_SOURCE = re.compile(r"(^|/)backend/src/")

_SENSITIVE_KEY = re.compile(
    r"(?i)(api[_-]?key|access[_-]?key|secret|token|password|passwd|"
    r"authorization|bearer|client[_-]?secret|private[_-]?key)"
)
_REDACTED = "**REDACTED**"
_VALUE_PATTERNS = [
    (re.compile(r"(?i)\b(Bearer)\s+[A-Za-z0-9._\-+/=]{8,}"), r"\1 **REDACTED**"),
    (re.compile(r"\beyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+"), _REDACTED),
    (re.compile(r"\bsk-(?:ant-)?[A-Za-z0-9_\-]{16,}"), _REDACTED),
    (re.compile(r"\bgh[pousr]_[A-Za-z0-9]{20,}"), _REDACTED),
    (re.compile(r"\bAKIA[0-9A-Z]{16}\b"), _REDACTED),
]


@dataclass
class PatchSection:
    path: str
    added_lines: list[str]


def _load_payload() -> dict[str, Any]:
    try:
        value = json.load(sys.stdin)
    except Exception:
        return {}
    return value if isinstance(value, dict) else {}


def _parse_frontmatter(text: str) -> tuple[dict[str, str], str]:
    if not text.startswith("---\n"):
        return {}, text

    end = text.find("\n---\n", 4)
    if end == -1:
        return {}, text

    metadata: dict[str, str] = {}
    for line in text[4:end].splitlines():
        if ":" in line:
            key, value = line.split(":", 1)
            metadata[key.strip()] = value.strip()
    return metadata, text[end + 5 :]


def _lesson_context() -> tuple[list[str], list[str], int, int]:
    output: list[str] = []
    warnings: list[str] = []
    active_count = 0
    archived_count = 0

    if not LESSONS_DIR.is_dir():
        return output, warnings, active_count, archived_count

    paths = sorted(path for path in LESSONS_DIR.glob("*.md") if path.name != "_README.md")
    for path in paths:
        metadata, body = _parse_frontmatter(path.read_text(encoding="utf-8"))
        status = metadata.get("status", "")

        if status == "archived":
            archived_count += 1
            continue
        if status != "active":
            warnings.append(
                f"[session-init] {path.name} frontmatter status "
                f"missing or invalid ({status!r}); not injected or counted"
            )
            continue

        active_count += 1
        title = ""
        rule_line = ""
        for line in body.splitlines():
            if not title and line.startswith("# "):
                title = line[2:].strip()
            if line.startswith("**Rule:**"):
                rule_line = line

        output.append(f"### [{metadata.get('type', '')}] {title}")
        if rule_line:
            output.append(rule_line)

    return output, warnings, active_count, archived_count


def session_context(payload: dict[str, Any]) -> int:
    session_id = payload.get("session_id", "")
    session_id = session_id if isinstance(session_id, str) else ""
    marker = Path(
        os.environ.get(
            "SESSION_INIT_MARKER",
            str(REPO_ROOT / ".claude" / "session-init.marker"),
        )
    )

    if session_id and marker.is_file():
        try:
            if marker.read_text(encoding="utf-8") == session_id:
                return 0
        except OSError:
            pass

    print("## 必讀規範（寫 backend code 前）")
    print()
    print(
        "寫任何 Handler / Service / Repository / Endpoint（production 或 test）之前，"
        "先載入規則 — 以 CLAUDE.md §0 Reference Loading 為準："
    )
    print("- `.claude/references/dotnet/*.rule.md` 與 `.claude/references/general/*.rule.md`")
    print("- `docs/adr/` 內 Accepted 的 ADR（錯誤處理 / DI / 命名 / wire-format 決策）")
    print("- `docs/design/api-spec.md`（你要碰的 endpoint 章節）")
    print("- 多模型協調與驗證機制：`docs/orchestration.md`、`docs/verification-matrix.md`")
    print()
    print(
        "這些規則是機械化強制的，不是建議：違規會在「寫的當下」被 "
        "PreToolUse hook 擋（`scripts/agent/hook.py`），並由 Architecture.Tests 與 "
        "`scripts/source-lint.sh` 在 commit / push / CI 攔下（`scripts/ci-checks.sh`）。"
        "未讀就動手 = 高機率被擋、來回重做。"
    )
    print()

    lessons, warnings, active_count, archived_count = _lesson_context()
    if lessons or warnings:
        print("## Session Context: Lessons Learned")
        print()
        print("The following lessons have been captured from previous sessions in this project.")
        print("Apply them proactively without waiting to be reminded.")
        print()
        for warning in warnings:
            print(warning)
        if warnings:
            print()
        if lessons:
            print("\n".join(lessons))
            print()
        print(
            f"（Active {active_count} 條，Archived {archived_count} 條 — "
            "完整內容見 tasks/lessons/）"
        )

    if session_id:
        try:
            marker.parent.mkdir(parents=True, exist_ok=True)
            marker.write_text(session_id, encoding="utf-8")
        except OSError as error:
            print(f"[session-init] could not update marker {marker}: {error}", file=sys.stderr)
            return 1

    return 0


def _parse_patch(command: str) -> list[PatchSection]:
    sections: list[PatchSection] = []
    path = ""
    added_lines: list[str] = []

    def flush() -> None:
        nonlocal path, added_lines
        if path:
            sections.append(PatchSection(path=path, added_lines=added_lines))
        path = ""
        added_lines = []

    for line in command.splitlines():
        file_match = _PATCH_FILE.match(line)
        if file_match:
            flush()
            operation, candidate = file_match.groups()
            if operation != "Delete":
                path = candidate
            continue

        move_match = _PATCH_MOVE.match(line)
        if move_match and path:
            path = move_match.group(1)
            continue

        if path and line.startswith("+") and not line.startswith("+++"):
            added_lines.append(line[1:])

    flush()
    return sections


def _normalized_edits(payload: dict[str, Any]) -> list[tuple[str, str]]:
    tool_name = payload.get("tool_name", "")
    tool_input = payload.get("tool_input") or {}
    if not isinstance(tool_input, dict):
        return []

    if tool_name == "apply_patch":
        command = tool_input.get("command", "")
        if not isinstance(command, str):
            return []
        return [(section.path, "\n".join(section.added_lines)) for section in _parse_patch(command)]

    if tool_name not in ("Edit", "Write", "MultiEdit"):
        return []

    path = tool_input.get("file_path", "")
    if not isinstance(path, str) or not path:
        return []

    chunks = [tool_input.get("content") or "", tool_input.get("new_string") or ""]
    edits = tool_input.get("edits") or []
    if isinstance(edits, list):
        chunks.extend(
            edit.get("new_string") or ""
            for edit in edits
            if isinstance(edit, dict)
        )
    text = "\n".join(chunk for chunk in chunks if isinstance(chunk, str) and chunk)
    return [(path, text)]


def pre_tool_edit(payload: dict[str, Any]) -> int:
    blocked: list[tuple[str, list[str]]] = []

    for raw_path, text in _normalized_edits(payload):
        path = raw_path.replace("\\", "/")
        if not _BACKEND_SOURCE.search(path) or not path.endswith(".cs") or not text:
            continue

        filename = path.rsplit("/", 1)[-1]
        scoped_path = "/" + path.lstrip("/")
        in_logger_zone = (
            "/Domain/" in scoped_path
            or "/Application/" in scoped_path
            or filename.endswith("Handler.cs")
        )
        violations: list[str] = []

        if filename != "FailureProvider.cs" and re.search(r"\bnew\s+Failure\s*\(", text):
            violations.append("`new Failure(...)` -> FailureProvider.CreateFailure(XFailureCodes.Foo)")
        if re.search(r'CreateFailure\("', text):
            violations.append(
                'bare-string failure code -> CreateFailure(XFailureCodes.Foo), '
                'not CreateFailure("...")'
            )
        if re.search(r"CancellationToken\s+(cancellationToken|ct)\b", text):
            violations.append(
                "CancellationToken parameter must be named `cancel` "
                "(not cancellationToken / ct)"
            )
        if in_logger_zone and re.search(r"\bILogger\s*<", text):
            violations.append(
                "ILogger must not be injected into Service/Domain/Handler; "
                "log at the boundary"
            )

        if violations:
            blocked.append((filename, violations))

    if not blocked:
        return 0

    messages: list[str] = []
    for filename, violations in blocked:
        lines = "\n".join(f"  - {violation}" for violation in violations)
        messages.append(
            f"[pre-tool-edit] blocked {filename} — write-time architecture guard:\n"
            f"{lines}"
        )
    print(
        "\n".join(messages)
        + "\nFix the snippet. If the rule itself is wrong, challenge it via an ADR; "
        "do not bypass.",
        file=sys.stderr,
    )
    return 2


def _blank(match: re.Match[str]) -> str:
    return " " * len(match.group(0))


def _mask_shell(command: str) -> str:
    masked = _SINGLE_QUOTED.sub(_blank, command)
    masked = _DOUBLE_QUOTED.sub(_blank, masked)
    return _ARITHMETIC.sub(_blank, masked)


def pre_tool_bash(payload: dict[str, Any]) -> int:
    if payload.get("tool_name", "") != "Bash":
        return 0

    tool_input = payload.get("tool_input") or {}
    if not isinstance(tool_input, dict):
        return 0
    command = tool_input.get("command", "")
    if not isinstance(command, str) or not command:
        return 0

    masked = _mask_shell(command)
    violations: list[str] = []
    if _HEREDOC.search(masked):
        violations.append(
            "heredoc（<<）建立/覆寫檔案改用 Write/apply_patch；需餵 stdin 給程式時，"
            "先寫腳本檔再執行（見 tasks/lessons/20260705-heredoc-write-tool.md）"
        )

    bad_tokens = [token for token in masked.split() if _ZSH_EQUALS_TOKEN.match(token)]
    if bad_tokens:
        rendered = ", ".join(repr(token) for token in bad_tokens)
        violations.append(
            f"zsh 對裸 `=` 開頭參數（例如 {rendered}）做 =word 展開會直接報錯，"
            "請加引號（如 echo '==='）"
        )

    if _ZSH_STATUS_ASSIGN.search(masked):
        violations.append(
            "zsh 的 status 是唯讀特殊變數，賦值會直接報錯——exit code 一律改用 exit_code"
            "（見 tasks/lessons/20260710-zsh-status-readonly-and-restore-trap.md）"
        )

    if not violations:
        return 0

    lines = "\n".join(f"  - {violation}" for violation in violations)
    print(
        f"[pre-tool-bash] blocked Bash command — write/quoting guard:\n{lines}",
        file=sys.stderr,
    )
    return 2


def _affected_paths(payload: dict[str, Any]) -> list[Path]:
    tool_input = payload.get("tool_input") or {}
    if not isinstance(tool_input, dict):
        return []

    raw_paths: list[str] = []
    if payload.get("tool_name") == "apply_patch":
        command = tool_input.get("command", "")
        if isinstance(command, str):
            raw_paths.extend(section.path for section in _parse_patch(command))
    else:
        path = tool_input.get("file_path", "")
        if isinstance(path, str) and path:
            raw_paths.append(path)

    cwd_value = payload.get("cwd")
    cwd = Path(cwd_value) if isinstance(cwd_value, str) and cwd_value else Path.cwd()
    resolved: list[Path] = []
    seen: set[Path] = set()
    for raw_path in raw_paths:
        path = Path(raw_path)
        if not path.is_absolute():
            path = cwd / path
        path = path.resolve()
        if path not in seen:
            seen.add(path)
            resolved.append(path)
    return resolved


def _syntax_error(path: Path) -> tuple[str, str] | None:
    suffix = path.suffix.lower()
    if suffix == ".sh":
        result = subprocess.run(
            ["bash", "-n", str(path)],
            capture_output=True,
            text=True,
            check=False,
        )
        if result.returncode != 0:
            return "bash syntax check (bash -n)", result.stderr.strip()
        return None

    if suffix == ".json":
        try:
            with path.open(encoding="utf-8") as stream:
                json.load(stream)
        except Exception as error:
            return "JSON parse", str(error)
        return None

    if suffix == ".py":
        try:
            py_compile.compile(str(path), doraise=True)
        except py_compile.PyCompileError as error:
            return "Python compile (py_compile)", str(error)
        return None

    if suffix in (".props", ".csproj", ".targets", ".xml"):
        try:
            text = path.read_text(encoding="utf-8")
        except OSError as error:
            return "XML read", str(error)
        if "<!DOCTYPE" in text or "<!ENTITY" in text:
            return (
                "XML safety check",
                "contains <!DOCTYPE or <!ENTITY (XXE / entity-expansion guard)",
            )
        try:
            ElementTree.parse(path)
        except ElementTree.ParseError as error:
            return "XML well-formed check (xml.etree.ElementTree.parse)", str(error)
    return None


def post_edit_validate(payload: dict[str, Any]) -> int:
    for path in _affected_paths(payload):
        if not path.is_file():
            continue
        failure = _syntax_error(path)
        if failure is None:
            continue
        check, error = failure
        print(
            f"[post-edit-validate] {path} — {check} failed:\n{error}",
            file=sys.stderr,
        )
        return 2
    return 0


def _scrub_string(value: str) -> str:
    scrubbed = value
    for pattern, replacement in _VALUE_PATTERNS:
        scrubbed = pattern.sub(replacement, scrubbed)
    return scrubbed


def _scrub(value: Any) -> Any:
    if isinstance(value, dict):
        return {
            key: _REDACTED if _SENSITIVE_KEY.search(str(key)) else _scrub(item)
            for key, item in value.items()
        }
    if isinstance(value, list):
        return [_scrub(item) for item in value]
    if isinstance(value, str):
        return _scrub_string(value)
    return value


def _first_value(value: Any, keys: tuple[str, ...]) -> Any:
    if isinstance(value, dict):
        for key in keys:
            if key in value:
                return value[key]
        for child in value.values():
            found = _first_value(child, keys)
            if found is not None:
                return found
    elif isinstance(value, list):
        for child in value:
            found = _first_value(child, keys)
            if found is not None:
                return found
    return None


def _codex_failure(response: Any) -> tuple[bool, Any, Any]:
    exit_code = _first_value(response, ("exit_code", "exitCode"))
    success = _first_value(response, ("success",))
    status = _first_value(response, ("status",))

    failed = False
    if isinstance(exit_code, int):
        failed = exit_code != 0
    elif success is False:
        failed = True
    elif isinstance(status, str) and status.lower() in ("failed", "failure", "error"):
        failed = True

    if not failed:
        return False, "", None

    error = _first_value(response, ("error", "stderr", "output", "message"))
    if error is None:
        error = response
    duration = _first_value(response, ("duration_ms", "durationMs"))
    return True, error, duration


def observe_tool_failure(payload: dict[str, Any]) -> int:
    tool_name = payload.get("tool_name", "")
    if not isinstance(tool_name, str) or not tool_name:
        return 0

    if "error" in payload:
        error: Any = payload.get("error", "")
        duration_ms = payload.get("duration_ms")
        is_interrupt = bool(payload.get("is_interrupt", False))
    else:
        failed, error, duration_ms = _codex_failure(payload.get("tool_response"))
        if not failed:
            return 0
        is_interrupt = False

    tool_input = payload.get("tool_input") or {}
    record: dict[str, Any] = {
        "ts": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "session": payload.get("session_id", "") or "",
        "tool": tool_name,
        "tool_use_id": payload.get("tool_use_id", "") or "",
        "input": _scrub(tool_input),
        "error": _scrub(error),
        "is_interrupt": is_interrupt,
    }
    if duration_ms is not None:
        record["duration_ms"] = duration_ms

    failure_file = Path(
        os.environ.get(
            "AGENT_FAILURE_FILE",
            str(REPO_ROOT / ".claude" / "failures.jsonl"),
        )
    )
    failure_file.parent.mkdir(parents=True, exist_ok=True)
    with failure_file.open("a", encoding="utf-8") as stream:
        stream.write(json.dumps(record, ensure_ascii=False) + "\n")
    return 0


ACTIONS: dict[str, Callable[[dict[str, Any]], int]] = {
    "session-context": session_context,
    "pre-tool-edit": pre_tool_edit,
    "pre-tool-bash": pre_tool_bash,
    "post-edit-validate": post_edit_validate,
    "observe-tool-failure": observe_tool_failure,
}


def main() -> int:
    if len(sys.argv) != 2 or sys.argv[1] not in ACTIONS:
        choices = ", ".join(sorted(ACTIONS))
        print(f"usage: {Path(sys.argv[0]).name} <action>; actions: {choices}", file=sys.stderr)
        return 64
    return ACTIONS[sys.argv[1]](_load_payload())


if __name__ == "__main__":
    sys.exit(main())
