"""Keep advisory CODEOWNERS aligned and constrain the privileged routing exception."""
import argparse
import json
from pathlib import Path
import re

import yaml

ROOT = Path(__file__).resolve().parents[2]


def codeowners(config):
    owners = config['owners']
    shared = ' '.join('@' + owners[lane] for lane in ('ui', 'rules'))
    lines = ['# Generated from collaboration.json: python tools/security/check_policy.py --write-codeowners',
             '# Advisory reviewers only. Standing autonomous authority and AGENTS.md still apply.',
             '# Last matching pattern wins; shared exceptions must remain last.', '* ' + shared,
             '# Per-task handoffs do not add another lane reviewer.', '/' + config['handoffs']]
    # classify() gives shared first priority, then UI, then rules.
    for lane in ('rules', 'ui', 'shared'):
        for path in config[lane]:
            if not re.fullmatch(r'[A-Za-z0-9_./-]+', path) or '..' in path.split('/'):
                raise ValueError('Ownership paths must be literal repository paths')
            lines.append('/' + path + ' ' + (shared if lane == 'shared' else '@' + owners[lane]))
    return '\n'.join(lines) + '\n'


def routing_errors(workflow):
    job = workflow['jobs']['route']
    steps = job['steps']
    errors = []
    if set(workflow['jobs']) != {'route'}:
        errors.append('The privileged trigger exception covers only the metadata routing job')
    if job.get('runs-on') != 'ubuntu-latest' or any(key in job for key in ('container', 'services')):
        errors.append('Privileged routing must use the hosted Ubuntu runner without containers/services')
    if len(steps) != 3:
        errors.append('Privileged routing must have only checkout, Python setup and the metadata router')
        return errors
    checkout = steps[0]
    settings = checkout.get('with', {})
    if settings.get('ref') != '${{ github.event.pull_request.base.sha }}' or settings.get('persist-credentials') != 'false':
        errors.append('Privileged routing must checkout the exact trusted base without persisted credentials')
    if not re.fullmatch(r'actions/checkout@[0-9a-f]{40}', checkout.get('uses', '')):
        errors.append('Privileged routing checkout must be hash pinned')
    if set(checkout) - {'uses', 'with'} or set(settings) != {'ref', 'persist-credentials'}:
        errors.append('Privileged checkout must not override repository, execution or environment')
    setup = steps[1]
    if set(setup) != {'uses', 'with'} or not re.fullmatch(r'actions/setup-python@[0-9a-f]{40}', setup.get('uses', '')):
        errors.append('Privileged routing middle step must only install pinned Python')
    if setup.get('with') != {'python-version': '3.12'}:
        errors.append('Privileged Python setup must not override execution or environment')
    if steps[-1].get('run') != 'python tools/collab.py route-pr --number "$PR_NUMBER"':
        errors.append('Privileged routing must execute only the trusted metadata router')
    if steps[-1].get('env', {}).get('PR_NUMBER') != '${{ github.event.pull_request.number }}':
        errors.append('Routing must receive a numeric PR identifier through the environment')
    if set(steps[-1]) != {'name', 'env', 'run'} or steps[-1].get('env') != {'GH_TOKEN': '${{ github.token }}', 'PR_NUMBER': '${{ github.event.pull_request.number }}'}:
        errors.append('Privileged router must not acquire extra execution settings or environment')
    if any(key in workflow for key in ('env', 'defaults')) or 'env' in job or 'defaults' in job:
        errors.append('Privileged routing must not override execution environment or working directory')
    if any('uses' in step and not re.fullmatch(r'actions/(checkout|setup-python)@[0-9a-f]{40}', step['uses']) for step in steps):
        errors.append('Privileged routing may use only pinned checkout and Python setup actions')
    return errors


def check(root):
    config = json.loads((root / '.github/collaboration.json').read_text(encoding='utf-8'))
    errors = []
    if (root / '.github/CODEOWNERS').read_text(encoding='utf-8') != codeowners(config):
        errors.append('CODEOWNERS drifted; regenerate from collaboration.json')
    # BaseLoader retains YAML strings (including on and false) without YAML 1.1 coercion.
    routing = yaml.load((root / '.github/workflows/review-routing.yml').read_text(encoding='utf-8'), Loader=yaml.BaseLoader)
    errors.extend(routing_errors(routing))
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write-codeowners', action='store_true')
    args = parser.parse_args()
    if args.write_codeowners:
        config = json.loads((ROOT / '.github/collaboration.json').read_text(encoding='utf-8'))
        (ROOT / '.github/CODEOWNERS').write_text(codeowners(config), encoding='utf-8')
    errors = check(ROOT)
    for error in errors:
        print(error)
    if not errors:
        print('Ownership map and trusted routing policy passed.')
    return int(bool(errors))


if __name__ == '__main__':
    raise SystemExit(main())
