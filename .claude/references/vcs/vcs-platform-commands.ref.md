# VCS Platform Commands (Code Review)

Platform-specific commands for fetching PR/MR details, diff, checkout, and posting comments. Use this file to select the correct commands based on the detected platform.

---

## Platform Detection

Detect platform from `git remote get-url origin`:

| Remote URL contains | Platform | CLI Tool |
|---------------------|----------|----------|
| `gitlab` | GitLab | glab |
| `github` | GitHub | gh |
| (other) | Unknown | Fallback to Self Mode or prompt user |

> If platform cannot be detected, default to GitLab (glab) for backward compatibility, or ask user to specify.

---

## Command Matrix

| Operation | GitLab (glab) | GitHub (gh) |
|-----------|---------------|-------------|
| **Fetch details** | `glab mr view <ID>` | `gh pr view <ID>` |
| **Fetch details (JSON)** | `glab mr view <ID> -F json` | `gh pr view <ID> --json title,body,isDraft,state,url` |
| **Fetch diff** | `glab mr diff <ID>` | `gh pr diff <ID>` |
| **Checkout branch** | `glab mr checkout <ID>` | `gh pr checkout <ID>` |
| **Cross-repo** | `glab mr view <ID> -R "group/namespace/project"` | `gh pr view <ID> -R owner/repo` |

---

## URL & Reference Patterns (ID Extraction)

| Pattern Type | GitLab | GitHub |
|--------------|--------|--------|
| **URL regex** | `merge_requests/(\d+)` | `pull/(\d+)` |
| **Reference regex** | `MR\s*#?\s*(\d+)` | `PR\s*#?\s*(\d+)` |
| **Generic (both)** | `/code-review <number>` → use extracted number for either platform |

---

## Posting Comments (Platform-Specific Ref)

| Platform | Reference File |
|----------|----------------|
| GitLab | `{CONFIG_ROOT}/references/vcs/code-review-posting-gitlab.ref.md` |
| GitHub | `{CONFIG_ROOT}/references/vcs/code-review-posting-github.ref.md` |

> Before posting, read the appropriate file for API endpoints, auth, and platform-specific notes.

---

## Pre-Fetch Notes (Proxy, etc.)

If VCS CLI calls fail due to proxy/network issues, clear proxy env vars **based on your shell**:

- PowerShell: see `{CONFIG_ROOT}/references/shell/powershell.ref.md`
- bash/zsh: see `{CONFIG_ROOT}/references/shell/bash.ref.md`

---

## Draft / WIP Check

| Platform | How to detect draft |
|----------|---------------------|
| GitLab | From `glab mr view -F json` → check `state` or title contains "WIP"/"Draft" |
| GitHub | From `gh pr view --json isDraft` → `isDraft: true` |
