using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Issue #630: every timing, size and switch of the "card played" showcase in one place so it can be tuned in play.</summary>
internal static class CardPlayTiming
{
    /// <summary>Seconds the card eases in (fade, and a small grow unless reduced motion is on).</summary>
    public const double Intro = .42;
    /// <summary>Seconds a card played from a hand is held face-up before it travels to the discard pile.</summary>
    public const double Hold = 2.04;
    /// <summary>Seconds a card that stays in play (an ability of a card on the table) is held.</summary>
    public const double BoardHold = 1.44;
    /// <summary>Seconds the card takes to travel to the discard pile (or just fade, for a card that stays in play or under reduced motion).</summary>
    public const double Outro = .96;
    /// <summary>With reduced motion the card simply fades in, holds, and fades out, for this many seconds in all.</summary>
    public const double ReducedHold = 1.68;
    /// <summary>Hold is multiplied by this while more plays are waiting behind it.</summary>
    public const double QueuedHoldScale = .6;
    /// <summary>Card height in the 1920x1080 presentation space, and where its centre sits (over the table, left of the sidebar).</summary>
    public const float CardHeight = 440;
    public static readonly Vector2 Center = new(865, 500);
    /// <summary>False: only the other player's plays are shown (you already know what you just did).</summary>
    public const bool ShowOwnPlays = false;
    /// <summary>True: an ability used by a card that stays on the table (a Personality, Item, Holding...) is shown too, so the cause of a change is visible.</summary>
    public const bool ShowBoardAbilities = true;
}

// Issue #630 (Mendon's playtest): "when an opponent plays a card, show the card being played and then moved to the discard pile". Driven only by the
// public event log (strategyPlayed / abilityUsed carry the player and the card id) and the card's public face once it is played, so nothing hidden is
// shown early. The practice opponent waits while a beat is showing (PlayShowcaseBusy), so its next move cannot cut the card short.
public partial class Main
{
    private sealed record PlayBeat(CardView Card, string Caption, bool Discarded);

    private string? _playsMatchKey;
    private long _playsSeen = -1;
    private readonly Queue<PlayBeat> _playQueue = [];
    private Control? _playCurrent;
    /// <summary>Played cards this update is showcasing: the older quick discard flight skips them, so a card is not shown twice.</summary>
    private readonly HashSet<string> _showcasedPlays = [];

    private bool PlayShowcaseBusy => _playQueue.Count > 0 || _playCurrent is not null && IsInstanceValid(_playCurrent);

    private void ObserveCardPlays()
    {
        _showcasedPlays.Clear();
        if (_session.Snapshot is null) { ClearCardPlays(); return; }
        if (_playsMatchKey != _session.MatchKey)
        {
            // A new match (or a reconnect): never replay history, only what happens from here.
            ClearCardPlays();
            _playsMatchKey = _session.MatchKey; _playsSeen = View.Events.LastOrDefault()?.Sequence ?? -1;
            return;
        }
        var fresh = View.Events.Where(e => e.Sequence > _playsSeen).ToArray();
        if (fresh.Length > 0) _playsSeen = fresh[^1].Sequence;
        var cards = VisibleCards(View).Concat(View.Players.SelectMany(p => p.EventsInPlay ?? [])).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        var queued = new HashSet<string>();
        foreach (var e in fresh)
        {
            if (e.Type is not ("strategyPlayed" or "abilityUsed") || e.CardId is not string id || e.PlayerId is not int who) continue;
            if (who == View.ViewerId && !CardPlayTiming.ShowOwnPlays) continue;
            if (!cards.TryGetValue(id, out var card) || card.Definition is null || !queued.Add(id)) continue;
            var discarded = card.Zone is Zone.FateDiscard or Zone.DynastyDiscard;
            if (!discarded && !CardPlayTiming.ShowBoardAbilities) continue;
            if (discarded) _showcasedPlays.Add(id);
            _playQueue.Enqueue(new(card, e.Message, discarded));
        }
        if (_playCurrent is null || !IsInstanceValid(_playCurrent)) StartNextCardPlay();
    }

    private void ClearCardPlays()
    {
        _playQueue.Clear();
        if (_playCurrent is not null && IsInstanceValid(_playCurrent)) _playCurrent.QueueFree();
        _playCurrent = null; _playsMatchKey = null; _playsSeen = -1;
    }

