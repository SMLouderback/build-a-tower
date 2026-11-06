#!/bin/sh
# Read a plaintext family key from stdin and store only the hash.
# Usage: printf '%s' 'your-key' | sh /opt/playtest/set-key.sh
set -eu
docker exec -i playtest-api dotnet Playtest.Api.dll --set-key
