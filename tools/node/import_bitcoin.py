#!/usr/bin/env python3
"""Offline, non-destructive import of a clean Bitcoin Core mainnet snapshot."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import tempfile
import time

def inventory(source):
    result = {}
    for root in ("blocks", "chainstate"):
        folder = source / root
        if folder.is_symlink() or not folder.is_dir():
            raise ValueError(f"Missing or unsafe directory: {root}")
        for current, dirs, files in os.walk(folder, followlinks=False):
            for name in dirs + files:
                path = Path(current) / name
                st = path.lstat()
                if not (stat.S_ISREG(st.st_mode) or stat.S_ISDIR(st.st_mode)):
                    raise ValueError(f"Unsafe file: {path}")
                if stat.S_ISREG(st.st_mode):
                    result[path.relative_to(source)] = (st.st_size, st.st_mtime_ns, st.st_ino)
    if not any(p.parent == Path("blocks") and p.name.startswith("blk") and p.suffix == ".dat" for p in result):
        raise ValueError("No block files found")
    for marker in ("blocks/index/CURRENT", "chainstate/CURRENT"):
        if Path(marker) not in result:
            raise ValueError(f"Missing database marker: {marker}")
    return result

def digest(path):
    with path.open("rb") as file:
        return hashlib.file_digest(file, "sha256").hexdigest()

def import_snapshot(source, destination, progress=None):
    source = Path(source).resolve(strict=True)
    destination = Path(destination).absolute()
    if destination.is_symlink():
        raise ValueError("Destination must not be a symlink")
    destination = destination.resolve()
    if source == destination or source in destination.parents or destination in source.parents:
        raise ValueError("Source and destination must be disjoint")
    if destination.exists():
        raise ValueError("Destination must not exist; stop before first sync or choose a new directory")
    if not destination.parent.is_dir():
        raise ValueError("Destination parent must exist")
    before = inventory(source)
    size = sum(v[0] for v in before.values())
    if shutil.disk_usage(destination.parent).free < size + max(size // 10, 1024**3):
        raise ValueError("Insufficient free disk space including safety reserve")
    stage = Path(tempfile.mkdtemp(prefix=".hashforge-import-", dir=destination.parent))
    started = time.monotonic()
    copied = 0
    def report(phase):
        if progress is not None:
            elapsed = max(time.monotonic() - started, 0.001)
            speed = copied / elapsed
            progress({"phase": phase, "bytes_verified": copied, "total_bytes": size,
                      "percent": round(100 * copied / size, 2) if size else 100,
                      "bytes_per_second": round(speed),
                      "eta_seconds": round((size - copied) / speed) if speed else None})
    try:
        report("copying")
        for rel, metadata in sorted(before.items()):
            target = stage / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            original = source / rel
            if original.is_symlink():
                raise ValueError(f"Source changed: {rel}")
            shutil.copyfile(original, target)
            if target.stat().st_size != metadata[0] or digest(original) != digest(target):
                raise ValueError(f"Copy verification failed: {rel}")
            copied += metadata[0]
            report("copying")
        report("finalizing")
        if inventory(source) != before:
            raise ValueError("Source changed during import")
        # Exclusive lock directory serializes our own importers; operator must keep destination node stopped.
        lock = destination.parent / (".hashforge-import-lock-" + destination.name)
        lock.mkdir()
        try:
            if destination.exists() or destination.is_symlink():
                raise ValueError("Destination appeared during import")
            # Linux renameat2 RENAME_NOREPLACE prevents replacing even an empty destination.
            import ctypes
            libc = ctypes.CDLL(None, use_errno=True)
            rename = libc.renameat2
            rename.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint]
            if rename(-100, os.fsencode(stage), -100, os.fsencode(destination), 1) != 0:
                err = ctypes.get_errno()
                raise OSError(err, os.strerror(err))
        finally:
            lock.rmdir()
        report("complete")
        return {"files": len(before), "bytes": size, "destination": str(destination)}
    finally:
        if stage.exists():
            shutil.rmtree(stage)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source")
    parser.add_argument("destination")
    parser.add_argument("--confirm-clean-compatible-archival-mainnet-snapshot", action="store_true", required=True)
    args = parser.parse_args()
    try:
        print(json.dumps(import_snapshot(args.source, args.destination,
            progress=lambda event: print(json.dumps(event), flush=True))))
    except (OSError, ValueError, AttributeError) as exc:
        parser.exit(1, f"Import failed: {exc}\n")

if __name__ == "__main__":
    main()
