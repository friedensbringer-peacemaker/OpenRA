#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
TOOLCHAINS=${OPENRA_TOOLCHAINS:-"$REPO_ROOT/../.toolchains"}
ANDROID_SDK=${ANDROID_SDK_ROOT:-"$TOOLCHAINS/android-sdk"}
NDK_VERSION=27.0.12077973
OPENXR_VERSION=1.1.58
NDK="$ANDROID_SDK/ndk/$NDK_VERSION"
SDK="$TOOLCHAINS/openxr-sdk-$OPENXR_VERSION"
LOADER="$TOOLCHAINS/openxr-loader-$OPENXR_VERSION/arm64-v8a/libopenxr_loader.so"
BUILD_DIR="$TOOLCHAINS/openra-xr-probe-build"

if [ ! -f "$NDK/build/cmake/android.toolchain.cmake" ]; then
    echo "Android NDK $NDK_VERSION fehlt: $NDK" >&2
    exit 1
fi

if [ ! -f "$SDK/include/openxr/openxr.h" ] || [ ! -f "$LOADER" ]; then
    echo "OpenXR SDK oder Loader fehlt. Zuerst scripts/prepare-openxr.sh ausführen." >&2
    exit 1
fi

if [ -f "$BUILD_DIR/CMakeCache.txt" ] && \
    ! grep -Fqx "CMAKE_HOME_DIRECTORY:INTERNAL=$REPO_ROOT/OpenRA.Quest.XrProbe/native" "$BUILD_DIR/CMakeCache.txt"; then
    rm -rf "$BUILD_DIR"
fi

cmake -S "$REPO_ROOT/OpenRA.Quest.XrProbe/native" -B "$BUILD_DIR" \
    -DCMAKE_TOOLCHAIN_FILE="$NDK/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-29 \
    -DOPENXR_SDK_ROOT="$SDK" -DOPENXR_LOADER_SO="$LOADER" \
    -DCMAKE_BUILD_TYPE=Release
cmake --build "$BUILD_DIR" --config Release

echo "Native OpenXR-Probe: $BUILD_DIR/libopenra_xr_probe.so"
