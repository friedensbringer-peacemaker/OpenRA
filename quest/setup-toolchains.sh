#!/usr/bin/env bash
# Lädt alle Build-Werkzeuge nach .toolchains/ (ohne Admin-Rechte, idempotent):
# .NET 10 SDK + Android-Workload, JDK 17, Android-SDK (Plattform, Build-Tools,
# NDK, CMake/Ninja, platform-tools/adb) sowie OpenXR-SDK und -Loader.
set -euo pipefail
. "$(dirname -- "$0")/lib.sh"

mkdir -p "$TOOLCHAINS/downloads"
DL="$TOOLCHAINS/downloads"

fetch() { # url ziel
    [ -s "$2" ] && return 0
    curl -L --fail --show-error --progress-bar --retry 3 -o "$(np "$2.part")" "$1"
    mv "$2.part" "$2"
}

step "JDK $JDK_MAJOR"
if [ ! -x "$JDK/bin/java$EXE" ]; then
    case "$HOST_OS-$(uname -m)" in
        windows-*) jdk_file=microsoft-jdk-$JDK_MAJOR-windows-x64.zip ;;
        macos-arm64) jdk_file=microsoft-jdk-$JDK_MAJOR-macos-aarch64.tar.gz ;;
        macos-*) jdk_file=microsoft-jdk-$JDK_MAJOR-macos-x64.tar.gz ;;
        linux-aarch64) jdk_file=microsoft-jdk-$JDK_MAJOR-linux-aarch64.tar.gz ;;
        *) jdk_file=microsoft-jdk-$JDK_MAJOR-linux-x64.tar.gz ;;
    esac
    fetch "https://aka.ms/download-jdk/$jdk_file" "$DL/$jdk_file"
    rm -rf "$TOOLCHAINS/jdk-extract" && mkdir -p "$TOOLCHAINS/jdk-extract"
    case "$jdk_file" in
        *.zip) unzip -q "$DL/$jdk_file" -d "$TOOLCHAINS/jdk-extract" ;;
        *) tar -xzf "$DL/$jdk_file" -C "$TOOLCHAINS/jdk-extract" ;;
    esac
    home=$(dirname "$(dirname "$(find "$TOOLCHAINS/jdk-extract" -path "*/bin/java$EXE" | head -n 1)")")
    rm -rf "$JDK" && mv "$home" "$JDK" && rm -rf "$TOOLCHAINS/jdk-extract"
fi
"$JDK/bin/java$EXE" -version 2>&1 | head -n 1

step ".NET $DOTNET_CHANNEL SDK"
if [ ! -x "$DOTNET" ] || ! "$DOTNET" --list-sdks | grep -q "^$DOTNET_CHANNEL"; then
    if [ "$HOST_OS" = windows ]; then
        fetch https://dot.net/v1/dotnet-install.ps1 "$DL/dotnet-install.ps1"
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(np "$DL/dotnet-install.ps1")" \
            -Channel "$DOTNET_CHANNEL" -InstallDir "$(np "$DOTNET_DIR")" -NoPath
    else
        fetch https://dot.net/v1/dotnet-install.sh "$DL/dotnet-install.sh"
        bash "$DL/dotnet-install.sh" --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_DIR" --no-path
    fi
fi
"$DOTNET" --version

step ".NET-Android-Workload"
if ! "$DOTNET" workload list | grep -q '^android'; then
    "$DOTNET" workload install android
fi

step "Android-SDK (cmdline-tools $CMDLINE_TOOLS_BUILD)"
SDKMANAGER="$ANDROID_SDK/cmdline-tools/latest/bin/sdkmanager$BAT"
if [ ! -f "$SDKMANAGER" ]; then
    case "$HOST_OS" in windows) os=win ;; macos) os=mac ;; *) os=linux ;; esac
    file=commandlinetools-$os-${CMDLINE_TOOLS_BUILD}_latest.zip
    fetch "https://dl.google.com/android/repository/$file" "$DL/$file"
    rm -rf "$ANDROID_SDK/cmdline-tools" && mkdir -p "$ANDROID_SDK/cmdline-tools"
    unzip -q "$DL/$file" -d "$ANDROID_SDK/cmdline-tools"
    mv "$ANDROID_SDK/cmdline-tools/cmdline-tools" "$ANDROID_SDK/cmdline-tools/latest"
fi
sdk() {
    if [ "$HOST_OS" = windows ]; then
        cmd.exe /c "$(cygpath -w "$SDKMANAGER")" "--sdk_root=$(np "$ANDROID_SDK")" "$@"
    else
        "$SDKMANAGER" "--sdk_root=$ANDROID_SDK" "$@"
    fi
}
# Mit diesem Aufruf werden die Android-SDK-Lizenzen akzeptiert.
yes | sdk --licenses >/dev/null 2>&1 || true
sdk --install "platform-tools" "platforms;$ANDROID_PLATFORM" "build-tools;$BUILD_TOOLS_VERSION" \
    "ndk;$NDK_VERSION" "cmake;$CMAKE_VERSION" | grep -v '^\[' || true
for need in "$ADB" "$BUILD_TOOLS/aapt2$EXE" "$ANDROID_SDK/ndk/$NDK_VERSION/build/cmake/android.toolchain.cmake" \
    "$ANDROID_SDK/cmake/$CMAKE_VERSION/bin/cmake$EXE"; do
    [ -f "$need" ] || die "Android-SDK-Bestandteil fehlt nach Installation: $need"
done

step "OpenXR-SDK und Android-Loader"
bash "$REPO_ROOT/OpenRA.Quest.XrProbe/scripts/prepare-openxr.sh"

step "Fertig: alle Werkzeuge liegen unter $TOOLCHAINS"
