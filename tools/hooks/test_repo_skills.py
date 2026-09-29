"""Keep skill discovery portable and exercise the canonical-to-Codex mirror."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('sync_skills', ROOT / 'tools/skills/sync_skills.py')
skills = importlib.util.module_from_spec(spec)
spec.loader.exec_module(skills)


class RepoSkillTests(unittest.TestCase):
    def fixture(self, root):
        folder = root / '.claude/skills/example'
        folder.mkdir(parents=True)
        (folder / 'SKILL.md').write_text('---\nname: example\ndescription: Exercise fixture instructions.\n---\nRead [input](references/input.md).\n', encoding='utf-8')
        (folder / 'references').mkdir()
        (folder / 'references/input.md').write_text('Fixture input.\n', encoding='utf-8')
        return folder

    def test_checked_in_skills_validate_and_mirrors_match(self):
        self.assertEqual(skills.sync(ROOT, check=True), [])

    def test_check_never_writes_and_sync_is_idempotent(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.fixture(root)
            self.assertEqual(len(skills.sync(root, check=True)), 2)
            self.assertFalse((root / '.agents').exists())
            self.assertEqual(len(skills.sync(root)), 2)
            self.assertEqual(skills.sync(root), [])
            self.assertEqual(skills.sync(root, check=True), [])

    def test_drift_is_detected_and_repaired_from_canonical_file(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            folder = self.fixture(root)
            skills.sync(root)
            canonical = folder / 'references/input.md'
            canonical.write_text('Updated fixture.\n', encoding='utf-8')
            self.assertEqual(skills.sync(root, check=True), ['example/references/input.md'])
            skills.sync(root)
            self.assertEqual((root / '.agents/skills/example/references/input.md').read_bytes(), canonical.read_bytes())

    def test_missing_local_reference_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            folder = self.fixture(root)
            (folder / 'references/input.md').unlink()
            with self.assertRaisesRegex(ValueError, 'Missing/escaping'):
                skills.sync(root)

    def test_mismatched_skill_name_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            folder = self.fixture(root)
            manifest = folder / 'SKILL.md'
            manifest.write_text(manifest.read_text(encoding='utf-8').replace('name: example', 'name: other'), encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'name must match'):
                skills.sync(root)

    def test_unexpected_mirror_file_is_preserved_and_reported(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.fixture(root)
            skills.sync(root)
            extra = root / '.agents/skills/example/local.txt'
            extra.write_text('Keep this work.', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'Unexpected mirror file'):
                skills.sync(root)
            self.assertEqual(extra.read_text(encoding='utf-8'), 'Keep this work.')


if __name__ == '__main__':
    unittest.main()
