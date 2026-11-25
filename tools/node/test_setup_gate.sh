#!/usr/bin/env bash
set -euo pipefail
root="$(mktemp -d)"
name="hashforge-gate-$$"
trap 'docker rm -f "$name" >/dev/null 2>&1 || true; rm -rf -- "$root"' EXIT
mkdir "$root/setup" "$root/bitcoin"
docker run -d --name "$name" --network none --user "$(id -u):$(id -g)" \
    -e NETWORK=regtest -e PRUNE_MB=0 -e RPC_USER=test -e RPC_PASSWORD=local-test-only \
    -e HASHFORGE_SETUP_GATE=1 -v "$root/setup:/setup:ro" -v "$root/bitcoin:/data" \
    "${1:-hashforge-bitcoind:ci}" >/dev/null
sleep 5
test -z "$(ls -A "$root/bitcoin")"
test "$(docker inspect -f '{{.State.Running}}' "$name")" = true
printf 'invalid\n' > "$root/setup/decision"
sleep 4
test -z "$(ls -A "$root/bitcoin")"
rm "$root/setup/decision"
python3 tools/node/approve_setup.py "$root/setup" fresh --confirm-destination-prepared
wait_rpc() {
    for ((i=0;i<60;i++)); do
        if docker exec "$name" bitcoin-cli -regtest -datadir=/data -rpcport=8332 -rpcuser=test -rpcpassword=local-test-only getblockcount; then return 0; fi
        sleep 2
    done
    docker logs "$name" >&2
    return 1
}
wait_rpc
docker stop -t 120 "$name" >/dev/null
docker start "$name" >/dev/null
wait_rpc
echo "Setup gate and persisted approval passed"
