#!/usr/bin/env bash
# Issue #905: counts of the Meziantou.Analyzer findings that are held at suggestion in src/L5R.Core/Meziantou.globalconfig, per rule, for the engine projects.
# Fix a rule's findings, then delete its dotnet_diagnostic line so the gate enforces it. Usage: bash tools/analyzer-report.sh
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/analyzers
for project in L5R.Core L5R.AI L5R.Server; do
  # Findings only reach the log for a rule that is not hidden, so the report builds with every held rule at suggestion level.
  dotnet build "src/$project" --nologo --no-incremental -v q -p:ErrorLog="$L5R_PROJECT_ROOT/artifacts/analyzers/$project.sarif" > /dev/null
done
python3 - <<'PY'
import collections, glob, json
found = collections.Counter(); where = {}
for path in sorted(glob.glob('artifacts/analyzers/*.sarif')):
    for run in json.load(open(path))['runs']:
        for result in run.get('results', []):
            rule = result['ruleId']
            if not rule.startswith('MA'): continue
            location = result['locations'][0]['resultFile']['uri'].rsplit('/', 1)[-1]
            key = (rule, location, result['locations'][0]['resultFile']['region']['startLine'])
            if key in where: continue
            where[key] = result['message'].get('text', '') if isinstance(result['message'], dict) else result['message']
            found[rule] += 1
total = sum(found.values())
print(f'{total} findings in {len(found)} rules (each place counted once).')
for rule, count in found.most_common():
    sample = next(text for (r, _, _), text in where.items() if r == rule)
    print(f'{count:6d}  {rule}  {sample[:100]}')
PY
