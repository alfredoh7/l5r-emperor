using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #419 (stage 5 of Mendon's rulings of 2026-09-21), third part: sequenced targets and effects -- one action whose second target is chosen only after its first effect has resolved.
// Engulfing Flames: an optional enemy attachment is destroyed and then, if the Monk has any Fire tokens, a Ranged 4 Attack is aimed at what is a legal target NOW.
internal static class SequencedTargetTests
{
    private const string EngulfingFlames = "oracle-l5r-2342";

    public static void Run(TestRunner runner) => runner.Run("sequenced-target", [
        ("Published shape: Engulfing Flames is the printed card with its one ability and art; the Attack half is its own ability id and is never offered as an action", PublishedShape),
        ("The second target is judged after the first effect: destroying the Follower that shielded a Personality makes him a legal Attack target, which he was not when the action was announced", SecondTargetAfterFirstEffect),
        ("Without Fire tokens only the first effect happens and nobody is asked; the first target is optional, and the second may be nothing", Variations),
        ("One action, two effects: a whole-action negation negates both, and a negated destruction still leaves the Attack to happen", NegationSemantics),
        ("Who may perform it and what may be targeted: an unbowed Monk of the player's, an enemy attachment only, and only the acting player answers the second choice", Legality),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void Fire(CardInstance card) => card.Tokens.Add(new CardToken("token-fire-" + card.Id, "Fire", "test", "Force", 1));

    private static void PublishedShape()
    {
        var card = CardCatalog.Get(EngulfingFlames);
        Check(card.Name == "Engulfing Flames" && card.Type == CardType.Strategy && card.Focus == 1 && card.Keywords!.Contains("Fire") && card.Keywords.Contains("Kiho")
            && card.Abilities!.SequenceEqual(new[] { "engulfing-flames-destroy" }), "Engulfing Flames");
        Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", EngulfingFlames + ".jpg")), "art is shipped");
        Eq(card.ArtworkKey, $"res://Content/Published/{EngulfingFlames}.jpg");
        var attack = AbilityCatalog.All["engulfing-flames-attack"];
        Check(attack.Target == "attack" && !attack.AttackTargetsProvince, "the Attack half is an Attack ability; the card does not name a province");
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var monk = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(monk, "Monk"); Fire(monk);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(s, EngulfingFlames, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "engulfing-flames-attack").Length == 0, "the Attack half is not offered as an action of its own");
    }

    // Player 0's Monk (Fire tokens as asked) faces player 1's E, who carries a Follower F: a Personality with an attached Follower is immune to a Ranged Attack until F is gone.
    private static (MatchEngine E, CardInstance Card, CardInstance Monk, CardInstance Followed, CardInstance F, CardInstance Bystander) Setup(bool fire = true)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var monk = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(monk, "Monk"); if (fire) Fire(monk);
        var followed = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: followed.Id);
        var bystander = Add(s, "training-water-personality", 1, Zone.Battlefield, province: 0);
        var card = Add(s, EngulfingFlames, 0, Zone.Hand);
        return (MatchEngine.FromTestState(s), card, monk, followed, follower, bystander);
    }

    private static GameCommand Play(MatchEngine e, CardInstance card, CardInstance monk, params string[] attachments)
    {
        var option = Enabled(e, 0, "engulfing-flames-destroy", card.Id, monk.Id);
        return option.Command! with { Selections = [Pick("attachment", attachments)] };
    }

    private static void SecondTargetAfterFirstEffect()
    {
        var (e, card, monk, followed, follower, bystander) = Setup();
        // At announcement the followed Personality is not a legal Attack target (the option's slot lists attachments only, and an Attack would refuse him).
        var attackable = e.QueryActions(0).Where(x => x.Type == "useAbility").Any(x => x.Command?.TargetId == followed.Id);
        Check(!attackable, "he is nobody's target now");
        Accept(e, Play(e, card, monk, follower.Id));
        Check(e.State.Cards[follower.Id].Zone == Zone.FateDiscard, "the first effect: the Follower was destroyed");
        Eq(e.State.PendingChoice?.Type, "sequencedTarget", "the second target is asked now");
        Eq(e.State.PendingChoice?.PlayerId, 0, "of the acting player");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent while the choice is open");
        var picks = e.QueryActions(0).Where(x => x.Type == "chooseSequencedTarget").ToArray();
        Check(picks.Any(x => x.CardId == followed.Id), "the Personality the Follower shielded is a target now: " + string.Join(",", picks.Select(x => x.Label)));
        Check(picks.Any(x => x.CardId == bystander.Id) && picks.Any(x => x.Command!.Choice == "none") && !picks.Any(x => x.Command!.Choice == "province"), "and so are the others, and no target; a province is not offered");
        // Ranged 4: a card of Force 4 may be attacked, one of Force 5 may not.
        var exact = Edit(e, st => { var x = Add(st, "training-water-personality", 1, Zone.Battlefield, province: 0); x.Modifiers.Add(new("test", "test", "Force", 4 - CardCatalog.Get(x.DefinitionId).Force)); var y = Add(st, "training-water-personality", 1, Zone.Battlefield, province: 0); y.Modifiers.Add(new("test", "test", "Force", 5 - CardCatalog.Get(y.DefinitionId).Force)); });
        var forces = exact.QueryActions(0).Where(x => x.Type == "chooseSequencedTarget" && x.CardId is not null).Select(x => exact.CardForceForTest(x.CardId!)).ToArray();
        Check(forces.Contains(4) && !forces.Any(f => f > 4), "strength 4: Force 4 is a target, nothing above it is: " + string.Join(",", forces));
        Check(!e.QueryActions(1).Any(x => x.Type == "chooseSequencedTarget"), "the other player has nothing to choose");
        Reject(e, new GameCommand("chooseSequencedTarget", 1, e.Version, TargetId: followed.Id));
        Reject(e, new GameCommand("chooseSequencedTarget", 0, e.Version, TargetId: monk.Id));
        Accept(e, new GameCommand("chooseSequencedTarget", 0, e.Version, TargetId: followed.Id));
        // The Attack goes through its own pipeline: nobody holds a reaction, so it resolves at once, destroying a Force-2 Personality against strength 4.
        Check(e.State.PendingChoice is null || e.State.PendingChoice.Type != "sequencedTarget", "the choice is answered");
        Check(e.State.Cards[followed.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "the second effect: the Ranged 4 Attack destroyed him");
        Check(e.State.PendingSequencedTarget is null, "and nothing is left pending");
    }

    private static void Variations()
    {
        // No Fire tokens: the first effect only, nobody asked.
        var (e, card, monk, _, follower, _) = Setup(fire: false);
        Accept(e, Play(e, card, monk, follower.Id));
        Check(e.State.Cards[follower.Id].Zone == Zone.FateDiscard, "the attachment was destroyed");
        Check(e.State.PendingChoice is null && e.State.PendingSequencedTarget is null, "with no Fire tokens there is no Attack and nothing is asked");
        // The first target is optional: none chosen, the Attack still follows.
        var (f, card2, monk2, followed2, follower2, _) = Setup();
        Accept(f, Play(f, card2, monk2));
        Check(f.State.Cards[follower2.Id].Zone == Zone.Attached, "nothing was destroyed");
        Eq(f.State.PendingChoice?.Type, "sequencedTarget", "the Attack is still aimed");
        Check(!f.QueryActions(0).Where(x => x.Type == "chooseSequencedTarget").Any(x => x.CardId == followed2.Id), "and the still-shielded Personality is not a target");
        Reject(f, new GameCommand("chooseSequencedTarget", 0, f.Version, Choice: "province"));
        Accept(f, new GameCommand("chooseSequencedTarget", 0, f.Version, TargetId: f.QueryActions(0).First(x => x.Type == "chooseSequencedTarget" && x.CardId is not null).CardId));
        Check(f.State.PendingChoice is null, "a chosen target: the Attack resolved");
        // No target at all: nothing is destroyed, the action completes and priority moves on.
        var (g, card3, monk3, followed3, follower3, bystander3) = Setup();
        Accept(g, Play(g, card3, monk3, follower3.Id));
        Accept(g, g.QueryActions(0).Single(x => x.Type == "chooseSequencedTarget" && x.Command!.Choice == "none").Command!);
        Check(g.State.PendingChoice is null && g.State.Cards[followed3.Id].Zone == Zone.Battlefield && g.State.Cards[bystander3.Id].Zone == Zone.Battlefield, "no Attack target: nothing further is destroyed");
        Eq(g.State.PriorityPlayerId, 1, "and the action is over: the other player has the opportunity");
    }

    private static void NegationSemantics()
    {
        // The whole action negated: it is taken and its cost paid, but neither part happens.
        var (e, card, monk, followed, follower, _) = Setup();
        e.NegateEveryAction = true;
        Accept(e, Play(e, card, monk, follower.Id));
        Check(e.State.Cards[follower.Id].Zone == Zone.Attached, "the destruction did not happen");
        Check(e.State.PendingChoice is null && e.State.PendingSequencedTarget is null, "and no second target is asked");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the action was taken");
        // The destruction negated: the Attack still follows (it does not depend on the destruction).
        var (f, card2, monk2, followed2, follower2, bystander2) = Setup();
        var state = f.State; state.NegatedEffects.Add(new("destroy", follower2.Id));
        var g = MatchEngine.FromTestState(state);
        Accept(g, Play(g, card2, monk2, follower2.Id));
        Check(g.State.Cards[follower2.Id].Zone == Zone.Attached, "the destruction was negated: the Follower stays");
        Eq(g.State.PendingChoice?.Type, "sequencedTarget", "and the second effect still comes");
        Check(!g.QueryActions(0).Where(x => x.Type == "chooseSequencedTarget").Any(x => x.CardId == followed2.Id), "aimed at what is legal now: the Personality is still shielded");
        Accept(g, new GameCommand("chooseSequencedTarget", 0, g.Version, TargetId: bystander2.Id));
        Check(g.State.Cards[bystander2.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "the Attack happened");
        // A negation already standing against the Attack's own target still applies to it: the negations the action began with are the ones the second effect runs under.
        var (h, card3, monk3, _, follower3, bystander3) = Setup();
        var stateH = h.State; stateH.NegatedEffects.Add(new("destroy", bystander3.Id));
        var i = MatchEngine.FromTestState(stateH);
        Accept(i, Play(i, card3, monk3, follower3.Id));
        Accept(i, new GameCommand("chooseSequencedTarget", 0, i.Version, TargetId: bystander3.Id));
        Eq(i.State.Cards[bystander3.Id].Zone, Zone.Battlefield, "the Attack's destruction of its target was negated");
    }

    private static void Legality()
    {
        var (e, card, monk, followed, follower, _) = Setup();
        var own = Add(e.State, "training-follower", 0, Zone.Attached, province: 0, parent: monk.Id);
        // Only an unbowed Monk performs it.
        var notMonk = Add(e.State, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        Check(!Options(e, 0, "engulfing-flames-destroy", card.Id).Any(o => o.Enabled && o.Label.Contains($"({notMonk.Id})")), "a Personality who is not a Monk cannot perform it");
        var bowed = Edit(e, st => st.Cards[monk.Id].Bowed = true);
        Check(!Options(bowed, 0, "engulfing-flames-destroy", card.Id).Any(o => o.Enabled && o.Label.Contains($"({monk.Id})")), "nor can a bowed one");
        // The first target: an enemy attachment only -- not his own, not a Personality.
        var option = Enabled(e, 0, "engulfing-flames-destroy", card.Id, monk.Id);
        var slot = option.Slots!.Single(x => x.Slot == "attachment");
        Check(slot.CardIds.Contains(follower.Id) && !slot.CardIds.Contains(followed.Id), "the enemy Follower may be chosen, the Personality carrying it may not");
        Check(slot.Min == 0 && slot.Max == 1, "at most one, and none is allowed");
    }
}
