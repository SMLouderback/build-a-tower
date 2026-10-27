#!/bin/sh
set -eu
mkdir -p "$HOME/playtest/data"
cp /tmp/Dockerfile "$HOME/playtest/Dockerfile"
cp /tmp/set-key.sh "$HOME/playtest/set-key.sh"
chmod +x "$HOME/playtest/set-key.sh"
rm -rf "$HOME/playtest/src"
cp -a /tmp/src "$HOME/playtest/src"
printf 'Smtp__Host=\n' > "$HOME/playtest/.env"
cp /tmp/Build-A-Tower.zip "$HOME/playtest/data/Build-A-Tower.zip"
cp /tmp/version.json "$HOME/playtest/data/version.json" 2>/dev/null || true
cd "$HOME/playtest"
docker build -t playtest-api:local .
docker rm -f playtest-api >/dev/null 2>&1 || true
docker run -d --name playtest-api --restart unless-stopped \
  -p 127.0.0.1:5080:8080 \
  --env-file "$HOME/playtest/.env" \
  -e Playtest__DataDirectory=/data \
  -v "$HOME/playtest/data:/data" \
  playtest-api:local
sudo cp /tmp/nginx-default.conf /opt/escapeproductions-site/nginx-default.conf
docker restart escape-web
curl -fsS http://127.0.0.1:5080/playtest/health
echo
