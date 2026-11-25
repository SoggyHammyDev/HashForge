#!/usr/bin/env python3
"""Read-only discovery and import capacity check; candidates are not certified compatible."""
import argparse
import json
from pathlib import Path
import shutil
from import_bitcoin import inventory

def inspect_candidate(path):
    path = Path(path)
    try:
        if path.is_symlink():
            raise ValueError("Candidate is a symbolic link")
        files = inventory(path)
        total = sum(info[0] for info in files.values())
        return {"path": str(path), "candidate": True, "bytes": total,
                "required_bytes": total + max(total // 10, 1024**3),
                "compatibility": "unverified; clean archival mainnet snapshot required"}
    except (OSError, ValueError) as exc:
        return {"path": str(path), "candidate": False, "reason": str(exc)}

def discover(roots, depth=5):
    """Bound traversal to supplied roots; never follow directory symlinks."""
    seen = set()
    for root in roots:
        root = Path(root)
        if not root.is_dir() or root.is_symlink():
            continue
        stack = [(root, 0)]
        while stack:
            path, level = stack.pop()
            if path in seen:
                continue
            seen.add(path)
            if (path / "blocks").is_dir() and (path / "chainstate").is_dir():
                yield inspect_candidate(path)
                continue
            if level >= depth:
                continue
            try:
                stack.extend((p, level + 1) for p in path.iterdir()
                             if not p.is_symlink() and p.is_dir() and not p.name.startswith("."))
            except OSError:
                continue

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("roots", nargs="+", help="Explicit app-data roots or snapshot directories")
    parser.add_argument("--destination-parent", type=Path, required=True)
    args = parser.parse_args()
    free = shutil.disk_usage(args.destination_parent).free
    results = list(discover(args.roots))
    for result in results:
        if result["candidate"]:
            result["enough_space"] = free >= result["required_bytes"]
    print(json.dumps({"free_bytes": free, "candidates": results}, indent=2))

if __name__ == "__main__":
    main()
