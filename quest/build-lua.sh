#!/usr/bin/env bash
# Baut Lua 5.1.5 (MIT-Lizenz) als liblua51.so für Android arm64. OpenRAs Lua-Anbindung (Eluant)
# lädt "lua51"; ohne diese Bibliothek scheitern Hauptmenü-Hintergrundkarte, Missionen und
# Skriptkarten mit DllNotFoundException. Entspricht dem Upstream-Build (OpenRA/Eluant,
# liblua.linux.patch: Lua 5.1.5 unverändert, nur als Shared Library).
# Ergebnis: .toolchains/lua-build/liblua51.so
set -euo pipefail
. "$(dirname -- "${BASH_SOURCE[0]}")/lib.sh"

ARCHIVE="$TOOLCHAINS/downloads/lua-$LUA_VERSION.tar.gz"
SOURCE="$TOOLCHAINS/lua-$LUA_VERSION"
BUILD_DIR="$TOOLCHAINS/lua-build"
OUTPUT="$BUILD_DIR/liblua51.so"
NDK="$ANDROID_SDK/ndk/$NDK_VERSION"
BIN=$(ls -d "$NDK"/toolchains/llvm/prebuilt/*/bin | head -n 1)

if [ -f "$OUTPUT" ]; then
    echo "Lua $LUA_VERSION bereits gebaut: $OUTPUT"
    exit 0
fi

mkdir -p "$TOOLCHAINS/downloads" "$BUILD_DIR"
if [ ! -s "$ARCHIVE" ]; then
    curl -L --fail --show-error -o "$(np "$ARCHIVE")" "https://www.lua.org/ftp/lua-$LUA_VERSION.tar.gz"
fi

actual=$(sha256sum "$ARCHIVE" | cut -d ' ' -f 1)
[ "$actual" = "$LUA_SHA256" ] || die "Unerwartete Prüfsumme für lua-$LUA_VERSION.tar.gz: $actual"

rm -rf "$SOURCE"
tar -xzf "$ARCHIVE" -C "$TOOLCHAINS"

# Bibliothek = alle Lua-Quellen außer Interpreter (lua.c), Compiler (luac.c) und dessen print.c.
sources=()
for file in "$SOURCE"/src/*.c; do
    case "$(basename "$file")" in lua.c|luac.c|print.c) continue ;; esac
    sources+=("$(np "$file")")
done

"$BIN/clang$EXE" --target=aarch64-linux-android29 -shared -fPIC -O2 -w \
    -DLUA_USE_POSIX -DLUA_USE_DLOPEN \
    -Wl,-soname,liblua51.so -o "$(np "$OUTPUT")" "${sources[@]}" -ldl -lm
"$BIN/llvm-strip$EXE" --strip-unneeded "$(np "$OUTPUT")"
echo "Lua $LUA_VERSION: $OUTPUT"
