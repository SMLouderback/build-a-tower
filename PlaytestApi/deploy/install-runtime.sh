#!/bin/sh
set -eu
mkdir -p "$HOME/playtest/data" "$HOME/playtest/publish"
rm -rf "$HOME/playtest/publish"
cp -a /tmp/publish "$HOME/playtest/publish"
cp /tmp/Dockerfile.runtime "$HOME/playtest/Dockerfile"
cp /tmp/set-key.sh "$HOME/playtest/set-key.sh"
chmod +x "$HOME/playtest/set-key.sh"
printf 'Smtp__Host=\n' > "$HOME/playtest/.env"
cp /tmp/Build-A-Tower.zip "$HOME/playtest/data/Build-A-Tower.zip"
printf '%s\n' '{"version":"1.0.0","releasedAt":"2026-09-27T21:10:00Z","downloadPage":"https://escapeproductions.biz/#download"}' > "$HOME/playtest/data/version.json"
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
