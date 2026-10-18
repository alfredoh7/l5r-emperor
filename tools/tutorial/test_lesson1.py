import re
import subprocess
import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_lesson1 as build  # noqa: E402
import lesson1_source as source  # noqa: E402

BRITISH = re.compile(r"\b(honour\w*|armour\w*|favour\w*|manoeuv\w*|defence\w*|colour\w*|centre\w*|Scopion|Chis)\b", re.IGNORECASE)


class LessonOneSourceTests(unittest.TestCase):
    def test_generated_files_are_current(self):
        result = subprocess.run([sys.executable, str(HERE / "build_lesson1.py"), "--check"], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_every_page_fits_the_note_column(self):
        for number, step, beats in build.expand():
            for _, _, pages in beats:
                for page in pages:
                    self.assertLessEqual(len(page), source.SIDEBAR_PAGE_LIMIT, f"step {number} {step.id}")

    def test_step_ids_and_move_keys_are_unique(self):
        ids = [s.id for s in source.STEPS]
        keys = [b.key for s in source.STEPS for b in s.beats if isinstance(b, source.Move)]
        self.assertEqual(len(ids), len(set(ids)))
        self.assertEqual(len(keys), len(set(keys)))

    def test_spelling_is_american_and_phases_use_the_phase_bar_names(self):
        for step in source.STEPS:
            for beat in step.beats:
                text = step.title + " " + beat.text + " " + getattr(beat, "instruction", "")
                self.assertIsNone(BRITISH.search(text), f"{step.id}: {BRITISH.search(text)}")
                self.assertNotRegex(text, r"\b(Actions phase|Battle phase|Combat phase)\b", step.id)


if __name__ == "__main__":
    unittest.main()
