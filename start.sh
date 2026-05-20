#!/bin/bash
# Didasco startup script — fast restart via incremental publish cache.
# Rebuilds only when .cs/.csproj/.cshtml files change; otherwise boots in ~2s.

set -e

export ASPNETCORE_ENVIRONMENT=Development

PROJ_DIR="/home/runner/workspace/artifacts/bocconi-lms"
PUBLISH_DIR="$PROJ_DIR/.preprod-publish"
MARKER="$PUBLISH_DIR/.build_ok"

cd "$PROJ_DIR"

need_rebuild() {
    [ ! -f "$MARKER" ]                          && return 0
    [ ! -f "$PUBLISH_DIR/BocconiLMS.dll" ]      && return 0
    find . -type f \( -name "*.cs" -o -name "*.csproj" -o -name "*.cshtml" -o -name "*.json" \) \
        ! -path "*/obj/*" \
        ! -path "*/.preprod-publish/*" \
        ! -path "*/bin/*" \
        ! -path "*/node_modules/*" \
        -newer "$MARKER" 2>/dev/null | grep -q . && return 0
    return 1
}

if need_rebuild; then
    echo "[Didasco] Building application (source changed)..."
    dotnet publish -c Release -o "$PUBLISH_DIR" --nologo -v q
    touch "$MARKER"
    echo "[Didasco] Build complete."
else
    echo "[Didasco] Using cached build — no source changes detected."
fi

echo "[Didasco] Starting server on port ${PORT:-8081}..."
exec dotnet "$PUBLISH_DIR/BocconiLMS.dll"
