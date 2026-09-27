#!/usr/bin/env bash
# Spielt die Red-Alert-Musik (scores.mix) aus einer EIGENEN Originalkopie in die App ein.
# Die Musik ist nicht im freien Quick-Install-Paket enthalten. Quellen laut OpenRA-Installer
# (mods/ra-content/installer/*.yaml): Red-Alert-CD (Allies/Soviet, Ordner MAIN.MIX → scores.mix),
# "The First Decade" oder eine Desktop-OpenRA-Installation, deren Content-Installer die Musik
# bereits eingerichtet hat (z. B. %APPDATA%\OpenRA\Content\ra\v2\scores.mix).
# Aufruf: bash quest/import-music.sh /pfad/zu/scores.mix
set -euo pipefail
. "$(dirname -- "$0")/lib.sh"

[ "$#" -eq 1 ] && [ -s "$1" ] || die "Aufruf: bash quest/import-music.sh /pfad/zu/scores.mix"
[ -x "$ADB" ] || ADB=adb
"$ADB" get-state >/dev/null 2>&1 || die "Keine Quest per ADB gefunden."

remote=/data/local/tmp/openra-scores.mix
trap '"$ADB" shell rm -f "$remote" >/dev/null 2>&1 || true' EXIT
"$ADB" push "$(np "$1")" "$remote"
"$ADB" shell "run-as $PACKAGE sh -c 'mkdir -p files/Content/ra/v2 && cp $remote files/Content/ra/v2/scores.mix && test -s files/Content/ra/v2/scores.mix'"
echo "Musik eingespielt. xr-openra neu starten, damit OpenRA sie findet."
