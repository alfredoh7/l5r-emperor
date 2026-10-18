"""Legacy board commands must fail locally after retirement."""
import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('board', Path(__file__).parents[1] / 'board.py')
board = importlib.util.module_from_spec(spec)
spec.loader.exec_module(board)


class RetiredBoardTests(unittest.TestCase):
    def test_old_configuration_never_sends_credentials_or_requests(self):
        env = {'L5R_BOARD_TOKEN': 'retired-secret', 'L5R_BOARD_URL': 'https://untrusted.invalid'}
        with patch.dict(os.environ, env), patch('urllib.request.build_opener') as opener, \
                patch('urllib.request.urlopen') as urlopen:
            for action in (lambda: board.request('board'),
                           lambda: board.task_for_issue(62),
                           lambda: board.mutate(62, 'claim', {'executor': 'old-agent'})):
                with self.assertRaises(board.BoardError) as caught:
                    action()
                self.assertIn('retired', str(caught.exception))
                self.assertNotIn('retired-secret', str(caught.exception))
            opener.assert_not_called()
            urlopen.assert_not_called()

    def test_old_imported_context_is_a_notice_not_live_or_empty_claims(self):
        result = board.context()
        self.assertTrue(result['retired'])
        self.assertEqual(result['repository'], 'https://github.com/l5r-emperor-dev/l5r-emperor')
        self.assertNotIn('tasks', result)
        self.assertNotIn('user', result)

    def test_every_legacy_command_fails_without_echoing_private_arguments(self):
        for command in ('context', 'claim', 'resume', 'handoff', 'evidence', 'link-pr', 'unknown'):
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                status = board.main([command, '--summary', 'private-note'])
            self.assertNotEqual(status, 0)
            result = json.loads(output.getvalue())
            self.assertTrue(result['retired'])
            self.assertIn('GitHub', result['error'])
            self.assertNotIn('private-note', output.getvalue())

    def test_real_cli_cannot_report_a_successful_claim(self):
        env = dict(os.environ, L5R_BOARD_TOKEN='retired-secret',
                   L5R_BOARD_URL='https://untrusted.invalid')
        result = subprocess.run([sys.executable, str(Path(board.__file__)), 'claim',
                                 '--issue', '62', '--executor', 'old-agent', '--branch', 'old-branch'],
                                env=env, text=True, capture_output=True, timeout=10)
        self.assertNotEqual(result.returncode, 0)
        self.assertTrue(json.loads(result.stdout)['retired'])
        self.assertNotIn('retired-secret', result.stdout + result.stderr)
        self.assertNotIn('untrusted.invalid', result.stdout + result.stderr)


if __name__ == '__main__':
    unittest.main()
