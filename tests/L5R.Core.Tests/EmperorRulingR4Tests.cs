using L5R.Core;
using static RegressionAssertions;

// Emperor Edition remaining-cards pass (issue #492), Mendon's rulings on the open questions of the decision document, group R4:
//   1. Rhetoric / Solid Defense (and Shiro Chugo): "may be played on any honor gain or loss" -- the window is at the Honor change itself; Honor paid as a true cost is outside it;
//   2. Cherry Blossom Festival: Proclaim is a Reaction; Overconfident and traits (the trait-modelled abilities, TraitReactions) are not;
//   3. Luxurious Silk: what "paying for an action" is for a recruit;
//   4. The Killing Grounds: the first pass of each player;
//   5. Bad Kharma / Footsteps of Madness: playable on top of the other Reactions and modifiers; a Focus Effect is not an action;
//   plus the coordinator's two follow-ups: ordered searches (must take from the first place found, reshuffle what was searched) and Deception Revealed's Rings (bowed by an action).
internal static partial class EmperorRulingR4Tests
{
    private const string Rhetoric = "oracle-l5r-6312", SolidDefense = "oracle-l5r-7329", ShiroChugo = "oracle-l5r-7008", ShamateKeep = "oracle-l5r-6691", WeakPoison = "oracle-l5r-99",
        ExploitedAdvantage = "oracle-l5r-2413", CherryBlossom = "oracle-l5r-1314", LuxuriousSilk = "oracle-l5r-4787", KillingGrounds = "oracle-l5r-8188", BadKharma = "oracle-l5r-663",
        Footsteps = "oracle-l5r-2629", Kakihara = "oracle-l5r-223", Forefather = "oracle-l5r-23", KhansEstate = "oracle-l5r-8182", DeceptionRevealed = "oracle-l5r-1908",
        AsakoChukage = "oracle-l5r-10072", MeetingTheKeepers = "oracle-l5r-5018", ShinjosGuidance = "oracle-l5r-6978", TogashisGuidance = "oracle-l5r-8603", Dokansuto = "oracle-l5r-9437",
        Sakuma = "oracle-l5r-9624", Ayumi = "oracle-l5r-10462", Horume = "oracle-l5r-4412", Steel = "oracle-l5r-7483", Conspiracy = "oracle-l5r-2011", TheAerie = "oracle-l5r-7949";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static CardInstance StrongholdAs(MatchState s, int player, string id) { var c = s.Cards[s.Players[player].StrongholdId]; c.DefinitionId = id; return c; }
    private static CardInstance Deck(MatchState s, int owner) { var c = Add(s, "training-focus-strategy", owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, c.Id); return c; }

