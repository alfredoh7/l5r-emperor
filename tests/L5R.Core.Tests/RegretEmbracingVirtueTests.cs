using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #619: Regret (oracle-l5r-9923, The Shadow's Embrace) and Embracing Virtue (oracle-l5r-2313, War of Honor), strategy-wave-105. Every scenario is an explicit position on the economy table
// (player 0 the Crane, player 1 the Lion), driven through QueryActions and the commands it offers. Mendon's readings are in RegretEmbracingVirtue.cs.
internal static class RegretEmbracingVirtueTests
{
    private const string Regret = "oracle-l5r-9923", Virtue = "oracle-l5r-2313", Blades = "oracle-l5r-10111", Gonji = "oracle-l5r-4167", Ichizo = "oracle-l5r-5104", Natsumi = "oracle-l5r-9839";
    private const string Open = "regret-attach";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    public static void Run(TestRunner runner) => runner.Run("regret-embracing-virtue", [
        ("Published shape: Regret and Embracing Virtue print their scans' stats, text, keywords and abilities, ship their art and are in the strategy-wave-105 manifest", PublishedShape),
        ("Embracing Virtue: before the End Phase its owner may put it in his hand from his discard pile -- ahead of the draw, so he may draw above his hand size and must discard", EmbracingVirtueBeforeTheDraw),
        ("Embracing Virtue: declining leaves it in the pile; a copy anywhere else is not asked; each copy is asked once a turn", EmbracingVirtueDeclineAndWhere),
        ("Embracing Virtue: an End Phase is anyone's, so the other player may take his copy back too -- the active player is asked first, and several copies are asked one at a time", EmbracingVirtueEveryPlayer),
        ("Regret's Focus Effect: the duel's winner's controller loses 1 Honor for each card his side focused, whoever's Regret it is; a loser loses nothing", RegretFocusEffect),
        ("Regret's Focus Effect: Kakita Gonji's +4 instead of focusing is not a focus and is not counted", RegretIgnoresGonji),
        ("Regret's Focus Effect: two Regrets in one duel each take their toll", TwoRegrets),
        ("Regret's Open action: one or more Followers and Items from your own discard pile go to your Personalities, each to the one named for it, for the sum of their Equip costs, and Regret is spent", OpenAttachesSeveral),
        ("Regret's Open action: the attachments' own legality still applies as one -- caps, Unique, hosts that refuse, counts that do not match, other piles and Spells are refused with nothing changed", OpenLegality),
        ("Regret's Open action is not Equip (Battle): no additional action, and an action printed on a card attached by it is usable at the very next action", OpenIsNotEquipBattle),
        ("Regret's Open action: Platinum Mine's bonus for paying for an attachment applies to the whole payment, whatever number of cards are attached", OpenPlatinumMine),
        ("Regret's Open action: every attached card opens its own entering-play window, one after another", OpenEntryWindows),
    ]);

