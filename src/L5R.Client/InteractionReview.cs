using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

/// <summary>Opt-in prepared review/native checks. Fixture injection never runs in a normal match.</summary>
public partial class InteractionReview : Node
{
    private readonly Main _main;
    private readonly string _mode, _capture;
    private MatchSession _session = null!;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private bool _checking;
    private int _seat;
    private OptionButton _cases = null!;
    private HBoxContainer? _toolbar;
    private readonly (string Label, int Limit, int Count)[] _examples =
    [ ("Hand limit 10 · 11 cards",10,11), ("Hand limit 7 · 8 cards",7,8), ("Hand limit 8 · 9 cards",8,9),
      ("Hand limit 10 · 13 cards",10,13), ("Hand limit 10 · 10 cards",10,10), ("Pass the device",8,3) ];
    public InteractionReview(Main main, string mode, string capture) { _main = main; _mode = mode; _capture = capture; }
    private T Field<T>(string name) => (T)typeof(Main).GetField(name, Private)!.GetValue(_main)!;
    private void Set(string name, object? value) => typeof(Main).GetField(name, Private)!.SetValue(_main, value);
    private object? Call(string name, params object?[] args) => typeof(Main).GetMethod(name, Private)!.Invoke(_main, args);
    private MatchEngine Engine => (MatchEngine)typeof(MatchSession).GetField("_engine", Private)!.GetValue(_session)!;
    private static IEnumerable<T> Descendants<T>(Node node) where T : Node => node.GetChildren().SelectMany(n => (n is T t ? new[] { t } : []).Concat(Descendants<T>(n)));
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private async Task Wait(double seconds = .16) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private Button Button(string text) => Descendants<Button>(_main).First(b => b.Text == text && b.IsVisibleInTree());
    private async Task Click(Control control)
    {
        Check(control.IsVisibleInTree(), "Control is hidden");
        for (var parent = control.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(control);
        await Wait();
        var point = (Vector2)Call("ScreenCenter", control)!;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await Wait(.04);
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await Wait();
    }
    private async Task Key(Window window, Godot.Key key, int repeats = 1)
    {
        window.GrabFocus();
        if (key == Godot.Key.Escape)
        {
            // Escape is handled by the embedded-window manager before child GUI input.
            Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
        }
        else
        {
            for (var i = 0; i < repeats; i++)
            {
                window.PushInput(new InputEventKey { Keycode = key, Pressed = true }, true);
                window.PushInput(new InputEventKey { Keycode = key, Pressed = false }, true);
            }
        }
        await Wait();
    }
    private async Task Capture(string suffix)
    {
        Call("HideTableTooltip"); await Wait();
        RenderingServer.ForceDraw(false); RenderingServer.ForceSync();
        var path = Path.Combine(Path.GetDirectoryName(_capture)!, Path.GetFileNameWithoutExtension(_capture) + "-" + suffix + ".png");
        GetViewport().GetTexture().GetImage().SavePng(path);
    }
    private static CardInstance Add(MatchState s, string definition, int owner, Zone zone)
    {
        var c = new CardInstance { Id = "interaction-review-" + s.Cards.Count, DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        if (zone == Zone.FateDeck) s.Players[owner].FateDeck.Add(c.Id);
        return c;
    }
    private async Task Load(int limit, int count, int seat, bool pending = true)
    {
        Call("StartFromMenu", seat); Set("_autoOpponent", false); await Wait();
        var s = MatchEngine.CreatePublishedTestMatch(463).State;
        foreach (var c in s.Cards.Values.Where(c => c.Zone != Zone.Stronghold).ToArray()) s.Cards.Remove(c.Id);
        foreach (var p in s.Players) { p.Hand.Clear(); p.FateDeck.Clear(); p.DynastyDeck.Clear(); p.TurnsTaken = 2; foreach (var v in p.Provinces) v.CardId = null; }
        s.ActivePlayerId = s.PriorityPlayerId = seat; s.Phase = pending ? Phase.Dynasty : Phase.Action; s.PendingChoice = null; s.Events.Clear();
        if (limit == 7) Add(s, "oracle-l5r-10532", 1 - seat, Zone.Home);
        if (limit == 10) for (var i = 0; i < 2; i++) Add(s, PublishedContent.PersonalityWave13.Cards.Single(c => c.Name == "Asahina Tsugio").Id, 1 - seat, Zone.Home);
        var cards = new[] { "oracle-l5r-2619", "oracle-l5r-6051", "oracle-l5r-9916" };
        for (var i = 0; i < (pending ? count - 1 : count); i++) Add(s, cards[i % cards.Length], seat, Zone.Hand);
        for (var i = 0; i < 3; i++) Add(s, cards[(i + 1) % cards.Length], 1 - seat, Zone.Hand);
        for (var p = 0; p < 2; p++) for (var i = 0; i < 10; i++) Add(s, cards[i % cards.Length], p, Zone.FateDeck);
        var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [s])!;
        typeof(MatchSession).GetField("_engine", Private)!.SetValue(_session, engine);
        typeof(MatchSession).GetField("_localSeat", Private)!.SetValue(_session, seat);
        Set("_presentationSnapshot", null); Set("_motionView", null); Set("_battlePresentationStage", 0);
        Field<TurnBloom>("_turnBloom").Cancel();
        typeof(MatchSession).GetMethod("RefreshLocal", Private)!.Invoke(_session, null);
        if (pending) await _session.Submit(new GameCommand("endDynasty", seat, engine.Version));
        await Wait(.35); Call("Render"); Call("ResetTableCamera"); Field<TurnBloom>("_turnBloom").Cancel();
        if (pending && engine.State.PendingChoice?.Type == "discardHand") Call("RenderInspector"); // issue #791: the discard begins by itself, announced by the ink banner
        await Wait(.25);
    }
    private async Task LoadExample()
    {
        var e = _examples[_cases.Selected]; await Load(e.Limit, e.Count, _seat, _cases.Selected != 5);
        await Capture("ready");
    }
    public override void _Process(double delta)
    {
        if (_toolbar is null) return;
        _toolbar.Visible = Field<Control>("_root").Visible;
        var width = GetViewport().GetVisibleRect().Size.X;
        _toolbar.Scale = Vector2.One * Mathf.Min(1f, (width - 570) / 680f);
        _toolbar.Position = new Vector2(width * .23f, GetViewport().GetVisibleRect().Size.Y * .17f);
    }
    private static void ReviewStyle(Button button)
    {
        button.AddThemeFontSizeOverride("font_size", 18);
        foreach (var name in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            button.AddThemeStyleboxOverride(name, new StyleBoxFlat { BgColor = new Color(.065f, .08f, .07f, .96f), BorderColor = new Color(.48f, .40f, .24f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1, ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8 });
    }
    public override async void _Ready()
    {
        try
        {
            GetWindow().Title = "L5R · Interaction Review";
            Directory.CreateDirectory(Path.GetDirectoryName(_capture)!);
            _session = Field<MatchSession>("_session");
            Set("_autoOpponent", false); Set("_reducedMotion", _mode.Contains("reduced"));
            _checking = _mode.Contains("check");
            if (_checking)
            {
                GetWindow().GrabFocus(); await Wait(.8);
                if (!_mode.Contains("discard-only")) await CheckHandoff();
                if (!_mode.Contains("privacy-only")) await CheckDiscard();
                GD.Print("INTERACTION_CHECK_PASS: " + _mode); GetTree().Quit(); return;
            }
            var layer = new CanvasLayer { Layer = 30 }; AddChild(layer);
            var row = new HBoxContainer(); _toolbar = row; layer.AddChild(row);
            _cases = new OptionButton { CustomMinimumSize = new Vector2(440, 45), Theme = SidebarSkin.Theme() };
            foreach (var e in _examples) _cases.AddItem(e.Label);
            _cases.Selected = _mode.Contains("handoff") ? 5 : 0; ReviewStyle(_cases); row.AddChild(_cases);
            _cases.ItemSelected += async _ => await LoadExample();
            var reset = new Button { Text = "Reset", CustomMinimumSize = new Vector2(90, 45) }; ReviewStyle(reset); row.AddChild(reset); reset.Pressed += async () => await LoadExample();
            var other = new Button { Text = "Other seat", CustomMinimumSize = new Vector2(120, 45) }; ReviewStyle(other); row.AddChild(other); other.Pressed += async () => { _seat = 1 - _seat; await LoadExample(); };
            await LoadExample(); GD.Print("INTERACTION_REVIEW_READY: real engine fixture; no automated human actions.");
        }
        catch (Exception e) { GD.PushError("INTERACTION_CHECK_FAIL: " + e); GetTree().Quit(1); }
    }
    private async Task<AcceptDialog> OpenHandoff()
    {
        GetWindow().GrabFocus();
        await Click(Button("Pass device to other player"));
        var dialog = Descendants<AcceptDialog>(_main).Single(d => d.Title == "Pass the device");
        Check(!Field<Control>("_root").Visible, "Private table must be covered while handing off");
        Check(!Field<Control>("_hand").IsVisibleInTree(), "Hand remained visible during handoff"); return dialog;
    }
    private async Task CheckHandoff()
    {
        foreach (var seat in new[] { 0, 1 })
        {
            GD.Print("HANDOFF_BEGIN: seat " + seat);
            await Load(8, 3, seat, false); var original = _session.Snapshot!.View.Players[seat].Hand.Select(c => c.Id).ToArray();
            var dialog = await OpenHandoff(); await Capture("covered-seat-" + seat);
            // Exercise the window-close signal independently of the readiness button.
            dialog.EmitSignal(Window.SignalName.CloseRequested); await Wait();
            Check(_session.Snapshot!.View.ViewerId == seat && Field<Control>("_root").Visible, "Window close switched seats or left the original seat covered");
            GD.Print("HANDOFF_CLOSED: seat " + seat);
            dialog = await OpenHandoff(); await Key(dialog, Godot.Key.Escape);
            Check(_session.Snapshot.View.ViewerId == seat, "Escape switched seats and exposed the receiving hand");
            Check(Field<Control>("_root").Visible, $"Escape did not restore original seat; dialog valid={IsInstanceValid(dialog)}, visible={(IsInstanceValid(dialog) ? dialog.Visible : false)}");
            Check(_session.Snapshot.View.Players[seat].Hand.Select(c => c.Id).SequenceEqual(original), "Cancellation changed the original hand");
            GD.Print("HANDOFF_ESCAPED: seat " + seat);
            dialog = await OpenHandoff();
            Call("SwitchSeat"); Check(Descendants<AcceptDialog>(_main).Count(d => d.Title == "Pass the device") == 1, "Repeated handoff opened a second dialog");
            var cancel = Descendants<Button>(dialog).Single(b => b.Text == "Cancel handoff"); cancel.GrabFocus(); await Key(dialog, Godot.Key.Enter);
            Check(_session.Snapshot.View.ViewerId == seat && Field<Control>("_root").Visible, "Cancel handoff changed seats");
            dialog = await OpenHandoff(); dialog.QueueFree(); await Wait();
            Check(_session.Snapshot.View.ViewerId == seat, "Dialog cleanup switched seats");
            dialog = await OpenHandoff(); var ready = Descendants<Button>(dialog).Single(b => b.Text.StartsWith("I'm ready")); ready.GrabFocus(); await Key(dialog, Godot.Key.Enter, 2);
            Check(_session.Snapshot.View.ViewerId == 1 - seat && Field<Control>("_root").Visible, "Explicit readiness did not reveal the intended seat exactly once");
            Check(_session.Snapshot.View.Players[seat].Hand.Length == 0, "Prior private hand remained in receiving view");
            await Capture("revealed-seat-" + (1 - seat));
            await Load(8, 3, seat, false); dialog = await OpenHandoff();
            _session.StartPractice(false, true, seat); await Wait();
            if (IsInstanceValid(dialog) && dialog.IsInsideTree()) { var stale = Descendants<Button>(dialog).FirstOrDefault(b => b.Text.StartsWith("I'm ready")); if (stale is not null) { stale.GrabFocus(); await Key(dialog, Godot.Key.Enter); } }
            Check(_session.Snapshot!.View.ViewerId == seat, "Old handoff switched a replacement session");
            GD.Print("HANDOFF_PASS: seat " + seat + " Escape, close, cleanup, explicit readiness, session replacement");
        }
    }
    // Hand cards fan over one another, so a player clicks a card's visible left edge, not its centre.
    private async Task ClickCardEdge(CardTile tile)
    {
        Check(tile.IsVisibleInTree(), "Card is hidden");
        var corners = tile.FaceCornersOnViewport(); var centre = (Vector2)Call("ScreenCenter", tile)!;
        var left = corners.OrderBy(c => c.X).Take(2).Aggregate(Vector2.Zero, (sum, c) => sum + c) / 2;
        var point = centre.Lerp(left, .7f);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await Wait(.04);
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await Wait();
    }
    private async Task CheckDiscard()
    {
        bool ConfirmShown() => Descendants<Button>(_main).Any(b => b.IsVisibleInTree() && b.Text == "Confirm discard");
        foreach (var limit in new[] { 7, 8, 10 }) foreach (var seat in new[] { 0, 1 })
        {
            await Load(limit, limit + 2, seat); var before = Engine.State.Players[seat].Hand.ToArray(); var version = Engine.Version;
            var bloom = Field<TurnBloom>("_turnBloom");
            // The probe swaps in a brand-new engine, so the turn-change observer plays "YOUR TURN" after the announcement; start the discard again as a live game would.
            Set("_discardAnnouncedKey", null); Set("_chosen", null); Call("RenderInspector"); await Wait(.25);
            Check(bloom.Text == "DISCARD TO " + limit && bloom.IsAnnouncement, "The discard was not announced with the ink banner: " + bloom.Text);
            Check(!ConfirmShown(), "Confirm discard is offered before any card is selected");
            var tiles = before.Select(id => Field<List<CardTile>>("_tiles").Single(t => t.Card.Id == id)).ToArray();
            Check(Descendants<CheckBox>(Field<VBoxContainer>("_inspector")).Count() == 0, "The old checkbox list is still shown");
            await ClickCardEdge(tiles[^1]); Check(tiles[^1].DecisionSelected, "Native click did not select the last card"); Check(!ConfirmShown(), "One selection wrongly satisfies a two-card discard");
            await ClickCardEdge(tiles[^2]); Check(tiles[^2].DecisionSelected, "Native click did not select the second-last card"); Check(ConfirmShown(), "Two selections should satisfy the authoritative discard requirement");
            await ClickCardEdge(tiles[^3]); Check(!tiles[^3].DecisionSelected && ConfirmShown(), "A third card was selected beyond the required count");
            await ClickCardEdge(tiles[^2]); Check(!tiles[^2].DecisionSelected && !ConfirmShown(), "Clicking a selected card did not deselect it");
            await ClickCardEdge(tiles[^2]); Check(tiles[^2].DecisionSelected && ConfirmShown(), "Reselecting the card did not restore Confirm");
            Check(Engine.Version == version, "Selecting cards submitted a command before confirmation"); await Capture("discard-" + limit + "-seat-" + seat);
            await Click(Button("Confirm discard")); await Wait(.3);
            Check(Engine.State.Players[seat].Hand.SequenceEqual(before.Take(before.Length - 2)), "UI discarded the wrong cards or wrong count");
            Check(Engine.State.ActivePlayerId == 1 - seat, "Valid discard failed to finish the turn");
            GD.Print("DISCARD_PASS: limit " + limit + ", seat " + seat + ", banner, exact native hand selection and engine result");
        }
        await Load(10, 10, 0); Check(!Descendants<Button>(_main).Any(b => b.IsVisibleInTree() && b.Text == "Confirm discard"), "At-limit hand incorrectly requires a discard");
    }
}
