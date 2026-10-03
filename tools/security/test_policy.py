"""Adversarial guards for ownership precedence and the privileged trigger exception."""
from copy import deepcopy
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

import yaml

import check_policy as policy


class PolicyTests(unittest.TestCase):
    def setUp(self):
        self.workflow = yaml.load((policy.ROOT / '.github/workflows/review-routing.yml').read_text(encoding='utf-8'), Loader=yaml.BaseLoader)

    def test_current_policy_passes(self):
        self.assertEqual(policy.check(policy.ROOT), [])

    def test_pr_head_or_external_checkout_is_rejected(self):
        for setting, value in [('ref', '${{ github.event.pull_request.head.sha }}'),
                               ('repository', '${{ github.event.pull_request.head.repo.full_name }}'),
                               ('persist-credentials', 'true')]:
            workflow = deepcopy(self.workflow)
            workflow['jobs']['route']['steps'][0]['with'][setting] = value
            self.assertTrue(policy.routing_errors(workflow))

    def test_extra_script_artifact_or_execution_override_is_rejected(self):
        mutations = [lambda steps: steps.insert(2, {'uses': 'actions/download-artifact@' + 'a' * 40}),
                     lambda steps: steps[1].update(run='python untrusted.py'),
                     lambda steps: steps[-1].update(run='bash pull-request.sh'),
                     lambda steps: steps[-1]['env'].update(BASH_ENV='untrusted.sh'),
                     lambda steps: steps[-1].update({'working-directory': 'untrusted'})]
        for mutate in mutations:
            workflow = deepcopy(self.workflow)
            mutate(workflow['jobs']['route']['steps'])
            self.assertTrue(policy.routing_errors(workflow))

    def test_floating_privileged_action_is_rejected(self):
        workflow = deepcopy(self.workflow)
        workflow['jobs']['route']['steps'][1]['uses'] = 'actions/setup-python@v5'
        self.assertTrue(policy.routing_errors(workflow))

    def test_exception_cannot_cover_another_job_or_container(self):
        workflow = deepcopy(self.workflow)
        workflow['jobs']['extra'] = {'runs-on': 'ubuntu-latest', 'steps': [{'run': 'bash untrusted.sh'}]}
        self.assertTrue(policy.routing_errors(workflow))
        workflow = deepcopy(self.workflow)
        workflow['jobs']['route']['container'] = 'untrusted/image:latest'
        self.assertTrue(policy.routing_errors(workflow))

    def test_real_auditor_rejects_an_unpinned_action(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'unpinned.yml'
            path.write_text('''name: Negative security probe
on: push
permissions:
  contents: read
jobs:
  probe:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          persist-credentials: false
''', encoding='utf-8')
            result = subprocess.run(['zizmor', '--offline', '--persona', 'pedantic', '--format', 'json', str(path)],
                                    capture_output=True, text=True, encoding='utf-8', timeout=30)
            self.assertNotEqual(result.returncode, 0, 'A finding must fail the audit, not merely upload a green report')
            self.assertIn('unpinned-uses', {finding['ident'] for finding in json.loads(result.stdout)})

    def test_owner_drift_is_rejected(self):
        config = json.loads((policy.ROOT / '.github/collaboration.json').read_text(encoding='utf-8'))
        config['owners']['rules'] = 'replacement-owner'
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / '.github/workflows').mkdir(parents=True)
            (root / '.github/collaboration.json').write_text(json.dumps(config), encoding='utf-8')
            for name in ('CODEOWNERS', 'workflows/review-routing.yml'):
                (root / '.github' / name).write_text((policy.ROOT / '.github' / name).read_text(encoding='utf-8'), encoding='utf-8')
            self.assertIn('CODEOWNERS drifted; regenerate from collaboration.json', policy.check(root))

    def test_shared_overrides_ui_and_ui_overrides_rules(self):
        config = {'owners': {'ui': 'ui-lead', 'rules': 'rules-lead'},
                  'rules': ['src/'], 'ui': ['src/client/'], 'shared': ['src/client/Contract.cs'], 'handoffs': 'docs/handoffs/tasks/'}
        lines = policy.codeowners(config).splitlines()
        self.assertLess(lines.index('/src/ @rules-lead'), lines.index('/src/client/ @ui-lead'))
        self.assertEqual(lines[-1], '/src/client/Contract.cs @ui-lead @rules-lead')
        self.assertIn('/docs/handoffs/tasks/', lines)

    def test_unsafe_pattern_is_rejected(self):
        config = {'owners': {'ui': 'ui', 'rules': 'rules'}, 'ui': [], 'rules': ['../escape'], 'shared': [], 'handoffs': 'docs/handoffs/tasks/'}
        with self.assertRaises(ValueError):
            policy.codeowners(config)


if __name__ == '__main__':
    unittest.main()
