#!/bin/sh
set -eu
chmod +x /opt/playtest/set-key.sh
if [ ! -f /opt/playtest/.env ]; then
  printf 'Smtp__Host=\n' > /opt/playtest/.env
fi
cd /opt/playtest
docker build -t playtest-api:local .
docker rm -f playtest-api >/dev/null 2>&1 || true
docker run -d --name playtest-api --restart unless-stopped \
  -p 127.0.0.1:5080:8080 \
  --env-file /opt/playtest/.env \
  -e Playtest__DataDirectory=/data \
  -v /opt/playtest/data:/data \
  playtest-api:local
docker restart escape-web
curl -fsS http://127.0.0.1:5080/playtest/health
echo
