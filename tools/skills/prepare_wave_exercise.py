#!/usr/bin/env python3
"""Prepare an offline, synthetic two-card skill exercise without changing playable content."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]


def prepare(output: Path, root: Path = ROOT) -> Path:
    root = root.resolve()
    output = output.resolve()
    if not output.is_relative_to(root / 'artifacts'):
        raise ValueError('Exercise output must be inside this repository\'s ignored artifacts directory')
    if output.exists():
        raise ValueError('Output already exists; preserve it and choose another exercise directory')
    output.mkdir(parents=True)
    for directory in ('src/L5R.Core', 'tests/L5R.Core.Tests', 'tools/content', 'tools/collaboration',
                      'tools/hooks', 'tools/skills', 'content/published', '.claude/skills', '.agents/skills'):
        shutil.copytree(root / directory, output / directory,
                        ignore=shutil.ignore_patterns('bin', 'obj', '__pycache__'))
    files = ('AGENTS.md', 'CLAUDE.md', 'global.json', 'Directory.Build.props', 'tests/TestRunner.cs',
             'src/L5R.Client/BattleJournal.cs', 'tools/collab.py', 'tools/lint_comment_swallow.py',
             'tools/check.sh', 'tools/env.sh', 'content/deck-pool.json', 'content/oracle/emperor-catalogue.json',
             'docs/rules/RULINGS.md', 'docs/PITFALLS.md', 'docs/REPO_SKILLS.md',
             'docs/decisions/2026-09-28-engine-hook-registry.md', 'docs/decisions/2026-09-28-wave-registration.md',
             'docs/requests/created-card-scans-needed.md')
    for name in files:
        destination = output / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(root / name, destination)
    (output / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>\n', encoding='utf-8')
    (output / '.gitignore').write_text('artifacts/\n**/bin/\n**/obj/\n**/__pycache__/\n', encoding='utf-8')
    catalogue = []
    cards = ((900001, 'Fixture Quiet Samurai', '', (10, 60, 80)),
             (900002, 'Fixture Generous Samurai', 'Open: Gain 1 Honor.', (80, 60, 10)))
    existing = {card['Id'] for path in (root / 'content/published').glob('*.json')
                if not path.name.endswith('-sources.json')
                for card in json.loads(path.read_text(encoding='utf-8')).get('Cards', [])}
    for number, name, text, color in cards:
        card_id = f'oracle-l5r-{number}'
        if card_id in existing:
            raise ValueError(f'Fixture ID overlaps published content: {card_id}; revise the exercise IDs')
        image_id = f'fixture-{number}'
        relative = f'assets/reference/oracle/skill-acceptance/images/{image_id}.jpg'
        path = output / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        Image.new('RGB', (750, 1050), color).save(path, 'JPEG', quality=95)
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        catalogue.append({'id': card_id, 'image_ids': [image_id],
                          'source_url': f'https://fixture.invalid/cards/{number}',
                          'source_record': {'title': [name], 'type': ['Personality'], 'clan': ['Lion'],
                                            'cost': ['4'], 'force': ['3'], 'chi': ['3'], 'ph': ['2'],
                                            'keywords': ['Samurai'], 'text': [text] if text else []},
                          'selected_printings': [{'set': ['Skill Acceptance Fixture'], 'printimagehash': [image_id]}]})
        receipt = {'source_image_hash': image_id, 'path': relative, 'sha256': digest,
                   'source_url': f'https://fixture.invalid/images/{image_id}.jpg', 'width': 750, 'height': 1050}
        destination = output / f'assets/reference/oracle/skill-acceptance/receipts/{image_id}.json'
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    directory = output / 'content/oracle/skill-acceptance'
    directory.mkdir(parents=True)
    (directory / 'catalogue.json').write_text(json.dumps({'cards': catalogue}, indent=2) + '\n', encoding='utf-8')
    (output / 'FIXTURE_INPUTS.md').write_text(
        'Synthetic reviewed inputs for the repository-skill acceptance exercise only; not historical L5R cards.\n\n'
        'Printing: Skill Acceptance Fixture; source slug: skill-acceptance.\n\n'
        '- oracle-l5r-900001, Fixture Quiet Samurai: Personality, Lion, 4 Gold, 3F/3C, 2 Personal Honor, Samurai; no text.\n'
        '- oracle-l5r-900002, Fixture Generous Samurai: same stats; Open: Gain 1 Honor.\n\n'
        'Solid-color JPEG scans and matching records/receipts are fixture evidence only. No network source exists; do not download or publish them.\n',
        encoding='utf-8')
    subprocess.run(['git', 'init', '-b', 'main'], cwd=output, check=True, capture_output=True)
    subprocess.run(['git', 'add', '.'], cwd=output, check=True, capture_output=True)
    subprocess.run(['git', '-c', 'user.name=Skill Fixture', '-c', 'user.email=fixture@example.invalid',
                    'commit', '-m', 'Acceptance input baseline'], cwd=output, check=True, capture_output=True)
    return output


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/skills-wave-exercise')
    args = parser.parse_args()
    try:
        print(prepare(args.output))
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        parser.error(str(error))


if __name__ == '__main__':
    main()
