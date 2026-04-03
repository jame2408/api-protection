# Runtime Mapping: Warp (Oz)

This file maps abstract capabilities in `skills/code-review/SKILL.md` to Warp tool names.

## Tool Name Mapping

- Run commands: `run_shell_command`
- Exact search: `grep`
- Semantic search: `codebase_semantic_search`
- Read files: `read_files`
- Find files by pattern: `file_glob`

## Notes

- Prefer `grep` for exact symbols/types/constants.
- Prefer `codebase_semantic_search` when you don't know the exact name, or when you need “where is this used?” style queries.
- Prefer reading templates / references via `read_files` (do not assume their content).
