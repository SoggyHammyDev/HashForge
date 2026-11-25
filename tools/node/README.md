# Experimental offline blockchain importer

Linux, Python 3.11+, glibc with renameat2 support. This is a developer utility, not a released UI.

Use only a clean, immutable snapshot of a **compatible Bitcoin Core archival mainnet** data directory. Shut down the source node cleanly before taking the snapshot, then it can restart while the snapshot is imported. Keep the destination node stopped. Mount the snapshot read-only; run as the destination service user (typically UID 1000). Never point the destination at another application's storage. A live database, pruned node, Bitcoin Knots variant or different network/version is not yet supported or automatically detected. A filesystem snapshot of a running node is not sufficient unless application consistency is established.

    python3 tools/node/import_bitcoin.py /read-only/snapshot /path/to/new/bitcoin --confirm-clean-compatible-archival-mainnet-snapshot

Destination must not exist. The utility copies blocks and chainstate only, checks each copied file by SHA256, and publishes the staged directory using a no-replace rename. No source files are intentionally modified. It does not prove consensus validity or detect all database compatibility issues; Bitcoin Core must open and verify the result before mining. It does not copy wallets, configuration, indexes outside blocks/index, or credentials. Bitcoin Core may need to rebuild indexes or resynchronize. Disk space must accommodate a second copy plus reserve. A failed copy removes only its own staging directory; a process kill can leave a staging directory for manual review.

Run fixture tests: python3 -m unittest discover -s tools/node -p 'test_*.py'

Before production: validate with real regtest databases, detect version/network/pruning, enforce read-only mounts, implement power-loss durability, integrate with node lifecycle, and test Soggy Pools paths. Do not use production funds or mining until validation is complete.

## Real database test

Run `bash tools/node/test_regtest_import.sh hashforge-bitcoind:ci` from the repository root after building the node image. The test creates an isolated regtest chain, stops the source cleanly, copies the database, verifies the destination tip and chain, generates an additional block, and compares source file checksums. This does not yet prove mainnet compatibility or pool Stratum block submission.

## Read-only discovery

Run `python3 tools/node/preflight.py /path/to/umbrel/app-data /path/to/snapshots --destination-parent /path/to/hashforge/data` on the host. Only explicitly supplied roots are scanned, to a bounded depth. Results are candidates, not proof of version, pruning or network compatibility. Do not expose host directories to the web container. Source-specific Soggy Pools layouts still require verification on a real installation.
