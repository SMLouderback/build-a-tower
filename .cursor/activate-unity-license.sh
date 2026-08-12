#!/usr/bin/env bash
# Per-boot Unity license activation for Build-A-Tower Cloud Agents.
#
# Runs in the `start` phase because it depends on runtime secrets and must make
# the license active on the current machine. Idempotent and non-fatal: if no
# license secrets are configured it prints guidance and exits 0 so the agent can
# still open a shell (tests simply will not run until a license is provided).
set -uo pipefail

UNITY_VERSION="6000.4.7f1"
UNITY_ROOT="${UNITY_ROOT:-$HOME/Unity/Hub/Editor/${UNITY_VERSION}}"
UNITY_BIN="${UNITY_ROOT}/Editor/Unity"
LOG_DIR="${HOME}/.cache/build-a-tower"
mkdir -p "${LOG_DIR}"
ACT_LOG="${LOG_DIR}/unity-activation.log"

if [[ ! -x "${UNITY_BIN}" ]]; then
  echo "[license] Unity editor not found at ${UNITY_BIN}; run .cursor/install.sh first." >&2
  exit 0
fi

run_unity() {
  xvfb-run -a "${UNITY_BIN}" -batchmode -nographics -logFile "${ACT_LOG}" "$@"
}

# Path A: Personal license supplied as full .ulf file contents.
if [[ -n "${UNITY_LICENSE:-}" ]]; then
  echo "[license] Activating Unity Personal license from UNITY_LICENSE (.ulf)..."
  ulf="${LOG_DIR}/Unity_lic.ulf"
  printf '%s' "${UNITY_LICENSE}" > "${ulf}"
  if run_unity -manualLicenseFile "${ulf}" -quit; then
    echo "[license] Personal license activated."
  else
    # Unity returns non-zero on -manualLicenseFile even when it succeeds in some
    # versions; treat presence of an installed license as success.
    echo "[license] Unity exited non-zero; see ${ACT_LOG} (this can be benign)."
  fi
  exit 0
fi

# Path B: Plus/Pro serial + account credentials.
if [[ -n "${UNITY_SERIAL:-}" && -n "${UNITY_EMAIL:-}" && -n "${UNITY_PASSWORD:-}" ]]; then
  echo "[license] Activating Unity Plus/Pro license via serial..."
  if run_unity -quit -serial "${UNITY_SERIAL}" -username "${UNITY_EMAIL}" -password "${UNITY_PASSWORD}"; then
    echo "[license] Pro/Plus license activated."
  else
    echo "[license] Activation reported non-zero exit; see ${ACT_LOG}." >&2
  fi
  exit 0
fi

cat <<'EOF'
[license] No Unity license configured.
[license] Set ONE of:
[license]   - UNITY_LICENSE (full contents of a Personal .ulf file), or
[license]   - UNITY_SERIAL + UNITY_EMAIL + UNITY_PASSWORD (Plus/Pro).
[license] The editor is installed but cannot import the project or run tests
[license] until a license is activated.
EOF
exit 0
