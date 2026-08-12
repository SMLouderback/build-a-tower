#!/usr/bin/env bash
# Cloud Agent install phase for Build-A-Tower (Unity 6000.4.7f1).
#
# Idempotent, non-interactive, and safe to re-run. Installs the system
# libraries the headless Unity Editor needs, then installs the pinned Unity
# Editor. Both steps are captured in the environment snapshot; per-boot license
# activation happens later in the `start` phase.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "[install] Installing system dependencies for headless Unity..."
export DEBIAN_FRONTEND=noninteractive
sudo apt-get update -qq
# Runtime libraries required by the Linux Unity Editor + a virtual framebuffer
# so PlayMode tests can create a GL context without a physical display.
sudo apt-get install -y --no-install-recommends \
  xvfb \
  libgtk-3-0 \
  libnss3 \
  libasound2t64 \
  libgbm1 \
  libglu1-mesa \
  libxtst6 \
  libxrandr2 \
  libxcursor1 \
  libxss1 \
  libatk-bridge2.0-0 \
  libnotify4 \
  libxkbcommon0 \
  libsecret-1-0

echo "[install] Ensuring Unity Editor is installed..."
bash "${SCRIPT_DIR}/install-unity.sh"

echo "[install] Done."
