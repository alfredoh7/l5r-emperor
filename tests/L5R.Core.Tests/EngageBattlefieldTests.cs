using L5R.Core;

// Issue #840 (rules lead): Engage is a battlefield-specific reaction, for every card. A Personality, Follower or Item taking an Engage must itself be at the battlefield being engaged, and an Engage
// reaches only cards at that battlefield. Exempt: a Home or Absent Engage, and an Engage whose text names another place (EngageRules.cs lists each).
internal static partial class EngageBattlefieldTests
{
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase, int actor) => Batch336Support.Table(phase, actor);
    private static void Check(bool ok, string message) => Batch336Support.Check(ok, message);

    private static readonly HashSet<string> SourceElsewhere = ["w821-tsumaro-illusion", "wl-sg-recovering-lost", "hayan-assign", "wave-persists-move", "rokku-move", "pf-sled-move", "wl-false-ring-of-void-lock"];

    private static IEnumerable<(CardDefinition Card, string Id, AbilityDefinition Ability)> EngageReactions() =>
        CardCatalog.Cards.Values.Where(c => !c.CreatedOnly).SelectMany(c => (c.Abilities ?? []).Select(id => (Card: c, Id: id, Ability: AbilityCatalog.All.TryGetValue(id, out var a) ? a : null)))
            .Where(x => x.Ability is { Timing: "Reaction", Trigger: "after-engaging" or "after-engaging-at-own-battlefield" })
            .Select(x => (x.Card, x.Id, x.Ability!));

    // Player 0 attacks province 0 of player 1. `arrange` places the cards under test; both sides always have a Personality at the battlefield so the battle is a real one.
    private static MatchEngine Engaged(int owner, Action<MatchState> arrange)
    {
        var s = Table(Phase.BattleSelection, 0);
        s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        for (var i = 0; i < 6; i++) { Add(s, "training-marketplace", 0, Zone.Home); Add(s, "training-marketplace", 1, Zone.Home); }
        arrange(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        return e;
    }
    private static void Accept(MatchEngine e, GameCommand c) { var r = e.Execute(c); Check(r.Accepted, r.Error ?? "rejected"); }
    private static bool Offered(MatchEngine e, int player, string ability, string cardId) => e.QueryActions(player).Any(a => a.Enabled && a.CardId == cardId && a.Command?.AbilityId == ability);

    private static void UnitCardsAwayFromTheBattlefieldAreNotOffered()
    {
        var checkedCards = 0;
        foreach (var (card, id, ability) in EngageReactions().Where(x => x.Card.Type is CardType.Personality or CardType.Follower or CardType.Item && !SourceElsewhere.Contains(x.Id) && !x.Ability.Home && !x.Ability.Absent))
            foreach (var owner in new[] { 0, 1 })
            {
                string source = "";
                var e = Engaged(owner, s =>
                {
                    var home = Add(s, "training-crane-samurai", owner, Zone.Home);
                    source = (card.Type == CardType.Personality ? Add(s, card.Id, owner, Zone.Home) : Add(s, card.Id, owner, Zone.Attached, parent: home.Id)).Id;
                });
                Check(!Offered(e, owner, id, source), $"{card.Name} {id}: a unit card away from the battlefield must not be offered an Engage (owner {owner})");
                checkedCards++;
            }
        Check(checkedCards >= 14, "the sweep covered the Engage unit cards: " + checkedCards);
    }

    private static void EngageTargetsAreAtTheBattlefield()
    {
        // (source card, ability, owner of the targets, whether the source is an attachment/Personality in play or a Strategy in hand)
        foreach (var (def, ability, targetOwner) in new[]
        {
            ("oracle-l5r-10677", "wl-bestial-engage", 1), ("oracle-l5r-10498", "wl-pc-takaki-madness", 1), ("oracle-l5r-10498", "wl-pc-takaki-madness", 0),
            ("training-engage-destroy-strategy", "training-engage-destroy", 1), ("training-engage-political-claim-strategy", "training-engage-claim-political", 1), ("oracle-l5r-10724", "proxy677-cavalry-tactics", 0),
        })
        {
            var def0 = CardCatalog.Get(def);
            Check((def0.Abilities ?? []).Contains(ability), $"{def} carries {ability}");
            CardInstance there = null!, away = null!; string src = "";
            var e = Engaged(0, s =>
            {
                if (def0.Type == CardType.Personality) src = Add(s, def, 0, Zone.Battlefield, province: 0).Id;
                else if (def0.Type is CardType.Follower or CardType.Item) { var h = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); src = Add(s, def, 0, Zone.Attached, province: 0, parent: h.Id).Id; }
                else src = Add(s, def, 0, Zone.Hand).Id;
                there = Add(s, "training-lion-samurai", targetOwner, Zone.Battlefield, province: 0);
                away = Add(s, "training-lion-samurai", targetOwner, Zone.Home);
            });
            var option = e.QueryActions(0).FirstOrDefault(a => a.Enabled && a.CardId == src && a.Command?.AbilityId == ability);
            Check(option is not null, $"{ability}: offered with a target at the battlefield");
            var targets = option!.TargetIds ?? [];
            Check(targets.Contains(there.Id) && !targets.Contains(away.Id), $"{ability}: only a card at the battlefield is a target: " + string.Join(",", targets));
        }
    }

    public static void Run(TestRunner runner) => runner.Run("engage-battlefield", [
        ("A Personality, Follower or Item taking an Engage must be at the engaged battlefield: away from it no Engage is offered (every such card; issue #840)", UnitCardsAwayFromTheBattlefieldAreNotOffered),
        ("An Engage reaches only cards at the engaged battlefield: The Bestial Ones, Iuchi Takaki and the Engage fixtures offer no target at home (issue #840)", EngageTargetsAreAtTheBattlefield),
    ]);
}
