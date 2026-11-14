#!/bin/sh
set -eu
printf '%s\n' '{"key":"nope"}' > /tmp/bad.json
printf '%s\n' "{\"key\":\"${PLAYTEST_KEY:?set PLAYTEST_KEY}\"}" > /tmp/good.json
echo BAD
curl -sS -o /tmp/bad.out -w '%{http_code}\n' -X POST http://172.17.0.1:5080/playtest/download -H 'Content-Type: application/json' --data-binary @/tmp/bad.json
cat /tmp/bad.out
echo
echo GOOD
curl -sS -o /tmp/good.zip -w '%{http_code} %{size_download}\n' -X POST http://172.17.0.1:5080/playtest/download -H 'Content-Type: application/json' --data-binary @/tmp/good.json
