# Regenerates docs/openapi.json (build-time generator, Microsoft.Extensions.ApiDescription.Server).
# One-line regenerate command:  powershell -File scripts/generate-openapi.ps1 (tested with Windows PowerShell 5.1)
#
# The generator launches the app's entry point, so ValidateOnStart needs settings.
# They come from the SAFE dev-only placeholders in .env.example, passed as
# environment variables for this process only. No secret is stored in the repo.
# Environment = Production: no dev-only migration, no database connection.
# Keep the variables identical to scripts/generate-openapi.sh.
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

function Get-EnvExampleValue([string] $Key) {
    $line = Get-Content '.env.example' | Where-Object { $_ -match "^$Key=" } | Select-Object -First 1
    if (-not $line) { throw "$Key not found in .env.example" }
    return $line.Substring($Key.Length + 1)
}

Remove-Item 'docs/openapi.json' -ErrorAction SilentlyContinue

$saved = @{}
$values = @{
    'ASPNETCORE_ENVIRONMENT' = 'Production'
    'Database__Name'         = Get-EnvExampleValue 'DB_NAME'
    'Database__Password'     = Get-EnvExampleValue 'DB_PASSWORD'
    'Jwt__SigningKey'        = Get-EnvExampleValue 'JWT_SIGNING_KEY'
    # Redis needs ssl=true outside Development/Testing; the generator never connects.
    'Redis__ConnectionString' = "localhost:$(Get-EnvExampleValue 'REDIS_PORT'),password=$(Get-EnvExampleValue 'REDIS_PASSWORD'),ssl=true"
}
try {
    foreach ($name in $values.Keys) {
        $saved[$name] = [Environment]::GetEnvironmentVariable($name)
        [Environment]::SetEnvironmentVariable($name, $values[$name])
    }
    dotnet build src/Api -c Release --no-incremental -nologo -v q -p:GenerateOpenApiDocument=true
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }
}
finally {
    foreach ($name in $saved.Keys) {
        [Environment]::SetEnvironmentVariable($name, $saved[$name])
    }
}

# The build does not fail when the generator fails, so check the output exists.
if (-not (Test-Path 'docs/openapi.json') -or (Get-Item 'docs/openapi.json').Length -eq 0) {
    Write-Host 'OpenAPI generation produced no (or an empty) docs/openapi.json (see build output above).' -ForegroundColor Red
    exit 1
}

# It must also be a valid OpenAPI JSON document.
try {
    $document = Get-Content 'docs/openapi.json' -Raw | ConvertFrom-Json
}
catch {
    Write-Host "docs/openapi.json does not parse as JSON: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
if (-not ("$($document.openapi)".StartsWith('3.')) -or -not $document.paths) {
    Write-Host 'docs/openapi.json is JSON but not an OpenAPI 3.x document.' -ForegroundColor Red
    exit 1
}

$hash = (Get-FileHash 'docs/openapi.json' -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "docs/openapi.json $hash"
