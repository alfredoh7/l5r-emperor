using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Native input and rendering scenarios; storage is isolated from the player's decks.</summary>
public partial class DeckBuilderProbe : Node
{
    private DeckBuilderScreen _screen = null!;
    private string _out = "";
    public override async void _Ready()
    {
        try
        {
            _out = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--out="))?[6..] ?? ProjectSettings.GlobalizePath("res://../../artifacts/deck-builder-checks"); Directory.CreateDirectory(_out);
            var storage = Path.Combine(_out, "decks-" + Guid.NewGuid().ToString("N"));
            _screen = new DeckBuilderScreen { StorageDirectory = storage }; AddChild(_screen); _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await Wait(2); await Capture("empty-decks");
            Check(Button("New deck").Size == Button("Import deck").Size && Button("New deck").Size == Button("Open deck folder").Size, "three equal landing actions");
            Button("New deck").GrabFocus(); await Wait(.2); await Capture("landing-focus"); Click(Button("New deck")); await Wait(3); await Capture("setup");
            Check(_screen.Draft.Count("Border Keep") == 1 && _screen.Draft.Count("Bamboo Harvesters") == 1, "default setup chosen outside deck totals");
            Check(_screen.Draft.Review().Dynasty == 0 && _screen.Draft.Review().Fate == 0, "setup excluded from counts");
            var select = Walk(_screen).OfType<Button>().First(b => b.Text == "Select" && b.IsVisibleInTree()); Click(select); await Wait(.5); Check(_screen.Draft.Cards.Count(c => c.SetupSlot == "Stronghold") == 1, "native single stronghold selection"); await Capture("setup-selected");
            Click(Walk(_screen).OfType<Button>().Single(b => b.AccessibilityName == "Choose Border Keep" && b.IsVisibleInTree())); await Wait(.4); var alternatives = Walk(_screen).OfType<Button>().Where(b => b.Text == "Select" && b.IsVisibleInTree()).ToArray();
            if (alternatives.Length > 0) { Click(alternatives[0]); await Wait(.3); Check(_screen.Draft.Cards.Count(c => c.SetupSlot == "Border Keep") == 1, "setup variant replaces previous starting card"); }
            Check(!Walk(_screen).OfType<OptionButton>().Any(o => o.GetItemText(0).StartsWith("Emperor Edition")), "no redundant printing dropdown");
            var source = PublishedContent.Snapshot.Decks.First(d => d.Clan == "Crane");
            var all = new[] { source.StrongholdId, "oracle-l5r-1106", "oracle-l5r-679" }.Concat(source.Dynasty).Concat(source.Fate);
            var entries = all.GroupBy(id => id).Select(g => new DeckEntry(_screen.Data.Cards.Single(c => c.Id == g.Key).Name, g.Count())).ToArray();
            var path = _screen.Store.Save("Crane — Court and Steel", new Deck("Emperor", entries)); Check(_screen.OpenFile(path), "published deck loads into builder"); await Wait(5); await Capture("mat");
            Check(_screen.Collection.Size.X >= 590, "collection has doubled logical width");
            Search("Asahina"); await Wait(3); await Capture("collection-large"); Search(""); await Wait(.3);
            var more = Walk(_screen).OfType<MenuButton>().Single(); Click(more); await Wait(.3); Check(more.GetPopup().ItemCount == 2, "compact save-copy and export actions available"); await Capture("deck-actions"); KeyPress(Key.Escape); await Wait(.3);
            Click(Walk(_screen).OfType<Button>().First(b => b.Text.StartsWith("Clans") && b.IsVisibleInTree())); await Wait(.3);
            Activate(Walk(_screen).OfType<CheckBox>().First(c => c.Text == "Crane")); await Wait(.2);
            Check(_screen.Collection.Cards.Length > 0 && _screen.Collection.Cards.All(c => c.Clans.Contains("Crane")), "native clan checklist filters collection");
            Activate(Walk(_screen).OfType<CheckBox>().First(c => c.Text == "Mantis")); await Wait(.2);
            Check(_screen.Collection.Cards.All(c => c.Clans.Contains("Crane") || c.Clans.Contains("Mantis")), "native additive multi-clan filter");
            await Capture("filter-menu");
            Activate(Walk(_screen).OfType<CheckBox>().First(c => c.Text == "Crane")); Activate(Walk(_screen).OfType<CheckBox>().First(c => c.Text == "Mantis")); await Wait(.2);
            KeyPress(Key.Escape); await Wait(.3);
            Click(Walk(_screen).OfType<Button>().First(b => b.Text.StartsWith("Types") && b.IsVisibleInTree())); await Wait(.3);
            Check(!Walk(_screen).OfType<CheckBox>().Any(c => c.IsVisibleInTree() && c.Text == "Spell"), "Dynasty-scoped Type list drops Fate-only types");
            KeyPress(Key.Escape); await Wait(.3);
            Click(Walk(_screen).OfType<Button>().First(b => b.Text.StartsWith("Keywords") && b.IsVisibleInTree())); await Wait(.3);
            var dynastyKeywords = _screen.Data.Cards.Where(c => c.DeckTypes.Contains("Dynasty") && new DeckCardInfo(c.Id, c.Name, c.Types, c.DeckTypes, c.Keywords, c.Playable).SetupSlot.Length == 0).SelectMany(c => c.Keywords).Distinct().ToHashSet();
            // Exclude each collection tile's own "Compare" checkbox; only the popover's keyword rows matter here.
            Check(Walk(_screen).OfType<CheckBox>().Where(c => c.IsVisibleInTree() && c.Text != "Compare").All(c => dynastyKeywords.Contains(c.Text)), "keyword list scoped to current family selection");
            KeyPress(Key.Escape); await Wait(.3);
            var before = _screen.Draft.Review(); Check(before.Dynasty == source.Dynasty.Length && before.Fate == source.Fate.Length, "authoritative source totals");
            var card = _screen.Data.Cards.First(c => c.Name == "Daidoji Kenshi");
            Search(card.Name); await Wait(.6); var plus = Walk(_screen.Collection).OfType<Button>().Single(b => b.AccessibilityName == "Add " + card.Name); Click(plus); await Wait(.5); Check(_screen.Dirty, "native quantity edit marks draft dirty");
            _screen.Adjust(card, 4 - _screen.Draft.Count(card.Name)); await Wait(.4); Check(_screen.Draft.Review().OverLimit.Contains(card.Name), "over-limit count gets Core warning"); await Capture("over-limit");
            Search("nonesuchzzzz"); await Wait(.2); Check(_screen.Collection.Cards.Length == 0, "empty search results"); Search("");
            var members = _screen.Draft.Cards.Where(c => c.SetupSlot.Length == 0 && c.Families.Contains("Dynasty")).Take(3).Select(c => _screen.Data.Cards.Single(x => x.Id == c.Id)).ToArray();
            Click(Button("List")); await Wait(.3); Check(_screen.Mat.Rows, "list view selected through native input"); Click(Button("Mat")); await Wait(.3);
            var compareToggle = Walk(_screen.Mat).OfType<CheckBox>().First(c => c.IsVisibleInTree()); Click(compareToggle); await Wait(.3); Check(compareToggle.ButtonPressed, "native comparison selection");
            _screen.ShowComparison(members); await Wait(3); await Capture("compare"); KeyPress(Key.Escape); await Wait(.3);
            var events = _screen.Data.Cards.Where(c => c.Name == "A Fever in the Blood" || c.Name == "A Dangerous Game").GroupBy(c => c.Name).Select(g => g.First()).OrderBy(c => c.Name == "A Fever in the Blood" ? 0 : 1).ToArray();
            _screen.ShowComparison(events); await Wait(3); await Capture("compare-two");
            var parchments = Walk(_screen).OfType<Panel>().Where(p => p.Name.ToString().StartsWith("RulesParchment")).ToArray(); Check(parchments.Length == 2 && parchments.All(p => p.Size.Y >= 180), "two readable rules panels");
            var longRules = _screen.Data.Cards.OrderByDescending(c => c.CurrentOracleText.Length).Take(3).ToArray(); _screen.ShowComparison(longRules); await Wait(2); await Capture("compare-long-rules");
            var rulesScroll = Walk(_screen).OfType<ScrollContainer>().First(s => s.Name == "RulesScroll" && s.GetVScrollBar().MaxValue > s.Size.Y); rulesScroll.ScrollVertical = 100; await Wait(.2); Check(rulesScroll.ScrollVertical > 0, "long comparison rules remain scrollable"); KeyPress(Key.Escape); await Wait(.3);
            var snapshot = DeckFormat.Write(_screen.Draft.ToDeck()); var bad = Path.Combine(_out, "malformed.txt"); File.WriteAllText(bad, "Legality: Emperor\n1 Missing Imaginary Card"); Check(!_screen.OpenFile(bad) && DeckFormat.Write(_screen.Draft.ToDeck()) == snapshot, "bad import preserves draft"); await Wait(.4); KeyPress(Key.Enter); await Wait(.4); Check(!Walk(_screen).OfType<AcceptDialog>().Any(d => d.Visible), "error notice dismissed with keyboard");
            _screen.SaveDraft(); await Wait(.3); Check(!_screen.Dirty && DeckFileStore.Read(path) == snapshot, "save and reopen exact text roundtrip");
            _screen.Adjust(card, 1); await Wait(.4); await Capture("before-guard"); Click(Button("‹ My decks")); await Wait(.3); await Capture("after-guard"); Check(Walk(_screen).OfType<ConfirmationDialog>().Any(), "unsaved navigation guarded"); KeyPress(Key.Escape); await Wait(.3); Check(_screen.Page == "editor" && _screen.Dirty, "cancel keeps draft");
            _screen.SaveDraft();
            File.AppendAllText(path, "\n"); _screen.Adjust(card, 1); _screen.SaveDraft(); await Wait(.3); Check(Walk(_screen).OfType<ConfirmationDialog>().Any(d => d.Visible), "external edit requires overwrite decision"); KeyPress(Key.Escape); await Wait(.3); Check(_screen.Dirty, "overwrite cancel preserves dirty draft");
            _screen.SaveDraft(); await Wait(.3); KeyPress(Key.Enter); await Wait(.3); Check(!_screen.Dirty, "confirmed overwrite saves draft");
            Click(Button("‹ My decks")); await Wait(2); await Capture("saved-decks"); Click(Button("Open in builder")); await Wait(.5);
            var slider = Walk(_screen).OfType<HSlider>().Single(); slider.Value = 110; await Wait(.7); await Capture("compact"); Check(_screen.Mat.VisibleCount < 80 && _screen.Collection.VisibleCount < 25, "virtualized nodes bounded");
            GetWindow().Size = new Vector2I(1280, 800); await Wait(1); await Capture("small-window");
            Check(_screen.Mat.GetGlobalRect().End.X <= 1280, "mat within compact viewport");
            GetWindow().Size = new Vector2I(1920, 1080); await Wait(.5); await Capture("wide-window");
            _screen.ShowComparison(events); await Wait(.4); await Capture("wide-compare"); KeyPress(Key.Escape); await Wait(.3);
            GetWindow().Size = new Vector2I(1280, 800); await Wait(.4); _screen.ShowComparison(events); await Wait(.4); await Capture("small-compare"); KeyPress(Key.Escape); await Wait(.3);
            _screen.ShowDecks(); await Wait(.3); await Capture("small-saved-decks"); GetWindow().Size = new Vector2I(1920, 1080); await Wait(.4);
            _screen.QueueFree(); await Wait(.3); var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate(); AddChild(main); await Wait(2); await Capture("main-menu");
            var menu = Walk(main).OfType<TitleMenu>().Single(); Check(menu.Entries.GetChildren().OfType<Button>().All(b => b.GetGlobalRect().End.Y <= GetViewport().GetVisibleRect().Size.Y), "all six menu entries fit in viewport"); var entry = Walk(menu).OfType<Button>().Single(b => string.Equals(b.Text, "Deck Builder", StringComparison.OrdinalIgnoreCase) && b.IsVisibleInTree()); Click(entry); await Wait(2);
            Check(GetTree().Root.GetNodeOrNull("DeckBuilderLayer") is not null, "main menu opens deck builder"); await Capture("menu-deck-builder");
            GD.Print("DECK BUILDER PASS: native setup, quantity, search, warnings, comparison, save/load, corrupt import, unsaved cancel, density, responsive captures");
            if (!OS.GetCmdlineUserArgs().Contains("--stay")) GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name) { await Wait(.15); RenderingServer.ForceDraw(); GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_out, name + ".png")); GD.Print("CAPTURE " + name); }
    private static IEnumerable<Node> Walk(Node root) { foreach (var child in root.GetChildren()) { yield return child; foreach (var n in Walk(child)) yield return n; } }
    private Button Button(string text) => Walk(_screen).OfType<Button>().First(b => b.Text == text && b.IsVisibleInTree());
    private static void Click(Control c) { var p = c.GetGlobalRect().GetCenter(); Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p }); Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = true }); Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = false }); }
    private void Search(string text) { _screen.Search.GrabFocus(); _screen.Search.SelectAll(); KeyPress(Key.Backspace); foreach (var ch in text) { Input.ParseInputEvent(new InputEventKey { Unicode = ch, Keycode = (Key)char.ToUpperInvariant(ch), Pressed = true }); Input.ParseInputEvent(new InputEventKey { Unicode = ch, Keycode = (Key)char.ToUpperInvariant(ch), Pressed = false }); } }
    private static void KeyPress(Key key) { Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true }); Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false }); }
    // A checkbox inside a popover Window's global rect does not map back to main-viewport click
    // coordinates, so popover content is activated via keyboard focus + Enter instead of Click().
    private static void Activate(Control control) { control.GrabFocus(); KeyPress(Key.Enter); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); GD.Print("PASS " + message); }
}
