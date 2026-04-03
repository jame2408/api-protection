# Shell Notes: PowerShell

Use this file when a reference mentions “clear proxy” or “read content from file”.

## Clear Proxy (Optional)

If VCS CLI calls fail due to proxy/network issues:

```powershell
$env:HTTP_PROXY = ""
$env:HTTPS_PROXY = ""
$env:NO_PROXY = "*"
```

## Read Markdown from File into a Variable

```powershell
$body = Get-Content -Path "comment.md" -Raw -Encoding UTF8
```

## Command Chaining Notes (PowerShell)

- Avoid chaining with `&&` when the next command starts with a variable assignment (e.g. `&& $body = ...`). This can cause parser errors.
- Prefer using `;` or new lines.
- Prefer `Set-Location` over `cd ... && ...` when you need to change directories.

Example:

```powershell
Set-Location "D:\path\to\repo"
$body = Get-Content -Path "comment.md" -Raw -Encoding UTF8
# ... then run glab/gh
```

## GitHub: Prefer `gh pr comment --body-file`

```powershell
gh pr comment <ID> --body-file comment.md
```

## GitLab: `glab api` POST note (preferred in this repo)

```powershell
$body = Get-Content -Path "comment.md" -Raw -Encoding UTF8

# Same-repo: Endpoint uses project inferred by glab via :fullpath
glab api projects/:fullpath/merge_requests/<MR_IID>/notes --method POST -f "body=$body"
```

Cross-repo (recommended):

```powershell
$body = Get-Content -Path "comment.md" -Raw -Encoding UTF8

glab api -R "group/namespace/project" "projects/<PROJECT_ID>/merge_requests/<MR_IID>/notes" --method POST -f "body=$body"
```

## UTF-8 Display Notes (Chinese looks garbled in console)

If the GitLab UI shows correct content but your PowerShell console displays `�`:

```powershell
# Set console output encoding to UTF-8 for this session
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
```
