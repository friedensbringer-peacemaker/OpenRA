#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
PROBE_ROOT="$REPO_ROOT/OpenRA.Quest.XrProbe"
TOOLCHAINS=${OPENRA_TOOLCHAINS:-"$REPO_ROOT/../.toolchains"}
ANDROID_SDK=${ANDROID_SDK_ROOT:-"$TOOLCHAINS/android-sdk"}
JDK=${JAVA_HOME:-"$TOOLCHAINS/jdk"}
JAVA_HOME="$JDK"
PATH="$JDK/bin:$PATH"
export JAVA_HOME PATH
BUILD_TOOLS="$ANDROID_SDK/build-tools/36.0.0"
ANDROID_JAR="$ANDROID_SDK/platforms/android-36/android.jar"
BUILD_DIR="$TOOLCHAINS/openra-xr-probe-apk-build"
NATIVE_BUILD="$TOOLCHAINS/openra-xr-probe-build/libopenra_xr_probe.so"
LOADER="$TOOLCHAINS/openxr-loader-1.1.58/arm64-v8a/libopenxr_loader.so"
KEYSTORE="$TOOLCHAINS/openra-xr-probe-debug.keystore"
OUTPUT=${1:-"$REPO_ROOT/../Artifacts/OpenRA-XR-Loader-Probe-untested.apk"}

for required in "$BUILD_TOOLS/aapt2" "$BUILD_TOOLS/d8" "$BUILD_TOOLS/zipalign" \
    "$BUILD_TOOLS/apksigner" "$ANDROID_JAR" "$JDK/bin/javac" "$NATIVE_BUILD" "$LOADER"; do
    if [ ! -f "$required" ]; then
        echo "Build-Abhängigkeit fehlt: $required" >&2
        exit 1
    fi
done

mkdir -p "$BUILD_DIR/classes" "$BUILD_DIR/dex" "$BUILD_DIR/stage/lib/arm64-v8a" "$(dirname "$OUTPUT")"
"$JDK/bin/javac" -source 8 -target 8 -cp "$ANDROID_JAR" -d "$BUILD_DIR/classes" \
    "$PROBE_ROOT/java/com/friedensbringer/openra/xr/XrProbe.java" \
    "$PROBE_ROOT/java/com/friedensbringer/openra/xr/XrProbeActivity.java"

"$BUILD_TOOLS/d8" --min-api 29 --lib "$ANDROID_JAR" \
    --output "$BUILD_DIR/dex" \
    "$BUILD_DIR/classes/com/friedensbringer/openra/xr/XrProbe.class" \
    "$BUILD_DIR/classes/com/friedensbringer/openra/xr/XrProbeActivity.class"

"$BUILD_TOOLS/aapt2" link -I "$ANDROID_JAR" \
    --manifest "$PROBE_ROOT/AndroidManifest.xml" \
    -o "$BUILD_DIR/base.apk"

cp "$BUILD_DIR/dex/classes.dex" "$BUILD_DIR/stage/classes.dex"
cp "$NATIVE_BUILD" "$BUILD_DIR/stage/lib/arm64-v8a/libopenra_xr_probe.so"
cp "$LOADER" "$BUILD_DIR/stage/lib/arm64-v8a/libopenxr_loader.so"
cp "$BUILD_DIR/base.apk" "$BUILD_DIR/manifest.apk"
(
    cd "$BUILD_DIR/stage"
    zip -q -r "$BUILD_DIR/manifest.apk" classes.dex lib
)

"$BUILD_TOOLS/zipalign" -f 4 "$BUILD_DIR/manifest.apk" "$BUILD_DIR/aligned.apk"
if [ ! -f "$KEYSTORE" ]; then
    "$JDK/bin/keytool" -genkeypair -keystore "$KEYSTORE" -storepass android \
        -keypass android -alias openra-xr-probe -keyalg RSA -keysize 2048 \
        -validity 3650 -dname "CN=OpenRA XR Probe, O=Local Test"
fi
"$BUILD_TOOLS/apksigner" sign --ks "$KEYSTORE" --ks-key-alias openra-xr-probe \
    --ks-pass pass:android --key-pass pass:android --out "$OUTPUT" "$BUILD_DIR/aligned.apk"
"$BUILD_TOOLS/apksigner" verify --verbose "$OUTPUT"
echo "Ungetestete OpenXR-Diagnose-APK: $OUTPUT"
