"""Exercise Git selection and real SDK rejection without touching production C#."""
import importlib.util
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('check_format', ROOT / 'tools/quality/check_format.py')
quality = importlib.util.module_from_spec(spec)
spec.loader.exec_module(quality)


class SelectionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.git('init', '-b', 'main')
        self.write('old.cs', 'class Old{}\n')
        self.write('deleted.cs', 'class Deleted{}\n')
        self.commit()
        self.base = self.git('rev-parse', 'HEAD').decode().strip()

    def git(self, *args):
        return subprocess.run(['git', *args], cwd=self.root, check=True, capture_output=True).stdout

    def write(self, name, value):
        (self.root / name).write_text(value, encoding='utf-8')

    def commit(self):
        self.git('add', '.')
        self.git('-c', 'user.name=Quality Fixture', '-c', 'user.email=fixture@example.invalid',
                 'commit', '-m', 'fixture')

    def test_local_selection_includes_staged_unstaged_and_new_files(self):
        self.write('old.cs', 'class Old { }\n')
        self.git('add', 'old.cs')
        self.write('old.cs', 'class Old { int X; }\n')
        self.write('new file.cs', 'class New { }\n')
        self.write('notes.md', 'Not C#.\n')
        self.assertEqual(quality.changed_files(self.root, self.base), ['new file.cs', 'old.cs'])

    def test_committed_selection_handles_rename_and_delete_and_ignores_working_changes(self):
        self.git('mv', 'old.cs', 'renamed.cs')
        self.git('rm', 'deleted.cs')
        self.commit()
        self.write('uncommitted.cs', 'class Later { }\n')
        self.assertEqual(quality.changed_files(self.root, self.base, 'HEAD'), ['renamed.cs'])

    def test_empty_selection_does_not_invoke_formatter(self):
        self.assertEqual(quality.changed_files(self.root, self.base), [])
        self.assertEqual(quality.verify(self.root, [], dotnet='not-an-executable'), 0)

    def test_missing_diff_base_fails_instead_of_passing(self):
        with self.assertRaises(subprocess.CalledProcessError):
            quality.changed_files(self.root, 'missing-base')

    def test_sparse_checkout_cannot_silently_omit_a_changed_file(self):
        with patch.object(quality, 'git', return_value=b'missing.cs\0'):
            with self.assertRaisesRegex(ValueError, 'absent from the checkout'):
                quality.changed_files(self.root, self.base, 'HEAD')


@unittest.skipUnless(os.environ.get('L5R_QUALITY_INTEGRATION') == '1', 'SDK integration runs in quality CI')
class SdkTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        shutil.copy2(ROOT / '.editorconfig', self.root / '.editorconfig')

    def test_bad_changed_file_fails_without_modifying_it_or_bad_legacy_file(self):
        changed = self.root / 'changed file.cs'
        legacy = self.root / 'untouched.cs'
        bad = b'class Example{public int Value=>1+2;}\n'
        changed.write_bytes(bad)
        legacy.write_bytes(bad)
        self.assertNotEqual(quality.verify(self.root, [changed.name]), 0)
        self.assertEqual(changed.read_bytes(), bad)
        self.assertEqual(legacy.read_bytes(), bad)
        subprocess.run(['dotnet', 'format', 'whitespace', str(self.root), '--folder',
                        '--include', './' + changed.name], cwd=self.root, check=True, capture_output=True)
        self.assertEqual(quality.verify(self.root, [changed.name]), 0)
        self.assertEqual(legacy.read_bytes(), bad)

    def build(self):
        return subprocess.run(['dotnet', 'build', 'Fixture.csproj', '--no-incremental',
                               '--disable-build-servers', '-p:UseSharedCompilation=false', '-m:1', '--nologo'],
                              cwd=self.root, capture_output=True, text=True)

    def test_new_nullable_warning_fails_and_fixed_code_passes(self):
        for name in ('Directory.Build.props', 'Directory.Build.targets'):
            shutil.copy2(ROOT / name, self.root / name)
        (self.root / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>', encoding='utf-8')
        project = self.root / 'Fixture.csproj'
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>', encoding='utf-8')
        source = self.root / 'Example.cs'
        source.write_text('public static class Example { public static int Length(string? value) => value.Length; }', encoding='utf-8')
        failed = self.build()
        self.assertNotEqual(failed.returncode, 0, failed.stdout + failed.stderr)
        self.assertIn('CS8602', failed.stdout + failed.stderr)
        source.write_text('public static class Example { public static int Length(string? value) => value?.Length ?? 0; }', encoding='utf-8')
        fixed = self.build()
        self.assertEqual(fixed.returncode, 0, fixed.stdout + fixed.stderr)
        for setting in ('<Nullable>disable</Nullable>', '<TreatWarningsAsErrors>false</TreatWarningsAsErrors>'):
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>' + setting + '</PropertyGroup></Project>', encoding='utf-8')
            weakened = self.build()
            self.assertNotEqual(weakened.returncode, 0, weakened.stdout + weakened.stderr)
            self.assertIn('baseline requires', weakened.stdout + weakened.stderr)


if __name__ == '__main__':
    unittest.main()
