$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

$gitCommand = Get-Command git -ErrorAction Stop
& $gitCommand.Source -C $repoRoot config core.hooksPath .githooks

Write-Host 'Configured git hooksPath to .githooks.'
Write-Host 'The ask_user harness pre-commit check is now active for this repository.'
