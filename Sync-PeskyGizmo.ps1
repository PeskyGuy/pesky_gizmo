# Sync-PeskyGizmo.ps1
# Copies clean mod assets from F: (Source) to D: (Release).
# Excludes all source/build files per the Clean vs. Messy rule.
#
# Usage: .\Sync-PeskyGizmo.ps1
#        .\Sync-PeskyGizmo.ps1 -WhatIf

[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$Build
)

$Source  = "F:\Coding\Mods\pesky_gizmo"
$Release = "D:\SteamLibrary\steamapps\common\RimWorld\Mods\Pesky's Gizmo"

if ($Build) {
    Write-Host "=== Building Pesky's Gizmo ===" -ForegroundColor Cyan
    dotnet build "$Source\PeskyGizmo.sln" -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build FAILED." -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "Build successful." -ForegroundColor Green
    Write-Host ""
}

$ExcludeFiles = "*.cs *.csproj *.sln *.user *.md *.ps1"
$ExcludeDirs  = "Source Docs obj bin .git .vs"

Write-Host "=== Pesky's Gizmo -- Release Sync ===" -ForegroundColor Cyan
Write-Host "  From : $Source"
Write-Host "  To   : $Release"
Write-Host ""

$RoboArgs = @(
    "`"$Source`"",
    "`"$Release`"",
    "/E",
    "/PURGE",
    "/NFL",
    "/NDL",
    "/NP",
    "/XF $ExcludeFiles",
    "/XD $ExcludeDirs"
)

if ($WhatIfPreference) {
    Write-Host "[DRY RUN] Would run:" -ForegroundColor Yellow
    Write-Host "  robocopy $($RoboArgs -join ' ')"
}
else {
    $result = Start-Process -FilePath "robocopy" -ArgumentList $RoboArgs -Wait -PassThru -NoNewWindow
    if ($result.ExitCode -ge 8) {
        Write-Host "Sync FAILED (robocopy exit code $($result.ExitCode))." -ForegroundColor Red
        exit 1
    }
    Write-Host "Sync complete." -ForegroundColor Green
}
