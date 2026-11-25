import tempfile
from pathlib import Path
import unittest
from preflight import discover, inspect_candidate

class PreflightTests(unittest.TestCase):
    def test_discovery_and_space_estimate(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            candidate = root / "app" / "data" / "bitcoin"
            for name in ("blocks/blk00000.dat", "blocks/index/CURRENT", "chainstate/CURRENT"):
                p = candidate / name
                p.parent.mkdir(parents=True, exist_ok=True)
                p.write_bytes(b"abc")
            (root / "alias").symlink_to(candidate, target_is_directory=True)
            found = list(discover([root]))
            self.assertEqual(len(found), 1)
            self.assertTrue(found[0]["candidate"])
            self.assertEqual(found[0]["bytes"], 9)
            self.assertGreater(found[0]["required_bytes"], 9)
            self.assertIn("unverified", found[0]["compatibility"])

    def test_incomplete_candidate(self):
        with tempfile.TemporaryDirectory() as tmp:
            self.assertFalse(inspect_candidate(Path(tmp))["candidate"])

if __name__ == "__main__":
    unittest.main()
