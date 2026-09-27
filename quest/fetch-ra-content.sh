#!/usr/bin/env bash
# Lädt das Red-Alert-"Quick Install"-Paket, das auch OpenRAs eigener
# Content-Installer verwendet (mods/ra-content/installer/downloads.yaml), über
# OpenRAs offizielle Mirrorliste und prüft die dort hinterlegte SHA-1.
# Die Datei bleibt lokal unter Artifacts/content/ und kommt nie in Git oder die APK.
set -euo pipefail
. "$(dirname -- "$0")/lib.sh"

mkdir -p "$(dirname "$RA_ZIP")"
if [ -s "$RA_ZIP" ] && [ "$(sha1_of "$RA_ZIP")" = "$RA_QUICKINSTALL_SHA1" ]; then
    echo "Red-Alert-Paket bereits vorhanden und geprüft: $RA_ZIP"
    exit 0
fi

step "Mirrorliste laden: $RA_QUICKINSTALL_MIRRORS"
mirrors=$(curl -sL --fail "$RA_QUICKINSTALL_MIRRORS" | tr -d '\r')
[ -n "$mirrors" ] || die "Mirrorliste ist leer oder nicht erreichbar."

for url in $mirrors; do
    step "Versuche $url"
    rm -f "$RA_ZIP.part"
    if curl -L --fail --show-error --connect-timeout 20 -o "$(np "$RA_ZIP.part")" "$url"; then
        actual=$(sha1_of "$RA_ZIP.part")
        if [ "$actual" = "$RA_QUICKINSTALL_SHA1" ]; then
            mv "$RA_ZIP.part" "$RA_ZIP"
            echo "Geprüft (SHA-1 $actual): $RA_ZIP"
            exit 0
        fi
        echo "Falsche Prüfsumme $actual – nächster Mirror." >&2
    fi
done
rm -f "$RA_ZIP.part"
die "Kein Mirror lieferte ein Paket mit SHA-1 $RA_QUICKINSTALL_SHA1."
