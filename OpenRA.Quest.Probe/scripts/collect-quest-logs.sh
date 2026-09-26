#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
ADB=${ADB:-"$REPO_ROOT/../.toolchains/android-sdk/platform-tools/adb"}
PACKAGE=com.friedensbringer.openra.questprobe
OUTPUT=${1:-"$REPO_ROOT/../Artifacts/Quest-Logs-$(date +%Y-%m-%d-%H%M%S)"}

mkdir -p "$OUTPUT"
"$ADB" exec-out run-as "$PACKAGE" cat files/quest-diagnostics.log > "$OUTPUT/quest-diagnostics.log"
"$ADB" logcat -d -v time -s OpenRA.Quest.Probe AndroidRuntime ActivityManager WindowManager > "$OUTPUT/android-logcat.txt"
"$ADB" logcat -d -b crash -v time > "$OUTPUT/android-crashes.txt"
echo "Quest-Logs: $OUTPUT"
