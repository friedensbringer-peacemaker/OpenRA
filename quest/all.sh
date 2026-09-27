#!/usr/bin/env bash
# Kompletter Ablauf: Werkzeuge laden → Red-Alert-Paket laden → bauen → auf Quest installieren.
# "all.sh --no-install" baut nur.
set -euo pipefail
here=$(dirname -- "$0")
bash "$here/setup-toolchains.sh"
bash "$here/fetch-ra-content.sh"
bash "$here/build.sh"
if [ "${1:-}" != "--no-install" ]; then
    bash "$here/install.sh"
fi
