#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$SCRIPT_DIR/../../quest/lib.sh"
OPENXR_VERSION=1.1.58
NDK="$ANDROID_SDK/ndk/$NDK_VERSION"
CMAKE_BIN="$ANDROID_SDK/cmake/$CMAKE_VERSION/bin"
SDK="$TOOLCHAINS/openxr-sdk-$OPENXR_VERSION"
LOADER="$TOOLCHAINS/openxr-loader-$OPENXR_VERSION/arm64-v8a/libopenxr_loader.so"
BUILD_DIR="$TOOLCHAINS/openra-xr-probe-build"
SOURCE_DIR="$REPO_ROOT/OpenRA.Quest.XrProbe/native"

if [ ! -f "$NDK/build/cmake/android.toolchain.cmake" ]; then
    echo "Android NDK $NDK_VERSION fehlt: $NDK (quest/setup-toolchains.sh ausführen)" >&2
    exit 1
fi

if [ ! -f "$CMAKE_BIN/cmake$EXE" ]; then
    echo "CMake $CMAKE_VERSION aus dem Android-SDK fehlt: $CMAKE_BIN (quest/setup-toolchains.sh ausführen)" >&2
    exit 1
fi

if [ ! -f "$SDK/include/openxr/openxr.h" ] || [ ! -f "$LOADER" ]; then
    echo "OpenXR SDK oder Loader fehlt. Zuerst scripts/prepare-openxr.sh ausführen." >&2
    exit 1
fi

if [ -f "$BUILD_DIR/CMakeCache.txt" ] && \
    ! grep -Fqx "CMAKE_HOME_DIRECTORY:INTERNAL=$(np "$SOURCE_DIR")" "$BUILD_DIR/CMakeCache.txt"; then
    rm -rf "$BUILD_DIR"
fi

"$CMAKE_BIN/cmake$EXE" -G Ninja -S "$(np "$SOURCE_DIR")" -B "$(np "$BUILD_DIR")" \
    -DCMAKE_MAKE_PROGRAM="$(np "$CMAKE_BIN/ninja$EXE")" \
    -DCMAKE_TOOLCHAIN_FILE="$(np "$NDK/build/cmake/android.toolchain.cmake")" \
    -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-29 \
    -DOPENXR_SDK_ROOT="$(np "$SDK")" -DOPENXR_LOADER_SO="$(np "$LOADER")" \
    -DCMAKE_BUILD_TYPE=Release
"$CMAKE_BIN/cmake$EXE" --build "$(np "$BUILD_DIR")" --config Release

echo "Native OpenXR-Probe: $BUILD_DIR/libopenra_xr_probe.so"
