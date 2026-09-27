#!/usr/bin/env bash
# Baut die Quest-APK (OpenRA + native OpenXR-Brücke) nach Artifacts/xr-openra-quest3.apk.
# Voraussetzung: quest/setup-toolchains.sh wurde einmal ausgeführt.
set -euo pipefail
. "$(dirname -- "$0")/lib.sh"

[ -x "$DOTNET" ] || die ".NET fehlt – zuerst quest/setup-toolchains.sh ausführen."
bash "$REPO_ROOT/OpenRA.Quest.XrProbe/scripts/build-dotnet-package.sh" "$APK_OUTPUT"
