#!/usr/bin/env bash
# Run Build-A-Tower's automated tests headlessly with the Unity Test Runner.
#
# Usage: .cursor/run-unity-tests.sh [editmode|playmode]   (default: both)
# Results are written as NUnit XML under Logs/ and a summary is printed.
set -uo pipefail

UNITY_VERSION="6000.4.7f1"
UNITY_ROOT="${UNITY_ROOT:-$HOME/Unity/Hub/Editor/${UNITY_VERSION}}"
UNITY_BIN="${UNITY_ROOT}/Editor/Unity"
PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG_DIR="${PROJECT_PATH}/Logs"
mkdir -p "${LOG_DIR}"

if [[ ! -x "${UNITY_BIN}" ]]; then
  echo "Unity editor not found at ${UNITY_BIN}. Run .cursor/install.sh first." >&2
  exit 1
fi

run_platform() {
  local platform="$1"
  local results="${LOG_DIR}/test-results-${platform}.xml"
  local logfile="${LOG_DIR}/unity-tests-${platform}.log"
  echo "=== Running ${platform} tests ==="
  xvfb-run -a "${UNITY_BIN}" \
    -runTests \
    -batchmode \
    -projectPath "${PROJECT_PATH}" \
    -testPlatform "${platform}" \
    -testResults "${results}" \
    -logFile "${logfile}" \
    -forgetProjectPath
  local status=$?
  echo "${platform} exit code: ${status}"
  if [[ -f "${results}" ]]; then
    echo "Results: ${results}"
    grep -oE '<test-run [^>]*>' "${results}" | head -1
  fi
  return ${status}
}

target="${1:-both}"
overall=0
case "${target}" in
  editmode) run_platform editmode || overall=$? ;;
  playmode) run_platform playmode || overall=$? ;;
  both)
    run_platform editmode || overall=$?
    run_platform playmode || overall=$?
    ;;
  *) echo "Unknown target '${target}' (use editmode|playmode|both)" >&2; exit 2 ;;
esac

exit ${overall}
