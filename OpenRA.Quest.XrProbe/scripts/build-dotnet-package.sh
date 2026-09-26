#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
TOOLCHAINS=${OPENRA_TOOLCHAINS:-"$REPO_ROOT/../.toolchains"}
ANDROID_SDK=${ANDROID_SDK_ROOT:-"$TOOLCHAINS/android-sdk"}
JDK=${JAVA_HOME:-"$TOOLCHAINS/jdk"}
JAVA_HOME="$JDK"
PATH="$JDK/bin:$PATH"
export JAVA_HOME PATH
APK="$REPO_ROOT/OpenRA.Quest.Probe/bin/Debug/net10.0-android/android-arm64/com.friedensbringer.openra.questprobe-Signed.apk"
OUTPUT=${1:-"$REPO_ROOT/../Artifacts/OpenRA-Tabletop-XR-Quest3-Preview.apk"}

"$SCRIPT_DIR/build-native.sh"
mkdir -p "$TOOLCHAINS/dotnet-home"
(
    cd "$REPO_ROOT"
    export DOTNET_CLI_HOME="$TOOLCHAINS/dotnet-home"
    export NUGET_PACKAGES="$DOTNET_CLI_HOME/.nuget/packages"
    export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1 MSBUILDDISABLENODEREUSE=1
    dotnet restore OpenRA.Quest.Probe/OpenRA.Quest.Probe.csproj \
        -p:EnableQuestXr=true -p:OpenRaToolchains="$TOOLCHAINS" \
        -p:AndroidSdkDirectory="$ANDROID_SDK" -p:JavaSdkDirectory="$JDK" \
        -p:AppSettingsDirectory="$TOOLCHAINS/android-settings" \
        -p:RestoreIgnoreFailedSources=true -v:q
    dotnet build OpenRA.Quest.Probe/OpenRA.Quest.Probe.csproj --no-restore -m:1 \
        -p:UseSharedCompilation=false -p:EnableQuestXr=true \
        -p:OpenRaToolchains="$TOOLCHAINS" \
        -p:AndroidSdkDirectory="$ANDROID_SDK" \
        -p:JavaSdkDirectory="$JDK" \
        -p:AppSettingsDirectory="$TOOLCHAINS/android-settings" \
        -p:AndroidPackageFormat=apk -v:q
)

unzip -Z1 "$APK" | rg -q '^lib/arm64-v8a/libopenra_xr_probe.so$'
unzip -Z1 "$APK" | rg -q '^lib/arm64-v8a/libopenxr_loader.so$'
"$ANDROID_SDK/build-tools/36.0.0/aapt2" dump badging "$APK" | \
    rg -q "uses-permission: name='org.khronos.openxr.permission.OPENXR'"
"$ANDROID_SDK/build-tools/36.0.0/aapt2" dump xmltree --file AndroidManifest.xml "$APK" | \
    rg -q 'org.khronos.openxr.intent.category.IMMERSIVE_HMD'
"$ANDROID_SDK/build-tools/36.0.0/apksigner" verify --verbose "$APK"

mkdir -p "$(dirname "$OUTPUT")"
cp "$APK" "$OUTPUT"
echo "Ungetestete OpenRA-/OpenXR-Kombi-APK: $OUTPUT"
