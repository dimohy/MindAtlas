Param()
Write-Host "Installing git hooks: setting core.hooksPath to .githooks"
& git config core.hooksPath .githooks
if ($LASTEXITCODE -eq 0) { Write-Host "Installed. You can now commit; pre-commit will run the ask_user check." } else { Write-Error "Failed to set core.hooksPath" }
