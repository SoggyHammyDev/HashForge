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

The import CLI emits JSON progress events after each fully copied and verified file. Rates and ETA are approximate and include verification time. No web-controlled import is enabled yet.

Development image publishing is manual, restricted to the standalone branch of the fork, and uses immutable commit tags rather than latest. After green CI, run Publish standalone development images and verify both package visibility and digests before updating the Umbrel package.

## Optional startup gate

Set HASHFORGE_SETUP_GATE=1 on the node container and mount a dedicated host-owned setup directory at /setup:ro. The node waits for a decision file containing fresh or imported. The host administrator must prepare the destination, ensure no import is running, then use `python3 tools/node/approve_setup.py /path/to/setup fresh --confirm-destination-prepared` (or imported). Decisions are one-time and persist over restart. The approval utility does not validate the destination; never approve an unverified import. The web application does not have write access to this directory. This opt-in gate is not yet enabled in the default Compose stacks, and no GUI start action exists yet.
