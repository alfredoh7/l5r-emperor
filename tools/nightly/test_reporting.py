"""Regression checks for issue creation, reuse and explicit reporting failures."""
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('report_failure', Path(__file__).with_name('report_failure.py'))
reporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reporter)
REPO = 'owner/repo'
SHA = 'a' * 40
URL = 'https://github.com/owner/repo/actions/runs/1'


class FakeApi:
    def __init__(self):
        self.labels = []
        self.issues = []
        self.writes = []

    def __call__(self, endpoint, method='GET', data=None):
        if method != 'GET':
            self.writes.append((endpoint, method, data))
        if '/labels?' in endpoint:
            return self.labels
        if '/issues?' in endpoint:
            return self.issues
        if endpoint.endswith('/labels'):
            self.labels.append(data)
            return data
        if method == 'POST':
            created = dict(data, number=len(self.issues) + 1, html_url=f'https://github.com/{REPO}/issues/{len(self.issues) + 1}')
            self.issues.append(created)
            return created
        self.issues[int(endpoint.rsplit('/', 1)[1]) - 1].update(data)
        return self.issues[0]


class ReportingTests(unittest.TestCase):
    def setUp(self):
        self.api = FakeApi()
        self.patcher = patch.object(reporter, 'api', self.api)
        self.patcher.start()
        self.addCleanup(self.patcher.stop)

    def test_missing_label_and_failure_create_one_marked_issue(self):
        url = reporter.report(REPO, SHA, URL, 'Actual failure evidence')
        self.assertEqual(url, f'https://github.com/{REPO}/issues/1')
        self.assertEqual(len(self.api.labels), 1)
        self.assertIn(reporter.MARKER, self.api.issues[0]['body'])
        self.assertIn('Actual failure evidence', self.api.issues[0]['body'])

    def test_repeated_identical_failure_is_idempotent(self):
        reporter.report(REPO, SHA, URL, 'failure')
        writes = len(self.api.writes)
        reporter.report(REPO, SHA, URL, 'failure')
        self.assertEqual(len(self.api.writes), writes)

    def test_later_runs_update_one_issue_and_keep_first_evidence(self):
        reporter.report(REPO, SHA, URL, 'first')
        for number in (2, 3):
            reporter.report(REPO, SHA, URL[:-1] + str(number), 'later')
        self.assertEqual(len(self.api.issues), 1)
        self.assertIn('First recorded evidence: ' + URL, self.api.issues[0]['body'])
        writes = len(self.api.writes)
        reporter.report(REPO, SHA, URL[:-1] + '3', 'later')
        self.assertEqual(len(self.api.writes), writes)

    def test_acceptance_issue_never_replaces_production_failure(self):
        reporter.report(REPO, SHA, URL, 'production')
        reporter.report(REPO, SHA, URL, 'injected', acceptance=True)
        self.assertEqual(len(self.api.issues), 2)
        self.assertIn(reporter.MARKER, self.api.issues[0]['body'])
        self.assertIn(reporter.TEST_MARKER, self.api.issues[1]['body'])
        self.assertIn('not a defect on main', self.api.issues[1]['body'])

    def test_unrelated_labelled_issue_is_not_overwritten(self):
        self.api.issues.append({'number': 1, 'body': 'Manually reported failure', 'html_url': 'other'})
        reporter.report(REPO, SHA, URL, 'nightly failure')
        self.assertEqual(self.api.issues[0]['body'], 'Manually reported failure')
        self.assertEqual(len(self.api.issues), 2)

    def test_invalid_repository_or_evidence_is_rejected_before_api_writes(self):
        for repository, sha, url in [('invalid', SHA, URL), (REPO, 'not-a-sha', URL), (REPO, SHA, 'https://other.example/run')]:
            with self.assertRaises(ValueError):
                reporter.report(repository, sha, url, 'failure')
        self.assertEqual(self.api.writes, [])

    def test_api_failure_is_not_mistaken_for_an_empty_issue_list(self):
        with patch.object(reporter, 'api', side_effect=OSError('offline')):
            with self.assertRaises(OSError):
                reporter.report(REPO, SHA, URL, 'failure')

    def test_all_issue_pages_are_read(self):
        with patch.object(reporter, 'api', side_effect=[[{'number': n} for n in range(100)], [{'number': 100}]]) as api:
            self.assertEqual(len(list(reporter.pages('issues?state=open'))), 101)
            self.assertIn('page=2', api.call_args.args[0])


if __name__ == '__main__':
    unittest.main()
