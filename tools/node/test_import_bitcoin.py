import tempfile
from pathlib import Path
import unittest
from import_bitcoin import import_snapshot

class ImportTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.src = self.root / "source"
        for file in ("blocks/blk00000.dat", "blocks/index/CURRENT", "chainstate/CURRENT"):
            p = self.src / file
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(b"fixture")
        (self.src / "wallet.dat").write_bytes(b"private")
        self.dest = self.root / "destination"

    def test_copies_only_blockchain(self):
        import_snapshot(self.src, self.dest)
        self.assertEqual((self.dest / "blocks/blk00000.dat").read_bytes(), b"fixture")
        self.assertFalse((self.dest / "wallet.dat").exists())
        self.assertEqual((self.src / "wallet.dat").read_bytes(), b"private")
        self.assertEqual((self.src / "blocks/blk00000.dat").read_bytes(), b"fixture")

    def test_progress(self):
        events = []
        import_snapshot(self.src, self.dest, progress=events.append)
        self.assertEqual(events[0]["phase"], "copying")
        self.assertEqual(events[0]["bytes_verified"], 0)
        self.assertEqual(events[-1]["phase"], "complete")
        self.assertEqual(events[-1]["percent"], 100)
        counts = [event["bytes_verified"] for event in events]
        self.assertEqual(counts, sorted(counts))

    def test_existing_destination_rejected(self):
        self.dest.mkdir()
        with self.assertRaises(ValueError):
            import_snapshot(self.src, self.dest)

    def test_nested_destination_rejected(self):
        with self.assertRaises(ValueError):
            import_snapshot(self.src, self.src / "new")

    def test_symlink_rejected(self):
        (self.src / "blocks/link").symlink_to(self.src / "wallet.dat")
        with self.assertRaises(ValueError):
            import_snapshot(self.src, self.dest)
        self.assertFalse(self.dest.exists())

if __name__ == "__main__":
    unittest.main()
