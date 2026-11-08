# Renames this template to a new company and project name (Windows PowerShell 5.1).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/new-project.ps1 <NewCompany> <NewProject>
#   e.g. ... -File scripts/new-project.ps1 YourCompany YourProject
#
# Same behaviour as scripts/new-project.sh: reads the CURRENT names from
# Directory.Build.props and replaces their PascalCase and lowercase forms, whole
# tokens only, in file contents, file names and folder names, in a single pass.
# Skips .git, bin, obj, TestResults and binary files; keeps bytes (line endings,
# BOMs) unchanged except the replaced names. Generates a new UserSecretsId and
# regenerates docs/openapi.json. Refuses with uncommitted git changes, invalid
# names, or new names that already occur; does nothing when the names are unchanged.
param(
    [Parameter(Mandatory = $true, Position = 0)] [string] $NewCompany,
    [Parameter(Mandatory = $true, Position = 1)] [string] $NewProject
)
$ErrorActionPreference = 'Continue'
Set-Location (Join-Path $PSScriptRoot '..')
$root = (Get-Location).Path

function Fail([string] $Message) {
    Write-Host "new-project: $Message" -ForegroundColor Red
    exit 1
}

# Git state (native stderr must not become a terminating error in 5.1).
$null = git rev-parse --is-inside-work-tree 2>&1
if ($LASTEXITCODE -eq 0) {
    $dirty = git status --porcelain
    if ($dirty) {
        Write-Host ($dirty -join "`n")
        Fail 'the git working tree has uncommitted changes; commit or stash them first.'
    }
}
else {
    Write-Host 'new-project: WARNING: not a git repository; changes cannot be reviewed or undone with git.' -ForegroundColor Yellow
}
$ErrorActionPreference = 'Stop'

[xml] $props = Get-Content 'Directory.Build.props'
$oldCompany = ($props.Project.PropertyGroup | Where-Object { $_.COMPANY_NAME } | Select-Object -First 1).COMPANY_NAME
$oldProject = ($props.Project.PropertyGroup | Where-Object { $_.PROJECT_NAME } | Select-Object -First 1).PROJECT_NAME
if (-not $oldCompany -or -not $oldProject) { Fail 'COMPANY_NAME or PROJECT_NAME not found in Directory.Build.props' }

foreach ($value in $NewCompany, $NewProject) {
    if ($value -cnotmatch '^[A-Z][A-Za-z0-9]*$') {
        Fail "invalid name '$value': use PascalCase, letters and digits only (e.g. YourCompany, YourProject)"
    }
}

# Ordinal dictionary: PowerShell hashtables are case-insensitive.
$mapping = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
$newNames = @()
$oldNames = @()
foreach ($pair in @(@($oldCompany, $NewCompany), @($oldProject, $NewProject))) {
    if ($pair[0] -cne $pair[1]) {
        $mapping[$pair[0]] = $pair[1]
        $mapping[$pair[0].ToLowerInvariant()] = $pair[1].ToLowerInvariant()
        $oldNames += $pair[0]
        $newNames += $pair[1]
    }
}
if ($mapping.Count -eq 0) {
    Write-Host "new-project: names are already $oldCompany/$oldProject; nothing to do."
    exit 0
}

function New-TokenRegex([string[]] $Words, [System.Text.RegularExpressions.RegexOptions] $Options) {
    $alternation = ($Words | Sort-Object Length -Descending | ForEach-Object { [regex]::Escape($_) }) -join '|'
    return New-Object System.Text.RegularExpressions.Regex ("(?<![A-Za-z0-9])($alternation)(?![A-Za-z0-9])", $Options)
}
$replaceRegex = New-TokenRegex ([string[]] $mapping.Keys) ([System.Text.RegularExpressions.RegexOptions]::None)
$newRegex = New-TokenRegex $newNames ([System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
$evaluator = [System.Text.RegularExpressions.MatchEvaluator] { param($m) $mapping[$m.Groups[1].Value] }

$skipDirs = @('.git', 'bin', 'obj', 'TestResults', '.vs')
function Get-RepoItems([string] $Path) {
    foreach ($item in Get-ChildItem -LiteralPath $Path -Force) {
        if ($item.PSIsContainer) {
            if ($skipDirs -notcontains $item.Name) {
                Get-RepoItems $item.FullName
                $item
            }
        }
        else { $item }
    }
}
$items = @(Get-RepoItems $root)   # children before their folder (deepest first)
$strictUtf8 = New-Object System.Text.UTF8Encoding ($false, $true)

$texts = @{}
foreach ($file in $items | Where-Object { -not $_.PSIsContainer }) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    $probe = [Math]::Min($bytes.Length, 8000)
    if ($probe -gt 0 -and [Array]::IndexOf($bytes, [byte]0, 0, $probe) -ge 0) { continue }
    try { $texts[$file.FullName] = $strictUtf8.GetString($bytes) } catch { continue }
}

function Get-Relative([string] $Full) { '.' + $Full.Substring($root.Length).Replace('\', '/') }

# Refuse when a new name already occurs (contents or paths), case-insensitively.
$clashes = @()
foreach ($path in $texts.Keys) { if ($newRegex.IsMatch($texts[$path])) { $clashes += Get-Relative $path } }
foreach ($item in $items) { if ($newRegex.IsMatch($item.Name)) { $clashes += Get-Relative $item.FullName } }
if ($clashes.Count -gt 0) {
    Fail ("the new name(s) $($newNames -join '/') already occur in:`n  " + (($clashes | Sort-Object -Unique) -join "`n  "))
}

# 1. File contents (single pass).
$changed = 0
foreach ($path in ($texts.Keys | Sort-Object)) {
    $text = $texts[$path]
    $count = $replaceRegex.Matches($text).Count
    if ($count -gt 0) {
        $newText = $replaceRegex.Replace($text, $evaluator)
        [IO.File]::WriteAllBytes($path, $strictUtf8.GetBytes($newText))
        $changed++
        Write-Host "  edited  $(Get-Relative $path) ($count)"
    }
}

# 2. File and folder names, deepest first.
$renamed = 0
foreach ($item in $items) {
    $newName = $replaceRegex.Replace($item.Name, $evaluator)
    if ($newName -cne $item.Name) {
        Rename-Item -LiteralPath $item.FullName -NewName $newName
        $renamed++
        Write-Host "  renamed $(Get-Relative $item.FullName) -> $newName"
    }
}

# 3. New UserSecretsId.
$api = Join-Path $root 'src/Api/Api.csproj'
$csproj = $strictUtf8.GetString([IO.File]::ReadAllBytes($api))
$secretsId = [guid]::NewGuid().ToString()
$secretsRegex = New-Object System.Text.RegularExpressions.Regex '<UserSecretsId>[^<]*</UserSecretsId>'
if ($secretsRegex.Matches($csproj).Count -ne 1) { Fail 'UserSecretsId not found in src/Api/Api.csproj' }
$csproj = $secretsRegex.Replace($csproj, "<UserSecretsId>$secretsId</UserSecretsId>")
[IO.File]::WriteAllBytes($api, $strictUtf8.GetBytes($csproj))
Write-Host "  new UserSecretsId $secretsId in ./src/Api/Api.csproj"

Write-Host "new-project: $($oldNames -join '/') -> $($newNames -join '/'): $changed files edited, $renamed paths renamed."

Write-Host 'new-project: regenerating docs/openapi.json'
& (Join-Path $PSScriptRoot 'generate-openapi.ps1')
if ($LASTEXITCODE -ne 0) { Fail 'OpenAPI regeneration failed' }
Write-Host 'new-project: done. Review the changes, then run the verify script.'