    // ---- Published shape --------------------------------------------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        var root = RepoRoot();
        var regret = CardCatalog.Get(Regret);
        Check(regret.Type == CardType.Strategy && regret.Name == "Regret" && regret.GoldCost == 0 && regret.Focus == 1 && (regret.Keywords ?? []).Length == 0 && regret.Effect == "none"
            && regret.Abilities!.SequenceEqual(["regret-focus", "regret-attach"]) && !regret.ScenarioOnly, "Regret's scan");
        Eq(regret.Text, "As a Focus Effect: After this duel ends, its winner's controller loses 1 Honor for each card he focused.\nOpen: Target one or more Followers or Items in your discard pile: Attach them to any of your Personalities, paying all costs.");
        var virtue = CardCatalog.Get(Virtue);
        Check(virtue.Type == CardType.Strategy && virtue.Name == "Embracing Virtue" && virtue.GoldCost == 0 && virtue.Focus == 2 && (virtue.Keywords ?? []).Order(StringComparer.Ordinal).SequenceEqual(["Bushido Virtue", "Duty"])
            && virtue.Effect == "embracing-virtue-return" && (virtue.Abilities ?? []).Length == 0, "Embracing Virtue's scan");
        Eq(virtue.Text, "Before an End Phase begins, if this card is in your discard pile: You may put it in your hand.");
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-105.txt")).Where(l => l.Length > 0).Order(StringComparer.Ordinal);
        Check(listed.SequenceEqual([Virtue, Regret]), "the manifest lists exactly the two cards");
        foreach (var id in new[] { Regret, Virtue })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Eq(AbilityCatalog.All["regret-focus"].Timing, "FocusEffect"); Eq(AbilityCatalog.All["regret-attach"].Timing, "Open");
        Eq(AbilityCatalog.All[Open].Cost!.Basis, GoldCostBasis.AttachSlotsCost);
        Check(AbilityCatalog.All[Open].Slots!.Length == 2 && AbilityCatalog.All[Open].Slots![0].Max > 1 && AbilityCatalog.All[Open].Slots![1].Repeatable, "several cards, and a repeatable choice of Personalities");
    }

    // ---- Embracing Virtue -------------------------------------------------------------------------------------------------------------------------------
    // Player 0 ends his Dynasty Phase with `hand` cards in hand and an Embracing Virtue in his discard pile (the economy scenario's end of turn is the End Phase).
    private static (MatchEngine E, CardInstance Virtue) VirtueRig(int hand, Action<MatchState>? tweak = null)
    {
        var s = Table(Phase.Dynasty, 0); s.PriorityPlayerId = 0;
        for (var i = 0; i < hand; i++) Add(s, "training-farm", 0, Zone.Hand);
        var virtue = Add(s, Virtue, 0, Zone.FateDiscard);
        tweak?.Invoke(s);
        return (MatchEngine.FromTestState(s), virtue);
    }

    private static void EndDynasty(MatchEngine e) => Accept(e, e.QueryActions(0).First(a => a.Type == "endDynasty").Command!);
    private static GameCommand Answer(MatchEngine e, int player, string choice) => e.QueryActions(player).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == choice).Command!;

    private static void EmbracingVirtueBeforeTheDraw()
    {
        var (e, virtue) = VirtueRig(7);
        EndDynasty(e);
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the owner is asked before the End Phase begins");
        Eq(e.State.PendingChoice?.PlayerId, 0, "the owner decides"); Check(e.State.Phase != Phase.End, "the End Phase has not begun");
        Eq(e.State.Players[0].Hand.Count, 7, "nothing was drawn yet");
        var options = e.QueryActions(0).Where(a => a.Type == "chooseEitherOr").ToArray();
        Check(options.Length == 2 && options.All(o => o.Enabled) && options.Any(o => o.Command!.Choice == "return") && options.Any(o => o.Command!.Choice == "decline"), "return it, or leave it");
        Check(!e.QueryActions(1).Any(a => a.Type == "chooseEitherOr"), "the other player has nothing to answer");
        Accept(e, Answer(e, 0, "return"));
        Eq(e.State.Cards[virtue.Id].Zone, Zone.Hand, "it is in his hand"); Check(e.State.Players[0].Hand.Contains(virtue.Id), "and in the hand list");
        // 7 + Embracing Virtue + the End Phase draw = 9: above the hand size of 8, so he must discard one.
        Eq(e.State.Phase, Phase.End); Eq(e.State.PendingChoice?.Type, "discardHand", "the hand is over its size, so he must discard");
        Eq(e.State.Players[0].Hand.Count, 9, "the draw came after the return");
        Accept(e, e.QueryActions(0).First(a => a.Type == "discardHand").Command! with { CardIds = [e.State.Players[0].Hand[0]] });
        Eq(e.State.ActivePlayerId, 1, "then his turn ended");
    }

    private static void EmbracingVirtueDeclineAndWhere()
    {
        var (e, virtue) = VirtueRig(7);
        EndDynasty(e);
        Accept(e, Answer(e, 0, "decline"));
        Eq(e.State.Cards[virtue.Id].Zone, Zone.FateDiscard, "declined: still in the discard pile");
        Check(e.State.PendingChoice is null && e.State.ActivePlayerId == 1, "7 + the draw is 8: no discard, and the turn ends");

        // In his hand, in play's Dynasty discard pile or the Fate deck: not "in your discard pile", so nobody is asked.
        foreach (var zone in new[] { Zone.Hand, Zone.DynastyDiscard, Zone.FateDeck })
        {
            var (f, other) = VirtueRig(0, s => { });
            var st = f.State; var card = st.Cards[other.Id]; card.Zone = zone; if (zone == Zone.Hand) st.Players[0].Hand.Add(card.Id); if (zone == Zone.FateDeck) st.Players[0].FateDeck.Insert(0, card.Id);
            var g = MatchEngine.FromTestState(st);
            EndDynasty(g);
            Check(g.State.PendingChoice?.Type != "eitherOr" && g.State.ActivePlayerId == 1, $"a copy in {zone} is not asked about");
        }

        // The copy is asked once a turn: declining and then the same turn's End Phase never asks again.
        var (h, copy) = VirtueRig(0);
        EndDynasty(h); Accept(h, Answer(h, 0, "decline"));
        Check(h.State.Cards[copy.Id].AbilityUses.ContainsKey("embracing-virtue:turn"), "the copy remembers it was asked this turn");
    }

    private static void EmbracingVirtueEveryPlayer()
    {
        // The other player's copy: an End Phase is anyone's, so he is asked during player 0's turn.
        var s = Table(Phase.Dynasty, 0); s.PriorityPlayerId = 0;
        var theirs = Add(s, Virtue, 1, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        EndDynasty(e);
        Eq(e.State.PendingChoice?.Type, "eitherOr"); Eq(e.State.PendingChoice?.PlayerId, 1, "his own copy: he decides");
        Check(!e.QueryActions(0).Any(a => a.Type == "chooseEitherOr"), "the active player has nothing to answer");
        Accept(e, Answer(e, 1, "return"));
        Eq(e.State.Cards[theirs.Id].Zone, Zone.Hand, "in his hand"); Check(e.State.Players[1].Hand.Contains(theirs.Id), "in his hand list");
        Eq(e.State.ActivePlayerId, 1, "player 0's turn ended");

        // Both players, and two copies of one: the active player's first, one at a time, nobody asked twice.
        var s2 = Table(Phase.Dynasty, 0); s2.PriorityPlayerId = 0;
        var mine1 = Add(s2, Virtue, 0, Zone.FateDiscard); var mine2 = Add(s2, Virtue, 0, Zone.FateDiscard); var their = Add(s2, Virtue, 1, Zone.FateDiscard);
        var f = MatchEngine.FromTestState(s2);
        EndDynasty(f);
        Eq(f.State.PendingChoice?.PlayerId, 0, "the active player is asked first");
        var firstId = f.State.PendingChoice!.CardId;
        Accept(f, Answer(f, 0, "return"));
        Eq(f.State.PendingChoice?.Type, "eitherOr"); Eq(f.State.PendingChoice?.PlayerId, 0, "then his other copy");
        Check(f.State.PendingChoice!.CardId != firstId, "a different copy");
        Accept(f, Answer(f, 0, "decline"));
        Eq(f.State.PendingChoice?.PlayerId, 1, "then the other player's");
        Accept(f, Answer(f, 1, "decline"));
        Check(f.State.PendingChoice is null && f.State.ActivePlayerId == 1, "all asked once: the turn ends");
        var inHand = new[] { mine1, mine2 }.Count(c => f.State.Cards[c.Id].Zone == Zone.Hand);
        Eq(inHand, 1, "exactly the copy he took back");
        Eq(f.State.Cards[their.Id].Zone, Zone.FateDiscard, "and his opponent's declined copy stays");
    }

    // ---- Regret: the Focus Effect -----------------------------------------------------------------------------------------------------------------------
    private static void Strong(CardInstance card) { foreach (var stat in new[] { "Force", "Chi", "PersonalHonor" }) card.Modifiers.Add(new("test-source", "test", stat, 20)); }

    // Player 0 challenges with Steel on Steel: his Personality `mine` against player 1's `theirs`; the side given Strong() wins.
    private static (MatchEngine E, CardInstance Mine, CardInstance Theirs, CardInstance Duel) SteelDuel(Action<MatchState, CardInstance, CardInstance>? arrange = null)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); var theirs = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
        foreach (var owner in new[] { 0, 1 }) for (var i = 0; i < 4; i++) { var top = Add(s, "training-focus-strategy", owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, top.Id); }
        arrange?.Invoke(s, mine, theirs);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        return (e, mine, theirs, duel);
    }

    private static void FocusDeck(MatchEngine e, int player) => Accept(e, new GameCommand("focus", player, e.Version, Choice: "deck"));

    // Strike and see the duel through (ordering its Focus Effects when there are several).
    private static void Finish(MatchEngine e, int striker)
    {
        Accept(e, new GameCommand("strike", striker, e.Version));
        for (var guard = 0; guard < 20 && e.State.PendingDuel is not null; guard++)
        {
            var actor = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var action = e.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command is not null && (a.Type is "orderDuelFocusEffects" or "declineReaction" or "declineDuelReaction" or "pass" || a is { Type: "duelistRefocus", Command.CardId: null }));
            Check(action is not null, $"the duel is stuck at {e.State.PendingDuel?.Stage}");
            Accept(e, action!.Command!);
        }
        Check(e.State.PendingDuel is null, "the duel finished");
    }

    private static void RegretFocusEffect()
    {
        // Player 0 wins having focused Regret and two more cards: he loses 3 Honor. Player 1's two focused cards cost nothing (he lost).
        var (e, mine, theirs, _) = SteelDuel((s, m, t) => { Strong(m); Add(s, Regret, 0, Zone.Hand); });
        var regret = e.State.Cards.Values.Single(c => c.DefinitionId == Regret);
        var before = (Honor(e, 0), Honor(e, 1));
        FocusDeck(e, 1); Accept(e, new GameCommand("focus", 0, e.Version, CardId: regret.Id)); FocusDeck(e, 1); FocusDeck(e, 0); FocusDeck(e, 1); FocusDeck(e, 0);
        var focusedByWinner = e.State.PendingDuel!.ChallengerFocused.Count; Eq(focusedByWinner, 3, "the winner's side focused three cards");
        Finish(e, 1);
        Eq(e.State.Cards[theirs.Id].Zone == Zone.Dead || e.State.Cards[theirs.Id].Zone == Zone.DynastyDiscard, true, "player 0 won the duel");
        Eq(Honor(e, 0), before.Item1 - 3, "the winner's controller lost 1 Honor for each card he focused");
        Eq(Honor(e, 1), before.Item2, "the loser lost none to Regret");

        // Player 1 wins: the loser's Regret still takes Honor from the WINNER's controller, for the cards the winner's side focused.
        var (f, mine2, _, _) = SteelDuel((s, m, t) => { Strong(t); Add(s, Regret, 0, Zone.Hand); });
        var regret2 = f.State.Cards.Values.Single(c => c.DefinitionId == Regret);
        var honor0 = Honor(f, 0); var honor1 = Honor(f, 1);
        FocusDeck(f, 1); Accept(f, new GameCommand("focus", 0, f.Version, CardId: regret2.Id)); FocusDeck(f, 1);
        Eq(f.State.PendingDuel!.ChallengedFocused.Count, 2, "the winner's side focused two");
        Finish(f, 0);
        Eq(Honor(f, 1), honor1 - 2, "player 1 won, so he lost 1 Honor for each of his two focused cards");
        Eq(Honor(f, 0), honor0, "the owner of Regret lost none: he is the loser");
        Check(f.State.Cards[mine2.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "his Personality lost the duel");
    }

    private static void RegretIgnoresGonji()
    {
        // Player 1's Ichizo challenges player 0's Crane duellist; player 0 (challenged) gives +4 with Gonji instead of focusing once, focuses Regret, and wins.
        var s = Table(); var ichizo = Add(s, Ichizo, 1, Zone.Battlefield, province: 0); var duellist = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Strong(duellist); Add(s, Gonji, 0, Zone.Home); var regret = Add(s, Regret, 0, Zone.Hand);
        foreach (var owner in new[] { 0, 1 }) for (var i = 0; i < 3; i++) { var top = Add(s, "training-focus-strategy", owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, top.Id); }
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "ichizo-exp-duel", ichizo.Id, duellist.Id));
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingDuel?.Stage, "Focusing", "focusing");
        var honor = Honor(e, 0);
        Accept(e, e.QueryActions(0).First(a => a.Command?.Choice == MatchEngine.GonjiFocusChoice).Command!);
        FocusDeck(e, 1);
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: regret.Id));
        Eq(e.State.PendingDuel!.ChallengedFocused.Count, 1, "Gonji's +4 is not a focused card: only Regret is");
        Finish(e, 1);
        Eq(Honor(e, 0), honor - 1, "the winner lost 1 Honor: one card focused, Gonji's replaced focus not counted");
    }

    private static void TwoRegrets()
    {
        // Each player focuses a Regret; player 0 wins having focused only his own Regret: both Regrets take 1 Honor from him.
        var (e, _, _, _) = SteelDuel((s, m, t) => { Strong(m); Add(s, Regret, 0, Zone.Hand); Add(s, Regret, 1, Zone.Hand); });
        var mineRegret = e.State.Cards.Values.Single(c => c.DefinitionId == Regret && c.OwnerId == 0); var theirRegret = e.State.Cards.Values.Single(c => c.DefinitionId == Regret && c.OwnerId == 1);
        var honor = Honor(e, 0);
        Accept(e, new GameCommand("focus", 1, e.Version, CardId: theirRegret.Id)); Accept(e, new GameCommand("focus", 0, e.Version, CardId: mineRegret.Id));
        Finish(e, 1);
        Eq(Honor(e, 0), honor - 2, "two Regrets, one focused card each side: the winner lost 1 Honor to each");
    }

    // ---- Regret: the Open action ------------------------------------------------------------------------------------------------------------------------
    private sealed record Rig(MatchEngine E, CardInstance Regret, CardInstance A, CardInstance B, CardInstance Follower, CardInstance Item, CardInstance Weapon, CardInstance Weapon2, CardInstance Armor, CardInstance Armor2, CardInstance[] Farms);

    private static Rig OpenRig(Phase phase = Phase.Action, Action<MatchState, Rig>? tweak = null)
    {
        var s = Table(phase, phase == Phase.Battle ? 1 : 0); s.PriorityPlayerId = 0; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3; s.Players[0].Honor = 20;
        var regret = Add(s, Regret, 0, Zone.Hand);
        var a = Add(s, "training-crane-samurai", 0, phase == Phase.Battle ? Zone.Battlefield : Zone.Home, province: phase == Phase.Battle ? 0 : null);
        var b = Add(s, "training-crane-samurai", 0, Zone.Home);
        if (phase == Phase.Battle) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.FateDiscard); var item = Add(s, "training-item", 0, Zone.FateDiscard);
        var weapon = Add(s, "training-weapon", 0, Zone.FateDiscard); var weapon2 = Add(s, "training-weapon", 0, Zone.FateDiscard);
        var armor = Add(s, "training-armor", 0, Zone.FateDiscard); var armor2 = Add(s, "training-armor", 0, Zone.FateDiscard);
        var farms = new[] { Add(s, "training-farm", 0, Zone.Home), Add(s, "training-farm", 0, Zone.Home), Add(s, "training-farm", 0, Zone.Home) };
        var rig = new Rig(null!, regret, a, b, follower, item, weapon, weapon2, armor, armor2, farms);
        tweak?.Invoke(s, rig);
        return rig with { E = MatchEngine.FromTestState(s) };
    }

    // The offered command with the chosen cards and Personalities, paid with sources the engine suggests for the price it quotes.
    private static GameCommand Attach(Rig r, CardInstance[] cards, CardInstance[] hosts, bool paid = true)
    {
        var offer = Enabled(r.E, 0, Open, r.Regret.Id);
        var command = offer.Command! with { TargetId = null, Selections = [Pick("attachments", cards.Select(c => c.Id).ToArray()), Pick("personality", hosts.Select(h => h.Id).ToArray())] };
        var quote = r.E.QuoteAbilityGold(command);
        return paid && quote is { Cost: > 0 } ? command with { SourceIds = quote.Payments.First().SourceIds } : command;
    }

    private static bool Untouched(Rig r) => r.E.State.Cards[r.Regret.Id].Zone == Zone.Hand && r.E.State.Cards.Values.All(c => c.Zone != Zone.Attached) && r.E.State.Events.All(x => x.Type != "payment");

    private static void OpenAttachesSeveral()
    {
        var r = OpenRig();
        var offer = Enabled(r.E, 0, Open, r.Regret.Id);
        var attachments = offer.Slots!.Single(x => x.Slot == "attachments"); var hosts = offer.Slots!.Single(x => x.Slot == "personality");
        Check(attachments.Min == 1 && attachments.Max >= 6 && new[] { r.Follower, r.Item, r.Weapon, r.Weapon2, r.Armor, r.Armor2 }.All(c => attachments.CardIds.Contains(c.Id)), "every Follower and Item of his own pile is a candidate, up to as many as there are");
        Check(hosts.CardIds.Contains(r.A.Id) && hosts.CardIds.Contains(r.B.Id) && hosts.Min == 1, "his Personalities are the hosts");
        // A Follower and an Item and a Weapon: 2 + 2 + 1 = 5 Gold, the Follower and the Item to A, the Weapon to B.
        var command = Attach(r, [r.Follower, r.Item, r.Weapon], [r.A, r.A, r.B]);
        var quote = r.E.QuoteAbilityGold(command)!;
        Eq(quote.Cost, 5, "each card's own Equip cost, added up"); Eq(quote.Kind, GoldCostKind.Derived, "a derived price"); Check(quote.Basis.Contains('+'), "the basis names each term: " + quote.Basis);
        Reject(r.E, command with { SourceIds = r.Farms.Take(2).Select(f => f.Id).ToArray() });  // 4 Gold is not 5
        Check(Untouched(r), "the refused command changed nothing");
        Accept(r.E, command);
        var st = r.E.State;
        Check(st.Cards[r.Follower.Id].Zone == Zone.Attached && st.Cards[r.Follower.Id].AttachedTo == r.A.Id, "the Follower is on the first Personality");
        Check(st.Cards[r.Item.Id].Zone == Zone.Attached && st.Cards[r.Item.Id].AttachedTo == r.A.Id, "the Item on the same one, named twice");
        Check(st.Cards[r.Weapon.Id].Zone == Zone.Attached && st.Cards[r.Weapon.Id].AttachedTo == r.B.Id, "the Weapon on the second");
        Check(st.Cards[r.Weapon2.Id].Zone == Zone.FateDiscard && st.Cards[r.Armor.Id].Zone == Zone.FateDiscard, "what was not chosen is still in the pile");
        Eq(st.Cards[r.Regret.Id].Zone, Zone.FateDiscard, "Regret was spent");
        Eq(st.Events.Count(x => x.Type == "payment"), 1, "paid once, for all of it");
        Eq(Honor(r.E, 0), 20, "no Honor changed");
        Eq(r.E.CardForceForTest(r.A.Id), 3 + 2, "the Item's Force counts for the Personality (3 + 2); a Follower's is the unit's");

        // One card to one Personality: the plain case.
        var one = OpenRig();
        Accept(one.E, Attach(one, [one.Armor], [one.B]));
        Check(one.E.State.Cards[one.Armor.Id].AttachedTo == one.B.Id && one.E.State.Cards[one.Regret.Id].Zone == Zone.FateDiscard, "a single card works as Foothills Keep's does");
    }

    private static void OpenLegality()
    {
        var r = OpenRig();
        // Two Weapons for a Personality without Kensai are one too many; to two Personalities they are fine.
        Reject(r.E, Attach(r, [r.Weapon, r.Weapon2], [r.A, r.A])); Check(Untouched(r), "two Weapons for one Personality: refused, nothing moved or paid");
        Accept(r.E, Attach(r, [r.Weapon, r.Weapon2], [r.A, r.B]));
        Check(r.E.State.Cards[r.Weapon.Id].AttachedTo == r.A.Id && r.E.State.Cards[r.Weapon2.Id].AttachedTo == r.B.Id, "a Weapon for each");
        // Armor the same; and a Weapon already attached counts against the cap too.
        var r2 = OpenRig(); Reject(r2.E, Attach(r2, [r2.Armor, r2.Armor2], [r2.A, r2.A])); Accept(r2.E, Attach(r2, [r2.Armor, r2.Armor2], [r2.A, r2.B]));
        var r3 = OpenRig(Phase.Action, (s, rig) => Add(s, "training-weapon", 0, Zone.Attached, parent: rig.A.Id));
        Reject(r3.E, Attach(r3, [r3.Weapon], [r3.A])); Accept(r3.E, Attach(r3, [r3.Weapon], [r3.B]));
        // Unique: two copies of one Unique card cannot both enter; neither can one while you control another.
        var r4 = OpenRig(Phase.Action, (s, rig) => { Add(s, "training-unique-weapon", 0, Zone.FateDiscard); Add(s, "training-unique-weapon", 0, Zone.FateDiscard); });
        var uniques = r4.E.State.Cards.Values.Where(c => c.DefinitionId == "training-unique-weapon").ToArray();
        Reject(r4.E, Attach(r4, uniques, [r4.A, r4.B])); Accept(r4.E, Attach(r4, [uniques[0]], [r4.A]));
        var r5 = OpenRig(Phase.Action, (s, rig) => { Add(s, "training-unique-weapon", 0, Zone.Attached, parent: rig.B.Id); Add(s, "training-unique-weapon", 0, Zone.FateDiscard); });
        Reject(r5.E, Attach(r5, [r5.E.State.Cards.Values.Single(c => c.DefinitionId == "training-unique-weapon" && c.Zone == Zone.FateDiscard)], [r5.A]));
        // A host that refuses attachments refuses the whole action.
        var r6 = OpenRig(Phase.Action, (s, rig) => Add(s, Natsumi, 0, Zone.Home));
        var natsumi = r6.E.State.Cards.Values.Single(c => c.DefinitionId == Natsumi);
        Reject(r6.E, Attach(r6, [r6.Follower, r6.Item], [r6.A, natsumi])); Check(Untouched(r6), "one host that will not attach Followers or Items: nothing happens");
        // The count of Personalities must match the count of cards.
        var r10 = OpenRig();
        Reject(r10.E, Attach(r10, [r10.Follower, r10.Item], [r10.A])); Reject(r10.E, Attach(r10, [r10.Follower], [r10.A, r10.B])); Check(Untouched(r10), "a Personality for each card, and no more");
        // A Spell, another player's pile and a card that is not a Follower or Item are not targets.
        var strayIds = new List<string>();
        var r7 = OpenRig(Phase.Action, (s, rig) => { strayIds.Add(Add(s, "training-spell", 0, Zone.FateDiscard).Id); strayIds.Add(Add(s, "training-follower", 1, Zone.FateDiscard).Id); strayIds.Add(Add(s, "training-farm", 0, Zone.FateDiscard).Id); });
        var offer = Enabled(r7.E, 0, Open, r7.Regret.Id).Slots!.Single(x => x.Slot == "attachments").CardIds;
        var stray = strayIds.ToArray();
        Eq(stray.Length, 3, "the three stray cards exist");
        Check(stray.All(id => !offer.Contains(id)), "a Spell, an enemy Follower and a Holding are not offered: " + string.Join(", ", stray.Where(offer.Contains).Select(id => r7.E.State.Cards[id].DefinitionId)));
        foreach (var id in stray) Reject(r7.E, Enabled(r7.E, 0, Open, r7.Regret.Id).Command! with { TargetId = null, Selections = [Pick("attachments", id), Pick("personality", r7.A.Id)] });
        Check(Untouched(r7), "nothing was attached by any refused command");
        // Another player's Personality is not one of yours; an opponent may not use your Regret.
        var enemy = Add(r.E.State, "training-lion-samurai", 1, Zone.Home); var r8 = OpenRig(Phase.Action, (s, rig) => Add(s, "training-lion-samurai", 1, Zone.Home));
        var foe = r8.E.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality);
        Reject(r8.E, Enabled(r8.E, 0, Open, r8.Regret.Id).Command! with { TargetId = null, Selections = [Pick("attachments", r8.Follower.Id), Pick("personality", foe.Id)] });
        Check(!CanUse(r8.E, 1, Open, r8.Regret.Id) && enemy.OwnerId == 1, "the other player cannot use it");
        // Not in hand: no action.
        var r9 = OpenRig(Phase.Action, (s, rig) => rig.Regret.Zone = Zone.FateDiscard);
        Check(!CanUse(r9.E, 0, Open, r9.Regret.Id), "Regret is played from the hand");
    }

    private static void OpenIsNotEquipBattle()
    {
        // No additional action: the Open action ends as an ordinary action does, and priority passes.
        var r = OpenRig();
        Accept(r.E, Attach(r, [r.Follower], [r.A]));
        Eq(r.E.State.AdditionalActions.Count, 0, "no additional action was granted");
        Eq(r.E.State.PriorityPlayerId, 1, "priority passed to the other player");

        // An Item with an Open action of its own, attached from the discard pile: that action is offered at the player's very next action (an Open action is an Action Phase one; a Battle action, such as
        // Blades of the Black Dragon's, is likewise there at the next Battle action -- nothing here holds a newly attached card back).
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].Honor = 20; s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        var regret = Add(s, Regret, 0, Zone.Hand); var samurai = Add(s, "training-crane-samurai", 0, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Home);
        var valued = Add(s, "training-value-item", 0, Zone.FateDiscard); for (var i = 0; i < 3; i++) Add(s, "training-farm", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "training-create-from-wielder", valued.Id), "in the discard pile its action is not available");
        var command = Enabled(e, 0, Open, regret.Id).Command! with { TargetId = null, Selections = [Pick("attachments", valued.Id), Pick("personality", samurai.Id)] };
        var quote = e.QuoteAbilityGold(command);
        Accept(e, quote is { Cost: > 0 } ? command with { SourceIds = quote.Payments.First().SourceIds } : command);
        Eq(e.State.Cards[valued.Id].AttachedTo, samurai.Id, "the Item is on the Personality");
        Eq(e.State.AdditionalActions.Count, 0, "and no additional action was granted");
        Eq(e.State.PriorityPlayerId, 1, "the other player has the next action");
        Accept(e, e.QueryActions(1).First(a => a.Enabled && a.Command is not null && a.Type == "pass").Command!);
        Eq(e.State.PriorityPlayerId, 0, "then it is his next action");
        Check(CanUse(e, 0, "training-create-from-wielder", valued.Id), "the Item's own Open action is available straight away");
    }

    private static void OpenEntryWindows()
    {
        // Two Items that each have an "after entering play" Reaction: each opens its own window, the second only once the first has closed.
        var r = OpenRig(Phase.Action, (s, rig) => { Add(s, "training-item-entry-reaction", 0, Zone.FateDiscard); Add(s, "training-item-entry-reaction", 0, Zone.FateDiscard); });
        var items = r.E.State.Cards.Values.Where(c => c.DefinitionId == "training-item-entry-reaction").OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        Accept(r.E, Attach(r, items, [r.A, r.B]));
        Check(items.All(i => r.E.State.Cards[i.Id].Zone == Zone.Attached), "both are attached before either window opens");
        Eq(r.E.State.PendingChoice?.Type, "entryReaction", "the first window is open"); Eq(r.E.State.PendingChoice?.CardId, items[0].Id, "for the first card");
        Eq(r.E.State.Cards[r.Regret.Id].Zone, Zone.FateDiscard, "Regret was spent as the window opened");
        var honor = Honor(r.E, 0);
        Accept(r.E, Use(r.E, 0, "training-item-entry-reaction", items[0].Id));
        Eq(Honor(r.E, 0), honor + 1, "its Reaction was used");
        for (var guard = 0; guard < 6 && r.E.State.PendingChoice is { Type: "entryReaction" } && r.E.State.PendingChoice.CardId == items[0].Id; guard++)
            Accept(r.E, r.E.QueryActions(r.E.State.PriorityPlayerId).First(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "pass").Command!);
        Eq(r.E.State.PendingChoice?.Type, "entryReaction", "the second window opened when the first closed"); Eq(r.E.State.PendingChoice?.CardId, items[1].Id, "for the second card");
        for (var guard = 0; guard < 6 && r.E.State.PendingChoice is { Type: "entryReaction" }; guard++)
            Accept(r.E, r.E.QueryActions(r.E.State.PriorityPlayerId).First(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "pass").Command!);
        Check(r.E.State.PendingChoice is null && r.E.State.Regret.EntryQueue.Count == 0, "both windows closed and nothing is left owing");
        Eq(Honor(r.E, 0), honor + 1, "the second Reaction was passed");
    }

    private static void OpenPlatinumMine()
    {
        // Four cards cost 2 + 2 + 1 + 1 = 6 Gold. Platinum Mine alone produces 6 when paying for an attachment (5 otherwise), so it can pay all of them by itself.
        var r = OpenRig(Phase.Action, (s, rig) => Add(s, "oracle-l5r-9798", 0, Zone.Home));
        var mine = r.E.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-9798");
        var command = Attach(r, [r.Follower, r.Item, r.Weapon, r.Armor], [r.A, r.A, r.B, r.B], paid: false);
        Eq(r.E.QuoteAbilityGold(command)!.Cost, 6, "the four cards cost 6");
        Accept(r.E, command with { SourceIds = [mine.Id] });
        Check(r.E.State.Cards[mine.Id].Bowed && r.E.State.Cards[r.Armor.Id].AttachedTo == r.B.Id, "the Mine alone paid for every card");
        // Paying for one card is the same bonus.
        var one = OpenRig(Phase.Action, (s, rig) => Add(s, "oracle-l5r-9798", 0, Zone.Home));
        var mine1 = one.E.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-9798");
        Accept(one.E, Attach(one, [one.Item], [one.A], paid: false) with { SourceIds = [mine1.Id] });
        Check(one.E.State.Cards[mine1.Id].Bowed, "and for a single card");
    }
}
