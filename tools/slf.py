"""Read-only Sir-Tech SLF tooling. Python 3.10+, no third-party dependencies."""
from __future__ import annotations

import argparse
from collections import Counter
from dataclasses import asdict, dataclass
import fnmatch
import hashlib
import json
import re
from pathlib import Path, PurePosixPath
import struct
import sys

HEADER_SIZE = 532
ENTRY_SIZE = 280


class FormatError(ValueError):
    pass


def canonical(name: str) -> str:
    name = name.replace("\\", "/")
    parts = name.split("/")
    if not name or name.startswith("/") or any(p in ("", ".", "..") for p in parts):
        raise FormatError(f"Unsafe resource path: {name!r}")
    if any(":" in p or any(ord(c) < 32 for c in p) for p in parts):
        raise FormatError(f"Unsafe resource path: {name!r}")
    return name


def cstring(raw: bytes) -> str:
    head, separator, tail = raw.partition(b"\0")
    if not separator or any(tail):
        raise FormatError("Expected zero-padded archive string")
    return head.decode("latin1")


@dataclass(frozen=True)
class Entry:
    name: str
    offset: int
    size: int
    state: int


class Archive:
    def __init__(self, path: Path):
        self.path = Path(path)
        self.size = self.path.stat().st_size
        with self.path.open("rb") as f:
            header = f.read(HEADER_SIZE)
            if len(header) != HEADER_SIZE:
                raise FormatError("Truncated SLF header")
            self.name = cstring(header[:256])
            self.base = cstring(header[256:512])
            count, self.used = struct.unpack_from("<II", header, 512)
            self.index_offset = self.size - count * ENTRY_SIZE
            if self.index_offset < HEADER_SIZE or self.used > count:
                raise FormatError("Impossible directory size")
            f.seek(self.index_offset)
            entries = []
            seen = set()
            for _ in range(count):
                raw = f.read(ENTRY_SIZE)
                name = canonical(cstring(raw[:256]))
                offset, length = struct.unpack_from("<II", raw, 256)
                state = raw[264]
                if state == 0:
                    if offset < HEADER_SIZE or offset + length > self.index_offset:
                        raise FormatError(f"Resource outside payload: {name}")
                    if name.casefold() in seen:
                        raise FormatError(f"Duplicate active resource: {name}")
                    seen.add(name.casefold())
                entries.append(Entry(name, offset, length, state))
            active = sorted((e for e in entries if e.state == 0), key=lambda e: e.offset)
            end = HEADER_SIZE
            for entry in active:
                if entry.size and entry.offset < end:
                    raise FormatError(f"Overlapping payload: {entry.name}")
                end = max(end, entry.offset + entry.size)
            self.entries = tuple(entries)
            self.lookup = {e.name.casefold(): e for e in entries if e.state == 0}

    def read(self, name: str) -> bytes:
        e = self.lookup[canonical(name).casefold()]
        with self.path.open("rb") as f:
            f.seek(e.offset)
            data = f.read(e.size)
        if len(data) != e.size:
            raise FormatError(f"Truncated payload: {e.name}")
        return data

    def manifest(self) -> dict:
        with self.path.open("rb") as f:
            digest = hashlib.file_digest(f, "sha256").hexdigest() if hasattr(hashlib, "file_digest") else stream_hash(f)
        return {"schema": 1, "archive": self.name, "base": self.base,
                "size": self.size, "sha256": digest, "entries": [asdict(e) for e in self.entries],
                "extensions": dict(sorted(Counter(PurePosixPath(e.name).suffix.upper() for e in self.lookup.values()).items()))}

    def extract(self, destination: Path, pattern: str) -> list[dict]:
        root = destination.resolve()
        selected = [e for e in self.lookup.values() if fnmatch.fnmatchcase(e.name.casefold(), pattern.replace("\\", "/").casefold())]
        if not selected:
            raise FormatError(f"No resources match {pattern!r}")
        targets = [(e, root.joinpath(*e.name.split("/"))) for e in selected]
        for _, target in targets:
            if not target.resolve().is_relative_to(root) or target.exists() or target.is_symlink():
                raise FormatError(f"Refusing unsafe/existing extraction target: {target}")
        output = []
        for e, target in targets:
            target.parent.mkdir(parents=True, exist_ok=True)
            digest = hashlib.sha256()
            with self.path.open("rb") as source, target.open("xb") as dest:
                source.seek(e.offset)
                remaining = e.size
                while remaining:
                    chunk = source.read(min(remaining, 1024 * 1024))
                    if not chunk:
                        raise FormatError(f"Truncated payload: {e.name}")
                    dest.write(chunk)
                    digest.update(chunk)
                    remaining -= len(chunk)
            output.append({"name": e.name, "size": e.size, "sha256": digest.hexdigest()})
        return output


def stream_hash(f) -> str:
    h = hashlib.sha256()
    while chunk := f.read(1024 * 1024):
        h.update(chunk)
    return h.hexdigest()


