#!/usr/bin/env bash
# Installiert die gebaute APK auf der per USB verbundenen Quest, spielt die
# Red-Alert-Daten in den privaten App-Speicher ein und startet die App.
# Deinstalliert nichts und löscht keine Spielstände.
set -euo pipefail
. "$(dirname -- "$0")/lib.sh"

[ -f "$APK_OUTPUT" ] || die "APK fehlt: $APK_OUTPUT – zuerst quest/build.sh ausführen."
[ -x "$ADB" ] || ADB=adb

devices=$("$ADB" devices | awk 'NR>1 && $2=="device" {print $1}')
count=$(printf '%s\n' "$devices" | grep -c . || true)
[ "$count" -ge 1 ] || die "Keine Quest per ADB gefunden. USB verbinden, Entwicklermodus an, 'USB-Debugging zulassen' im Headset bestätigen."
if [ "$count" -gt 1 ] && [ -z "${ANDROID_SERIAL:-}" ]; then
    die "Mehrere ADB-Geräte ($devices). Gewünschtes Gerät mit ANDROID_SERIAL=<seriennummer> wählen."
fi
export ANDROID_SERIAL=${ANDROID_SERIAL:-$devices}
echo "Gerät: $ANDROID_SERIAL ($("$ADB" shell getprop ro.product.model | tr -d '\r'))"

step "APK installieren"
"$ADB" install -r "$(np "$APK_OUTPUT")"

if "$ADB" shell "run-as $PACKAGE test -s files/Content/ra/v2/conquer.mix" >/dev/null 2>&1 && [ "${REIMPORT:-0}" != 1 ]; then
    step "Red-Alert-Daten sind bereits in der App (REIMPORT=1 erzwingt einen neuen Import)"
else
    [ -s "$RA_ZIP" ] || bash "$QUEST_DIR/fetch-ra-content.sh"
    step "Red-Alert-Daten in die App importieren"
    ADB_BIN="$ADB" bash "$REPO_ROOT/OpenRA.Quest.Probe/scripts/import-ra-content.sh" "$RA_ZIP"
fi

step "App starten"
"$ADB" shell input keyevent KEYCODE_WAKEUP || true
"$ADB" shell am force-stop "$PACKAGE"
"$ADB" shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
echo "Startbefehl gesendet. Headset aufsetzen und einen Controller in die Hand nehmen:"
echo "Horizon OS zeigt bei schlafender Quest oder inaktiven Controllern erst einen Controller-Dialog."
echo "Falls xr.openra nicht erscheint: in der App-Bibliothek unter \"Unbekannte Quellen\" starten."