    // Both players pass until nothing is open.
    private static void PassWindow(MatchEngine e) { for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); }
    private static ActionOption[] Listed(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();

    public static void Run(TestRunner runner) => runner.Run("emperor-ruling-R4", [
        ("Rhetoric: the window opens where ANY Honor gain of an action's effects would be applied -- an action whose label never mentions Honor included -- nothing is applied until both have passed; declined, the gain stands; no Courtier, no window", RhetoricAnyGain),
        ("Rhetoric and Solid Defense: Honor paid as a true cost (a Reaction-free cost verb) is not an effect and opens no window; A Weak Poison's and Exploited Advantage's printed 'Lose 2 Honor.' is an effect and does", HonorCostVersusEffect),
        ("Solid Defense: an Honor loss from another player's action is offered for negation where it would be applied, for a loss printed after the colon; its own player's action and a player who cannot pay get nothing", SolidDefenseAnyLoss),
        ("Honor windows: an action with several Honor changes gets one window each (Rhetoric applies to the change it was played before); a Focus Effect is not an action and gets none; a duel's outcome and an Attack's targeting do", WindowsPerChangeAndFocusEffects),
        ("Honor windows: a paused action's Honor (an Either/Or answer, another player's payment or choice, a search's end) and Proclaim are from an action's effects; a Reaction played in the window is replayed correctly with the marks", PausedActionsAndProclaim),
        ("Unimpeachable Name: also played in the window before an Honor loss of its player's (an action that is not on the old curated list of Honor losses); reduces the loss by 3; not for another player's loss", UnimpeachableNameOnALoss),
        ("Cherry Blossom Festival: Reactions (Proclaim, hand Reactions, Gold-source Reactions, Dokansuto's) are barred; Overconfident draws and the trait-modelled abilities (TraitReactions) are not", CherryBlossomBarsReactionsOnly),
        ("Cherry Blossom Festival: the trait-modelled abilities are pinned and audited against every printed text -- a Reaction-timed ability whose card prints no Reaction is a trait, and no trait names Reaction", TraitReactionAudit),
        ("Luxurious Silk: pays for an Equip, a Strategy play and an ability, and for a recruit BY an ability whose cost is the Personality's Gold Cost; not for a Dynasty recruit or a recruit that 'pays all costs'", LuxuriousSilkRecruits),
        ("The Killing Grounds: the trait triggers on the first pass of EACH player, once for each", KillingGroundsEachPlayer),
        ("Bad Kharma and Footsteps of Madness: played on top of the other Reactions and modifiers of the same action (a before-action window is not the end of their chance); not for a negated action or an additional one skipped", BadKharmaAndFootstepsOnTop),
        ("Bad Kharma: an additional Battle action is an announced action too", BadKharmaAdditionalAction),
        ("A Focus Effect is not an action: Bad Kharma, Solid Defense and their windows do not apply to what a focused card's Focus Effect does", FocusEffectIsNotAnAction),
        ("Deception Revealed: a Ring bowed by an action's cost, by an action's effect and by an Attack's or duel's action is bowed by an action; a Focus Effect's bow and a bow by no action are not", DeceptionRevealedRings),
        ("Ordered searches: a qualifying card in the first place must be taken from it (the later place is neither searched nor shown nor shuffled); with none there the searcher goes on or stops; a searched deck is reshuffled found or not", OrderedSearches),
    ]);

    // ---- 1. Rhetoric, Solid Defense: any Honor gain or loss ------------------------------------------------------------------------------------------------------------------------

    // Shamate Keep's action ("Move home a Personality with equal or lower Personal Honor"): its printed label says nothing of Honor, yet it gains its performer's controller 2 Honor when the target is
    // another player's -- the case the old label guess could not see.
    private static MatchEngine ShamateRig(bool courtier, bool rhetoric, out CardInstance keep, out CardInstance performer, out CardInstance target, out CardInstance rhetoricCard)
    {
        var s = Table(Phase.Battle, 0);
        keep = StrongholdAs(s, 0, ShamateKeep);
        performer = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);   // Personal Honor 3
        target = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);        // Personal Honor 3
        rhetoricCard = rhetoric ? Add(s, Rhetoric, 1, Zone.Hand) : keep;
        if (courtier) Kw(Add(s, "training-lion-courtier", 1, Zone.Home), "Courtier");     // Chi 3
        return MatchEngine.FromTestState(s);
    }

    private static void RhetoricAnyGain()
    {
        var e = ShamateRig(true, true, out var keep, out var performer, out var target, out var rhetoric);
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        Accept(e, Offer(e, 0, "shamate-move", keep.Id, target.Id, performer.Id));
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "the window opens where the 2 Honor would be gained, though the action's label mentions no Honor");
        Check(e.State.PendingHonorChange is { Amount: 2, PlayerId: 0 }, "it names the change: 2 Honor for player 0");
        Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "nothing of the action has been applied yet");
        Eq(Honor(e, 0), h0, "and no Honor has changed");
        Eq(e.State.PriorityPlayerId, 1, "the player who has a Reaction to play has the first opportunity");
        var offers = Listed(e, 1, "rhetoric-redirect").Where(o => o.Enabled).ToArray();
        Check(offers.Any(o => o.Command!.TargetPlayerId == 1) && offers.Any(o => o.Command!.TargetPlayerId == 0), "he may choose either player");
        Check(Listed(e, 0, "rhetoric-redirect").Length == 0, "the acting player has no Rhetoric");
        Accept(e, offers.First(o => o.Command!.TargetPlayerId == 1).Command!);
        Check(e.State.Cards.Values.Any(c => c.OwnerId == 1 && c.Zone == Zone.Home && c.Bowed && CardCatalog.Get(c.DefinitionId).Name.Contains("courtier")), "the Courtier bowed as the cost");
        Eq(e.State.Cards[rhetoric.Id].Zone, Zone.FateDiscard, "the Strategy was played");
        PassWindow(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Home, "the action then resolved: the Personality moved home");
        Eq(Honor(e, 0), h0, "player 0 did not gain the Honor");
        Eq(Honor(e, 1), h1 + 2, "the chosen player took the gain instead");
        Check(e.State.PendingHonorChange is null && e.State.PendingChoice is null, "nothing is left open");

        // Declined: both pass, the gain stands and the Strategy is still in the hand.
        var d = ShamateRig(true, true, out var keep2, out var performer2, out var target2, out var rhetoric2);
        var d0 = Honor(d, 0);
        Accept(d, Offer(d, 0, "shamate-move", keep2.Id, target2.Id, performer2.Id));
        PassWindow(d);
        Eq(Honor(d, 0), d0 + 2, "declined: the performer's controller gains 2 Honor");
        Eq(d.State.Cards[rhetoric2.Id].Zone, Zone.Hand, "and Rhetoric was not spent");

        // No Courtier to bow: nothing to play, so no window.
        var n = ShamateRig(false, true, out var keep3, out var performer3, out var target3, out _);
        var n0 = Honor(n, 0);
        Accept(n, Offer(n, 0, "shamate-move", keep3.Id, target3.Id, performer3.Id));
        Check(n.State.PendingChoice is null, "no unbowed Courtier: no window");
        Eq(Honor(n, 0), n0 + 2, "the gain was applied at once");
        // A change of no Honor (the target is the player's own) is no change: no window.
        var s = Table(Phase.Battle, 0); var keepOwn = StrongholdAs(s, 0, ShamateKeep);
        var own = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); var ownTarget = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, Rhetoric, 1, Zone.Hand); Kw(Add(s, "training-lion-courtier", 1, Zone.Home), "Courtier");
        var o = MatchEngine.FromTestState(s);
        Accept(o, Offer(o, 0, "shamate-move", keepOwn.Id, ownTarget.Id, own.Id));
        Check(o.State.PendingChoice is null, "a move that gains no Honor opens no window");
    }
}
