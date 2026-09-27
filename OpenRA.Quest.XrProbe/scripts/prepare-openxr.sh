#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$SCRIPT_DIR/../../quest/lib.sh"
VERSION=1.1.58
SDK="$TOOLCHAINS/openxr-sdk-$VERSION"
AAR="$TOOLCHAINS/openxr_loader_for_android-$VERSION.aar"
LOADER_DIR="$TOOLCHAINS/openxr-loader-$VERSION/arm64-v8a"

mkdir -p "$TOOLCHAINS" "$LOADER_DIR"
if [ ! -d "$SDK/.git" ]; then
    git clone --depth 1 --branch "release-$VERSION" \
        https://github.com/KhronosGroup/OpenXR-SDK.git "$(np "$SDK")"
fi

EXPECTED_COMMIT=472d817ffe066d5be09a351b0d39ff420141208b
ACTUAL_COMMIT=$(git -C "$(np "$SDK")" rev-parse HEAD)
if [ "$ACTUAL_COMMIT" != "$EXPECTED_COMMIT" ]; then
    echo "Unerwarteter OpenXR-SDK-Commit: $ACTUAL_COMMIT" >&2
    exit 1
fi

if [ ! -f "$AAR" ]; then
    curl -L --fail --show-error --output "$(np "$AAR")" \
        "https://repo.maven.apache.org/maven2/org/khronos/openxr/openxr_loader_for_android/$VERSION/openxr_loader_for_android-$VERSION.aar"
fi

EXPECTED_SHA1=53fec8cbef8ad380c905ceb49f7cb040d7d8e68f
ACTUAL_SHA1=$(sha1_of "$AAR")
if [ "$ACTUAL_SHA1" != "$EXPECTED_SHA1" ]; then
    echo "Unerwartete OpenXR-Loader-Prüfsumme: $ACTUAL_SHA1" >&2
    exit 1
fi

unzip -p "$AAR" jni/arm64-v8a/libopenxr_loader.so > "$LOADER_DIR/libopenxr_loader.so"
echo "OpenXR SDK $VERSION und Android-ARM64-Loader vorbereitet."
