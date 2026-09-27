#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 /path/to/ra-quickinstall.zip" >&2
  exit 2
fi

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$script_dir/../../quest/lib.sh"

archive=$1
actual_sha1=$(sha1_of "$archive")
if [ "$actual_sha1" != "$RA_QUICKINSTALL_SHA1" ]; then
  echo "The archive does not match the SHA-1 in mods/ra-content/installer/downloads.yaml." >&2
  exit 1
fi

adb_bin=${ADB_BIN:-adb}
remote_zip=/data/local/tmp/openra-ra-quickinstall.zip
trap '"$adb_bin" shell rm -f "$remote_zip" >/dev/null 2>&1 || true' EXIT

"$adb_bin" push "$(np "$archive")" "$remote_zip"
"$adb_bin" shell "run-as $PACKAGE sh -c 'mkdir -p files/Content/ra/v2 && unzip -o $remote_zip -d files/Content/ra/v2'"
"$adb_bin" shell "run-as $PACKAGE sh -c 'test -s files/Content/ra/v2/snow.mix && test -s files/Content/ra/v2/conquer.mix'"
echo "Red Alert content imported into the private storage of $PACKAGE."
