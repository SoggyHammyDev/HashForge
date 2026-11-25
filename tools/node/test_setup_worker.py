import json
from pathlib import Path
import tempfile
import unittest
from setup_worker import process

class WorkerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        self.requests, self.status, self.setup, self.parent, self.snapshot = [
            root / name for name in ("requests", "status", "setup", "destination", "snapshot")]
        for p in (self.requests, self.status, self.setup, self.parent, self.snapshot):
            p.mkdir()
        self.dest = self.parent / "bitcoin"

    def run_worker(self):
        return process(self.requests, self.status, self.setup, self.dest, self.snapshot)

    def request(self, mode):
        (self.requests / "request").write_text(mode)

    def test_fresh_one_time(self):
        self.assertFalse(self.run_worker())
        self.request("fresh")
        self.assertTrue(self.run_worker())
        self.assertEqual((self.setup / "decision").read_text(), "fresh\n")
        self.assertTrue(self.dest.is_dir())
        self.assertFalse(self.run_worker())

    def test_import_preserves_source(self):
        for name in ("blocks/blk00000.dat", "blocks/index/CURRENT", "chainstate/CURRENT"):
            p = self.snapshot / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(b"fixture")
        self.request("imported")
        self.assertTrue(self.run_worker())
        self.assertEqual((self.setup / "decision").read_text(), "imported\n")
        self.assertEqual((self.dest / "blocks/blk00000.dat").read_bytes(), b"fixture")
        self.assertEqual((self.snapshot / "blocks/blk00000.dat").read_bytes(), b"fixture")
        self.assertEqual(json.loads((self.status / "state.json").read_text())["phase"], "approved")

    def test_invalid_choice_never_approves(self):
        self.request("../../other-app")
        with self.assertRaises(ValueError):
            self.run_worker()
        self.assertFalse((self.setup / "decision").exists())
        self.assertFalse(self.dest.exists())
        self.assertFalse(self.run_worker())

    def test_existing_destination_preserved(self):
        self.dest.mkdir()
        (self.dest / "sentinel").write_text("keep")
        self.request("fresh")
        with self.assertRaises(ValueError):
            self.run_worker()
        self.assertEqual((self.dest / "sentinel").read_text(), "keep")
        self.assertFalse((self.setup / "decision").exists())
