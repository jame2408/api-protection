# VCS Platform Commands (Code Review)

GitHub-specific commands for fetching PR details, diff, checkout, and posting comments. This project uses GitHub only; GitLab support has been removed.

---

## Platform Detection

Detect platform from `git remote get-url origin`:

| Remote URL contains | Platform | CLI Tool |
|---------------------|----------|----------|
| `github` | GitHub | gh |
| (other) | Unsupported | Fallback to Self Mode or prompt user |

> This project only supports GitHub. If the remote does not contain `github`, the skill should not enter PR Mode automatically — fall back to Self Mode or ask the user.

---

## Command Matrix

| Operation | GitHub (gh) |
|-----------|-------------|
| **Fetch details** | `gh pr view <ID>` |
| **Fetch details (JSON)** | `gh pr view <ID> --json title,body,isDraft,state,url` |
| **Fetch diff** | `gh pr diff <ID>` |
| **Checkout branch** | `gh pr checkout <ID>` |
| **Cross-repo** | `gh pr view <ID> -R owner/repo` |

---

## URL & Reference Patterns (ID Extraction)

| Pattern Type | GitHub |
|--------------|--------|
| **URL regex** | `pull/(\d+)` |
| **Reference regex** | `PR\s*#?\s*(\d+)` |
| **Generic** | `/code-review <number>` → use extracted number directly |

---

## Posting Comments

| Platform | Reference File |
|----------|----------------|
| GitHub | `{CONFIG_ROOT}/references/vcs/code-review-posting-github.ref.md` |

> Before posting, read the file above for API endpoints, auth, and platform-specific notes.

---

## Pre-Fetch Notes (Proxy, etc.)

If VCS CLI calls fail due to proxy/network issues, clear proxy env vars **based on your shell**:

- PowerShell: see `{CONFIG_ROOT}/references/shell/powershell.ref.md`
- bash/zsh: see `{CONFIG_ROOT}/references/shell/bash.ref.md`

---

## Draft / WIP Check

| Platform | How to detect draft |
|----------|---------------------|
| GitHub | From `gh pr view --json isDraft` → `isDraft: true` |
