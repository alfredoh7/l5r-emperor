using L5R.Core;
using static RegressionAssertions;

// Emperor Edition remaining-cards pass (issue #492), group A: the second half of the scenarios (see EmperorRemainingATests.cs).
internal static partial class EmperorRemainingATests
{
    // ---- Bad Kharma ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BadKharmaScenario()
    {
        // The Limited action of a Strategy played by a player with -1 Honor: the other player reacts from his hand; the action is negated (no Honor gained).
        var s = Table(Phase.Action, 1); s.Players[1].Honor = -1;
        var forefather = Add(s, Forefather, 1, Zone.Hand);
        Kw(Add(s, "training-lion-samurai", 1, Zone.Home), "Ancestor"); Kw(Add(s, "training-lion-veteran", 1, Zone.Home), "Ancestor");
        var kharma = Add(s, BadKharma, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 1, "forefather-honor", forefather.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "unimpeachableNameReaction", "the announcement of the action opens the reaction window");
        Eq(e.State.PendingChoice?.PlayerId, 0, "for the other player");
        Check(e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command is not null).All(a => AbilityCatalog.All[a.Command!.AbilityId!].Timing == "Reaction"), "only Reactions are on offer against a held action");
        Accept(e, Enabled(e, 0, "bad-kharma-negate", kharma.Id).Command!);
        PassWindow(e);
        Eq(Honor(e, 1), -1, "the negated action gained no Honor");
        Eq(e.State.Cards[forefather.Id].Zone, Zone.FateDiscard, "the action was taken: the Strategy left the hand");
        Eq(e.State.Cards[kharma.Id].Zone, Zone.FateDiscard, "and Bad Kharma was discarded");
        Check(e.State.PendingChoice is null, "the window closed");

