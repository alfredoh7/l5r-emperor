using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

// Native probe (issue #1014): an attachment equipped to a Personality must sit under it from the first frame, never over it. A real engine "equip" drives the beat; the frames
// after it are captured and every card tile of the attachment is checked against the Personality's for draw order wherever the two overlap. Mode: published-attach-order.
public partial class Main
{
    private bool _attachCaptureStarted;
    private void ProcessAttachOrderCapture(double delta)
    {
        _captureClock += delta; if (_attachCaptureStarted || _captureClock < 1.5) return;
        _attachCaptureStarted = true; _ = CaptureAttachOrder();
    }
    /// <summary>True when <paramref name="top"/> draws after <paramref name="under"/> (later in a pre-order walk of the scene tree, or with a higher z index).</summary>
    private static bool DrawsAbove(CanvasItem top, CanvasItem under)
    {
        if (top.ZIndex != under.ZIndex) return top.ZIndex > under.ZIndex;
        var order = new List<Node>(); void Walk(Node n) { order.Add(n); foreach (var c in n.GetChildren()) Walk(c); }
        Walk(top.GetTree().Root);
        return order.IndexOf(top) > order.IndexOf(under);
    }
    /// <summary>Whether the attachment's tiles change any pixel of the Personality's face on screen: the same frame is drawn with and without them. Pixels are compared, so a tile layer, a flight or a showcase over the Personality all count.</summary>
    private static bool OverlaysFace(Main main, CardTile personality, CardTile[] attached, out int differing)
    {
        differing = 0;
        var corners = personality.FaceCornersOnViewport();
        var min = new Vector2(corners.Min(c => c.X), corners.Min(c => c.Y)); var max = new Vector2(corners.Max(c => c.X), corners.Max(c => c.Y));
        var inset = (max - min) * .18f; min += inset; max -= inset;
        var window = main.GetViewport().GetVisibleRect().Size; var rect = new Rect2I(Vector2I.Zero.Max((Vector2I)min), Vector2I.Zero);
        rect = new Rect2I((Vector2I)min, (Vector2I)(max - min));
        if (rect.Size.X < 4 || rect.Size.Y < 4) return false;
        Image Shot() { RenderingServer.ForceDraw(); return main.GetViewport().GetTexture().GetImage().GetRegion(rect); }
        var with = Shot();
        var flights = main._root.GetChildren().OfType<CardFlight>().ToArray();
        foreach (var t in attached) t.Visible = false; foreach (var f in flights) f.Visible = false;
        var without = Shot();
        foreach (var t in attached) t.Visible = true; foreach (var f in flights) f.Visible = true;
        for (var y = 0; y < with.GetHeight(); y++)
            for (var x = 0; x < with.GetWidth(); x++)
            {
                var a = with.GetPixel(x, y); var b = without.GetPixel(x, y);
                if (Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) > .04f) differing++;
            }
        return differing > 0;
    }
    private async Task AttachScenario(string name, bool bowedHero, bool earlierAttachment, int owner = 0, bool selected = false)
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = owner; state.PendingChoice = null;
        SupportFixtureCard(state, 1726, 1 - owner, Zone.Home);
        var hero = SupportFixtureCard(state, 10292, owner, Zone.Home, bowed: bowedHero);
        foreach (var holding in new[] { 6333, 6339, 6333, 6339 }) SupportFixtureCard(state, holding, owner, Zone.Home);
        if (earlierAttachment) SupportFixtureCard(state, CardCatalog.Cards.Values.First(d => !d.ScenarioOnly && d.Type == CardType.Follower).Id, owner, Zone.Attached).AttachedTo = hero.Id;
        var item = SupportFixtureCard(state, 10118, owner, Zone.Hand);
        var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(engine, 0);
        var offered = owner == 0 ? Actions : engine.QueryActions(owner);
        var equip = offered.FirstOrDefault(a => a.Enabled && a.Type == "equip" && a.CardId == item.Id)
            ?? throw new InvalidOperationException(name + ": no enabled equip action: " + string.Join(", ", offered.Where(a => a.Enabled).Select(a => a.Type)));
        await _session.Submit((equip.Command ?? throw new InvalidOperationException("equip has no command")) with { TargetId = hero.Id });
        if (selected) { _selected = FindCard(item.Id); Render(); } // the card a player clicked to equip stays selected on the table afterwards
        var checkedFrames = 0;
        for (var frame = 0; frame < 60; frame++)
        {
            await EventFrames(1);
            var personality = _tiles.FirstOrDefault(t => t.Card.Id == hero.Id && t.IsVisibleInTree());
            var attached = _tiles.Where(t => t.Card.AttachedTo == hero.Id && t.IsVisibleInTree()).ToArray();
            if (personality is null || attached.Length == 0) continue;
            checkedFrames++;
            SupportRequire(!PlayShowcaseBusy, name + ": an attachment was showcased over the table");
            if (OverlaysFace(this, personality, attached, out var differing))
            {
                await SupportCapture("-overlay-" + name + "-" + frame);
                throw new InvalidOperationException($"{name}, frame {frame}: the attachment changes {differing} pixels of its Personality's face (it draws over it).");
            }
            if (frame is 0 or 6) await SupportCapture("-attach-" + name + "-" + frame);
        }
        SupportRequire(checkedFrames >= 20, $"{name}: only {checkedFrames} frames had both the Personality and its attachment on the table");
        SupportRequire(_tiles.Any(t => t.Card.Id == item.Id && t.Card.AttachedTo == hero.Id), name + ": the attachment did not attach");
    }
    private async Task CaptureAttachOrder()
    {
        try
        {
            _preferences.PausedAnimation = false; _preferences.ReducedMotion = false; ApplyPreferences(false);
            await AttachScenario("plain", false, false);
            await AttachScenario("bowed", true, false);
            await AttachScenario("second", false, true);
            await AttachScenario("selected", false, false, selected: true);
            _preferences.ReducedMotion = true; ApplyPreferences(false);
            await AttachScenario("reduced", false, false);
            _preferences.ReducedMotion = false; ApplyPreferences(false);
            await AttachScenario("opponent", false, false, owner: 1);
            GD.Print("ATTACH_ORDER_PASS: the attached card changes no pixel of its Personality's face in any frame after attaching (plain, bowed, second attachment, still selected, reduced motion)");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
