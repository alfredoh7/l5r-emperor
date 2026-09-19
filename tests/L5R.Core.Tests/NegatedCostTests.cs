using static RegressionAssertions;
using L5R.Core;

// Negated actions still pay their costs, atomically (issue #351, Mendon's rulings on PR #344), Fukuzo's copy end conditions, and the Unfortunate
// Hesitation bar on innate actions. Every reading is in docs/decisions/2026-09-19-copy-and-either-or-primitives.md. "Negated" is exercised two ways:
// through Yoritomo Sakuma's decline path (a real Political action in a real reaction window) and through the engine's test seam that resolves every
// announced action as a whole-action negation (MatchEngine.NegateEveryAction), which reaches the abilities no Political action names. The blocking
// negation Hida Mochitoko will supply is stood in for by MatchEngine.CostBlockHook, the fake predicate behind the CostPaymentBlockedByNegation
// extension point.
internal static class NegatedCostTests
{
    private const string Sakuma = "oracle-l5r-9624", Fukuzo = "oracle-l5r-2728", Adi = "oracle-l5r-9485", Kamina = "oracle-l5r-224", Obote = "oracle-l5r-10848";
    private const string Furugaro = "oracle-l5r-10236", Hunter = "oracle-l5r-3522", Torazu = "oracle-l5r-8590", Slanderer = "oracle-l5r-7291", Rin = "oracle-l5r-7151";
    private const string Goju = "oracle-l5r-2851", Donor = "training-skin-donor", Receiver = "training-skin-receiver", Negate = "sakuma-political-negate";
    private const string HonorCost = "training-honor-cost-attack", HonorStrategy = "training-honor-cost-strategy";
    private const string Reason = "You must pass at this opportunity (Unfortunate Hesitation).";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Sakuma's decline path: a negated Political action still pays what it costs (Slanderer bows, Shosuro Rin bows) and none of its effects happen (no dishonor, no shame decision)", SakumaDeclinePaysCosts),
            ("A negated action pays a cost bow and skips its effect (Akodo Kamina bows, the +4F does not happen), while the same action un-negated does both", NegatedBowSource),
            ("A negated action pays a cost discard (Togashi Obote discards the chosen card and gets no Force bonus)", NegatedDiscard),
            ("A negated action pays an Honor-loss COST (Honor is lost, the Attack does not happen); an Honor loss that is an EFFECT (Yogo Adi on a dishonorable target) is negated with the other effects", NegatedHonorLoss),
            ("A negated action pays a remove-from-game cost (target or itself), a sacrifice, a token destruction, a return to a deck and a Ring's bow-unless-played-from-hand, and none of their effects happen", NegatedOtherVerbs),
            ("Atomic payment (the fake blocking predicate): a blocked cost negates the ENTIRE action -- nothing is paid, not the Honor loss, not the other cost -- whether or not the action was negated; an unblocked negated action pays every cost including the Honor loss", AtomicCostPayment),
            ("The alternative cost the acting player chose (Kitsu Fukui: bow or destroy the Ancestor) is a cost like any other: paid when the action is negated, and it is what a blocking predicate is asked about, in which case nothing is paid", CostOptionIsPaidAndAtomic),
            ("The blocking predicate is asked once per cost, in order, with the card the cost applies to, and only for the costs the action really pays", BlockingPredicateSeesEveryCost),
            ("Fukuzo's copies end when he is removed from the game (announced), a permanent change stays, and a Personality can only be reshuffled into the Dynasty deck, never a Fate deck or a hand", CopiesEndOnRemoval),
            ("Unfortunate Hesitation bars Order Seppuku: listed disabled with the reason and refused at the owing player's opportunity (with or without priority), allowed again once the pass is made or the phase has moved on", SeppukuBarred),
            ("The Hesitation bar leaves what is not an action alone: attack and defense assignment, a Dynasty Phase command, and answering a pending decision", BarSparesNonActions),
        ];
        runner.Run("negated-costs", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"nc-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    private static MatchState Table(Phase phase = Phase.Action, int actor = 0)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = phase; s.ActivePlayerId = actor; s.PriorityPlayerId = actor;
        if (phase == Phase.Battle) { s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; }
        return s;
    }

    private static CardInstance Now(MatchEngine e, string id) => e.State.Cards[id];

    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? target = null, string? performer = null) =>
        new("useAbility", player, e.Version, CardId: cardId, TargetId: target, AbilityId: ability, PerformerId: performer);

    // A targeted action through the option the engine offers; an untargeted one when no target is given.
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string? targetId = null)
    {
        var option = e.QueryActions(player).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.CardId == cardId && a.Command?.AbilityId == ability
            && (targetId is null || (a.TargetIds?.Contains(targetId) ?? false)));
        Check(option is not null, $"{ability} on {cardId} against {targetId} was not offered to player {player}: "
            + string.Join("; ", e.QueryActions(player).Where(a => a.Type == "useAbility" && a.CardId == cardId).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return targetId is null ? option!.Command! : option!.Command! with { TargetId = targetId };
    }

    private static void Pass(MatchEngine e, int player) => Accept(e, new GameCommand("pass", player, e.Version));

    private static void Answer(MatchEngine e, int player, string key)
    {
        var option = e.QueryActions(player).SingleOrDefault(a => a.Type == "chooseEitherOr" && a.Command?.Choice == key);
        Check(option is { Enabled: true, Command: not null }, $"answering {key} was not offered");
        Accept(e, option!.Command!);
    }

    // Sakuma's reaction from the window, then the acting player declines to pay, and the window closes: the action resolves negated.
    private static void NegateWithSakuma(MatchEngine e, CardInstance sakuma)
    {
        var option = e.QueryActions(0).SingleOrDefault(a => a.Type == "useAbility" && a.CardId == sakuma.Id && a.Command?.AbilityId == Negate);
        Check(option is { Enabled: true }, "Sakuma's Political Reaction was not offered: " + string.Join("; ", e.QueryActions(0).Select(a => $"{a.Type}:{a.Label}[{a.Enabled}] {a.Reason}")));
        Accept(e, option!.Command!);
        Answer(e, 1, "negate");
        Check(e.State.DeferredActionNegated, "the action is marked negated");
        Pass(e, 1); Pass(e, 0);
        Check(e.State.PendingChoice is null && e.State.PendingNegatableAbility is null && !e.State.DeferredActionNegated, "everything is cleared");
    }

    // The engine with every announced action resolved as a whole-action negation, and the command already taken.
    private static MatchEngine Negated(MatchState s, Func<MatchEngine, GameCommand> command)
    {
        var e = MatchEngine.FromTestState(s);
        e.NegateEveryAction = true;
        Accept(e, command(e));
        return e;
    }

    private static string LastUse(MatchEngine e) => e.State.Events.Last(x => x.Type == "abilityUsed").Message;

    // ---- Sakuma's decline path ------------------------------------------------------------------------------------
    private static void SakumaDeclinePaysCosts()
    {
        // Slanderer ("Bow this card: if you control a Courtier or Magistrate, dishonor a target Personality"): the acting player is player 1, so
        // Sakuma's controller (player 0) reacts.
        var s = Table(Phase.Action, 1);
        var sakuma = Add(s, Sakuma, 0, Zone.Home);
        var target = Add(s, "training-crane-samurai", 0, Zone.Home);
        var slanderer = Add(s, Slanderer, 1, Zone.Home);
        Add(s, "training-magistrate-personality", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "slanderer-dishonor", slanderer.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction");
        Check(!Now(e, slanderer.Id).Bowed, "nothing is paid while the action waits in the window");
        NegateWithSakuma(e, Now(e, sakuma.Id));
        Check(Now(e, slanderer.Id).Bowed, "the cost (bow Slanderer) was paid though the action was negated");
        Check(!Now(e, target.Id).Dishonorable, "the effect (dishonor) did not happen");
        Check(LastUse(e).Contains("(negated)"), LastUse(e));
        // The control: not negated, both happen.
        s = Table(Phase.Action, 1);
        target = Add(s, "training-crane-samurai", 0, Zone.Home); slanderer = Add(s, Slanderer, 1, Zone.Home); Add(s, "training-magistrate-personality", 1, Zone.Home);
        e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "slanderer-dishonor", slanderer.Id, target.Id));
        Check(Now(e, slanderer.Id).Bowed && Now(e, target.Id).Dishonorable, "un-negated: bowed and dishonored");
        // Shosuro Rin: her bow is the cost; the shame decision (the effect) is never opened.
        s = Table(Phase.Action, 1);
        sakuma = Add(s, Sakuma, 0, Zone.Home); target = Add(s, "training-crane-samurai", 0, Zone.Home);
        var rin = Add(s, Rin, 1, Zone.Home);
        e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "rin-shame", rin.Id, target.Id));
        NegateWithSakuma(e, Now(e, sakuma.Id));
        Check(Now(e, rin.Id).Bowed, "Rin bowed (the cost)");
        Check(e.State.PendingChoice is null && !e.State.Events.Any(x => x.Type == "reactionWindow" && x.Message.Contains("Shosuro Rin")), "and no shame decision was opened (the effect)");
        Check(!Now(e, target.Id).Bowed && !Now(e, target.Id).Dishonorable, "the target is untouched");
    }

    // ---- the verbs, through the whole-action negation seam ----------------------------------------------------------
    private static void NegatedBowSource()
    {
        var s = Table();
        var kamina = Add(s, Kamina, 0, Zone.Home); var ally = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = Negated(s, e => Use(e, 0, "kamina-boost", kamina.Id, ally.Id));
        Check(Now(e, kamina.Id).Bowed, "the cost bow was paid");
        Check(!Now(e, ally.Id).Modifiers.Any(m => m.Amount == 4), "the +4F did not happen");
        Check(LastUse(e).Contains("(negated)"), LastUse(e));
        Eq(e.State.PriorityPlayerId, 1, "priority passes on as after any action");
        // The control: the same action un-negated does both.
        var f = MatchEngine.FromTestState(s);
        Accept(f, Use(f, 0, "kamina-boost", kamina.Id, ally.Id));
        Check(Now(f, kamina.Id).Bowed && Now(f, ally.Id).Modifiers.Any(m => m.Amount == 4), "un-negated: bow and +4F");
    }

    private static void NegatedDiscard()
    {
        var s = Table();
        var obote = Add(s, Obote, 0, Zone.Home); var card = Add(s, "training-focus-strategy", 0, Zone.Hand);
        var e = Negated(s, e => Use(e, 0, "obote-boost", obote.Id, card.Id));
        Eq(Now(e, card.Id).Zone, Zone.FateDiscard, "the cost discard was paid");
        Check(!e.State.Players[0].Hand.Contains(card.Id), "and it left the hand");
        Check(!Now(e, obote.Id).Modifiers.Any(), "no Force bonus");
        var f = MatchEngine.FromTestState(s);
        Accept(f, Use(f, 0, "obote-boost", obote.Id, card.Id));
        Check(Now(f, obote.Id).Modifiers.Any(m => m.Amount == 3), "un-negated: the bonus (Focus Value 3)");
    }

    private static void NegatedHonorLoss()
    {
        // Cost: "Lose 2 Honor" (the training Honor-cost Attack, a Strategy played from the hand).
        var s = Table(Phase.Battle, 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, HonorStrategy, 0, Zone.Hand);
        var honor = s.Players[0].Honor;
        var e = Negated(s, e => Use(e, 0, HonorCost, strategy.Id, enemy.Id));
        Eq(e.State.Players[0].Honor, honor - 2, "the Honor-loss cost was paid though the effects were negated");
        Eq(Now(e, enemy.Id).Zone, Zone.Battlefield, "the Attack did not happen");
        Eq(Now(e, strategy.Id).Zone, Zone.FateDiscard, "the Strategy was spent");
        // The control: un-negated, the Attack is declared (and the Honor is paid the same).
        var f = MatchEngine.FromTestState(s);
        Accept(f, Use(f, 0, HonorCost, strategy.Id, enemy.Id));
        Eq(f.State.Players[0].Honor, honor - 2); Eq(Now(f, enemy.Id).Zone, Zone.Dead, "un-negated: the Ranged 3 Attack destroyed the Force 3 target");
        // Effect: Yogo Adi's Honor loss on a dishonorable target is one of the action's EFFECTS -- negated with the rest.
        s = Table(Phase.Action, 1);
        var adi = Add(s, Adi, 1, Zone.Home); var dishonorable = Add(s, "training-crane-samurai", 0, Zone.Home); dishonorable.Dishonorable = true;
        honor = s.Players[0].Honor;
        e = Negated(s, e => Use(e, 1, "yogo-adi-choice", adi.Id, dishonorable.Id));
        Eq(e.State.Players[0].Honor, honor, "an Honor loss that is an effect is negated with the other effects");
        f = MatchEngine.FromTestState(s);
        Accept(f, Use(f, 1, "yogo-adi-choice", adi.Id, dishonorable.Id));
        Eq(f.State.Players[0].Honor, honor - 1, "un-negated: the effect loses the Honor");
    }

    private static void NegatedOtherVerbs()
    {
        // Remove the target from the game (Fukuzo's cost): removed, and no copy decision follows.
        var s = Table();
        var fuk = Add(s, Fukuzo, 0, Zone.Home); var donor = Add(s, Donor, 1, Zone.DynastyDiscard);
        var e = Negated(s, e => Offer(e, 0, "fukuzo-wear-skin", fuk.Id, donor.Id));
        Eq(Now(e, donor.Id).Zone, Zone.RemovedFromGame, "the removal (the cost) was paid");
        Check(e.State.PendingCopy is null && !Now(e, fuk.Id).GrantedKeywords.Any() && !Now(e, fuk.Id).GrantedAbilities.Any(), "nothing was copied");
        // Bow the chosen Personality (Lobby, a player ability: "bow a Personality to take Favor"): bowed, and the Favor is not taken.
        // (A player ability needs the published deck profile, and strictly more Honor than the opponent.)
        s = MatchEngine.CreatePublishedTestMatch(11).State;
        s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Players[0].Honor = 10; s.Players[1].Honor = 7;
        var lobbyist = Add(s, "training-crane-samurai", 0, Zone.Home);
        e = Negated(s, e => new GameCommand("useAbility", 0, e.Version, TargetId: lobbyist.Id, AbilityId: "lobby"));
        Check(Now(e, lobbyist.Id).Bowed, "the Personality bowed (the cost)"); Check(e.State.FavorPlayerId is null, "and the Imperial Favor was not taken");
        var control = MatchEngine.FromTestState(s);
        Accept(control, new GameCommand("useAbility", 0, control.Version, TargetId: lobbyist.Id, AbilityId: "lobby"));
        Eq(control.State.FavorPlayerId, 0, "un-negated: the Favor is taken");
        // Remove this card from the game (Hunter of Harmony): removed; the Kiho Strategy stays in the discard pile.
        s = Table();
        var host = Add(s, "training-crane-samurai", 0, Zone.Home); var hunter = Add(s, Hunter, 0, Zone.Attached, parent: host.Id);
        var kiho = Add(s, "oracle-l5r-9186", 0, Zone.FateDiscard);
        e = Negated(s, e => Use(e, 0, "hunter-of-harmony-return", hunter.Id, kiho.Id));
        Eq(Now(e, hunter.Id).Zone, Zone.RemovedFromGame, "Hunter of Harmony removed itself as the cost");
        Eq(Now(e, kiho.Id).Zone, Zone.FateDiscard, "the Strategy did not return to the hand");
        // Destroy this Follower (Furugaro): destroyed; no Charging tokens.
        s = Table();
        host = Add(s, "training-crane-samurai", 0, Zone.Home); var furugaro = Add(s, Furugaro, 0, Zone.Attached, parent: host.Id);
        var fudo = Add(s, "training-fudo-personality", 0, Zone.Home);
        e = Negated(s, e => Use(e, 0, "furugaro-charge", furugaro.Id, fudo.Id));
        Eq(Now(e, furugaro.Id).Zone, Zone.FateDiscard, "the sacrifice was paid");
        Eq(Now(e, fudo.Id).Tokens.Count(t => t.Type == "Charging"), 0, "no Charging tokens");
        // Destroy a Fire token (Togashi Torazu): one token gone; no Attack.
        s = Table(Phase.Battle, 0);
        var torazu = Add(s, Torazu, 0, Zone.Battlefield, province: 0);
        torazu.Tokens.Add(new("t1", "Fire", "test-fixture", "Force", 1)); torazu.Tokens.Add(new("t2", "Fire", "test-fixture", "Force", 1));
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        e = Negated(s, e => Use(e, 0, "torazu-fire-attack", torazu.Id));
        Eq(Now(e, torazu.Id).Tokens.Count, 1, "the token cost was paid"); Check(e.State.PendingAttack is null, "no Attack");
        // Put this card on the deck (Goju House Guard): Goju goes to the Fate deck; the target is untouched.
        s = Table(Phase.Battle, 0);
        var wielder = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var goju = Add(s, Goju, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var victim = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        e = Negated(s, e => Use(e, 0, "goju-house-guard-return", goju.Id, victim.Id) with { DeckPosition = "Top" });
        Eq(Now(e, goju.Id).Zone, Zone.FateDeck, "the return to the deck was paid"); Eq(Now(e, victim.Id).Zone, Zone.Battlefield, "the target stayed");
        // A Ring ("bow this card or discard it from your hand"): bowed in play, discarded (not bowed) from the hand; the province is unchanged either way.
        foreach (var fromHand in new[] { false, true })
        {
            s = Table(Phase.Battle, 0);
            var ring = Add(s, "training-ring-of-earth-stub", 0, fromHand ? Zone.Hand : Zone.Home);
            var strength = s.Players[1].Provinces[0].Strength;
            e = Negated(s, e => new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "ring-of-earth-raise", TargetPlayerId: 1, Province: 0));
            Eq(e.State.Players[1].Provinces[0].Strength, strength, "the effect did not happen");
            if (fromHand) { Eq(Now(e, ring.Id).Zone, Zone.FateDiscard, "from the hand it is spent"); Check(!Now(e, ring.Id).Bowed, "and not bowed"); }
            else Check(Now(e, ring.Id).Bowed, "in play it bows");
        }
    }

    // ---- atomic payment --------------------------------------------------------------------------------------------
    private static MatchState HonorRig(out CardInstance strategy, out CardInstance enemy)
    {
        var s = Table(Phase.Battle, 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        strategy = Add(s, HonorStrategy, 0, Zone.Hand);
        return s;
    }

    private static void AtomicCostPayment()
    {
        // Blocked, not negated: the ordinary flow pays nothing and resolves nothing -- yet the action was taken.
        var s = HonorRig(out var strategy, out var enemy1); var honor = s.Players[0].Honor;
        var e = MatchEngine.FromTestState(s);
        e.CostBlockHook = (cost, _) => cost.Verb == CostVerbs.LoseHonor;
        Accept(e, Use(e, 0, HonorCost, strategy.Id, enemy1.Id));
        Eq(e.State.Players[0].Honor, honor, "blocked: the Honor loss was NOT paid");
        Eq(Now(e, enemy1.Id).Zone, Zone.Battlefield, "and no Attack");
        Eq(Now(e, strategy.Id).Zone, Zone.FateDiscard, "the action was taken: the Strategy left the hand");
        Eq(e.State.PriorityPlayerId, 1, "and priority passed on");
        Check(LastUse(e).Contains("nothing was paid"), "announced as negated because a cost could not be paid: " + LastUse(e));
        // Blocked AND negated: still nothing paid.
        s = HonorRig(out strategy, out enemy1);
        e = MatchEngine.FromTestState(s); e.NegateEveryAction = true; e.CostBlockHook = (cost, _) => cost.Verb == CostVerbs.LoseHonor;
        Accept(e, Use(e, 0, HonorCost, strategy.Id, enemy1.Id));
        Eq(e.State.Players[0].Honor, honor, "blocked and negated: nothing is paid");
        // Not blocked but negated: the cost, the Honor loss included, is paid; the effects are not.
        s = HonorRig(out strategy, out enemy1);
        e = MatchEngine.FromTestState(s); e.NegateEveryAction = true; e.CostBlockHook = (_, _) => false;
        Accept(e, Use(e, 0, HonorCost, strategy.Id, enemy1.Id));
        Eq(e.State.Players[0].Honor, honor - 2, "unblocked, effects negated: the Honor loss is paid"); Eq(Now(e, enemy1.Id).Zone, Zone.Battlefield, "and no Attack");
        // Not blocked, not negated: paid and resolved.
        s = HonorRig(out strategy, out enemy1);
        e = MatchEngine.FromTestState(s); e.CostBlockHook = (_, _) => false;
        Accept(e, Use(e, 0, HonorCost, strategy.Id, enemy1.Id));
        Eq(e.State.Players[0].Honor, honor - 2); Eq(Now(e, enemy1.Id).Zone, Zone.Dead, "unblocked: the Attack resolved");
        // Two costs, one blocked (Ring of Fire bows itself and destroys the performer): the OTHER is not paid either, in both flows.
        foreach (var negated in new[] { false, true })
        {
            s = Table(Phase.Battle, 0);
            var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            var ring = Add(s, "training-ring-of-fire-stub", 0, Zone.Home);
            e = MatchEngine.FromTestState(s); e.NegateEveryAction = negated;
            e.CostBlockHook = (cost, _) => cost.Verb == CostVerbs.Sacrifice;
            Accept(e, Use(e, 0, "ring-of-fire-destroy", ring.Id, enemy.Id, performer.Id));
            Check(!Now(e, ring.Id).Bowed, $"one cost blocked (negated={negated}): the ring is NOT bowed either");
            Eq(Now(e, performer.Id).Zone, Zone.Battlefield, "the performer lives"); Eq(Now(e, enemy.Id).Zone, Zone.Battlefield, "and there is no effect");
        }
    }

    private static void CostOptionIsPaidAndAtomic()
    {
        // Kitsu Fukui: "Battle: Bow or destroy your performing Ancestor Personality, and target an enemy card: Bow it. Gain 1 Honor."
        MatchState Rig(out CardInstance fukui, out CardInstance ancestor, out CardInstance enemy)
        {
            var st = Table(Phase.Battle, 0);
            fukui = Add(st, Support337.Fukui, 0, Zone.Battlefield, province: 0);
            ancestor = Add(st, "training-ancestor", 0, Zone.Battlefield, province: 0);
            enemy = Add(st, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return st;
        }
        foreach (var way in new[] { CostVerb.Bow, CostVerb.Destroy })
        {
            // Negated: the chosen way is paid, the effects (bow the target, gain 1 Honor) are not.
            var s = Rig(out var fukui, out var ancestor, out var enemy); var honor = s.Players[0].Honor;
            var e = Negated(s, e => new GameCommand("useAbility", 0, e.Version, CardId: fukui.Id, TargetId: enemy.Id, AbilityId: "fukui-bow", PerformerId: ancestor.Id, CostOption: way));
            if (way == CostVerb.Bow) Check(Now(e, ancestor.Id).Bowed && Now(e, ancestor.Id).Zone == Zone.Battlefield, "bow: the Ancestor bowed as the cost");
            else Eq(Now(e, ancestor.Id).Zone, Zone.Dead, "destroy: the Ancestor was destroyed as the cost");
            Check(!Now(e, enemy.Id).Bowed, $"{way}: the target was not bowed (an effect)"); Eq(e.State.Players[0].Honor, honor, $"{way}: no Honor gained (an effect)");
            // Blocked: the predicate is asked about this very cost, with the Ancestor, and a block pays nothing.
            s = Rig(out fukui, out ancestor, out enemy);
            e = MatchEngine.FromTestState(s);
            var asked = new List<(string Verb, string? Subject)>();
            e.CostBlockHook = (cost, subject) => { asked.Add((cost.Verb, subject?.Id)); return cost.Verb == way; };
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: fukui.Id, TargetId: enemy.Id, AbilityId: "fukui-bow", PerformerId: ancestor.Id, CostOption: way));
            Check(asked.SequenceEqual([(way, ancestor.Id)]), $"{way}: asked about the chosen cost and its card: " + string.Join(", ", asked));
            Check(!Now(e, ancestor.Id).Bowed && Now(e, ancestor.Id).Zone == Zone.Battlefield, $"{way}: blocked, so the Ancestor is untouched");
            Check(!Now(e, enemy.Id).Bowed && e.State.Players[0].Honor == honor, $"{way}: and the action did nothing");
        }
    }

    private static void BlockingPredicateSeesEveryCost()
    {
        var s = Table(Phase.Battle, 0);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var ring = Add(s, "training-ring-of-fire-stub", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var asked = new List<(string Verb, string? Subject)>();
        e.CostBlockHook = (cost, subject) => { asked.Add((cost.Verb, subject?.Id)); return false; };
        Accept(e, Use(e, 0, "ring-of-fire-destroy", ring.Id, enemy.Id, performer.Id));
        Check(asked.SequenceEqual([(CostVerb.Bow, ring.Id), (CostVerbs.Sacrifice, performer.Id)]), "each cost, in order, with its card: " + string.Join(", ", asked));
        // A Gold-only action has no other cost to ask about.
        var t = Table(); var free = Add(t, "training-cost-fixture", 0, Zone.Home);
        var f = MatchEngine.FromTestState(t); var count = 0; f.CostBlockHook = (_, _) => { count++; return false; };
        Accept(f, Offer(f, 0, "training-charity-honor", free.Id));
        Eq(count, 0, "nothing to ask");
        // A cost that does not apply is not asked: from the hand a Ring pays no bow.
        var u = Table(Phase.Battle, 0); var handRing = Add(u, "training-ring-of-earth-stub", 0, Zone.Hand);
        var g = MatchEngine.FromTestState(u); var seen = 0; g.CostBlockHook = (_, _) => { seen++; return false; };
        Accept(g, new GameCommand("useAbility", 0, g.Version, CardId: handRing.Id, AbilityId: "ring-of-earth-raise", TargetPlayerId: 1, Province: 0));
        Eq(seen, 0, "from the hand the Ring bows nothing, so there is no bow cost");
    }

    // ---- Fukuzo's end conditions ------------------------------------------------------------------------------------
    private static void CopiesEndOnRemoval()
    {
        // Fukuzo, in a discard pile carrying a copied keyword and ability and a permanent grant, is the donor of a receiver's removal cost.
        var s = Table();
        var fukuzo = Add(s, Fukuzo, 1, Zone.DynastyDiscard);
        fukuzo.GrantedKeywords = [new("Berserker", "fukuzo-wear-skin", "Copy", Donor, "Training skin donor"), new("Kensai", "test-fixture", "Permanent")];
        fukuzo.GrantedAbilities = [new("training-bow-any", "fukuzo-wear-skin", "Copy", Donor, "Training skin donor"), new("training-create-token", "test-fixture", "Permanent")];
        var receiver = Add(s, Receiver, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-copy-keyword", receiver.Id, fukuzo.Id));
        var removed = Now(e, fukuzo.Id);
        Eq(removed.Zone, Zone.RemovedFromGame);
        Check(!removed.GrantedKeywords.Any(g => g.Duration == "Copy") && !removed.GrantedAbilities.Any(g => g.Duration == "Copy"), "his copies ended when he left the game");
        Check(e.State.Events.Any(x => x.Type == "copyEnded" && x.CardId == fukuzo.Id), "announced");
        Check(removed.GrantedKeywords.Any(g => g.Keyword == "Kensai") && removed.GrantedAbilities.Any(g => g.AbilityId == "training-create-token"), "a permanent change is not a copy and stays");
        Check(e.State.PendingCopy is not null, "the receiver's own copy decision opened (the removal was its cost)");
        // While he is dead or in play, nothing ends them: the death case is CardCopyTests.CopiesPersistAndReset. Reshuffled: a Personality goes to its
        // Dynasty deck as a fresh card -- never a Fate deck, never a hand -- whichever effect puts it in a deck (Goju House Guard, on a Fukuzo in play).
        var t = Table(Phase.Battle, 0);
        var host = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var goju = Add(t, Goju, 0, Zone.Attached, province: 0, parent: host.Id);
        var wearer = Add(t, Fukuzo, 1, Zone.Battlefield, province: 0);
        wearer.GrantedKeywords = [new("Berserker", "fukuzo-wear-skin", "Copy", Donor, "Training skin donor")];
        wearer.GrantedAbilities = [new("training-bow-any", "fukuzo-wear-skin", "Copy", Donor, "Training skin donor")];
        var f = MatchEngine.FromTestState(t);
        Accept(f, Use(f, 0, "goju-house-guard-return", goju.Id, wearer.Id) with { DeckPosition = "Bottom" });
        var back = Now(f, wearer.Id);
        Eq(back.Zone, Zone.DynastyDeck, "into the Dynasty deck");
        Check(back.GrantedKeywords.Count == 0 && back.GrantedAbilities.Count == 0, "reshuffled: his copies ended");
        Check(f.State.Players[1].DynastyDeck.Contains(wearer.Id) && !f.State.Players[1].FateDeck.Contains(wearer.Id) && !f.State.Players[1].Hand.Contains(wearer.Id), "and it is in no Fate deck or hand");
    }

    // ---- the Hesitation bar on innate actions -------------------------------------------------------------------------
    private static CardInstance Dishonorable(MatchState s, int owner)
    {
        var c = Add(s, Donor, owner, Zone.Home); c.Dishonorable = true; return c;
    }

    private static void SeppukuBarred()
    {
        // The owing player holds priority: Seppuku (an innate Open action) is listed disabled with the reason and refused.
        var s = Table(Phase.Action, 0);
        var samurai = Dishonorable(s, 0);
        s.ForcedPasses.Add(new(0, PrintedPhases.Instance(s.TurnNumber, Phase.Action), "owed"));
        var e = MatchEngine.FromTestState(s);
        var seppuku = e.QueryActions(0).Single(a => a.Type == "orderSeppuku" && a.CardId == samurai.Id);
        Check(!seppuku.Enabled && seppuku.Reason == Reason, "listed disabled with the reason: " + seppuku.Enabled + " / " + seppuku.Reason);
        Reject(e, new GameCommand("orderSeppuku", 0, e.Version, CardId: samurai.Id));
        Check(Now(e, samurai.Id).Zone == Zone.Home, "nothing happened to him");
        // Without priority too (Seppuku ignores priority, so the bar must too).
        var t = Table(Phase.Action, 1);
        var other = Dishonorable(t, 0);
        t.ForcedPasses.Add(new(0, PrintedPhases.Instance(t.TurnNumber, Phase.Action), "owed"));
        var f = MatchEngine.FromTestState(t);
        var waiting = f.QueryActions(0).Single(a => a.Type == "orderSeppuku");
        Check(!waiting.Enabled && waiting.Reason == Reason, "listed disabled for the waiting player as well");
        Reject(f, new GameCommand("orderSeppuku", 0, f.Version, CardId: other.Id));
        // Every other innate action is barred at the same opportunity (the existing rule).
        Check(e.QueryActions(0).Where(a => a.Type is "useAbility" or "playStrategy" or "recruitReserve" or "equip").All(a => !a.Enabled), "no other action is enabled");
        // The pass ends the bar: he may then order Seppuku.
        Pass(e, 0);
        Eq(e.State.ForcedPasses.Count, 0, "the debt is settled");
        var again = e.QueryActions(0).Single(a => a.Type == "orderSeppuku" && a.CardId == samurai.Id);
        Check(again.Enabled && again.Reason is null, "Seppuku is offered enabled once the pass is made");
        Accept(e, new GameCommand("orderSeppuku", 0, e.Version, CardId: samurai.Id));
        Eq(Now(e, samurai.Id).Zone, Zone.Dead);
        // Phase end: a debt of another phase does not bar Seppuku, and one left over from the Action Phase is gone once the next phase begins.
        var u = Table(Phase.Action, 0); Dishonorable(u, 0);
        u.ForcedPasses.Add(new(0, PrintedPhases.Instance(u.TurnNumber, Phase.Dynasty), "lapsed"));
        var g = MatchEngine.FromTestState(u);
        Check(g.QueryActions(0).Single(a => a.Type == "orderSeppuku").Enabled, "a lapsed entry does not bar it");
        var v = Table(Phase.Dynasty, 0); var fourth = Dishonorable(v, 1);
        v.ForcedPasses.Add(new(1, PrintedPhases.Instance(v.TurnNumber, Phase.Action), "left-over"));
        var h = MatchEngine.FromTestState(v);
        Accept(h, new GameCommand("endDynasty", 0, h.Version));
        Eq(h.State.ForcedPasses.Count, 0, "dropped when the next phase began");
        Check(h.QueryActions(1).Single(a => a.Type == "orderSeppuku" && a.CardId == fourth.Id).Enabled, "so Seppuku is available again");
    }

    private static void BarSparesNonActions()
    {
        // The Attack Phase's own assignments are not actions: attack and defense assignment are untouched, and neither settles the debt.
        var s = Table(Phase.AttackDeclaration, 0);
        Add(s, "training-crane-samurai", 0, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Home);
        s.ForcedPasses.Add(new(0, PrintedPhases.Instance(s.TurnNumber, Phase.AttackDeclaration), "owed"));
        s.ForcedPasses.Add(new(1, PrintedPhases.Instance(s.TurnNumber, Phase.AttackDeclaration), "owed"));
        var e = MatchEngine.FromTestState(s);
        var attack = e.QueryActions(0).First(a => a.Type == "attack");
        Check(attack.Enabled, "declaring an attack stays enabled: " + attack.Reason);
        Accept(e, attack.Command!);
        Eq(e.State.Phase, Phase.DefenseAssignment);
        var defend = e.QueryActions(1).First(a => a.Type == "assignDefense");
        Check(defend.Enabled, "assigning a defender stays enabled: " + defend.Reason);
        Accept(e, defend.Command!);
        Eq(e.State.ForcedPasses.Count, 2, "neither assignment paid or lifted a debt");
        // A non-action command outside the Action and Battle phases: the Dynasty Phase's end is untouched by a debt of that phase.
        var d = Table(Phase.Dynasty, 0);
        d.ForcedPasses.Add(new(0, PrintedPhases.Instance(d.TurnNumber, Phase.Dynasty), "owed"));
        var f = MatchEngine.FromTestState(d);
        Check(f.QueryActions(0).Single(a => a.Type == "endDynasty").Enabled, "ending the Dynasty Phase is not an action");
        Accept(f, new GameCommand("endDynasty", 0, f.Version));
        // A pending decision is not an action either: Rin's shame choice is answered as usual while the answering player owes a pass.
        var r = Table(Phase.Action, 0);
        var rin = Add(r, Rin, 0, Zone.Home); var target = Add(r, "training-crane-samurai", 1, Zone.Home);
        r.ForcedPasses.Add(new(1, PrintedPhases.Instance(r.TurnNumber, Phase.Action), "owed"));
        var g = MatchEngine.FromTestState(r);
        Accept(g, Offer(g, 0, "rin-shame", rin.Id, target.Id));
        Eq(g.State.PendingChoice?.Type, "rinShame");
        var answer = g.QueryActions(1).First(a => a.Enabled && a.Command is not null && a.Type is not "concede" and not "orderSeppuku");
        Accept(g, answer.Command!);
        Eq(g.State.ForcedPasses.Count, 1, "answering a decision does not settle the debt");
    }
}