        // Costs are ignored: the Battle action's cost (destroying an Ancestor) is not paid, and nothing happens.
        var b = Table(Phase.Battle, 1); b.Players[1].Honor = -3;
        var strategy = Add(b, Forefather, 1, Zone.Hand);
        var ancestor = Kw(Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Ancestor");
        var target = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var kharma2 = Add(b, BadKharma, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(b);
        Accept(e2, Offer(e2, 1, "forefather-destroy", strategy.Id, target.Id, ancestor.Id));
        Eq(e2.State.PendingChoice?.Type, "unimpeachableNameReaction", "the Battle action opens the window too");
        Accept(e2, Enabled(e2, 0, "bad-kharma-negate", kharma2.Id).Command!);
        PassWindow(e2);
        Eq(e2.State.Cards[ancestor.Id].Zone, Zone.Battlefield, "he ignores the action's costs: the Ancestor was not destroyed");
        Eq(e2.State.Cards[target.Id].Zone, Zone.Battlefield, "and the effects were negated");
        Eq(e2.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy was still taken");

        // A player with Family Honor above -1: no window opens, the action resolves.
        var t = Table(Phase.Action, 1); t.Players[1].Honor = 0;
        var strat3 = Add(t, Forefather, 1, Zone.Hand);
        Kw(Add(t, "training-lion-samurai", 1, Zone.Home), "Ancestor");
        Add(t, BadKharma, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(t);
        Accept(e3, Enabled(e3, 1, "forefather-honor", strat3.Id).Command!);
        Check(e3.State.PendingChoice is null, "Family Honor of 0 is not -1 or lower: no window");
        Eq(Honor(e3, 1), 1, "the action resolved");
        // The player's own action never opens it for himself.
        var u = Table(Phase.Action, 0); u.Players[0].Honor = -5;
        var own = Add(u, Forefather, 0, Zone.Hand);
        Kw(Add(u, "training-crane-samurai", 0, Zone.Home), "Ancestor");
        Add(u, BadKharma, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(u);
        Accept(e4, Enabled(e4, 0, "forefather-honor", own.Id).Command!);
        Check(e4.State.PendingChoice is null, "Bad Kharma reacts to another player's action only");
    }

    // ---- Footsteps of Madness --------------------------------------------------------------------------------------------------------------------------------------

    private static void FootstepsScenario()
    {
        MatchEngine Build(bool canPay, out CardInstance footsteps, out CardInstance stronghold, out CardInstance tactician)
        {
            var s = Table(Phase.Action, 1);
            stronghold = s.Cards[s.Players[1].StrongholdId]; stronghold.DefinitionId = KhansEstate; // base Gold Production 5
            tactician = Kw(Add(s, "training-lion-samurai", 1, Zone.Home), "Tactician");
            Deck(s, 1);
            footsteps = Add(s, Footsteps, 0, Zone.Hand);
            Add(s, "training-border-keep", 0, Zone.Home); Add(s, "training-border-keep", 0, Zone.Home);
            if (!canPay) foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && (CardCatalog.Get(c.DefinitionId).GoldProduction > 0))) c.Bowed = true;
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(true, out var footsteps, out var stronghold, out var tactician);
            var hand = e.State.Players[1].Hand.Count;
            Accept(e, Enabled(e, 1, "khans-estate-draw", stronghold.Id, tactician.Id).Command!);
            Eq(e.State.PendingChoice?.Type, "unimpeachableNameReaction", "an action from a Stronghold opens the window");
            var option = Enabled(e, 0, "footsteps-madness-negate", footsteps.Id);
            Eq(option.Gold?.Cost, 5, "the price is the Stronghold's base Gold Production");
            Accept(e, option.Command!);
            PassWindow(e);
            Eq(e.State.Players[1].Hand.Count, hand, "the negated action drew no card");
            Check(e.State.Cards[stronghold.Id].AbilityUses.Count > 0, "the negated action was still taken: its use is spent");
            Eq(e.State.Cards[footsteps.Id].Zone, Zone.FateDiscard, "Footsteps of Madness was discarded");
            Check(e.State.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0 && c.Zone is Zone.Home or Zone.Stronghold).Any(c => c.Bowed), "the Gold was paid by bowing producers");
        }
        {
            // Unable to pay 5 Gold: no window.
            var e = Build(false, out _, out var stronghold, out var tactician);
            var hand = e.State.Players[1].Hand.Count;
            Accept(e, Enabled(e, 1, "khans-estate-draw", stronghold.Id, tactician.Id).Command!);
            Check(e.State.PendingChoice is null, "with no Gold to pay no window opens");
            Eq(e.State.Players[1].Hand.Count, hand + 1, "the action resolved: a card was drawn");
        }
        {
            // An action not from a Stronghold: no window.
            var s = Table(Phase.Action, 1);
            var strategy = Add(s, Forefather, 1, Zone.Hand);
            Kw(Add(s, "training-lion-samurai", 1, Zone.Home), "Ancestor");
            Add(s, Footsteps, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Enabled(e, 1, "forefather-honor", strategy.Id).Command!);
            Check(e.State.PendingChoice is null, "an action on a Strategy is not an action from a Stronghold");
        }
    }

    // ---- Bend like a Reed ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BendReedScenario()
    {
        MatchEngine Build(out CardInstance mine, out CardInstance theirs, out CardInstance bend1, out CardInstance bend2)
        {
            var s = Table(Phase.Battle, 0);
            mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Chi 3
            theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Chi 3
            var duelCard = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
            bend1 = Add(s, BendReed, 0, Zone.Hand); bend2 = Add(s, BendReed, 0, Zone.Hand);
            Add(s, "training-focus-strategy", 0, Zone.Hand);
            Deck(s, 1);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "steel-on-steel-duel", duelCard.Id, theirs.Id, mine.Id));
            return e;
        }
        {
            // Player 1 (challenged) focuses from the deck, player 0 focuses a card, player 1 strikes: player 0's Personality did not strike.
            var e = Build(out var mine, out var theirs, out var bend1, out var bend2);
            var focus0 = e.State.Players[0].Hand.First(id => e.State.Cards[id].DefinitionId == "training-focus-strategy");
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: focus0));
            Accept(e, new GameCommand("strike", 1, e.Version));
            Eq(e.State.PendingChoice?.Type, "duelReaction", "a window opens before the Focus Effects resolve");
            Eq(e.State.PendingDuel?.Stage, "FocusEffectsReaction", "in its own stage");
            Check(!e.QueryActions(1).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == "bend-reed-boost"), "the player who struck holds no such reaction");
            var play = e.QueryActions(0).First(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == "bend-reed-boost" && a.CardId == bend1.Id);
            Accept(e, play.Command!);
            // Issue #644: the other copy may not also be played in this duel and the opponent holds no reaction, so nobody is asked to pass: the window closes and the duel resolves.
            Check(e.State.PendingChoice is null, "only one copy of Bend like a Reed per duel: nothing else is offered, so the window closed");
            Eq(e.State.Cards[theirs.Id].Zone, Zone.Dead, "with +2 the tie became a win: the loser was destroyed");
            Eq(e.State.Cards[mine.Id].Zone, Zone.Battlefield, "and his Personality survived");
            Eq(e.State.Cards[bend1.Id].Zone, Zone.FateDiscard, "the copy was discarded");
        }
        {
            // The Personality struck himself: no window, the tie loses.
            var e = Build(out var mine, out var theirs, out var bend1, out _);
            var focus0 = e.State.Players[0].Hand.First(id => e.State.Cards[id].DefinitionId == "training-focus-strategy");
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: focus0));
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck")); // player 1 focuses again; now it is player 0's turn
            Accept(e, new GameCommand("strike", 0, e.Version));
            Check(e.State.PendingChoice is null, "the player who struck has no Bend like a Reed to play: no window");
            Eq(e.State.Cards[bend1.Id].Zone, Zone.Hand, "the copy stays in the hand");
        }
    }

    // ---- Breath of the Heavens ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void BreathScenario()
    {
        MatchEngine Build(int tokens, out CardInstance card, out CardInstance fire, out CardInstance enemy, out CardInstance plain)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, Breath, 0, Zone.Hand);
            fire = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Fire");
            plain = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
            for (var i = 0; i < tokens; i++) fire.Tokens.Add(new CardToken($"tok-{i}", "Fire", "test", "Force", 1));
            return MatchEngine.FromTestState(s);
        }
        {
            var e = Build(1, out var card, out var fire, out var enemy, out var plain);
            Check(!CanUse(e, 0, "breath-heavens-attack", card.Id, plain.Id), "a Personality who is neither Fire nor Tattooed may not perform");
            Accept(e, Batch336Support.Options(e, 0, "breath-heavens-attack", card.Id, fire.Id).First(o => o.Enabled).Command! with { TargetId = enemy.Id });
            Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "strength 3 + 1 for his Fire token reaches Force 4");
            Eq(e.State.Cards[fire.Id].Tokens.Count(t => t.Type == "Fire"), 2, "and he gains a +1F Fire token");
        }
        {
            var e = Build(0, out var card, out var fire, out var enemy, out _);
            Accept(e, Batch336Support.Options(e, 0, "breath-heavens-attack", card.Id, fire.Id).First(o => o.Enabled).Command! with { TargetId = enemy.Id });
            Eq(e.State.Cards[enemy.Id].Zone, Zone.Battlefield, "with no token the strength is 3: Force 4 survives");
            Eq(e.State.Cards[fire.Id].Tokens.Count(t => t.Type == "Fire"), 1, "the token is given all the same");
            Eq(e.State.Cards[fire.Id].Tokens.Single().Amount, 1, "a +1F Fire token");
        }
        {
            // Tattooed qualifies too, and the bowed one does not.
            var s = Table(Phase.Battle, 0);
            var card = Add(s, Breath, 0, Zone.Hand);
            var tattooed = Kw(Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Tattooed");
            var bowed = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Fire");
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Check(CanUse(e, 0, "breath-heavens-attack", card.Id, tattooed.Id), "a Tattooed Personality may perform");
            Check(!CanUse(e, 0, "breath-heavens-attack", card.Id, bowed.Id), "a bowed Fire Personality may not");
        }
    }

    // ---- Burn the Towers -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BurnTowersScenario()
    {
        MatchEngine Build(bool bowed, out CardInstance card, out CardInstance scout, out CardInstance plain)
        {
            var s = Table(Phase.BattleSelection, 0); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
            scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, bowed: bowed, province: 0);
            plain = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            card = Add(s, BurnTowers, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
            return e;
        }
        {
            var e = Build(false, out var card, out var scout, out var plain);
            Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open");
            Check(!CanUse(e, 0, "burn-towers-recon", card.Id, plain.Id), "a Personality who is not a Scout may not perform");
            var strength = e.ProvinceStrengthForTest(1, 0);
            Accept(e, Enabled(e, 0, "burn-towers-recon", card.Id, scout.Id).Command!);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "its player has Reconnaissance there");
            Eq(e.ProvinceStrengthForTest(1, 0), strength - 3, "the battlefield's province has -3 strength");
            Eq(Batch336Support.StrengthNextTurn(e, 1, 0), strength, "Burn the Towers: until end of turn (issue #492 ruling R3)");
            Eq(e.ProvinceStrengthForTest(1, 1), e.ProvinceStrengthForTest(1, 0) + 3, "another province is untouched");
        }
        {
            var e = Build(true, out var card, out var scout, out _);
            Check(!CanUse(e, 0, "burn-towers-recon", card.Id, scout.Id), "a bowed Scout may not perform: unbowed is printed");
        }
    }

    // ---- Caught in the Act -------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CaughtScenario()
    {
        // One targeted Personality: Dangerous Indulgence bows him; Caught in the Act negates it.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var strategy = Add(s, DangerousIndulgence, 1, Zone.Hand);
        var tactician = Kw(Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0), "Tactician"); // Chi 4
        var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var caught = Add(s, Caught, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Options(e, 1, "dangerous-indulgence-bow", strategy.Id, tactician.Id).First(o => o.Enabled).Command! with { TargetId = victim.Id });
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the action targets one of the reactor's Personalities and bows");
        Accept(e, Enabled(e, 0, "caught-act-negate-bow", caught.Id).Command!);
        PassWindow(e);
        Check(!e.State.Cards[victim.Id].Bowed, "his bowing was negated");
        Eq(e.State.Cards[caught.Id].Zone, Zone.FateDiscard, "Caught in the Act was discarded");

        // Two targeted Personalities: Final Confrontation (a Duelist bows one or two), both bows negated.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 1;
        var confrontation = Add(t, FinalConfrontation, 1, Zone.Hand);
        var duelist = Kw(Add(t, "training-lion-veteran", 1, Zone.Battlefield, province: 0), "Duelist"); // Chi 4
        var a = Add(t, "training-scout-samurai", 0, Zone.Battlefield, province: 0); // Chi 2
        var b = Add(t, "training-scout-samurai", 0, Zone.Battlefield, province: 0); // Chi 2
        var caught2 = Add(t, Caught, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Enabled(e2, 1, "final-confrontation-bow", confrontation.Id, duelist.Id).Command! with { Selections = [Pick("targets", a.Id, b.Id)], TargetId = null });
        Eq(e2.State.PendingChoice?.Type, "beforeActionReaction", "a window for the first target");
        Accept(e2, Enabled(e2, 0, "caught-act-negate-bow", caught2.Id).Command!);
        PassWindow(e2);
        Check(!e2.State.Cards[a.Id].Bowed && !e2.State.Cards[b.Id].Bowed, "the bowing of both targeted Personalities was negated");

        // An action that does not bow: no window.
        var u = Table(Phase.Battle, 1); u.PriorityPlayerId = 1;
        var move = Add(u, Paragon, 1, Zone.Hand);
        var paragon = Kw(Add(u, "training-lion-veteran", 1, Zone.Battlefield, province: 0), "Paragon");
        var victim3 = Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(u, Caught, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(u);
        Accept(e3, Offer(e3, 1, "paragon-strength-move", move.Id, victim3.Id, paragon.Id));
        Check(e3.State.PendingChoice is null, "an action that only moves its target gives Caught in the Act nothing to negate");
        Eq(e3.State.Cards[victim3.Id].Zone, Zone.Home, "the move happened");
    }

    // ---- Control the Field -------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ControlFieldScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, ControlField, 0, Zone.Hand);
        var scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, bowed: true, province: 0); // Chi 2, Scout; bowed is fine
        var tactician = Kw(Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0), "Tactician");
        tactician.Modifiers.Add(new("test-source", "test", "Chi", -1));
        var weak = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
        weak.Modifiers.Add(new("test-source", "test", "Chi", -1)); // Chi 2, neither Scout nor Tactician
        var strong = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Chi 3
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var terrain = Add(s, ExtendedManeuvers, 1, Zone.Battlefield, province: 0);
        var handTerrain = Add(s, FloodedPass, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "control-field-destroy", card.Id, weak.Id), "a Personality with neither Scout, Tactician nor 3 Chi may not perform");
        Check(CanUse(e, 0, "control-field-destroy", card.Id, scout.Id) && CanUse(e, 0, "control-field-destroy", card.Id, tactician.Id) && CanUse(e, 0, "control-field-destroy", card.Id, strong.Id), "a Scout, a Tactician and a Chi-3 Personality may");
        Accept(e, Offer(e, 0, "control-field-destroy", card.Id, terrain.Id, strong.Id));
        Eq(e.State.Cards[terrain.Id].Zone, Zone.FateDiscard, "the Terrain was destroyed");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds is { } ids && ids.Contains(handTerrain.Id)), "an additional Battle action to play a Terrain in the hand");
        Eq(e.State.PriorityPlayerId, 0, "its player keeps priority");
        Accept(e, new GameCommand("playStrategy", 0, e.Version, CardId: handTerrain.Id));
        Eq(e.State.Cards[handTerrain.Id].Zone, Zone.Battlefield, "the Terrain was played with the additional action");
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "and the grant was spent");
        // Nothing in the hand to play: no grant.
        var t = Table(Phase.Battle, 0);
        var card2 = Add(t, ControlField, 0, Zone.Hand);
        var strong2 = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var terrain2 = Add(t, ExtendedManeuvers, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 0, "control-field-destroy", card2.Id, terrain2.Id, strong2.Id));
        Check(!e2.State.AdditionalActions.Any(g => g.PlayerId == 0), "with no Terrain to play no additional action is granted");
    }

    // ---- Dangerous Indulgence ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void DangerousIndulgenceScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, DangerousIndulgence, 0, Zone.Hand);
        var tactician = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Tactician"); // Chi 3
        var bowedTactician = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Tactician");
        var ally = Add(s, "training-crane-courtier", 0, Zone.Battlefield, bowed: true, province: 0); // Chi 3, bowed
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Chi 3
        var mighty = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Chi 4
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "dangerous-indulgence-bow", card.Id, bowedTactician.Id), "a bowed Tactician may not perform");
        var bow = Batch336Support.Options(e, 0, "dangerous-indulgence-bow", card.Id, tactician.Id).First(o => o.Enabled);
        Check(bow.TargetIds!.Contains(enemy.Id) && bow.TargetIds.Contains(ally.Id) && !bow.TargetIds.Contains(mighty.Id), "any Personality with equal or lower Chi, either side's; not a higher one");
        Accept(e, bow.Command! with { TargetId = enemy.Id });
        Check(e.State.Cards[enemy.Id].Bowed, "bowed");
        // One use between the two choices.
        Check(!CanUse(e, 0, "dangerous-indulgence-straighten", card.Id, tactician.Id), "bow and straighten share one use");
        // The straighten choice.
        var t = MatchEngine.FromTestState(s);
        Accept(t, Batch336Support.Options(t, 0, "dangerous-indulgence-straighten", card.Id, tactician.Id).First(o => o.Enabled).Command! with { TargetId = ally.Id });
        Check(!t.State.Cards[ally.Id].Bowed, "straightened");
    }
}
