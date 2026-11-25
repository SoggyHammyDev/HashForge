#!/usr/bin/env bash
# Exercise the actual Bitcoin Core databases, not synthetic fixtures.
set -euo pipefail
image="${1:-hashforge-bitcoind:ci}"
root="$(mktemp -d)"
source_name="hashforge-import-source-$$"
dest_name="hashforge-import-dest-$$"
cleanup() {
  docker rm -f "$source_name" "$dest_name" >/dev/null 2>&1 || true
  rm -rf -- "$root"
}
trap cleanup EXIT
mkdir "$root/source"
start_node() {
  docker run -d --name "$1" --network none --user "$(id -u):$(id -g)" \
    -e NETWORK=regtest -e PRUNE_MB=0 -e RPC_USER=importtest -e RPC_PASSWORD=local-test-only \
    -v "$2:/data" "$image" >/dev/null
}
rpc() {
  local name="$1"; shift
  docker exec "$name" bitcoin-cli -regtest -datadir=/data -rpcport=8332 \
    -rpcuser=importtest -rpcpassword=local-test-only "$@"
}
wait_rpc() {
  for ((i=0;i<90;i++)); do
    if rpc "$1" getblockcount >/dev/null 2>&1; then return; fi
    sleep 2
  done
  docker logs "$1" >&2
  return 1
}
start_node "$source_name" "$root/source"
wait_rpc "$source_name"
# A fixed valid compressed public key avoids creating a wallet.
rpc "$source_name" generatetodescriptor 110 'pk(0279be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798)' >/dev/null
height="$(rpc "$source_name" getblockcount)"
tip="$(rpc "$source_name" getbestblockhash)"
test "$height" = 110
docker stop -t 120 "$source_name" >/dev/null
test "$(docker inspect -f '{{.State.ExitCode}}' "$source_name")" = 0
# Hash the clean source data before importing and confirm byte-for-byte preservation.
snapshot_hash() {
  (cd "$root/source/regtest" && find blocks chainstate -type f -print0 | sort -z | xargs -0 sha256sum)
}
snapshot_hash > "$root/before"
mkdir "$root/destination"
# The CLI intentionally accepts archival mainnet only. Exercise the same underlying
# copy function directly with isolated regtest directories.
PYTHONPATH=tools/node python3 - "$root/source/regtest" "$root/destination/regtest" <<'PY'
import sys
from import_bitcoin import import_snapshot
print(import_snapshot(sys.argv[1], sys.argv[2]))
PY
snapshot_hash > "$root/after"
cmp "$root/before" "$root/after"
start_node "$dest_name" "$root/destination"
wait_rpc "$dest_name"
test "$(rpc "$dest_name" getblockcount)" = "$height"
test "$(rpc "$dest_name" getbestblockhash)" = "$tip"
test "$(rpc "$dest_name" verifychain 4 0)" = true
rpc "$dest_name" generatetodescriptor 1 'pk(0279be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798)' >/dev/null
test "$(rpc "$dest_name" getblockcount)" = 111
snapshot_hash > "$root/after-mine"
cmp "$root/before" "$root/after-mine"
echo "Real regtest import, verification, independent mining and source preservation passed"
