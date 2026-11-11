#!/usr/bin/env bash
# Regenerates docs/openapi.json (build-time generator, Microsoft.Extensions.ApiDescription.Server).
# One-line regenerate command:  bash scripts/generate-openapi.sh
#
# The generator launches the app's entry point, so ValidateOnStart needs settings.
# They come from the SAFE dev-only placeholders in .env.example, passed as
# environment variables for this process only. No secret is stored in the repo.
# Environment = Production: no dev-only migration, no database connection.
# Redis needs ssl=true outside Development/Testing; the generator never connects.
set -euo pipefail

cd "$(dirname "$0")/.."

env_value() {
  grep -E "^$1=" .env.example | head -n 1 | cut -d= -f2-
}

rm -f docs/openapi.json

ASPNETCORE_ENVIRONMENT=Production \
Database__Name="$(env_value DB_NAME)" \
Database__Password="$(env_value DB_PASSWORD)" \
Jwt__SigningKey="$(env_value JWT_SIGNING_KEY)" \
Redis__ConnectionString="localhost:$(env_value REDIS_PORT),password=$(env_value REDIS_PASSWORD),ssl=true" \
  dotnet build src/Api -c Release --no-incremental -nologo -v q -p:GenerateOpenApiDocument=true

# The build does not fail when the generator fails, so check the output exists.
if [ ! -s docs/openapi.json ]; then
  echo "OpenAPI generation produced no (or an empty) docs/openapi.json (see build output above)." >&2
  exit 1
fi

# It must also be a valid OpenAPI JSON document.
python3 -c '
import json, sys
with open("docs/openapi.json", encoding="utf-8") as f:
    document = json.load(f)
if not str(document.get("openapi", "")).startswith("3.") or "paths" not in document:
    sys.exit("docs/openapi.json is JSON but not an OpenAPI 3.x document")
' || { echo "docs/openapi.json does not parse as an OpenAPI JSON document." >&2; exit 1; }

echo "docs/openapi.json $(sha256sum docs/openapi.json | cut -d' ' -f1)"
