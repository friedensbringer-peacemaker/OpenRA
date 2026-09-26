#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: $0 install /path/to/OpenRA-Quest-XR-Combined-untested.apk" >&2
  echo "       $0 capture [output-directory]" >&2
  exit 2
}

if [ "$#" -lt 1 ]; then
  usage
fi

mode=$1
adb_bin=${ADB_BIN:-adb}
package=com.friedensbringer.openra.questprobe

if ! "$adb_bin" get-state >/dev/null 2>&1; then
  echo "No ADB device is connected. Reconnect and wake the Quest first." >&2
  exit 1
fi

case "$mode" in
  install)
    if [ "$#" -ne 2 ] || [ ! -f "$2" ]; then
      usage
    fi

    "$adb_bin" install -r "$2"
    "$adb_bin" shell am force-stop "$package"
    "$adb_bin" shell monkey -p "$package" -c android.intent.category.LAUNCHER 1
    echo "OpenRA launched. Import your own Red Alert Quickinstall ZIP if needed,"
    echo "then tap 'XR-Fläche starten (Experiment)' in the app."
    echo "After checking the board in the headset, run '$0 capture'."
    ;;
  capture)
    if [ "$#" -gt 2 ]; then
      usage
    fi

    output_dir=${2:-"${TMPDIR:-/tmp}/openra-quest-xr-$(date +%Y%m%d-%H%M%S)"}
    mkdir -p "$output_dir"
    "$adb_bin" logcat -d -v time -s OpenRA.Quest.Probe:I OpenRA.XrProbe:I AndroidRuntime:E > "$output_dir/xr-logcat.txt"
    "$adb_bin" exec-out screencap -p > "$output_dir/quest-screen.png"
    "$adb_bin" shell dumpsys activity activities > "$output_dir/activity-state.txt"

    for frame in openra-game-world-preview.png openra-regular-world-preview.png; do
      if "$adb_bin" shell run-as "$package" ls "files/$frame" >/dev/null 2>&1; then
        "$adb_bin" exec-out run-as "$package" cat "files/$frame" > "$output_dir/$frame"
      fi
    done

    echo "Quest XR evidence saved to: $output_dir"
    echo "Check for 'OpenXR-Session läuft', 'Quad relativ zur ersten gültigen Blickpose platziert'"
    echo "and repeated 'XR-Bildübergabe' lines in xr-logcat.txt."
    echo "Activity.OnPause/OnResume lines reveal whether the Android game surface stopped."
    echo "Also check for AndroidRuntime or OpenRA errors."
    echo "A system screenshot may not capture the immersive quad; inspect it in the headset too."
    ;;
  *)
    usage
    ;;
esac
