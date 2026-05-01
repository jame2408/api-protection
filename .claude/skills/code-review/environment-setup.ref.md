# Environment Setup Reference

## Phase 0: Environment Detection

> ⚠️ Must determine `{CONFIG_ROOT}` before loading any references.

Check project root for the first matching AI config directory:

| Directory | Sets |
|-----------|------|
| `.claude/` | `{CONFIG_ROOT}` = `.claude` |
| `.cursor/` | `{CONFIG_ROOT}` = `.cursor` |
| `.gemini/` | `{CONFIG_ROOT}` = `.gemini` |
| `.github/copilot/` | `{CONFIG_ROOT}` = `.github/copilot` |
| `ai-config/` | `{CONFIG_ROOT}` = `ai-config` |

Cache `{CONFIG_ROOT}` for the entire conversation. Set `{SKILL_DIR}` = `{CONFIG_ROOT}/skills/code-review`.

## Tool & Shell Compatibility

This repo supports multiple agent runtimes and shells. Load the appropriate reference for your environment:

- **Runtime**: Warp → `{CONFIG_ROOT}/references/runtime/warp.ref.md` | Claude/Cursor → `{CONFIG_ROOT}/references/runtime/claude-cursor.ref.md`
- **Shell**: PowerShell → `{CONFIG_ROOT}/references/shell/powershell.ref.md` | bash/zsh → `{CONFIG_ROOT}/references/shell/bash.ref.md`
