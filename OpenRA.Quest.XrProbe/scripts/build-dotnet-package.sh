#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$SCRIPT_DIR/../../quest/lib.sh"
APK="$REPO_ROOT/OpenRA.Quest.Probe/bin/Debug/net10.0-android/android-arm64/$PACKAGE-Signed.apk"
OUTPUT=${1:-"$APK_OUTPUT"}
AAPT2="$BUILD_TOOLS/aapt2$EXE"

apksigner() {
    if [ "$HOST_OS" = windows ]; then
        cmd.exe /c "$(cygpath -w "$BUILD_TOOLS/apksigner.bat")" "$@"
    else
        "$BUILD_TOOLS/apksigner" "$@"
    fi
}

require() { # beschreibung muster text
    if ! printf '%s\n' "$3" | grep -Eq "$2"; then
        echo "APK-Prüfung fehlgeschlagen: $1" >&2
        exit 1
    fi
}

"$SCRIPT_DIR/build-native.sh"
mkdir -p "$TOOLCHAINS/dotnet-home"
MSBUILD_PROPS=(
    -p:EnableQuestXr=true
    -p:OpenRaToolchains="$(np "$TOOLCHAINS")"
    -p:AndroidSdkDirectory="$(np "$ANDROID_SDK")"
    -p:JavaSdkDirectory="$(np "$JDK")"
    -p:AppSettingsDirectory="$(np "$TOOLCHAINS/android-settings")"
)
(
    cd "$REPO_ROOT"
    export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1 MSBUILDDISABLENODEREUSE=1
    "$DOTNET" restore OpenRA.Quest.Probe/OpenRA.Quest.Probe.csproj \
        "${MSBUILD_PROPS[@]}" -p:RestoreIgnoreFailedSources=true -v:q
    "$DOTNET" build OpenRA.Quest.Probe/OpenRA.Quest.Probe.csproj --no-restore -m:1 \
        -p:UseSharedCompilation=false "${MSBUILD_PROPS[@]}" \
        -p:AndroidPackageFormat=apk -v:q
)

ENTRIES=$(unzip -Z1 "$APK")
require "libopenra_xr_probe.so fehlt" '^lib/arm64-v8a/libopenra_xr_probe.so$' "$ENTRIES"
require "libopenxr_loader.so fehlt" '^lib/arm64-v8a/libopenxr_loader.so$' "$ENTRIES"
BADGING=$("$AAPT2" dump badging "$(np "$APK")")
require "Paketname ist nicht $PACKAGE" "package: name='$PACKAGE'" "$BADGING"
require "OpenXR-Berechtigung fehlt" "uses-permission: name='org.khronos.openxr.permission.OPENXR'" "$BADGING"
require "targetSdkVersion ist nicht 32" "targetSdkVersion:'32'" "$BADGING"
MANIFEST_DUMP=$("$AAPT2" dump xmltree --file AndroidManifest.xml "$(np "$APK")" | tr -d '\r')
require "IMMERSIVE_HMD-Kategorie fehlt" 'org.khronos.openxr.intent.category.IMMERSIVE_HMD' "$MANIFEST_DUMP"
require "VR-Kategorie fehlt" 'com.oculus.intent.category.VR' "$MANIFEST_DUMP"
require "Querformat fehlt" 'screenOrientation.*=0$' "$MANIFEST_DUMP"
require "singleTask fehlt" 'launchMode.*=2$' "$MANIFEST_DUMP"
require "configChanges falsch" 'configChanges.*=0x000017f0$' "$MANIFEST_DUMP"
require "resizeableActivity nicht false" 'resizeableActivity.*=false$' "$MANIFEST_DUMP"
apksigner verify --verbose "$(np "$APK")"

mkdir -p "$(dirname "$OUTPUT")"
cp "$APK" "$OUTPUT"
echo "Quest-APK ($PACKAGE): $OUTPUT"
