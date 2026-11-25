#!/usr/bin/env python3
"""Publish an explicit setup decision to a dedicated host-owned setup directory."""
import argparse
import os
from pathlib import Path
import tempfile

def approve(setup, mode):
    setup = Path(setup)
    if setup.is_symlink() or not setup.is_dir():
        raise ValueError("Setup must be an existing dedicated directory, not a symlink")
    if mode not in ("fresh", "imported"):
        raise ValueError("Invalid setup mode")
    target = setup / "decision"
    fd, name = tempfile.mkstemp(prefix=".decision-", dir=setup)
    try:
        with os.fdopen(fd, "w") as handle:
            handle.write(mode + "\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.chmod(name, 0o644)
        # Link is atomic and refuses to replace any existing decision.
        os.link(name, target)
        directory = os.open(setup, os.O_RDONLY | os.O_DIRECTORY)
        try:
            os.fsync(directory)
        finally:
            os.close(directory)
    finally:
        os.unlink(name)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("setup", type=Path)
    parser.add_argument("mode", choices=["fresh", "imported"])
    parser.add_argument("--confirm-destination-prepared", required=True, action="store_true")
    args = parser.parse_args()
    try:
        approve(args.setup, args.mode)
    except (OSError, ValueError) as exc:
        parser.exit(1, f"Setup approval failed: {exc}\n")

if __name__ == "__main__":
    main()
