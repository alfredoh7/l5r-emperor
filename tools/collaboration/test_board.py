import importlib.util
import json
import os
from pathlib import Path
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('board', Path(__file__).parents[1] / 'board.py')
board = importlib.util.module_from_spec(spec)
spec.loader.exec_module(board)


class BoardClientTests(unittest.TestCase):
    def test_requires_token_and_https(self):
        for env in ({}, {'L5R_BOARD_TOKEN': 'test', 'L5R_BOARD_URL': 'http://evil.test'},
                    {'L5R_BOARD_TOKEN': 'test', 'L5R_BOARD_URL': 'https://user:secret@example.test'}):
            with patch.dict(os.environ, env, clear=True):
                with self.assertRaises(board.BoardError):
                    board.request('board')

    def test_refuses_redirect_without_forwarding_credentials(self):
        with self.assertRaises(board.BoardError):
            board.NoRedirect().redirect_request(None, None, 302, '', {}, 'https://other.test')

    def test_mutation_uses_current_version(self):
        with patch.object(board, 'task_for_issue', return_value={'id': 'abc', 'version': 8}), patch.object(board, 'request') as send:
            board.mutate(10, 'claim', {'executor': 'codex', 'branch': 'codex/10'})
            send.assert_called_once_with('tasks/abc', {'version': 8, 'action': 'claim', 'data': {'executor': 'codex', 'branch': 'codex/10'}})

    def test_unknown_issue_does_not_create_or_claim(self):
        with patch.object(board, 'request', return_value={'tasks': []}):
            with self.assertRaises(board.BoardError):
                board.task_for_issue(99)

    def test_compact_context_preserves_claims_and_excludes_archived(self):
        tasks = [{'id': 'a', 'number': 1, 'stage': 'In progress', 'claim': {'executor': 'other'}},
                 {'id': 'b', 'stage': 'Done'}, {'id': 'c', 'stage': 'Ready', 'archived': 'duplicate'}]
        with patch.object(board, 'request', return_value={'user': {'login': 'mdornbrook'}, 'tasks': tasks}):
            result = board.context()
            self.assertEqual(result['user'], 'mdornbrook')
            self.assertEqual(len(result['tasks']), 1)
            self.assertEqual(result['tasks'][0]['claim'], {'executor': 'other'})

    def test_network_failure_does_not_expose_token(self):
        import urllib.error
        class Opener:
            def open(self, *args, **kwargs):
                raise urllib.error.URLError('sensitive upstream error')
        with patch.dict(os.environ, {'L5R_BOARD_TOKEN': 'do-not-print'}, clear=True), patch('urllib.request.build_opener', return_value=Opener()):
            with self.assertRaises(board.BoardError) as raised:
                board.request('board')
            self.assertNotIn('do-not-print', str(raised.exception))
            self.assertNotIn('sensitive', str(raised.exception))


if __name__ == '__main__':
    unittest.main()
