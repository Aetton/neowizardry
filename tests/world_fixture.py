"""Authored floor/wall fixture for the real Godot controller smoke test."""
import hashlib
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from test_oct import fixture as oct_fixture
from test_pvl import fixture as pvl_fixture
from tools import oct, pvl

root = Path(sys.argv[1])
root.mkdir(parents=True, exist_ok=True)
source = pvl_fixture()
source_hash = hashlib.sha256(source).hexdigest()
(root / 'fixture.glb').write_bytes(pvl.glb(pvl.parse(source), source_hash=source_hash))
(root / 'fixture.nwcol').write_bytes(oct.encode(oct.parse(oct_fixture()), source_hash))
