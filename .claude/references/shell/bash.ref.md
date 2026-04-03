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

## GitLab: `glab api` POST note (preferred in this repo)

1. Prepare `comment.md` with Markdown.
2. Post via API (use `--raw-field` for Markdown body):

Same-repo (default, relies on current git repo context):

```bash
body="$(cat comment.md)"

# Endpoint uses project inferred by glab via :fullpath
glab api projects/:fullpath/merge_requests/<MR_IID>/notes --method POST -f "body=$body"
```

Cross-repo (recommended: explicit project ID + `-R`):

```bash
body="$(cat comment.md)"

glab api -R "group/namespace/project" "projects/<PROJECT_ID>/merge_requests/<MR_IID>/notes" --method POST -f "body=$body"
```

Verification (recommended): fetch notes and search for your stable marker:

```bash
marker="ai-code-review:gitlab-mr:<MR_IID>:bug-01"

# Use cross-repo form when needed
glab api -R "group/namespace/project" "projects/<PROJECT_ID>/merge_requests/<MR_IID>/notes" --method GET | grep -F "$marker"
```
