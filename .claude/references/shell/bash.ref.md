# Shell Notes: bash / zsh

Use this file when a reference mentions “clear proxy” or “read content from file”.

## Clear Proxy (Optional)

If VCS CLI calls fail due to proxy/network issues:

```bash
unset HTTP_PROXY HTTPS_PROXY ALL_PROXY
export NO_PROXY='*'
```

## Read Markdown from File into a Variable

```bash
body="$(cat comment.md)"
```

## GitHub: Prefer `gh pr comment --body-file`

```bash
gh pr comment <ID> --body-file comment.md
```

Cross-repo:

```bash
gh pr comment <ID> -R owner/repo --body-file comment.md
```

Verification (recommended): fetch comments and search for your stable marker:

```bash
marker="ai-code-review:github-pr:<ID>:bug-01"

gh pr view <ID> --json comments --jq '.comments[].body' | grep -F "$marker"
```
