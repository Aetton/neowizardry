"""Inventory original monastery resources; keep extracted bytes outside public outputs."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys

from tools.slf import Archive, FormatError

REQUIRED = ("monastery/monastery1.pvl", "monastery/monastery2.pvl")


def audit(archive_path: Path, extract: Path | None = None) -> dict:
    archive = Archive(archive_path)
    inventory = archive.manifest()
    selected = sorted((e for e in archive.lookup.values()
                       if e.name.casefold().startswith("monastery/")),
                      key=lambda e: e.name.casefold())
    missing = [name for name in REQUIRED if name not in archive.lookup]
    entries = []
    for entry in selected:
        data = archive.read(entry.name)
        entries.append({"name": entry.name, "size": entry.size,
                        "sha256": hashlib.sha256(data).hexdigest()})
    report = {"schema": 1, "archive": archive.path.name,
              "size": archive.size, "sha256": inventory["sha256"],
              "active_entries": len(archive.lookup),
              "extensions": inventory["extensions"],
              "monastery": entries, "missing_required": missing,
              "geometry_decoded": False,
              "scope": "Base archive only; patches, loose overrides and external dependencies are not resolved."}
    if extract is not None:
        if missing:
            raise FormatError("Required monastery PVL resources are missing")
        archive.extract(extract, "monastery/*")
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--extract", type=Path,
                        help="Private local destination; never upload this directory")
    args = parser.parse_args()
    try:
        report = audit(args.archive, args.extract)
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"SLF entries: {report['active_entries']}; monastery resources: {len(report['monastery'])}")
        if report["missing_required"]:
            print("Missing required monastery PVL resources", file=sys.stderr)
            return 1
        return 0
    except (OSError, FormatError, KeyError) as error:
        # Do not echo installation paths or file contents into public Actions logs.
        print(f"World audit failed ({type(error).__name__}); check the local archive/destination", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
