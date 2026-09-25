#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 /path/to/ra-quickinstall.zip" >&2
  exit 2
fi

archive=$1
expected_sha1=44241f68e69db9511db82cf83c174737ccda300b
actual_sha1=$(shasum -a 1 "$archive" | awk '{print $1}')
if [ "$actual_sha1" != "$expected_sha1" ]; then
  echo "The archive does not match the SHA-1 in mods/ra-content/installer/downloads.yaml." >&2
  exit 1
fi

adb_bin=${ADB_BIN:-adb}
remote_zip=/data/local/tmp/openra-ra-quickinstall.zip
package=com.friedensbringer.openra.questprobe
trap '"$adb_bin" shell rm -f "$remote_zip" >/dev/null 2>&1 || true' EXIT

"$adb_bin" push "$archive" "$remote_zip"
"$adb_bin" shell "run-as $package sh -c 'mkdir -p files/Content/ra/v2 && unzip -o $remote_zip -d files/Content/ra/v2'"
"$adb_bin" shell "run-as $package sh -c 'test -s files/Content/ra/v2/snow.mix && test -s files/Content/ra/v2/conquer.mix'"
echo "Red Alert content imported into the Quest probe app's private storage."
