# Fails when any package (top-level or transitive) is vulnerable or deprecated.
# Usage: powershell -File scripts/check-packages.ps1 [project-or-solution]
#
# `dotnet package list --vulnerable` exits 0 even when it finds something
# (NuGet/Home#11315), so the JSON output is parsed and the script fails explicitly.
param([string] $Target = '')
$ErrorActionPreference = 'Stop'

# Default: the single *.slnx in the repository root.
if (-not $Target) {
    $Target = Get-ChildItem -Path (Join-Path $PSScriptRoot '..') -File -Filter '*.slnx' |
        Select-Object -First 1 -ExpandProperty FullName
}

function Test-Packages([string] $Kind, [string[]] $Options) {
    $json = dotnet package list --project $Target @Options --format json | Out-String
    if ($LASTEXITCODE -ne 0) { throw "dotnet package list failed with exit code $LASTEXITCODE" }
    $report = $json | ConvertFrom-Json
    $findings = @()
    foreach ($problem in @($report.problems)) { if ($problem) { $findings += "problem: $($problem.text)" } }
    foreach ($project in @($report.projects)) {
        foreach ($problem in @($project.problems)) { if ($problem) { $findings += "$($project.path): problem: $($problem.text)" } }
        foreach ($framework in @($project.frameworks)) {
            if (-not $framework) { continue }
            foreach ($group in 'topLevelPackages', 'transitivePackages') {
                foreach ($package in @($framework.$group)) {
                    if ($package) { $findings += "$($project.path): $($package.id) $($package.resolvedVersion) ($group)" }
                }
            }
        }
    }
    if ($findings.Count -gt 0) {
        Write-Host "$Kind packages found:" -ForegroundColor Red
        $findings | ForEach-Object { Write-Host "  $_" }
        exit 1
    }
    Write-Host "    no $Kind packages"
}

Test-Packages 'vulnerable' @('--vulnerable', '--include-transitive')
Test-Packages 'deprecated' @('--deprecated', '--include-transitive')
