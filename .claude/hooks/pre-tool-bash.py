#!/usr/bin/env python3
"""PreToolUse hook — Bash command guard against two observed harness incidents.

Blocks Bash commands that contain:
  (A) a heredoc (`<<` / `<<-`, but not the `<<<` herestring) — heredoc writes
      have caused this harness to hang in the background for hours; use the
      Write tool to create/overwrite files, or to stage a script before
      executing it.
  (B) a zsh bare `=`-prefixed token (e.g. `==`, `=foo`) outside of quotes —
      zsh expands a leading `=word` to the path of the `word` command
      (`=word` expansion) and aborts the whole compound command when no such
      command exists, exactly as seen in the `(eval):N: == not found`
      incidents from unquoted `[ "$a" == "$b" ]`-style comparisons.

Both patterns are checked on a masked copy of the command: single-quoted
strings, double-quoted strings, and `$((...))` arithmetic expansions are
replaced with equal-length blanks first, so that legitimate uses like
`grep '<<' file`, `awk '$1 == "x"'`, or `$((a<<2))` are not false-positived.

Block mechanism: exit code 2 with an explanation on stderr (Claude reads it
and self-corrects). Exit 0 = allow. Malformed input never blocks.
"""
import json
import re
import sys

_SINGLE_QUOTED = re.compile(r"'[^']*'")
_DOUBLE_QUOTED = re.compile(r'"(?:[^"\\]|\\.)*"')
_ARITHMETIC = re.compile(r"\$\(\([^)]*\)\)")

_HEREDOC = re.compile(r"(?<!<)<<-?(?!<)")
_ZSH_EQUALS_TOKEN = re.compile(r"^=.+")


def _blank(match: "re.Match[str]") -> str:
    return " " * len(match.group(0))


def _mask(command: str) -> str:
    masked = _SINGLE_QUOTED.sub(_blank, command)
    masked = _DOUBLE_QUOTED.sub(_blank, masked)
    masked = _ARITHMETIC.sub(_blank, masked)
    return masked


def main() -> None:
    try:
        data = json.load(sys.stdin)
    except Exception:
        sys.exit(0)

    if data.get("tool_name", "") != "Bash":
        sys.exit(0)

    tool_input = data.get("tool_input") or {}
    command = tool_input.get("command", "") or ""
    if not command:
        sys.exit(0)

    masked = _mask(command)

    violations = []

    if _HEREDOC.search(masked):
        violations.append(
            "heredoc（<<）建立/覆寫檔案改用 Write 工具；需餵 stdin 給程式時，"
            "先用 Write 寫腳本檔再執行（heredoc 在本 harness 曾致背景卡死，"
            "見 tasks/lessons.md heredoc 條）"
        )

    bad_tokens = [tok for tok in masked.split() if _ZSH_EQUALS_TOKEN.match(tok)]
    if bad_tokens:
        violations.append(
            "zsh 對裸 `=` 開頭參數（例如 "
            + ", ".join(repr(t) for t in bad_tokens)
            + "）做 =word 展開會直接報錯，請加引號（如 echo '===' ）"
        )

    if violations:
        lines = "\n".join("  - " + v for v in violations)
        print(
            f"[pre-tool-bash] blocked Bash command — write/quoting guard:\n{lines}",
            file=sys.stderr,
        )
        sys.exit(2)

    sys.exit(0)


if __name__ == "__main__":
    main()
