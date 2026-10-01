# Gemeinsame Einstellungen für alle quest/*.sh-Skripte (wird per "." eingebunden).
# Läuft unter macOS, Linux und Windows (Git Bash).

QUEST_DIR=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$QUEST_DIR/.." && pwd)

# Alle Werkzeuge und Downloads bleiben innerhalb des Checkouts (gitignored).
TOOLCHAINS=${OPENRA_TOOLCHAINS:-"$REPO_ROOT/.toolchains"}
ARTIFACTS=${OPENRA_ARTIFACTS:-"$REPO_ROOT/Artifacts"}

# Festgelegte Versionen: bei Änderungen auch docs/xr/BUILD-QUEST.md anpassen.
DOTNET_CHANNEL=10.0
JDK_MAJOR=17
CMDLINE_TOOLS_BUILD=13114758
ANDROID_PLATFORM=android-36
BUILD_TOOLS_VERSION=36.0.0
NDK_VERSION=27.0.12077973
CMAKE_VERSION=3.22.1
OPENAL_VERSION=1.24.3
OPENAL_COMMIT=dc7d7054a5b4f3bec1dc23a42fd616a0847af948
LUA_VERSION=5.1.5
LUA_SHA256=2640fc56a795f29d28ef15e13c34a47e223960b0240e8cb0a82d9b0738695333
RA_QUICKINSTALL_SHA1=44241f68e69db9511db82cf83c174737ccda300b
RA_QUICKINSTALL_MIRRORS=https://www.openra.net/packages/ra-quickinstall-mirrors.txt
PACKAGE=xr.openra

case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) HOST_OS=windows; EXE=.exe; BAT=.bat ;;
    Darwin) HOST_OS=macos; EXE=; BAT= ;;
    *) HOST_OS=linux; EXE=; BAT= ;;
esac

ANDROID_SDK=${ANDROID_SDK_ROOT:-"$TOOLCHAINS/android-sdk"}
JDK=${OPENRA_JDK:-"$TOOLCHAINS/jdk"}
DOTNET_DIR="$TOOLCHAINS/dotnet"
DOTNET="$DOTNET_DIR/dotnet$EXE"
ADB="$ANDROID_SDK/platform-tools/adb$EXE"
BUILD_TOOLS="$ANDROID_SDK/build-tools/$BUILD_TOOLS_VERSION"
APK_OUTPUT="$ARTIFACTS/xr-openra-quest3.apk"
RA_ZIP="$ARTIFACTS/content/ra-quickinstall.zip"

# Pfad für native Windows-Programme (dotnet, cmake, java) umwandeln.
np() {
    if [ "$HOST_OS" = windows ]; then cygpath -m "$1"; else printf '%s' "$1"; fi
}

sha1_of() {
    if command -v sha1sum >/dev/null 2>&1; then sha1sum "$1" | cut -d ' ' -f 1
    else shasum -a 1 "$1" | cut -d ' ' -f 1; fi
}

step() { printf '\n==> %s\n' "$*"; }
die() { printf 'FEHLER: %s\n' "$*" >&2; exit 1; }

export JAVA_HOME
JAVA_HOME=$(np "$JDK")
export DOTNET_ROOT
DOTNET_ROOT=$(np "$DOTNET_DIR")
export DOTNET_CLI_HOME
DOTNET_CLI_HOME=$(np "$TOOLCHAINS/dotnet-home")
export NUGET_PACKAGES
NUGET_PACKAGES=$(np "$TOOLCHAINS/dotnet-home/.nuget/packages")
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_MULTILEVEL_LOOKUP=0
# Git Bash soll Argumente wie /data/local/tmp nicht in Windows-Pfade umschreiben;
# lokale Pfade für native Programme wandelt np() gezielt um.
export MSYS_NO_PATHCONV=1
export ANDROID_HOME ANDROID_SDK_ROOT
ANDROID_HOME=$(np "$ANDROID_SDK")
ANDROID_SDK_ROOT=$ANDROID_HOME
export OPENRA_TOOLCHAINS="$TOOLCHAINS"
PATH="$DOTNET_DIR:$JDK/bin:$ANDROID_SDK/platform-tools:$PATH"
export PATH
