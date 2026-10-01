using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Interactive prepared catalogue workspaces; no scripted clicks or match actions.</summary>
public partial class CatalogueControlsReview : Control
{
    private Control? _screen;
    private string _capture = "";
    private bool _captured;
    public override void _Ready()
    {
        _capture = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture="))?[10..] ?? "";
        var builder = OS.GetCmdlineUserArgs().Any(a => a == "--deck-filters-review" || a == "--capture-mode=deck-filters-review");
        if (builder) OpenBuilder(); else OpenLibrary();
    }
    private void RemoveScreen()
    {
        if (_screen is null) return; RemoveChild(_screen); _screen.QueueFree(); _screen = null;
    }
    private void OpenLibrary()
    {
        RemoveScreen();
        var library = new CatalogueScreen { OpenBuilder = OpenBuilder }; _screen = library; AddChild(library); library.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        library.Closed += OpenBuilder; Prepare(library.Browser);
        _ = CaptureOnce();
    }
    private void OpenBuilder()
    {
        RemoveScreen();
        var builder = new DeckBuilderScreen { StorageDirectory = ProjectSettings.GlobalizePath("user://unified-filter-review/decks"), OpenLibrary = OpenLibrary };
        _screen = builder; AddChild(builder); builder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        builder.Closed += OpenLibrary;
        var source = PublishedContent.Snapshot.Decks.First(d => d.Clan == "Crane");
        var ids = new[] { source.StrongholdId, "oracle-l5r-1106", "oracle-l5r-679" }.Concat(source.Dynasty).Concat(source.Fate);
        var entries = ids.GroupBy(id => id).Select(g => new DeckEntry(builder.Data.Cards.Single(c => c.Id == g.Key).Name, g.Count())).ToArray();
        builder.OpenPracticeDeck(new Deck("Emperor", entries), "Review — Crane"); Prepare(builder.Browser);
        _ = CaptureOnce();
    }
    private static void Prepare(CatalogueBrowserBar bar)
    {
        bar.SetFamily("Dynasty"); bar.Clans.UnionWith(["Crane", "Mantis"]); bar.Types.Selected.Add("Personality"); bar.Refresh();
    }
    private async Task CaptureOnce()
    {
        if (_capture.Length == 0 || _captured) return; _captured = true;
        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        if (!IsInsideTree()) return; var dir = Path.GetDirectoryName(_capture); if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        RenderingServer.ForceDraw(); GetViewport().GetTexture().GetImage().SavePng(_capture);
        GD.Print("UNIFIED_FILTER_REVIEW_READY: interactive browser, additive facets and draggable card-size slider.");
    }
}
