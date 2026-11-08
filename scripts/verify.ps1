# Single verify entry point for Windows; same checks and order as verify.sh.
# Exits non-zero on the first failure.
# Requires: .NET SDK (global.json), git, Docker.
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

function Fail([string] $Message) {
    Write-Host "VERIFY FAILED: $Message" -ForegroundColor Red
    exit 1
}

function Step([string] $Message) {
    Write-Host ''
    Write-Host "==> $Message"
}

function Get-EnvExampleValue([string] $Key) {
    $line = Get-Content '.env.example' | Where-Object { $_ -match "^$Key=" } | Select-Object -First 1
    if (-not $line) { Fail "$Key missing in .env.example" }
    return $line.Substring($Key.Length + 1)
}

# The solution is the single *.slnx file in the repository root (no name literal here).
$solutions = @(Get-ChildItem -File -Filter '*.slnx' | Select-Object -ExpandProperty Name)
if ($solutions.Count -ne 1) { Fail "expected exactly one *.slnx in the repository root, found $($solutions.Count)" }
$solution = $solutions[0]

# 1. Restore and build (Release, 0 warnings; NuGetAudit also fails restore).
Step '1. Restore and build (Release, 0 warnings)'
dotnet restore $solution
if ($LASTEXITCODE -ne 0) { Fail 'restore failed' }
$buildOutput = dotnet build $solution -c Release --no-restore -tl:off
$buildExit = $LASTEXITCODE
$buildOutput | ForEach-Object { Write-Host $_ }
if ($buildExit -ne 0) { Fail 'build failed' }
if (-not ($buildOutput -match '^\s+0 Warning\(s\)')) { Fail 'build produced warnings' }
if (-not ($buildOutput -match '^\s+0 Error\(s\)')) { Fail 'build produced errors' }

# 2. Formatting.
Step '2. dotnet format --verify-no-changes'
dotnet format $solution --verify-no-changes
if ($LASTEXITCODE -ne 0) { Fail 'formatting differs; run: dotnet format' }

# 3. Tests.
Step '3. dotnet test'
# TRX reports go to TestResults/ (git-ignored); CI uploads them as an artifact.
Remove-Item 'TestResults' -Recurse -Force -ErrorAction SilentlyContinue
dotnet test --solution $solution -c Release --no-build --report-xunit-trx --results-directory (Join-Path (Get-Location) 'TestResults')
if ($LASTEXITCODE -ne 0) { Fail 'tests failed' }

# 4. Vulnerable and deprecated packages (parsed; the command itself exits 0).
Step '4. Vulnerable and deprecated packages'
& (Join-Path $PSScriptRoot 'check-packages.ps1') $solution
if ($LASTEXITCODE -ne 0) { Fail 'vulnerable or deprecated packages' }

# 5. Placeholder check. These files name the placeholder on purpose.
Step '5. Placeholder check (MyApp)'
$found = git grep --untracked -n -i 'MyApp' -- . ':!scripts/verify.sh' ':!scripts/verify.ps1'
if ($LASTEXITCODE -eq 0) { $found | ForEach-Object { Write-Host $_ }; Fail 'placeholder MyApp found (see lines above)' }
if ($LASTEXITCODE -gt 1) { Fail "git grep failed with exit code $LASTEXITCODE" }
Write-Host '    ok'

# 6. Naming consistency and TLS rule. The names are read from Directory.Build.props
# and .env.example; this script contains no project or company name.
Step '6. Naming checks and TLS check'
$projectName = Get-EnvExampleValue 'PROJECT_NAME'
$imageName = Get-EnvExampleValue 'IMAGE_NAME'
$dbName = Get-EnvExampleValue 'DB_NAME'
$appAssembly = Get-EnvExampleValue 'APP_ASSEMBLY'
[xml] $props = Get-Content 'Directory.Build.props'
$propsProject = ($props.Project.PropertyGroup | Where-Object { $_.PROJECT_NAME } | Select-Object -First 1).PROJECT_NAME
$propsCompany = ($props.Project.PropertyGroup | Where-Object { $_.COMPANY_NAME } | Select-Object -First 1).COMPANY_NAME

if (-not $propsCompany) { Fail 'COMPANY_NAME missing in Directory.Build.props' }
if (-not $projectName) { Fail 'PROJECT_NAME missing in .env.example' }
if ($imageName -cne $projectName.ToLowerInvariant()) { Fail "IMAGE_NAME ($imageName) must equal lowercase PROJECT_NAME" }
if ($dbName -cne $projectName) { Fail "DB_NAME ($dbName) must equal PROJECT_NAME ($projectName)" }
if ($projectName -cne $propsProject) { Fail "PROJECT_NAME differs: .env.example=$projectName Directory.Build.props=$propsProject" }
if ($appAssembly -cne "$propsCompany.$propsProject.Api") { Fail "APP_ASSEMBLY ($appAssembly) must be $propsCompany.$propsProject.Api" }
# PascalCase and lowercase forms of both names (case-insensitive, fixed strings).
$found = git grep --untracked -n -i -F -e $propsCompany -e $propsCompany.ToLowerInvariant() -e $propsProject -e $propsProject.ToLowerInvariant() -- docker-compose.yml Dockerfile '.github/workflows/*.yml' '**/appsettings*.json'
if ($LASTEXITCODE -eq 0) { $found | ForEach-Object { Write-Host $_ }; Fail "project or company name ($propsCompany/$propsProject) found in compose/Dockerfile/workflows/appsettings" }
if ($LASTEXITCODE -gt 1) { Fail "git grep failed with exit code $LASTEXITCODE" }
$found = git grep --untracked -n -i 'TrustServerCertificate' -- . ':!docs/' ':!scripts/verify.sh' ':!scripts/verify.ps1'
if ($LASTEXITCODE -eq 0) { $found | ForEach-Object { Write-Host $_ }; Fail 'TrustServerCertificate found (see lines above). Use Encrypt=false only in Development/Testing (ADR 0011).' }
if ($LASTEXITCODE -gt 1) { Fail "git grep failed with exit code $LASTEXITCODE" }
Write-Host '    ok'

# 7. OpenAPI: deterministic and identical to the committed docs/openapi.json.
Step '7. OpenAPI: generate twice, compare hashes and the committed docs/openapi.json'
$hashes = @()
foreach ($run in 1, 2) {
    & (Join-Path $PSScriptRoot 'generate-openapi.ps1')
    if ($LASTEXITCODE -ne 0) { Fail "OpenAPI generation failed (run $run)" }
    $hashes += (Get-FileHash 'docs/openapi.json' -Algorithm SHA256).Hash.ToLowerInvariant()
}
Write-Host "    run 1: $($hashes[0])"
Write-Host "    run 2: $($hashes[1])"
if ($hashes[0] -ne $hashes[1]) { Fail 'OpenAPI generation is not deterministic' }
git ls-files --error-unmatch docs/openapi.json *> $null
if ($LASTEXITCODE -ne 0) { Fail 'docs/openapi.json is not committed. Regenerate: powershell -File scripts/generate-openapi.ps1' }
git diff --quiet -- docs/openapi.json
if ($LASTEXITCODE -ne 0) { Fail 'docs/openapi.json is out of date. Regenerate (powershell -File scripts/generate-openapi.ps1) and commit it.' }
Write-Host '    ok'

# 8. Docker image builds (no push).
Step '8. docker build (no push)'
docker build --build-arg "APP_ASSEMBLY=$appAssembly" -t "${imageName}:verify" .
if ($LASTEXITCODE -ne 0) { Fail 'docker build failed' }

Write-Host ''
Write-Host 'VERIFY PASSED'
