#!/usr/bin/env python3
"""One-shot restricted setup worker. Host must supply a clean immutable compatible snapshot."""
import json
import os
from pathlib import Path
import tempfile
import time
from approve_setup import approve
from import_bitcoin import import_snapshot

def publish_status(directory, event):
    fd, name = tempfile.mkstemp(prefix=".status-", dir=directory)
    try:
        with os.fdopen(fd, "w") as handle:
            json.dump(event, handle)
            handle.flush()
            os.fsync(handle.fileno())
        os.chmod(name, 0o644)
        os.replace(name, directory / "state.json")
    finally:
        if os.path.exists(name):
            os.unlink(name)

def process(requests, status, setup, destination, snapshot):
    """Process only a fixed choice; never accept a path from the browser."""
    request = requests / "request"
    if not request.is_file() or request.is_symlink():
        return False
    if (setup / "decision").exists():
        return False
    claim = status / "claimed"
    try:
        claim.mkdir()
    except FileExistsError:
        return False
    def report(event):
        # Do not report copy completion as node readiness.
        if event.get("phase") == "complete":
            event = dict(event, phase="copied")
        publish_status(status, event)
    try:
        mode = request.read_text().strip()
        if destination.exists() or destination.is_symlink():
            raise ValueError("Destination already exists; operator review required")
        if mode == "fresh":
            report({"phase": "preparing"})
            destination.mkdir(mode=0o700)
        elif mode == "imported":
            if snapshot.is_symlink() or not snapshot.is_dir():
                raise ValueError("No prepared snapshot is mounted")
            import_snapshot(snapshot, destination, progress=report)
        else:
            raise ValueError("Unsupported setup choice")
        approve(setup, mode)
        report({"phase": "approved", "mode": mode,
                "message": "Storage prepared; Bitcoin Core must validate and synchronize"})
    except (OSError, ValueError, AttributeError):
        report({"phase": "failed", "message": "Setup stopped; host administrator review required"})
        raise
    return True

def main():
    requests, status, setup = map(Path, ("/requests", "/status", "/setup"))
    while True:
        if (setup / "decision").exists() or (status / "claimed").exists():
            # A crash or failure requires explicit host recovery, never implicit retry.
            return
        if process(requests, status, setup, Path("/destination/bitcoin"), Path("/snapshot")):
            return
        time.sleep(2)

if __name__ == "__main__":
    main()
