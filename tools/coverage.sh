#!/usr/bin/env bash
# Issue #905: line, branch and method coverage of L5R.Core from the Core test runner, as a trend report (never a gate).
#   bash tools/coverage.sh                         # the whole Core suite (instrumented, it takes far longer than the 2 to 3 minutes of a normal run)
#   bash tools/coverage.sh --filter core/Privacy   # any filter the runner takes (about 7 minutes, mostly instrumentation, for a small slice)
# Output: artifacts/coverage/Summary.txt (and SummaryGithub.md, coverage.cobertura.xml). The two tools are installed once, pinned, into artifacts/tools.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
tools="$L5R_PROJECT_ROOT/artifacts/tools/coverage"
if [ ! -x "$tools/coverlet" ]; then dotnet tool install coverlet.console --version 10.1.0 --tool-path "$tools" > /dev/null; fi
if [ ! -x "$tools/reportgenerator" ]; then dotnet tool install dotnet-reportgenerator-globaltool --version 5.5.11 --tool-path "$tools" > /dev/null; fi
dotnet build tests/L5R.Core.Tests --nologo -v q
dir="tests/L5R.Core.Tests/bin/Debug/net10.0"
rm -rf artifacts/coverage; mkdir -p artifacts/coverage
"$tools/coverlet" "$dir" --target dotnet --targetargs "$dir/L5R.Core.Tests.dll $*" --include "[L5R.Core]*" --format cobertura --output artifacts/coverage/ > artifacts/coverage/coverlet.log || { tail -20 artifacts/coverage/coverlet.log; exit 1; }
"$tools/reportgenerator" -reports:artifacts/coverage/coverage.cobertura.xml -targetdir:artifacts/coverage -reporttypes:"TextSummary;MarkdownSummaryGithub" > /dev/null
cat artifacts/coverage/Summary.txt
