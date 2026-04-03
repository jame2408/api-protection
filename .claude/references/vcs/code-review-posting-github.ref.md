# Code Review: GitHub PR Posting

Code Review Skill 在 **PR Mode (GitHub)** 發布留言時使用。詳見 `vcs-platform-commands.ref.md` 的 Platform Detection 與 Posting Comments 區塊。

---

## GitHub CLI Quick Reference

### Essential Commands

```bash
# View PR details
gh pr view <ID>
gh pr view <ID> --json title,body,isDraft,state,url,repository

# View diff
gh pr diff <ID>

# Checkout branch
gh pr checkout <ID>

# Cross-repo
gh pr view <ID> -R owner/repo
```

---

## Posting PR Comments

### Option 1: GitHub CLI (Recommended)

```bash
# Post comment from file (recommended)
gh pr comment <ID> --body-file comment.md

# Or from literal (avoid for long Markdown)
gh pr comment <ID> --body "Your comment here"
```

> Prefer `--body-file` to avoid escaping issues with code blocks in Markdown.

### Option 2: GitHub API via `gh api` (Cross-shell)

When `gh pr comment` is not sufficient (e.g., custom formatting, batch posting), use `gh api`.

- Endpoint: `POST /repos/{owner}/{repo}/issues/{pr_number}/comments`
  - Note: PR comments use the Issues API; PR number equals issue number.

Examples (prepare `comment.md` first):

```bash
# bash/zsh
repo=$(gh pr view <ID> --json repository -q '.repository.nameWithOwner')
body="$(cat comment.md)"

gh api -X POST "/repos/$repo/issues/<ID>/comments" -f "body=$body"
```

```powershell
# PowerShell
$repo = gh pr view <ID> --json repository -q '.repository.nameWithOwner'
$body = Get-Content -Path "comment.md" -Raw -Encoding UTF8

gh api -X POST "/repos/$repo/issues/<ID>/comments" -f "body=$body"
```

---

## Batch Posting Multiple Comments (One issue per comment)

Use this mode when you want to post **N separate comments** (e.g. 2 Bugs + 3 Suggestions) so the author can reply and resolve them one-by-one.

### File Naming Convention (Recommended: scope by PR number)

To avoid collisions across different reviews, create files under a PR-scoped directory:

- Directory: `comments/pr-<PR_NUMBER>/`
- Files:
  - `comments/pr-<PR_NUMBER>/bug-01.md`, `comments/pr-<PR_NUMBER>/bug-02.md`, ...
  - `comments/pr-<PR_NUMBER>/suggestion-01.md`, `comments/pr-<PR_NUMBER>/suggestion-02.md`, ...

### Required: Unique Marker per Comment

At the top of each comment file, add a stable marker so a future re-review can locate it via API:

- Bug example (first line): `<!-- ai-code-review:github-pr:<PR_NUMBER>:bug-01 -->`
- Suggestion example (first line): `<!-- ai-code-review:github-pr:<PR_NUMBER>:suggestion-01 -->`

### Recommended: `gh pr comment --body-file`

```bash
PR=<PR_NUMBER>
dir="comments/pr-$PR"

for f in "$dir"/bug-*.md "$dir"/suggestion-*.md; do
  [ -f "$f" ] || continue
  gh pr comment $PR --body-file "$f"
done
```

```powershell
$PR = <PR_NUMBER>
$dir = "comments\pr-$PR"

Get-ChildItem "$dir\bug-*.md", "$dir\suggestion-*.md" | ForEach-Object {
  gh pr comment $PR --body-file $_.FullName
}
```
