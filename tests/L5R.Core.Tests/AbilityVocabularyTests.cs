using System.Text.RegularExpressions;
using L5R.Core;

// Issue #845 (stability): ability Timing, Target, Performer and Trigger are plain strings, so a typo in a definition is a silent no-op. This lint is the check:
// every Timing must be a known verb, and every Target / Performer / Trigger string a definition names must also appear as a handled label (a `case "x"`, a `"x" =>`,
// an `or "x"` pattern, a comparison, or a named constant referenced outside its declaration and outside definitions) somewhere in the engine source.
// A string that occurs only inside definitions is handled by nothing.
internal static class AbilityVocabularyTests
{
    private static readonly string[] Timings = ["Battle", "Open", "Limited", "BattleOrOpen", "Reaction", "FocusEffect", "BeforeResolution", "Trait"];

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Vocabulary: every ability Timing is a known verb", TimingsKnown),
            ("Vocabulary: every Target, Performer and Trigger string a definition names is handled somewhere in the engine", EveryCategoryHandled),
        ];
        runner.Run("ability-vocabulary", tests);
    }

    private static void TimingsKnown()
    {
        var bad = AbilityCatalog.All.Where(kv => !Timings.Contains(kv.Value.Timing)).Select(kv => $"{kv.Key}:{kv.Value.Timing}").ToList();
        if (bad.Count > 0) throw new Exception("Unknown timing: " + string.Join(", ", bad.Take(20)));
    }

    private static readonly Regex HandledLiteral = new("""(?:case\s+|or\s+|==\s*|!=\s*|\bis\s+|\bnot\s+|\(\s*|\{\s*|,\s*)"([a-z0-9][a-z0-9\-]*)"\s*(?:=>|:|or\b|\)|,|\|\||&&|\?|;|\})|"([a-z0-9][a-z0-9\-]*)"\s*(?:=>|or\s+")""", RegexOptions.Compiled);
    private static readonly Regex ConstLine = new("""\bconst\s+string\s""", RegexOptions.Compiled);
    private static readonly Regex ConstValue = new("""\b(\w+)\s*=\s*"([^"]+)"\s*[,;]""", RegexOptions.Compiled);
    private static readonly Regex Identifier = new("""\b[A-Za-z_]\w*\b""", RegexOptions.Compiled);
    private static readonly Regex Definition = new("""new\s+AbilityDefinition\(|new\(\s*"(?:Battle|Open|Limited|BattleOrOpen|Reaction|FocusEffect|BeforeResolution)",""", RegexOptions.Compiled);

    private static void EveryCategoryHandled()
    {
        var root = Path.Combine(Support337.RepoRoot(), "src/L5R.Core");
        var handled = new HashSet<string>(StringComparer.Ordinal);
        var constants = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal); // the same constant name can exist in several classes
        var lines = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).SelectMany(File.ReadAllLines).ToList();
        var inConst = false; // a const declaration may continue over several lines until its semicolon
        var constLines = new HashSet<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (inConst || ConstLine.IsMatch(line))
            {
                foreach (Match c in ConstValue.Matches(line)) { if (!constants.TryGetValue(c.Groups[1].Value, out var set)) constants[c.Groups[1].Value] = set = []; set.Add(c.Groups[2].Value); }
                constLines.Add(i); inConst = !line.TrimEnd().EndsWith(';');
                continue;
            }
            if (Definition.IsMatch(line)) continue;
            foreach (Match m in HandledLiteral.Matches(line)) handled.Add(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
        }
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (constLines.Contains(i) || Definition.IsMatch(line)) continue;
            foreach (Match m in Identifier.Matches(line)) if (constants.TryGetValue(m.Value, out var values)) handled.UnionWith(values);
        }
        var unhandled = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (id, a) in AbilityCatalog.All)
        {
            if (a.Target is { Length: > 0 } t && t != "slots" && !handled.Contains(t)) unhandled.Add($"Target '{t}' ({id})");
            if (a.Performer is { Length: > 0 } p && !handled.Contains(p)) unhandled.Add($"Performer '{p}' ({id})");
            if (a.Trigger is { Length: > 0 } g && !g.StartsWith("p6-fact:", StringComparison.Ordinal) && !handled.Contains(g)) unhandled.Add($"Trigger '{g}' ({id})");
        }
        if (unhandled.Count > 0) throw new Exception($"{unhandled.Count} category strings are named by a definition but handled nowhere:\n  " + string.Join("\n  ", unhandled.Take(400)));
    }
}
