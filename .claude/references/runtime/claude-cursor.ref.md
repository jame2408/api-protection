# Runtime Mapping: Claude Code / Cursor (Generic)

This file maps abstract capabilities in `skills/code-review/SKILL.md` to common tool names seen in other agent runtimes.

## Tool Name Mapping (Common)

- Run commands: `run_terminal_cmd`
- Exact search: `grep`
- Semantic search: `codebase_search`
- Read files: `read_file`
- Find files by pattern: `glob_file_search`

## Notes

- Tool names vary by runtime/version; treat this as a reference mapping.
- If your runtime uses different names, update this file to match your environment.
