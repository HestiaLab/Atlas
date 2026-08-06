#!/usr/bin/env bash
set -euo pipefail

# Republish Atlas.Blazor to the local package source and verify that the
# independent NuGet consumer sample (samples/Atlas.Demo) resolves, builds,
# tests, and formats against the freshly packed nupkg.
#
# ADR-0019: single-package distribution — the Atlas.Blazor nupkg embeds
# Atlas.Core.dll, so packing after any Core change is sufficient.
#
# Run from anywhere:  bash scripts/republish-demo.sh

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# Keep in sync with samples/Atlas.Demo/Directory.Packages.props.
PACKAGE_VERSION="0.2.0"

echo "==> [1/4] pack Atlas.Blazor $PACKAGE_VERSION"
dotnet pack src/Atlas.Blazor/Atlas.Blazor.csproj \
  --configuration Release --no-restore \
  --output artifacts/packages \
  -p:PackageVersion="$PACKAGE_VERSION"

# The demo's NuGet.Config pins the global packages folder to .packages;
# a stale cached nupkg would shadow the freshly packed one.
echo "==> [2/4] clear demo package cache (.packages/atlas.blazor)"
rm -rf samples/Atlas.Demo/.packages/atlas.blazor

echo "==> [3/4] restore + build demo against the local source"
(cd samples/Atlas.Demo && dotnet restore Atlas.Demo.sln --configfile NuGet.Config)
(cd samples/Atlas.Demo && dotnet build Atlas.Demo.sln --no-restore)

echo "==> [4/4] test + format demo"
(cd samples/Atlas.Demo && dotnet test Atlas.Demo.sln --no-build --no-restore)
(cd samples/Atlas.Demo && dotnet format Atlas.Demo.sln --verify-no-changes --no-restore)

echo "==> OK: Atlas.Blazor $PACKAGE_VERSION republished and consumed by Atlas.Demo"