    private void StartNextCardPlay()
    {
        _playCurrent = null;
        if (_playQueue.Count == 0) return;
        var beat = _playQueue.Dequeue();
        var height = CardPlayTiming.CardHeight; var width = height * .72f;
        var stage = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 60, Modulate = new Color(1, 1, 1, 0) };
        _root.AddChild(stage);
        stage.Size = new Vector2(620, height + 120); stage.Position = CardPlayTiming.Center - new Vector2(stage.Size.X / 2, height / 2);
        var face = CardPreview.Face(beat.Card, height, width);
        face.MouseFilter = Control.MouseFilterEnum.Ignore;
        face.Size = new Vector2(width, height); face.Position = new Vector2((stage.Size.X - width) / 2, 0);
        face.PivotOffset = face.Size / 2;
        stage.AddChild(face);
        var caption = Palette.Text(beat.Caption, 17, Palette.Gold);
        caption.HorizontalAlignment = HorizontalAlignment.Center; caption.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        caption.MouseFilter = Control.MouseFilterEnum.Ignore;
        caption.Position = new Vector2(10, height + 14); caption.Size = new Vector2(stage.Size.X - 20, 90);
        stage.AddChild(caption);
        _playCurrent = stage;

        var waiting = _playQueue.Count > 0;
        var hold = (beat.Discarded ? CardPlayTiming.Hold : CardPlayTiming.BoardHold) * (waiting ? CardPlayTiming.QueuedHoldScale : 1);
        var tween = stage.CreateTween();
        if (_reducedMotion)
        {
            tween.TweenProperty(stage, "modulate:a", 1.0, .15);
            tween.TweenInterval(Math.Max(.2, CardPlayTiming.ReducedHold * (waiting ? CardPlayTiming.QueuedHoldScale : 1) - .3));
            tween.TweenProperty(stage, "modulate:a", 0.0, .15);
        }
        else
        {
            face.Scale = new Vector2(.7f, .7f);
            tween.SetParallel(true);
            tween.TweenProperty(stage, "modulate:a", 1.0, CardPlayTiming.Intro);
            tween.TweenProperty(face, "scale", Vector2.One, CardPlayTiming.Intro).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.SetParallel(false);
            tween.TweenInterval(hold);
            // The outro is built when the hold ends so the pile's final position is read then, not when the card arrived.
            tween.TweenCallback(Callable.From(() => PlayCardOutro(stage, face, caption, beat)));
            return;
        }
        tween.TweenCallback(Callable.From(() => FinishCardPlay(stage)));
    }

    private void PlayCardOutro(Control stage, Control face, Control caption, PlayBeat beat)
    {
        if (!IsInstanceValid(stage)) return;
        var pile = beat.Discarded ? TablePiles.FirstOrDefault(p => p.OwnerId == beat.Card.OwnerId && p.PileZone == beat.Card.Zone) : null;
        var tween = stage.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(caption, "modulate:a", 0.0, CardPlayTiming.Outro * .4);
        if (pile is not null)
        {
            // Travel to the pile's face on screen, shrinking as it goes, and fade out as it lands.
            var corners = PileCorners(pile);
            var target = corners.Aggregate(Vector2.Zero, (a, b) => a + b) / corners.Length;
            var pileWidth = (corners[1] - corners[0]).Length();
            var shrink = Mathf.Clamp(pileWidth / (CardPlayTiming.CardHeight * .72f), .1f, .6f);
            var faceCentre = stage.Position + face.Position + face.Size / 2;
            tween.TweenProperty(stage, "position", stage.Position + (target - faceCentre), CardPlayTiming.Outro).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(face, "scale", new Vector2(shrink, shrink), CardPlayTiming.Outro).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(stage, "modulate:a", 0.0, CardPlayTiming.Outro * .3).SetDelay(CardPlayTiming.Outro * .7);
        }
        else tween.TweenProperty(stage, "modulate:a", 0.0, CardPlayTiming.Outro * .6);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(() => FinishCardPlay(stage)));
    }

    private void FinishCardPlay(Control stage)
    {
        if (IsInstanceValid(stage)) stage.QueueFree();
        if (_playCurrent == stage) StartNextCardPlay();
    }
}
