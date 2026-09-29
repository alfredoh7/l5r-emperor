"""Contract extraction and read-only Git behavior against adversarial source fixtures."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('contract_ledger', Path(__file__).with_name('ledger.py'))
ledger = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = ledger
spec.loader.exec_module(ledger)


class ExtractionTests(unittest.TestCase):
    def inventory(self, source):
        return ledger.inventory({'src/L5R.Core/Fixture.cs': source})

    def test_comments_strings_and_method_locals_are_not_contracts(self):
        result = self.inventory('''
// new GameCommand("fakeComment", 0, 0); public record FakeView(int Hidden);
/* new PendingChoice("fakeBlock", 0, null, ""); */
public class MatchState {
    public string Real { get; set; } = "public int Fake { get; set; }";
    public void Method() { var local = "public record FakeView(int Local);"; }
}
''')
        self.assertEqual(set(result['state_fields']), {'MatchState.Real'})
        self.assertFalse(result['commands'])
        self.assertFalse(result['pending_choices'])

    def test_multiline_records_generics_defaults_and_partials(self):
        result = ledger.inventory({
            'src/L5R.Core/One.cs': '''public sealed record CardView(
                string Id, Dictionary<string, int>? Amounts = null,
                string[]? Labels = null);
                public partial class MatchState { public ChildState Child { get; set; } = new(); }
                public class ChildState { public int Count; public static string Cache = ""; }
            ''',
            'src/L5R.Core/Two.cs': '''public partial class MatchState { public int Extra { get; set; } }
                public record MatchView(CardView[] Cards);
            '''})
        self.assertEqual(set(result['view_fields']), {'CardView.Id', 'CardView.Amounts', 'CardView.Labels', 'MatchView.Cards'})
        self.assertEqual(set(result['state_fields']), {'MatchState.Child', 'MatchState.Extra', 'ChildState.Count'})

    def test_literal_constant_named_and_target_typed_choice_constructors(self):
        result = self.inventory('''public static class ChoiceKinds { public const string Select = "select"; }
public class Engine {
    void Run() {
        state.PendingChoice = new(ChoiceKinds.Select, 0, null, "prompt");
        var a = new PendingChoice(Type: @"verbatim", PlayerId: 0, CardId: null, Prompt: "prompt");
        var b = new GameCommand("answer", 0, 0, Choice: "yes");
        Add(Command("factoryAnswer"));
        switch (command.Type) { case "dispatched": break; default: break; }
    }
}''')
        self.assertEqual(set(result['pending_choices']), {'select', 'verbatim'})
        self.assertEqual(set(result['commands']), {'answer', 'factoryAnswer', 'dispatched'})
        self.assertTrue(any('Choice : "yes"' in key for key in result['payload_sites']))

    def test_ambiguous_unqualified_constants_are_not_guessed(self):
        result = self.inventory('''public static class A { public const string Kind = "a"; }
public static class B { public const string Kind = "b"; }
public class Engine { void Run() { var c = new GameCommand(Kind, 0, 0); } }''')
        self.assertFalse(result['commands'])

    def test_shadowing_dynamic_name_does_not_resolve_to_unrelated_constant(self):
        result = self.inventory('''public static class K { public const string Kind = "actual"; }
public class Engine { void Run(string Kind) { var c = new GameCommand(Kind, 0, 0); } }''')
        self.assertFalse(result['commands'])

    def test_expression_properties_and_comparison_defaults(self):
        result = self.inventory('''public record MatchView(int Count = 1 < 2 ? 1 : 0, int Next = 2) {
    public int Total => Count + Next;
    public static int Cache => 3;
}''')
        self.assertEqual(set(result['view_fields']), {'MatchView.Count', 'MatchView.Next', 'MatchView.Total'})

    def test_relational_payload_arguments_remain_separate(self):
        result = self.inventory('''public record CardView(bool Larger = 2 > 1, int Count = 0);
public class Engine { void Run() {
    var c = new GameCommand("existing", 0, 0, TargetId: a < b ? "small" : "large", Choice: "yes");
} }''')
        self.assertEqual(set(result['view_fields']), {'CardView.Larger', 'CardView.Count'})
        self.assertEqual(len(result['payload_sites']), 2)
        self.assertTrue(any('Choice : "yes"' in key for key in result['payload_sites']))

    def test_multiple_public_field_declarators_share_the_type(self):
        for declaration in ('public int A, B;', 'public int A = 1, B = 2;'):
            with self.subTest(declaration=declaration):
                result = self.inventory('public class MatchState { ' + declaration + ' }')
                self.assertEqual(set(result['state_fields']), {'MatchState.A', 'MatchState.B'})
                self.assertEqual({e['declaration'] for e in result['state_fields'].values()}, {'int'})

    def test_dynamic_payload_and_existing_command_choices_stay_visible(self):
        result = self.inventory('''public class Engine { void Run() {
            var c = new GameCommand("existing", 0, 0, TargetId: $"{spellId}>{hostId}", CardIds: [id]);
            if (command.Choice == AbilityKinds.Decline) Pass();
            if (command is { TargetId: { } id }) Pass();
        } }''')
        keys = list(result['payload_sites'])
        self.assertEqual(len(keys), 3)
        self.assertTrue(any('spellId' in key for key in keys))
        self.assertFalse(any('TargetId : {' in key for key in keys))


class GitLedgerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.git('init', '-b', 'main')
        self.git('config', 'user.name', 'Fixture')
        self.git('config', 'user.email', 'fixture@example.invalid')
        self.write('Models.cs', 'public partial class MatchState { public int Existing { get; set; } }')
        self.save()
        self.base = self.git('rev-parse', 'HEAD')

    def git(self, *args):
        result = subprocess.run(['git', *args], cwd=self.root, capture_output=True, text=True, check=True)
        return result.stdout.strip()

    def write(self, name, text):
        path = self.root / 'src/L5R.Core' / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8')

    def save(self):
        self.git('add', '.')
        self.git('commit', '-m', 'fixture')

    def test_changes_removals_and_partial_moves_use_member_identity(self):
        self.write('Models.cs', 'public partial class MatchState { public string Existing { get; set; } }')
        self.write('Feature.cs', 'public partial class MatchState { public int NewField { get; set; } }')
        self.save()
        report = ledger.ledger(self.root, self.base)
        fields = report['categories']['state_fields']
        self.assertEqual([(e['name'], e['change']) for e in fields], [('MatchState.Existing', 'changed'), ('MatchState.NewField', 'added')])
        second = self.git('rev-parse', 'HEAD')
        self.write('Models.cs', 'public partial class MatchState { }')
        self.write('Feature.cs', 'public partial class MatchState { public string Existing { get; set; } }')
        self.save()
        fields = ledger.ledger(self.root, second)['categories']['state_fields']
        self.assertEqual([(e['name'], e['change']) for e in fields], [('MatchState.NewField', 'removed')])

    def test_working_includes_staged_unstaged_untracked_and_deleted_sources(self):
        self.write('Models.cs', 'public partial class MatchState { public int Staged { get; set; } }')
        self.git('add', '.')
        self.write('Models.cs', 'public partial class MatchState { public int Unstaged { get; set; } }')
        self.write('New.cs', 'public partial class MatchState { public int Untracked { get; set; } }')
        self.assertFalse(ledger.ledger(self.root, self.base)['categories']['state_fields'])
        status = self.git('status', '--porcelain')
        report = ledger.ledger(self.root, self.base, working=True)
        self.assertEqual({e['name']: e['change'] for e in report['categories']['state_fields']},
                         {'MatchState.Existing': 'removed', 'MatchState.Unstaged': 'added', 'MatchState.Untracked': 'added'})
        self.assertEqual(self.git('status', '--porcelain'), status)
        (self.root / 'src/L5R.Core/Models.cs').unlink()
        self.assertNotIn('MatchState.Unstaged', {e['name'] for e in ledger.ledger(self.root, self.base, working=True)['categories']['state_fields']})

    def test_diverged_base_uses_merge_base_like_pr_diff(self):
        self.git('switch', '-c', 'task')
        self.write('Task.cs', 'public partial class MatchState { public int Task { get; set; } }')
        self.save()
        head = self.git('rev-parse', 'HEAD')
        self.git('switch', 'main')
        self.write('Main.cs', 'public partial class MatchState { public int MainOnly { get; set; } }')
        self.save()
        report = ledger.ledger(self.root, 'main', head)
        self.assertEqual(report['merge_base'], self.base)
        self.assertEqual([e['name'] for e in report['categories']['state_fields']], ['MatchState.Task'])
        with self.assertRaisesRegex(ValueError, '--working requires'):
            ledger.ledger(self.root, 'main', head, working=True)

    def test_invalid_option_like_ref_fails_without_mutation(self):
        status = self.git('status', '--porcelain')
        with self.assertRaises(RuntimeError):
            ledger.ledger(self.root, '--help')
        self.assertEqual(self.git('status', '--porcelain'), status)

    def test_export_attributes_do_not_hide_or_substitute_committed_source(self):
        (self.root / '.gitattributes').write_text('src/L5R.Core/Models.cs export-ignore\nsrc/L5R.Core/Other.cs export-subst\n', encoding='utf-8')
        source = 'public partial class MatchState { public string Other { get; set; } = "$Format:%H$"; }'
        self.write('Other.cs', source)
        self.save()
        snapshot = ledger.snapshot(self.root, self.git('rev-parse', 'HEAD'))
        self.assertIn('src/L5R.Core/Models.cs', snapshot)
        self.assertEqual(snapshot['src/L5R.Core/Other.cs'], source)

    def test_working_rejects_symlink_escaping_repository(self):
        with tempfile.TemporaryDirectory() as other:
            external = Path(other) / 'Private.cs'
            external.write_text('private outside data', encoding='utf-8')
            try:
                os.symlink(external, self.root / 'src/L5R.Core/Link.cs')
            except OSError:
                self.skipTest('Host does not permit symlink creation; hosted Linux exercises this case')
            with self.assertRaisesRegex(ValueError, 'leaves repository'):
                ledger.ledger(self.root, self.base, working=True)

    def test_cli_json_markdown_and_error_status(self):
        command = [sys.executable, str(ROOT / 'tools/collab.py'), '--root', str(self.root), 'ledger', '--base', self.base]
        result = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(json.loads(result.stdout)['categories']['commands'], [])
        result = subprocess.run([*command, '--format', 'markdown'], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0)
        self.assertIn('an empty ledger does not prove', result.stdout)
        result = subprocess.run([*command, '--head', 'absent-ref'], capture_output=True, text=True)
        self.assertEqual(result.returncode, 1)
        self.assertIn('error', json.loads(result.stdout))


if __name__ == '__main__':
    unittest.main()
