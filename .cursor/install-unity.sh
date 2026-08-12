#!/usr/bin/env bash
# Idempotent Unity Editor bootstrap for Build-A-Tower Cloud Agents.
#
# Downloads and extracts the exact Unity Editor version this project is pinned
# to (see ProjectSettings/ProjectVersion.txt) if it is not already present.
# Safe to run repeatedly: an existing, working editor is left untouched.
#
# This is durable, source-independent setup and belongs in `install` so it is
# captured in the environment snapshot rather than re-run on every boot.
set -euo pipefail

UNITY_VERSION="6000.4.7f1"
UNITY_REVISION="f3c3c4248748"
UNITY_ROOT="${UNITY_ROOT:-$HOME/Unity/Hub/Editor/${UNITY_VERSION}}"
UNITY_BIN="${UNITY_ROOT}/Editor/Unity"
DL_URL="https://download.unity3d.com/download_unity/${UNITY_REVISION}/LinuxEditorInstaller/Unity.tar.xz"

if [[ -x "${UNITY_BIN}" ]]; then
  echo "[install-unity] Unity ${UNITY_VERSION} already present at ${UNITY_BIN}"
  exit 0
fi

echo "[install-unity] Installing Unity Editor ${UNITY_VERSION} (rev ${UNITY_REVISION})"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "${tmp_dir}"' EXIT

echo "[install-unity] Downloading editor (~4.1 GB)..."
curl -fL --retry 4 --retry-delay 5 -o "${tmp_dir}/Unity.tar.xz" "${DL_URL}"

echo "[install-unity] Extracting..."
mkdir -p "${UNITY_ROOT}"
# The archive expands into Editor/ and Data/ at its root.
tar -xJf "${tmp_dir}/Unity.tar.xz" -C "${UNITY_ROOT}"

if [[ ! -x "${UNITY_BIN}" ]]; then
  echo "[install-unity] ERROR: expected editor binary not found at ${UNITY_BIN}" >&2
  exit 1
fi

echo "[install-unity] Installed: $("${UNITY_BIN}" -version 2>/dev/null || echo "${UNITY_VERSION}")"
echo "[install-unity] Done."
