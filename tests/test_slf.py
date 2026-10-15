import json
from pathlib import Path
import struct
import tempfile
import unittest
from tools.slf import Archive, FormatError, audit_install, item_catalog


def fixture(path, entries):
    header = bytearray(532)
    header[:8] = b"TEST.SLF"
    header[256:261] = b"Data\\"
    struct.pack_into("<II", header, 512, len(entries), len(entries))
    body = bytearray()
    index = bytearray()
    for name, data, state in entries:
        row = bytearray(280)
        encoded = name.encode("latin1")
        row[:len(encoded)] = encoded
        struct.pack_into("<II", row, 256, 532+len(body), len(data))
        row[264] = state
        body.extend(data)
        index.extend(row)
    path.write_bytes(header + body + index)


class SlfTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.file = self.root / "test.slf"

    def test_roundtrip_and_case(self):
        fixture(self.file, [("DIR\\test.txt", b"hello", 0)])
        a = Archive(self.file)
        self.assertEqual(a.read("dir/TEST.txt"), b"hello")
        output = self.root/"extract"
        result = a.extract(output, "DIR/*.TXT")
        self.assertEqual((output/"DIR/test.txt").read_bytes(), b"hello")
        self.assertEqual(result[0]["size"], 5)
        with self.assertRaises(FormatError): a.extract(output, "*")

    def test_path_escape(self):
        for name in ("../bad", "C:\\bad", "/bad", "a//bad", "a/./bad"):
            fixture(self.file, [(name, b"x", 0)])
            with self.assertRaises(FormatError): Archive(self.file)

    def test_truncation_and_bad_offset(self):
        self.file.write_bytes(b"x")
        with self.assertRaises(FormatError): Archive(self.file)
        fixture(self.file, [("one", b"x", 0)])
        raw = bytearray(self.file.read_bytes())
        struct.pack_into("<I", raw, len(raw)-280+256, 0)
        self.file.write_bytes(raw)
        with self.assertRaises(FormatError): Archive(self.file)

    def test_overlap_and_duplicate(self):
        fixture(self.file, [("one", b"xx", 0), ("two", b"xx", 0)])
        raw = bytearray(self.file.read_bytes())
        struct.pack_into("<I", raw, len(raw)-280+256, 533)
        self.file.write_bytes(raw)
        with self.assertRaises(FormatError): Archive(self.file)
        fixture(self.file, [("ONE", b"x", 0), ("one", b"x", 0)])
        with self.assertRaises(FormatError): Archive(self.file)

    def test_deleted_entry_is_not_read(self):
        fixture(self.file, [("one", b"x", 1)])
        with self.assertRaises(KeyError): Archive(self.file).read("one")

    def test_symlink_escape(self):
        fixture(self.file, [("dir/one", b"x", 0)])
        output = self.root/"out"
        output.mkdir()
        (output/"dir").symlink_to(self.root, target_is_directory=True)
        with self.assertRaises(FormatError): Archive(self.file).extract(output, "*")

    def test_items_shape_and_fields(self):
        data = bytearray(4+269)
        struct.pack_into("<I", data, 0, 1)
        data[4:16] = "Dagger".encode("utf-16le")
        struct.pack_into("<H", data, 4+0x78, 4)
        struct.pack_into("<b", data, 4+0x48, -6)
        struct.pack_into("<HBB", data, 4+0x4A, 5, 2, 8)
        struct.pack_into("<H", data, 4+0x8A, 45)
        fixture(self.file, [("DATABASES/ITEMS.DBS", data, 0)])
        item = item_catalog(Archive(self.file))["items"][0]
        self.assertEqual((item["name"], item["race_mask"]), ("Dagger", 4))
        self.assertEqual((item["initiative"],item["damage_constant"],item["damage_dice"],item["damage_sides"],item["weight_tenths_lb"]),(-6,5,2,8,45))
        fixture(self.file, [("DATABASES/ITEMS.DBS", data[:-1], 0)])
        with self.assertRaises(FormatError): item_catalog(Archive(self.file))

    def test_native_instance_fields_keep_signed_base_and_unknown_mode(self):
        import hashlib
        data=bytearray(4+269);struct.pack_into('<I',data,0,1)
        data[4+0x41]=0x81;data[4+0x66]=254
        struct.pack_into('<hBBB',data,4+0x67,-2,3,6,25)
        fixture(self.file,[("DATABASES/ITEMS.DBS",data,0)])
        catalog=item_catalog(Archive(self.file));item=catalog['items'][0];fields=item['instance']
        self.assertEqual(catalog['schema'],3)
        self.assertEqual((fields['mode'],fields['amount_constant'],fields['amount_dice'],fields['amount_sides'],fields['max_stack'],fields['flags_41']),(254,-2,3,6,25,0x81))
        self.assertEqual(bytes.fromhex(fields['raw_record_hex']),data[4:])
        self.assertEqual(item['record_sha256'],hashlib.sha256(data[4:]).hexdigest())

    def test_bonus_swings_preserves_full_signed_dword(self):
        for amount in (0, 1, 2, 256, -257, 2147483647, -2147483648):
            data = bytearray(4+269)
            struct.pack_into('<I', data, 0, 1)
            struct.pack_into('<i', data, 4+0xAD, amount)
            fixture(self.file, [("DATABASES/ITEMS.DBS", data, 0)])
            self.assertEqual(item_catalog(Archive(self.file))["items"][0]["bonus_swings"], amount)

    def test_audit_reports_failures(self):
        fixture(self.file, [("one", b"x", 0)])
        (self.root/"broken.slf").write_bytes(b"bad")
        result = audit_install(self.root)
        self.assertEqual(len(result["archives"]), 1)
        self.assertEqual(len(result["errors"]), 1)
        json.dumps(result)


if __name__ == "__main__": unittest.main()
