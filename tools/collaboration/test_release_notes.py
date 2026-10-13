"""Release notes from handoff records: selection by time, grouping by lane, and what is left out."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('release_notes', Path(__file__).with_name('release_notes.py'))
notes = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = notes
spec.loader.exec_module(notes)


def record(summary, lane='rules', issue=None, remaining='None; ready for review.', updated='2026-10-05T12:00:00+00:00'):
    return {'lane': lane, 'summary': summary, 'validation': 'checks pass', 'remaining': remaining, 'issue': issue, 'branch': 'claude/1-x', 'updated': updated}


class SelectionTests(unittest.TestCase):
    def test_range_is_inclusive_and_sorted_by_issue(self):
        records = [record('late', updated='2026-10-09T00:00:00+00:00', issue='https://github.com/o/r/issues/9'),
                   record('inside', updated='2026-10-05T00:00:00+00:00', issue='https://github.com/o/r/issues/2'),
                   record('early', updated='2026-09-01T00:00:00+00:00')]
        picked = notes.select(records, notes.parse_time('2026-10-01'), notes.parse_time('2026-10-08'))
        self.assertEqual([r['summary'] for r in picked], ['inside'])
        everything = notes.select(records, notes.parse_time('2026-10-01'))
        self.assertEqual([r['summary'] for r in everything], ['inside', 'late'])

    def test_naive_dates_are_utc_and_z_is_accepted(self):
        self.assertEqual(notes.parse_time('2026-10-01').utcoffset().total_seconds(), 0)
        self.assertEqual(notes.parse_time('2026-10-01T05:00:00Z'), notes.parse_time('2026-10-01T05:00:00+00:00'))

    def test_unreadable_or_incomplete_records_are_ignored(self):
        with tempfile.TemporaryDirectory() as folder:
            (Path(folder) / 'good.json').write_text(json.dumps(record('good')))
            (Path(folder) / 'bad.json').write_text('{not json')
            (Path(folder) / 'empty.json').write_text(json.dumps({'lane': 'ui'}))
            self.assertEqual([r['summary'] for r in notes.load(Path(folder))], ['good'])


class RenderTests(unittest.TestCase):
    def test_groups_by_lane_links_issues_and_keeps_open_work(self):
        text = notes.render([record('Faster battles', 'rules', 'https://github.com/o/r/issues/894', remaining='Rules lead to confirm one reading.'),
                             record('New table art', 'ui'), record('Docs only', 'weird-lane')], '2026-10-01')
        self.assertIn('3 tasks with a handoff record.', text)
        self.assertLess(text.index('## Rules and engine'), text.index('## Interface and presentation'))
        self.assertIn('- Faster battles ([#894](https://github.com/o/r/issues/894))', text)
        self.assertIn('  - Still open: Rules lead to confirm one reading.', text)
        self.assertIn('## Shared', text)
        self.assertIn('- Docs only', text, 'an unknown lane falls into Shared')

    def test_finished_work_has_no_still_open_line(self):
        for done in ('None; ready for review.', 'nothing left', 'N/A', ''):
            self.assertNotIn('Still open', notes.render([record('x', remaining=done)], '2026-10-01'))

    def test_empty_range_says_so_without_inventing_content(self):
        text = notes.render([], '2026-10-01')
        self.assertIn('0 tasks with a handoff record.', text)
        self.assertNotIn('##', text)


class GitRefTests(unittest.TestCase):
    def test_a_ref_resolves_to_its_commit_date(self):
        with tempfile.TemporaryDirectory() as folder:
            def git(*args, env=None):
                subprocess.run(['git', '-C', folder, *args], check=True, capture_output=True, env=env)
            git('init', '-q'); git('config', 'user.email', 't@example.com'); git('config', 'user.name', 'T')
            (Path(folder) / 'a.txt').write_text('a'); git('add', 'a.txt')
            import os
            env = {**os.environ, 'GIT_COMMITTER_DATE': '2026-09-15T08:00:00+00:00', 'GIT_AUTHOR_DATE': '2026-09-15T08:00:00+00:00'}
            git('commit', '-q', '-m', 'first', env=env); git('tag', 'preview-1')
            self.assertEqual(notes.resolve_time('preview-1', Path(folder)), notes.parse_time('2026-09-15T08:00:00+00:00'))
            with self.assertRaises(SystemExit):
                notes.resolve_time('no-such-ref', Path(folder))


if __name__ == '__main__':
    unittest.main()
