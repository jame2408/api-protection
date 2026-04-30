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
# ... then run gh
```

## GitHub: Prefer `gh pr comment --body-file`

```powershell
gh pr comment <ID> --body-file comment.md
```

Cross-repo:

```powershell
gh pr comment <ID> -R "owner/repo" --body-file comment.md
```

## UTF-8 Display Notes (Chinese looks garbled in console)

If the GitHub UI shows correct content but your PowerShell console displays `�`:

```powershell
# Set console output encoding to UTF-8 for this session
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
```
