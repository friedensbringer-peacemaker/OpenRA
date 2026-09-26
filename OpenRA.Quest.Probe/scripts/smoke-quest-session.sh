#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
  echo "Usage: $0 /path/to/OpenRA-Quest-Signed.apk [output-directory]" >&2
  exit 2
fi

apk=$1
output_dir=${2:-"${TMPDIR:-/tmp}/openra-quest-smoke-$(date +%Y%m%d-%H%M%S)"}
adb_bin=${ADB_BIN:-adb}
package=com.friedensbringer.openra.questprobe

if [ ! -f "$apk" ]; then
  echo "APK not found: $apk" >&2
  exit 2
fi

if ! "$adb_bin" get-state >/dev/null 2>&1; then
  echo "No ADB device is connected. Reconnect the Quest before running this test." >&2
  exit 1
fi

mkdir -p "$output_dir"
"$adb_bin" install -r "$apk"
"$adb_bin" shell am force-stop "$package"
"$adb_bin" shell monkey -p "$package" -c android.intent.category.LAUNCHER 1

# The initial run checks rules, graphics, map actors and then starts the
# persistent session. A fixed short wait also makes failed startups visible.
sleep 25
"$adb_bin" logcat -d -s OpenRA.Quest.Probe:I > "$output_dir/openra-logcat.txt"
"$adb_bin" exec-out screencap -p > "$output_dir/quest-screen.png"

for frame in openra-game-world-preview.png openra-regular-world-preview.png; do
  if "$adb_bin" shell run-as "$package" ls "files/$frame" >/dev/null 2>&1; then
    "$adb_bin" exec-out run-as "$package" cat "files/$frame" > "$output_dir/$frame"
  fi
done

echo "Quest smoke-test evidence saved to: $output_dir"
echo "Look for 'Fortlaufende lokale OpenRA-Spielsession initialisiert.' or an error in openra-logcat.txt."
