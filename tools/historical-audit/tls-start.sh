#!/bin/sh
set -eu
variant="${1:-server}"
cp "/tls/$variant.crt" /tmp/b03b21-server.crt
cp "/tls/$variant.key" /tmp/b03b21-server.key
chown postgres:postgres /tmp/b03b21-server.crt /tmp/b03b21-server.key
chmod 600 /tmp/b03b21-server.key
exec /usr/local/bin/docker-entrypoint.sh postgres -c ssl=on -c ssl_cert_file=/tmp/b03b21-server.crt -c ssl_key_file=/tmp/b03b21-server.key
