#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
TOOLCHAINS=${OPENRA_TOOLCHAINS:-"$REPO_ROOT/../.toolchains"}
SDK="$TOOLCHAINS/openxr-sdk-1.1.58"
OUTPUT="$TOOLCHAINS/openra-xr-board-geometry-test"

if [ ! -f "$SDK/include/openxr/openxr.h" ]; then
    echo "OpenXR-SDK fehlt: $SDK" >&2
    exit 1
fi

"${CXX:-c++}" -std=c++17 -Wall -Wextra -Werror \
    -I "$SDK/include" -I "$REPO_ROOT/OpenRA.Quest.XrProbe/native" \
    "$REPO_ROOT/OpenRA.Quest.XrProbe/tests/BoardGeometryTest.cpp" -o "$OUTPUT"
"$OUTPUT"
