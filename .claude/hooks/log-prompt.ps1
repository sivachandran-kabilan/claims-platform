$in = [Console]::In.ReadToEnd() | ConvertFrom-Json
$ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"
$root = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }
Add-Content (Join-Path $root "docs/prompt-log.md") "`n## $ts`n$($in.prompt)`n"
