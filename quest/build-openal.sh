#!/usr/bin/env bash
# Baut OpenAL Soft (festgelegter Release-Commit) für Android arm64 mit OpenSL-ES-Ausgabe.
# OpenRAs OpenAlSoundEngine spielt darüber Effekte, Sprache und Musik ab.
# Ergebnis: .toolchains/openal-soft-build/libopenal.so (wird in die APK gepackt).
set -euo pipefail
. "$(dirname -- "${BASH_SOURCE[0]}")/lib.sh"

SOURCE="$TOOLCHAINS/openal-soft-$OPENAL_VERSION"
BUILD_DIR="$TOOLCHAINS/openal-soft-build"
NDK="$ANDROID_SDK/ndk/$NDK_VERSION"
CMAKE_BIN="$ANDROID_SDK/cmake/$CMAKE_VERSION/bin"

if [ ! -d "$SOURCE/.git" ]; then
    git clone --depth 1 --branch "$OPENAL_VERSION" https://github.com/kcat/openal-soft.git "$(np "$SOURCE")"
fi
actual=$(git -C "$(np "$SOURCE")" rev-parse HEAD)
[ "$actual" = "$OPENAL_COMMIT" ] || die "Unerwarteter OpenAL-Soft-Commit: $actual"

if [ -f "$BUILD_DIR/libopenal.so" ] && [ "$BUILD_DIR/libopenal.so" -nt "$SOURCE/CMakeLists.txt" ]; then
    echo "OpenAL Soft $OPENAL_VERSION bereits gebaut: $BUILD_DIR/libopenal.so"
    exit 0
fi

"$CMAKE_BIN/cmake$EXE" -G Ninja -S "$(np "$SOURCE")" -B "$(np "$BUILD_DIR")" \
    -DCMAKE_MAKE_PROGRAM="$(np "$CMAKE_BIN/ninja$EXE")" \
    -DCMAKE_TOOLCHAIN_FILE="$(np "$NDK/build/cmake/android.toolchain.cmake")" \
    -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-29 -DCMAKE_BUILD_TYPE=Release \
    -DALSOFT_UTILS=OFF -DALSOFT_EXAMPLES=OFF -DALSOFT_TESTS=OFF -DALSOFT_INSTALL=OFF \
    -DALSOFT_BACKEND_OBOE=OFF -DALSOFT_BACKEND_OPENSL=ON -DALSOFT_REQUIRE_OPENSL=ON \
    -DALSOFT_BACKEND_WAVE=OFF -DALSOFT_EMBED_HRTF_DATA=OFF
"$CMAKE_BIN/cmake$EXE" --build "$(np "$BUILD_DIR")" --target OpenAL
[ -f "$BUILD_DIR/libopenal.so" ] || die "libopenal.so wurde nicht erzeugt."
# Debug-Symbole entfernen (16 MB → wenige MB in der APK).
STRIP=$(ls "$NDK"/toolchains/llvm/prebuilt/*/bin/llvm-strip"$EXE" | head -n 1)
"$STRIP" --strip-unneeded "$(np "$BUILD_DIR/libopenal.so")"
echo "OpenAL Soft $OPENAL_VERSION: $BUILD_DIR/libopenal.so"