class Resources:
    """Ordinary installation overlay, matching Wizardry8Editor's SLFFile precedence.

    Loose data files > numerically latest PATCH.NNN > base SLF.
    Nonzero-state entries are skipped, not interpreted as overlay tombstones.
    Parallel Worlds and fanpatch localization are deliberately unsupported.
    """
    def __init__(self, base: Archive, patches=(), loose: Path | None = None):
        self.base = base
        numbered = []
        seen = set()
        for patch in patches:
            match = re.fullmatch(r"PATCH\.(\d{3})", patch.path.name, re.IGNORECASE)
            if not match:
                raise FormatError(f"Not an enabled numbered patch: {patch.path.name}")
            number = int(match.group(1))
            if number in seen:
                raise FormatError(f"Ambiguous patch number: {number}")
            seen.add(number)
            numbered.append((number, patch))
        self.patches = [p for _, p in sorted(numbered, reverse=True)]
        self.loose = {}
        if loose is not None:
            root = loose.resolve()
            if not root.is_dir():
                raise FormatError("Loose data directory does not exist")
            for path in root.rglob("*"):
                if not path.is_file():
                    continue
                if not path.resolve().is_relative_to(root):
                    raise FormatError("Loose file symlink escapes data directory")
                key = canonical(path.relative_to(root).as_posix()).casefold()
                if key in self.loose:
                    raise FormatError(f"Ambiguous loose filename: {key}")
                self.loose[key] = path

    def resolve(self, name: str) -> tuple[bytes, dict]:
        key = canonical(name).casefold()
        if key in self.loose:
            path = self.loose[key]
            data = path.read_bytes()
            return data, {"kind": "loose", "file": canonical(name), "sha256": hashlib.sha256(data).hexdigest()}
        for archive in [*self.patches, self.base]:
            if key in archive.lookup:
                data = archive.read(name)
                return data, {"kind": "archive", "file": archive.path.name, "sha256": hashlib.sha256(data).hexdigest()}
        raise KeyError(name)

    def read(self, name: str) -> bytes:
        return self.resolve(name)[0]


def item_catalog(archive: Archive | Resources) -> dict:
    """Only documented fields; unknown record bytes are not guessed."""
    if isinstance(archive, Resources):
        data, provenance = archive.resolve("DATABASES/ITEMS.DBS")
    else:
        data = archive.read("DATABASES/ITEMS.DBS")
        provenance = {"kind": "archive", "file": archive.path.name, "sha256": hashlib.sha256(data).hexdigest()}
    count = struct.unpack_from("<I", data)[0]
    stride = 0x10D
    if len(data) != 4 + count * stride:
        raise FormatError("ITEMS.DBS does not match verified fixed-record layout")
    items = []
    for i in range(count):
        r = data[4+i*stride:4+(i+1)*stride]
        name = r[:60].decode("utf-16le").split("\0", 1)[0]
        items.append({"id": i, "name": name, "type_code": r[0x3E],
                      "icon_index": struct.unpack_from("<H", r, 0x3F)[0],
                      "profession_mask": struct.unpack_from("<H", r, 0x76)[0],
                      "race_mask": struct.unpack_from("<H", r, 0x78)[0],
                      "gender_mask": r[0x7C], "record_sha256": hashlib.sha256(r).hexdigest()})
        items[-1].update({"usage_skill": r[0x46], "range_code": r[0x47],
                          "initiative": struct.unpack_from("<b", r, 0x48)[0],
                          "to_hit": struct.unpack_from("<b", r, 0x49)[0],
                          "damage_constant": struct.unpack_from("<H", r, 0x4A)[0],
                          "damage_dice": r[0x4C], "damage_sides": r[0x4D],
                          "attack_modes": struct.unpack_from("<H", r, 0x4E)[0],
                          "weight_tenths_lb": struct.unpack_from("<H", r, 0x8A)[0],
                          "cursed": r[0x8C] == 1,
                          "bonus_swings": struct.unpack_from("<b", r, 0xAD)[0]})
    return {"schema": 2, "source": "DATABASES/ITEMS.DBS", "provenance": provenance, "record_size": stride,
            "count": count, "items": items}


def audit_install(root: Path) -> dict:
    """Hashes and indexes; never copies original assets into the report."""
    if not root.is_dir():
        raise FormatError("Installation directory does not exist")
    paths = sorted((p for p in root.rglob("*") if p.is_file() and
                    (p.suffix.casefold() == ".slf" or
                     (p.name.upper().startswith("PATCH.") and p.suffix[1:].isdigit()))),
                   key=lambda p: p.relative_to(root).as_posix().casefold())
    archives = []
    errors = []
    for p in paths:
        try:
            m = Archive(p).manifest()
            m["installation_path"] = p.relative_to(root).as_posix()
            archives.append(m)
        except (FormatError, OSError) as e:
            errors.append({"file": p.relative_to(root).as_posix(), "error": str(e)})
    if not paths:
        raise FormatError("No SLF archives found in installation")
    return {"schema": 1, "archives": archives, "errors": errors,
            "note": "Archive inventories only; patches are listed separately, not merged."}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    for command in ("index", "items", "extract"):
        p = sub.add_parser(command)
        p.add_argument("archive", type=Path)
        p.add_argument("--out", required=True, type=Path)
        if command == "items":
            p.add_argument("--patch", action="append", type=Path, default=[])
            p.add_argument("--loose-dir", type=Path)
        if command == "extract":
            p.add_argument("--pattern", required=True)
    p = sub.add_parser("audit")
    p.add_argument("installation", type=Path)
    p.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    try:
        if args.command == "audit":
            result = audit_install(args.installation)
        else:
            archive = Archive(args.archive)
            if args.command == "extract":
                result = archive.extract(args.out, args.pattern)
                print(json.dumps({"extracted": result}, ensure_ascii=False))
                return 0
            result = archive.manifest() if args.command == "index" else item_catalog(
                Resources(archive, [Archive(p) for p in args.patch], args.loose_dir))
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(json.dumps(result, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
        print(f"Written {args.out}")
        return int(bool(result.get("errors")))
    except (FormatError, OSError, KeyError, struct.error, UnicodeError) as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
