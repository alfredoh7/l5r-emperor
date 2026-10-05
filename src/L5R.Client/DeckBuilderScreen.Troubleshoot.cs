using Godot;
using L5R.Core;
namespace L5R.Client;

public partial class DeckBuilderScreen
{
    private const string LeaveOut = "Leave this card out";
    /// <summary>Import troubleshooter: when a deck file will not load because of card names the catalogue does not know (or a legality
    /// this builder cannot open), offer the nearest catalogue match for each line. The repaired list opens as an unsaved draft; the file is untouched.
    /// Returns false when there is nothing to repair so the caller can show the original error.</summary>
    private bool TryTroubleshoot(string path)
    {
        DeckTroubleshooting report; string text;
        try { text = DeckFileStore.Read(path); report = NewDraft().Troubleshoot(text); }
        catch (Exception) { return false; }
        var problems = report.Problems;
        if (problems.Length == 0 && !report.LegalityNeedsFixing) return false;
        var name = Path.GetFileNameWithoutExtension(path);
        var dialog = new ConfirmationDialog { Title = "Deck troubleshooter", Theme = Theme, Exclusive = true }; AddChild(dialog);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 10); dialog.AddChild(root);
        var summary = problems.Length == 0 ? "Every card was recognised."
            : $"{report.RecognisedLines} lines matched. {problems.Length} {(problems.Length == 1 ? "line was" : "lines were")} not found in the catalogue; pick the closest card for each.";
        root.AddChild(Palette.Wrapped(summary, 19, new Color("f0e6d2")));
        if (report.LegalityNeedsFixing)
            root.AddChild(Palette.Wrapped($"This list says Legality: {report.Legality ?? "(none)"}. The repaired copy opens as Legality: Emperor; cards from other formats will show as not found.", 17, new Color("e8c9a0")));
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(760, 330), SizeFlagsVertical = Control.SizeFlags.ExpandFill }; root.AddChild(scroll);
        var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; rows.AddThemeConstantOverride("separation", 12); scroll.AddChild(rows);
        var picks = new Dictionary<DeckProblem, OptionButton>();
        foreach (var problem in problems)
        {
            var row = new VBoxContainer(); row.AddThemeConstantOverride("separation", 4); rows.AddChild(row);
            row.AddChild(Palette.Wrapped($"Line {problem.LineNumber}: {(problem.Count > 1 ? problem.Count + " × " : "")}“{problem.Text}”", 18, new Color("f0e6d2")));
            var pick = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FitToLongestItem = false }; row.AddChild(pick);
            foreach (var s in problem.Suggestions) pick.AddItem($"{s.DeckName}  ·  {Math.Round(s.Score * 100)}% match");
            pick.AddItem(problem.Suggestions.Length == 0 ? "No close match · " + LeaveOut.ToLowerInvariant() : LeaveOut);
            pick.Select(0); picks[problem] = pick;
        }
        var ok = dialog.GetOkButton(); ok.Text = "Open repaired deck";
        dialog.Confirmed += () =>
        {
            var fixedText = report.Rebuild(p => picks[p].Selected < p.Suggestions.Length ? p.Suggestions[picks[p].Selected].DeckName : null);
            dialog.QueueFree();
            try { OpenPracticeDeck(DeckFormat.Parse(fixedText), name + " repaired"); }
            catch (Exception e) { Notice("Could not open the repaired deck: " + e.Message); }
        };
        dialog.Canceled += dialog.QueueFree;
        dialog.PopupCentered(new Vector2I(820, 520));
        return true;
    }
}
