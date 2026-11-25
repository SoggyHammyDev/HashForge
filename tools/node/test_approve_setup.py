import tempfile
from pathlib import Path
import unittest
from approve_setup import approve

class SetupTests(unittest.TestCase):
    def test_decision_persists_and_cannot_be_replaced(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            approve(root, "fresh")
            self.assertEqual((root / "decision").read_text(), "fresh\n")
            with self.assertRaises(FileExistsError):
                approve(root, "imported")
            self.assertEqual((root / "decision").read_text(), "fresh\n")

    def test_invalid_decision(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            with self.assertRaises(ValueError):
                approve(root, "unknown")
            self.assertFalse((root / "decision").exists())
