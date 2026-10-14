from pathlib import Path
import tempfile
import unittest
from tools.slf import Archive, FormatError, Resources
from tests.test_slf import fixture


class OverlayTest(unittest.TestCase):
    def setUp(self):
        t = tempfile.TemporaryDirectory(); self.addCleanup(t.cleanup)
        self.root = Path(t.name)
        self.base = self.root/"DATA.SLF"
        fixture(self.base, [("DATABASES/ITEMS.DBS", b"base", 0)])

    def patch(self, name, data, state=0):
        p = self.root/name
        fixture(p, [("DATABASES/ITEMS.DBS", data, state)])
        return Archive(p)

    def test_precedence_numeric_and_case(self):
        old = self.patch("PATCH.002", b"old")
        new = self.patch("patch.010", b"new")
        v = Resources(Archive(self.base), [new, old])
        data, source = v.resolve("databases/items.dbs")
        self.assertEqual(data, b"new")
        self.assertEqual(source["file"], "patch.010")
        loose = self.root/"Data"; (loose/"Databases").mkdir(parents=True)
        (loose/"Databases/Items.Dbs").write_bytes(b"loose")
        v = Resources(Archive(self.base), [old, new], loose)
        self.assertEqual(v.resolve("DATABASES/ITEMS.DBS")[0], b"loose")
        self.assertEqual(v.resolve("DATABASES/ITEMS.DBS")[1]["kind"], "loose")

    def test_disabled_patch_and_duplicate_number(self):
        disabled = self.patch("PATCH.001.BAK", b"bad")
        with self.assertRaises(FormatError): Resources(Archive(self.base), [disabled])
        a = self.patch("PATCH.001", b"a")
        b = self.patch("patch.001", b"b")
        with self.assertRaises(FormatError): Resources(Archive(self.base), [a,b])

    def test_inactive_entry_falls_through(self):
        inactive = self.patch("PATCH.001", b"inactive", 1)
        self.assertEqual(Resources(Archive(self.base), [inactive]).read("DATABASES/ITEMS.DBS"), b"base")

    def test_loose_ambiguity_and_symlink(self):
        loose = self.root/"Data"; loose.mkdir()
        (loose/"a").write_text("a"); (loose/"A").write_text("A")
        if (loose/"a").samefile(loose/"A"):
            # Case-insensitive filesystems overwrite one file rather than creating an ambiguity.
            self.assertEqual(Resources(Archive(self.base), loose=loose).read("a"), b"A")
        else:
            with self.assertRaises(FormatError): Resources(Archive(self.base), loose=loose)
        (loose/"A").unlink()
        (loose/"link").symlink_to(self.base)
        with self.assertRaises(FormatError): Resources(Archive(self.base), loose=loose)
