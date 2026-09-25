using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition three-scans pass (issue #496): Honor Never Falls (strategy-wave-65), Kurohito's Perfection (celestial-wave-9) and Makito (personality-wave-79), the three cards whose scans Mendon supplied after the
// remaining-cards pass left them unwired. Every position is built explicitly and driven only through QueryActions and the commands it offers (the primitives are in src/L5R.Core/EmperorThreeS.cs).
internal static class EmperorThreeSTests
{
    private const string HonorNeverFalls = "oracle-l5r-3426", Kurohito = "oracle-l5r-10258", Makito = "oracle-l5r-4806", Ichizo = "oracle-l5r-5104", FairWarning = "oracle-l5r-10149", WideValley = "oracle-l5r-10182",
        Halls = "oracle-l5r-2950", KaiuOkaru = "oracle-l5r-10043";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void ChiBoost(CardInstance card, int amount) => card.Modifiers.Add(new("test-fixture", "test-fixture", "Chi", amount));
    private static CardInstance StrongholdOf(MatchState s, int player, string definition) { var card = s.Cards[s.Players[player].StrongholdId]; card.DefinitionId = definition; return card; }
    private static string DefinitionWith(string ability) => CardCatalog.Cards.Values.Where(c => c.Abilities?.Contains(ability) == true).OrderBy(c => c.ScenarioOnly).Select(c => c.Id).First();
    private static bool Logged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);

    public static void Run(TestRunner runner) => runner.Run("emperor-three-S", [
        ("Published shape: Honor Never Falls (strategy-wave-65), Kurohito's Perfection (celestial-wave-9) and Makito (personality-wave-79) print their scans' stats, keywords, text and ability ids, ship their art and are in their manifests", PublishedShape),
        ("Honor Never Falls, Battle half: its bowed performing Personality is straightened; a Unique one may take an additional Battle action for one of his base abilities -- taken here with Makito's own Maho Battle, which keeps the priority", HonorNeverFallsBattle),
        ("Honor Never Falls, Battle half, edges: a non-Unique Personality is straightened without the grant; an unbowed one or another player's is not a legal performer; only a base (printed) ability spends the grant -- another Strategy the same Personality performs does not, and forfeits it", HonorNeverFallsBaseOnly),
        ("Honor Never Falls, Focus Effect: no choice is asked; every consequence of losing the duel on the owner's unit is delayed until the end of the turn and applied then", HonorNeverFallsFocus),
        ("Honor Never Falls, Focus Effect \"(from any source)\": a Fair Warning Focus Effect of the winner (its -4F on the loser) is delayed too, in the order of the Focus Effects; the winner's own effects and a winner's Honor Never Falls change nothing", HonorNeverFallsAnySource),
        ("Kurohito's Perfection, trait: with it in play the rulebook Favor Political Limited action may be taken without discarding a card -- the Favor is discarded, the draw is delayed until the End Phase begins; the ordinary action is untouched and both share their use", KurohitoFavor),
        ("Kurohito's Perfection, trait, edges: offered only to its controller, only while the Favor is held, and never once the Favor action of the turn was used", KurohitoFavorEdges),
        ("Kurohito's Perfection, Limited: discard it and permanently give a Personality Cavalry, Duelist, or Tactician (one of the three); Action phase only, its controller's, gone once used", KurohitoLimited),
        ("Makito, static: +1F while a Phoenix Clan player controls him, and only then", MakitoStatic),
        ("Makito, trait: each card destroyed (either player's Personality or attachment) gives him +1F until the turn ends; his own destruction and a card destroyed while he is out of play give nothing", MakitoTrait),
        ("Makito, Maho Battle: an enemy Personality with equal or lower Chi -- the acting player chooses the card without attachments in his unit (the Personality if he has none, else an attachment); nothing else is a legal target", MakitoMaho),
        ("Makito, Maho Battle: the destruction will not be negated (Wide Valley) or delayed (Halls of the Forgotten's delay, a held action's delay), where another action's destruction is", MakitoNotNegated),
    ]);

    // ---- published shape ---------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Manifest(string wave, string id)
        {
            Eq(string.Join(",", File.ReadAllLines(Path.Combine(root, $"tests/L5R.Core.Tests/PublishedWaves/{wave}.txt")).Where(l => l.Length > 0)), id, $"{wave}'s manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Check(File.ReadAllText(Path.Combine(root, $"content/published/{wave}-sources.json")).Contains("image_sha256"), $"{wave}: the source receipt records the scan's sha256");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        var hnf = CardCatalog.Get(HonorNeverFalls);
        Check(hnf.Type == CardType.Strategy && hnf.Name == "Honor Never Falls" && hnf.GoldCost == 0 && hnf.Focus == 3 && !hnf.ScenarioOnly, $"Honor Never Falls: {hnf.Type} cost {hnf.GoldCost} focus {hnf.Focus}");
        Eq(string.Join(",", hnf.Keywords ?? []), "Unique"); Eq(string.Join(",", hnf.Abilities ?? []), "honor-never-falls-focus,honor-never-falls-straighten");
        Eq(hnf.Text, "As a Focus Effect: Delay all consequences of losing this duel (from any source) until the end of the turn.\nBattle: Choose your performing bowed Personality: Straighten him. If he is Unique, you may take an additional Battle action to use one of his base abilities.");
        Manifest("strategy-wave-65", HonorNeverFalls);
        Eq(AbilityCatalog.All["honor-never-falls-focus"].Timing, "FocusEffect"); Eq(AbilityCatalog.All["honor-never-falls-straighten"].Timing, "Battle");

        var kur = CardCatalog.Get(Kurohito);
        Check(kur.Type == CardType.Celestial && kur.Name == "Kurohito's Perfection" && kur.GoldCost == 0 && !kur.ScenarioOnly, $"Kurohito's Perfection: {kur.Type}");
        Eq(string.Join(",", kur.Keywords ?? []), "Unique,Fortune"); Eq(kur.Effect, "kurohito-perfection");
        Eq(string.Join(",", kur.Abilities ?? []), "kurohito-perfection-cavalry,kurohito-perfection-duelist,kurohito-perfection-tactician");
        Eq(kur.Text, "When paying the cost of the rulebook Favor Political Limited action: You may ignore the cost of discarding a card; if you did, delay its effects until your End Phase begins.\nLimited: Discard this card and target a Personality: Permanently give him Cavalry, Duelist, or Tactician.");
        Manifest("celestial-wave-9", Kurohito);

        var mak = CardCatalog.Get(Makito);
        Check(mak.Type == CardType.Personality && mak.Name == "Makito" && mak.Force == 4 && mak.Chi == 4 && mak.GoldCost == 7 && mak.PersonalHonor == 0 && mak.HonorRequirement is null, $"Makito: {mak.Force}/{mak.Chi}/{mak.GoldCost}/{mak.PersonalHonor}/{mak.HonorRequirement}");
        Eq(string.Join(",", mak.Keywords ?? []), "Experienced Asako Makito,Unique,Bloodspeaker,Monk,Order of Death,Shadowlands,Student of Kinuye");
        Eq(mak.Effect, "makito-trait"); Eq(string.Join(",", mak.Abilities ?? []), "makito-maho-destroy");
        Eq(mak.Text, "Makito has +1F while a Phoenix Clan player controls him.\nAfter a card is destroyed: Give Makito +1F.\nMaho Battle: Target an enemy Personality with equal or lower Chi: Destroy a card without attachments in his unit; this destruction will not be negated or delayed.");
        Manifest("personality-wave-79", Makito);
        Eq(PublishedContent.StrategyWave65.Cards.Length, 1); Eq(PublishedContent.CelestialWave9.Cards.Length, 1); Eq(PublishedContent.PersonalityWave79.Cards.Length, 1);
        Check(!AbilityCatalog.All["favor-cycle-kurohito"].Timing.Equals("Battle") && AbilityCatalog.All["favor-cycle-kurohito"].PlayerAbility, "the Favor twin is a player ability of the rulebook action, not a card's");
    }

    // ---- Honor Never Falls: the Battle half --------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Makito, CardInstance Strategy, CardInstance Target) BattleRig(bool makitoBowed = true)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var makito = Add(s, Makito, 1, Zone.Battlefield, bowed: makitoBowed, province: 0);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);   // Chi 3
        var strategy = Add(s, HonorNeverFalls, 1, Zone.Hand);
        return (MatchEngine.FromTestState(s), makito, strategy, target);
    }

    private static void HonorNeverFallsBattle()
    {
        var (e, makito, strategy, target) = BattleRig();
        Check(e.State.Cards[makito.Id].Bowed, "Makito starts bowed (a bowed Personality cannot use his own action)");
        Check(!CanUse(e, 1, "makito-maho-destroy", makito.Id), "bowed, he cannot take his Maho Battle");
        Accept(e, Enabled(e, 1, "honor-never-falls-straighten", strategy.Id, makito.Id).Command!);
        Check(!e.State.Cards[makito.Id].Bowed, "the bowed performer is straightened");
        var grant = e.State.AdditionalActions.Single();
        Check(grant.PlayerId == 1 && grant.PerformerId == makito.Id && grant.BaseAbilityOnly, "an additional action for that Personality's base abilities only");
        Eq(e.State.PriorityPlayerId, 1, "the player keeps the priority");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Makito is Unique: he takes his own Maho Battle as the additional action.
        Accept(e, Offer(e, 1, "makito-maho-destroy", makito.Id, target.Id));
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "the Maho Battle was taken and destroyed the bare Personality");
        Check(e.State.AdditionalActions.Count == 0, "the grant was spent");
        Eq(e.State.PriorityPlayerId, 0, "the additional action was the last of the burst: the priority passes");
        Check(e.State.Events.Any(x => x.Type == "additionalActionGranted"), "the grant is logged");
    }

    private static void HonorNeverFallsBaseOnly()
    {
        // A non-Unique bowed Personality: straightened, no additional action.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var plain = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        var strategy = Add(s, HonorNeverFalls, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 1, "honor-never-falls-straighten", strategy.Id, plain.Id).Command!);
        Check(!e.State.Cards[plain.Id].Bowed, "he is straightened");
        Check(e.State.AdditionalActions.Count == 0, "but he is not Unique, so there is no additional action");
        Eq(e.State.PriorityPlayerId, 0, "the priority passes as after any action");

        // Not legal performers: an unbowed Personality, another player's bowed Personality.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 1;
        var unbowed = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var theirs = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var strategy2 = Add(s2, HonorNeverFalls, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 1, "honor-never-falls-straighten", strategy2.Id), "with no bowed Personality of his own at the battlefield it is not offered");
        Check(!Options(e2, 1, "honor-never-falls-straighten", strategy2.Id).Any(a => a.Label.Contains(theirs.Id)), "another player's bowed Personality is no performer");

        // Only a base ability spends the grant: a Unique Personality (Kaiu Okaru with a Unique keyword) and another Strategy he performs.
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 1;
        var okaru = Add(s3, KaiuOkaru, 1, Zone.Battlefield, bowed: true, province: 0); GiveKeyword(okaru, "Unique");
        var enemy = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var hnf = Add(s3, HonorNeverFalls, 1, Zone.Hand);
        var tireless = Add(s3, DefinitionWith("tireless-efforts-straighten"), 1, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Enabled(e3, 1, "honor-never-falls-straighten", hnf.Id, okaru.Id).Command!);
        Check(e3.State.AdditionalActions.Single().BaseAbilityOnly, "the grant is held");
        // Tireless Efforts is a Strategy, not one of Okaru's printed abilities: while only the base-ability grant is held it is not an action he may take.
        var refused = Options(e3, 1, "tireless-efforts-straighten", tireless.Id, okaru.Id);
        Check(refused.Length > 0 && refused.All(a => !a.Enabled && (a.Reason ?? "").Contains("base abilities")), "a Strategy is not a base ability: not on offer while the grant is held (" + string.Join("; ", refused.Select(a => a.Reason)) + ")");
        Check(e3.State.AdditionalActions.Count == 1 && e3.State.PriorityPlayerId == 1, "nothing changed");
        // Declining the additional action (passing) forfeits it.
        Accept(e3, e3.QueryActions(1).First(a => a.Type == "pass" && a.Enabled).Command!);
        Check(e3.State.AdditionalActions.Count == 0, "passing forfeits the grant");
        // Control: his own printed ability (Okaru's Force penalty) spends the grant and keeps the priority.
        var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = 1;
        var okaru4 = Add(s4, KaiuOkaru, 1, Zone.Battlefield, bowed: true, province: 0); GiveKeyword(okaru4, "Unique");
        var enemy4 = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var hnf4 = Add(s4, HonorNeverFalls, 1, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Enabled(e4, 1, "honor-never-falls-straighten", hnf4.Id, okaru4.Id).Command!);
        Accept(e4, Offer(e4, 1, "okaru-weaken", okaru4.Id, enemy4.Id));
        Check(e4.State.AdditionalActions.Count == 1 && !e4.State.AdditionalActions[0].BaseAbilityOnly && e4.State.PriorityPlayerId == 1, "his base ability is taken as the additional action: the restricted grant was spent and Okaru's own additional action replaced it");
        Eq(e4.CardForceForTest(enemy4.Id), 0, "and it resolved (Force reduced to 0)");
        // An ability given to him permanently (not printed) is not a base ability either.
        var s5 = Table(Phase.Battle, 1); s5.PriorityPlayerId = 1;
        var okaru5 = Add(s5, KaiuOkaru, 1, Zone.Battlefield, bowed: true, province: 0); GiveKeyword(okaru5, "Unique");
        okaru5.GrantedAbilities.Add(new("tireless-efforts-straighten", "test-fixture", GrantDuration.Permanent));
        var hnf5 = Add(s5, HonorNeverFalls, 1, Zone.Hand);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Enabled(e5, 1, "honor-never-falls-straighten", hnf5.Id, okaru5.Id).Command!);
        Check(e5.State.AdditionalActions.Single().BaseAbilityOnly && e5.State.AdditionalActions.Single().PerformerId == okaru5.Id, "grant held for Okaru");
    }

    // ---- Honor Never Falls: the Focus Effect ---------------------------------------------------------------------------------------------------------------

    private static void ClosePassingDuel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void DriveToFocusEffects(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingDuel is { Stage: "Focusing" or "DuelistRefocus" } duel && guard < 6; guard++)
            Accept(e, duel.Stage == "Focusing" ? new GameCommand("strike", duel.TurnPlayerId, e.Version) : new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version));
    }

    // Player 1's Mirumoto Ichizo, Experienced ("duel him. Destroy the duel's loser") challenges player 0's Personality; player 0 focuses `focusDef` and player 1 `p1Focus`. The winner is the one given the Chi.
    private static (MatchEngine E, CardInstance Ichizo, CardInstance Target, CardInstance Focus, CardInstance? Other) IchizoDuel(string focusDef, bool p0Loses = true, string? p1Focus = null, Action<MatchState, CardInstance>? more = null, bool order = true)
    {
        var s = Table();
        var ichizo = Add(s, Ichizo, 1, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        ChiBoost(p0Loses ? ichizo : target, 20);
        Force(target, 10); // player 0's army wins the battle that follows, so it is the duel (not the battle) that would destroy him
        var focus = Add(s, focusDef, 0, Zone.Hand);
        var other = p1Focus is null ? null : Add(s, p1Focus, 1, Zone.Hand);
        more?.Invoke(s, target);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "ichizo-exp-duel", ichizo.Id, target.Id));
        ClosePassingDuel(e);
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: focus.Id));
        if (other is not null) Accept(e, new GameCommand("focus", 1, e.Version, CardId: other.Id));
        DriveToFocusEffects(e);
        return (e, ichizo, target, focus, other);
    }

    private static void HonorNeverFallsFocus()
    {
        var (e, _, target, focus, _) = IchizoDuel(HonorNeverFalls);
        // Mandatory: no "(now)" choice is asked, the duel simply resolved with the loser's consequences held back.
        Check(e.State.PendingChoice is null && e.State.PendingDuel is null, "no decision is asked: the duel finished");
        Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "the loser was not destroyed by the duel");
        var recorded = e.State.DelayedEffects.Single();
        Check(recorded.Kind == DelayedKinds.Destroy && recorded.CardId == target.Id && recorded.Until == DelayUntil.EndOfTurn, $"a destruction waiting for the end of the turn: {recorded.Kind}/{recorded.Until}");
        Check(Logged(e, "duelLossDelayed") && Logged(e, "destructionDelayed"), "logged");
        Eq(e.State.Cards[focus.Id].Zone, Zone.FateDiscard, "the focused card is spent");
        RunUntil(e, () => e.State.Cards[target.Id].Zone == Zone.Dead);
        var log = e.State.Events.Select(x => x.Type).ToList();
        Check(log.IndexOf("delayedDestruction") > log.IndexOf("duelLossDelayed"), "destroyed later, as the delay ran out");
        Check(e.State.DelayedEffects.Count == 0, "the store is empty");

        // The owner WON: nothing to delay; the other player's loser is destroyed at once.
        var (e2, ichizo2, target2, _, _) = IchizoDuel(HonorNeverFalls, p0Loses: false);
        Eq(e2.State.Cards[ichizo2.Id].Zone, Zone.Dead, "the other player's loser is destroyed at once: only the owner's own loss is delayed");
        Eq(e2.State.Cards[target2.Id].Zone, Zone.Battlefield, "the winner is untouched");
        Check(e2.State.DelayedEffects.Count == 0, "nothing was recorded");
    }

    private static void HonorNeverFallsAnySource()
    {
        // Player 1's Ichizo wins and his Fair Warning gives the loser -4F ("from any source"): the -4F is recorded, not applied, and lands at the end of the turn.
        var (e, ichizo, target, hnf, fair) = IchizoDuel(HonorNeverFalls, p0Loses: true, p1Focus: FairWarning);
        if (e.State.PendingDuel is { Stage: "OrderingEffects" }) Accept(e, new GameCommand("orderDuelFocusEffects", e.State.ActivePlayerId, e.Version, CardIds: [hnf.Id, fair!.Id]));
        Eq(e.State.PendingChoice?.Type, "focusEffectChoice", "Fair Warning asks its owner whom to give -4F");
        Eq(e.State.PendingChoice!.PlayerId, 1, "player 1 is asked");
        var before = e.CardForceForTest(target.Id);
        var pick = e.QueryActions(1).Single(a => a.Type == "chooseFocusEffect" && a.Command!.TargetId == target.Id);
        Accept(e, pick.Command!);
        Eq(e.CardForceForTest(target.Id), before, "the -4F was not applied to the loser");
        Check(e.State.DelayedEffects.Any(d => d.CardId == target.Id && d.Kind is DelayedKinds.Token or DelayedKinds.Modifier && d.Until == DelayUntil.EndOfTurn && d.Amount == -4), "recorded until the end of the turn: " + string.Join(", ", e.State.DelayedEffects.Select(d => $"{d.Kind}:{d.CardId}:{d.Amount}")));
        Check(e.State.DelayedEffects.Any(d => d.Kind == DelayedKinds.Destroy && d.CardId == target.Id), "the outcome's destruction is delayed too");
        // Control: the same Fair Warning after a duel whose outcome leaves the loser alive (Reckless Duel: bow him, move him home) and with no Honor Never Falls -- the -4F applies at once.
        var s2 = Table();
        var performer = Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0); ChiBoost(performer, 20);
        var loser = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Force(loser, 10);
        var reckless = Add(s2, DefinitionWith("reckless-duel"), 1, Zone.Hand);
        var fair2 = Add(s2, FairWarning, 1, Zone.Hand); var plainFocus = Add(s2, "training-duel-challenge", 0, Zone.Hand);
        var f = MatchEngine.FromTestState(s2);
        Accept(f, Offer(f, 1, "reckless-duel", reckless.Id, loser.Id, performer.Id));
        ClosePassingDuel(f);
        Accept(f, new GameCommand("focus", 0, f.Version, CardId: plainFocus.Id));
        Accept(f, new GameCommand("focus", 1, f.Version, CardId: fair2.Id));
        DriveToFocusEffects(f);
        var before2 = f.CardForceForTest(loser.Id);
        var pickable = f.QueryActions(1).Where(a => a.Type == "chooseFocusEffect").ToArray();
        Check(pickable.Any(a => a.Command!.TargetId == loser.Id), "candidates: " + string.Join(",", pickable.Select(a => a.Command!.TargetId + "/" + a.Command.Choice)) + $" pending {f.State.PendingChoice?.Type} duel {f.State.PendingDuel?.Stage}");
        Accept(f, pickable.First(a => a.Command!.TargetId == loser.Id).Command!);
        Eq(f.CardForceForTest(loser.Id), before2 - 4, "control: without the delay the loser gets -4F at once");
        Check(f.State.DelayedEffects.All(d => d.Kind != DelayedKinds.Modifier && d.Kind != DelayedKinds.Token), "and nothing is recorded");
        // Both sides' Honor Never Falls: the winner's copy delays nothing (none of his Personalities lost), the loser's still does.
        var (g, _, target3, hnfLoser, _) = IchizoDuel(HonorNeverFalls, p0Loses: true, p1Focus: HonorNeverFalls);
        if (g.State.PendingDuel is { Stage: "OrderingEffects" }) Accept(g, new GameCommand("orderDuelFocusEffects", g.State.ActivePlayerId, g.Version, CardIds: g.State.Cards.Values.Where(c => c.Zone == Zone.Focused).Select(c => c.Id).ToArray()));
        Eq(g.State.Cards[target3.Id].Zone, Zone.Battlefield, "the loser's copy delays his destruction");
        Eq(g.State.DelayedEffects.Count(d => d.Kind == DelayedKinds.Destroy), 1, "one delayed destruction, the loser's");
        Check(g.State.Events.Any(x => x.Type == "focusEffectNothing" && x.Message.Contains("nothing to delay")), "the winner's copy is logged as having nothing to delay");
    }

    // ---- Kurohito's Perfection: the Favor trait ------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Celestial) FavorRig(int celestialOwner = 0, bool holdFavor = true, int handCards = 0)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        StrongholdOf(s, 0, Halls); // player abilities (the rulebook Favor action) are only on offer in a published deck profile
        var celestial = Add(s, Kurohito, celestialOwner, Zone.Celestial);
        if (holdFavor) s.FavorPlayerId = 0;
        for (var i = 0; i < handCards; i++) Add(s, "training-crane-samurai", 0, Zone.Hand);
        return (MatchEngine.FromTestState(s), celestial);
    }

    private static void KurohitoFavor()
    {
        var (e, _) = FavorRig();
        Eq(e.State.Players[0].Hand.Count, 0, "player 0 holds no card: the rulebook action (which discards one) cannot be taken");
        Check(!CanUse(e, 0, "favor-cycle") && CanUse(e, 0, "favor-cycle-kurohito"), "but Kurohito's Perfection lets him ignore that cost");
        var offered = Options(e, 0, "favor-cycle-kurohito").Single(a => a.Enabled);
        Check(offered.Label.Contains("Favor Political Limited"), "it is the Favor action: " + offered.Label);
        Accept(e, offered.Command!);
        Eq(e.State.FavorPlayerId, null, "the Imperial Favor was discarded as the cost");
        Eq(e.State.Players[0].Hand.Count, 0, "no card was drawn yet");
        var delayed = e.State.DelayedEffects.Single();
        Check(delayed.Kind == DelayedKinds.Draw && delayed.Until == DelayUntil.EndPhaseBegins && delayed.PlayerId == 0, $"the draw is recorded for the End Phase: {delayed.Kind}/{delayed.Until}/{delayed.PlayerId}");
        Eq(e.State.Players[0].FavorActionsThisTurn, 1, "it counts as a Favor action (Ring of Air)");
        Check(Logged(e, "effectDelayed") && Logged(e, "favorDiscarded"), "logged");
        var fateDeck = e.State.Players[0].FateDeck.Count;
        RunUntil(e, () => e.State.Phase == Phase.End);
        Check(e.State.DelayedEffects.Count == 0, "applied once the End Phase began");
        Check(e.State.Events.Any(x => x.Type == "delayedEffectApplied" && x.Message.Contains("draws a card")), "the delayed draw was applied");
        var events = e.State.Events.Select(x => x.Type + ":" + x.Message).ToList();
        Check(events.FindIndex(x => x == "phaseChanged:Phase: End.") < events.FindIndex(x => x.StartsWith("delayedEffectApplied:")), "after the End Phase began");
        Check(e.State.Players[0].FateDeck.Count <= fateDeck - 1, "a card left the Fate deck");

        // The ordinary rulebook action, with a card to discard, still draws at once and needs no Celestial.
        var (f, _) = FavorRig(handCards: 1);
        var discard = f.State.Players[0].Hand[0];
        Accept(f, Enabled(f, 0, "favor-cycle").Command! with { TargetId = discard });
        Check(f.State.DelayedEffects.Count == 0, "the ordinary action delays nothing");
        Eq(f.State.Players[0].Hand.Count, 1, "it discarded a card and drew one at once");
        // Both are the one rulebook action: after the twin, the ordinary one is spent for the turn (and the reverse).
        var (g, _) = FavorRig(handCards: 1);
        Accept(g, Enabled(g, 0, "favor-cycle-kurohito").Command!);
        g = Edit(g, st => { st.FavorPlayerId = 0; st.PriorityPlayerId = 0; });
        Check(!CanUse(g, 0, "favor-cycle") && !CanUse(g, 0, "favor-cycle-kurohito"), "the turn's Favor (Limited) action is used up");
        var (h, _) = FavorRig(handCards: 1);
        Accept(h, Enabled(h, 0, "favor-cycle").Command! with { TargetId = h.State.Players[0].Hand[0] });
        h = Edit(h, st => { st.FavorPlayerId = 0; st.PriorityPlayerId = 0; });
        Check(!CanUse(h, 0, "favor-cycle-kurohito"), "and the reverse: after the ordinary action the delayed one is not available either");
    }

    private static void KurohitoFavorEdges()
    {
        var (a, _) = FavorRig(celestialOwner: 1);
        Check(!CanUse(a, 0, "favor-cycle-kurohito"), "another player's Kurohito's Perfection gives its controller alone the option");
        var (b, _) = FavorRig(holdFavor: false);
        Check(!CanUse(b, 0, "favor-cycle-kurohito"), "without the Imperial Favor there is nothing to discard as a cost");
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.FavorPlayerId = 0; StrongholdOf(s, 0, Halls);
        var c = MatchEngine.FromTestState(s);
        Check(!CanUse(c, 0, "favor-cycle-kurohito"), "and without the Celestial in play it is not offered at all");
        Check(!c.QueryActions(0).Any(x => x.Command?.AbilityId == "favor-cycle-kurohito"), "not even listed disabled");
        var rejected = c.Execute(new GameCommand("useAbility", 0, c.Version, AbilityId: "favor-cycle-kurohito"));
        Check(!rejected.Accepted, "and a hand-made command is refused");
        // The Favor Political Limited action is Limited: not in a Battle.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0; s2.FavorPlayerId = 0; StrongholdOf(s2, 0, Halls); Add(s2, Kurohito, 0, Zone.Celestial);
        var d = MatchEngine.FromTestState(s2);
        Check(!CanUse(d, 0, "favor-cycle-kurohito"), "a Limited action cannot be taken in the Battle phase");
    }

    private static void KurohitoLimited()
    {
        foreach (var (id, keyword) in new[] { ("kurohito-perfection-cavalry", "Cavalry"), ("kurohito-perfection-duelist", "Duelist"), ("kurohito-perfection-tactician", "Tactician") })
        {
            var (e, celestial) = FavorRig(holdFavor: false);
            e = Edit(e, st => Add(st, "training-crane-samurai", 1, Zone.Home));
            var enemy = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.DefinitionId == "training-crane-samurai");
            Check(!enemy.GrantedKeywords.Any(k => k.Keyword == keyword), $"{keyword}: not yet");
            Accept(e, Offer(e, 0, id, celestial.Id, enemy.Id));
            Check(e.State.Cards[enemy.Id].GrantedKeywords.Any(k => k.Keyword == keyword && k.Duration == GrantDuration.Permanent), $"{keyword}: permanently granted (to any Personality, an enemy's too)");
            Check(e.State.Cards[celestial.Id].Zone != Zone.Celestial, $"{keyword}: the Celestial was discarded as the cost");
            foreach (var other in new[] { "kurohito-perfection-cavalry", "kurohito-perfection-duelist", "kurohito-perfection-tactician" })
                Check(!CanUse(e, 0, other, celestial.Id), $"{keyword}: {other} is gone with the card");
        }
        var (f, celestial2) = FavorRig(holdFavor: false);
        f = Edit(f, st => { Add(st, "training-lion-samurai", 0, Zone.Home); });
        var mine = f.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == "training-lion-samurai");
        Check(CanUse(f, 0, "kurohito-perfection-duelist", celestial2.Id), "offered in the Action phase to its controller");
        Check(!CanUse(f, 1, "kurohito-perfection-duelist", celestial2.Id), "not to the other player");
        var battle = Edit(f, st => { st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; });
        Check(!CanUse(battle, 0, "kurohito-perfection-duelist", celestial2.Id), "a Limited action is not for a Battle");
        // The keyword is real: a Duelist wins a tied duel (the Duelist keyword is read from granted keywords).
        Accept(f, Offer(f, 0, "kurohito-perfection-duelist", celestial2.Id, mine.Id));
        Check(f.State.Cards[mine.Id].GrantedKeywords.Any(k => k.Keyword == "Duelist"), "he has Duelist now");
    }

    // ---- Makito --------------------------------------------------------------------------------------------------------------------------------------------

    private static void MakitoStatic()
    {
        foreach (var (clan, expected) in new[] { ("Phoenix", 5), ("Crane", 4), ("Lion", 4), ("", 4) })
        {
            var s = Table(Phase.Action, 1);
            s.Players[1].Clan = clan;
            var makito = Add(s, Makito, 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Eq(e.CardForceForTest(makito.Id), expected, $"a {(clan == "" ? "clanless" : clan)} controller: Makito's Force");
        }
        // Under another player's control the reading follows the CONTROLLER: a Phoenix player who controls him, not his owner's own clan.
        var s2 = Table(Phase.Action, 1);
        s2.Players[0].Clan = "Phoenix"; s2.Players[1].Clan = "Lion";
        var stolen = Add(s2, Makito, 0, Zone.Home);
        Eq(MatchEngine.FromTestState(s2).CardForceForTest(stolen.Id), 5, "controlled by the Phoenix player (player 0)");
    }

    private static void MakitoTrait()
    {
        var s = Table(Phase.Action, 1);
        s.Players[1].Clan = "Lion"; s.Players[0].Clan = "Crane";
        var makito = Add(s, Makito, 1, Zone.Home);
        var mine = Add(s, "training-lion-samurai", 1, Zone.Home);
        var theirs = Add(s, "training-crane-samurai", 0, Zone.Home);
        var follower = Add(s, "training-follower", 0, Zone.Attached, parent: theirs.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(makito.Id), 4);
        e.DestroyUnitsForTest(mine.Id);
        Eq(e.CardForceForTest(makito.Id), 5, "his own player's Personality destroyed: +1F");
        e.DestroyUnitsForTest(theirs.Id);
        Eq(e.CardForceForTest(makito.Id), 7, "an enemy Personality and his Follower destroyed with him: +1F each card");
        Check(e.State.Events.Count(x => x.Type == "makitoGrows") == 3, "three destructions, three bonuses");
        // Until the turn ends: no "permanently".
        var start = e.State.TurnNumber;
        RunUntil(e, () => e.State.TurnNumber > start);
        Eq(e.CardForceForTest(makito.Id), 4, "the bonus lasted until the end of the turn");

        // A lone attachment destroyed (a Follower's own destruction): a card destroyed.
        var (f, makito2, _) = TraitRig();
        Eq(f.CardForceForTest(makito2.Id), 5, "a destroyed Follower (a lone attachment) gave +1F");

        // His own destruction gives nothing (he is out of play) and does not throw; a card destroyed while he is not in play gives nothing to a Makito still in a discard pile.
        var s3 = Table(Phase.Action, 1);
        var mak = Add(s3, Makito, 1, Zone.Home);
        var lone = Add(s3, "training-crane-samurai", 0, Zone.Home);
        var g = MatchEngine.FromTestState(s3);
        g.DestroyUnitsForTest(mak.Id);
        Eq(g.State.Cards[mak.Id].Zone, Zone.Dead, "Makito died");
        Eq(g.State.Events.Count(x => x.Type == "makitoGrows"), 0, "he gained nothing from his own destruction");
        g.DestroyUnitsForTest(lone.Id);
        Eq(g.State.Events.Count(x => x.Type == "makitoGrows"), 0, "nor from a card destroyed after he left play");
    }

    private static (MatchEngine E, CardInstance Makito, CardInstance Follower) TraitRig()
    {
        var s = Table(Phase.Action, 1);
        var makito = Add(s, Makito, 1, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var follower = Add(s, "training-follower", 0, Zone.Attached, parent: host.Id);
        var e = MatchEngine.FromTestState(s);
        e.DestroyUnitsForTest(follower.Id);
        Check(e.State.Events.Count(x => x.Type == "makitoGrows") >= 1, "a Follower's destruction (an attachment) gives Makito +1F");
        return (e, makito, follower);
    }

    private static (MatchEngine E, CardInstance Makito, CardInstance Enemy) MahoRig(Action<MatchState, CardInstance>? more = null)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var makito = Add(s, Makito, 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);   // Chi 3
        more?.Invoke(s, enemy);
        return (MatchEngine.FromTestState(s), makito, enemy);
    }

    private static void MakitoMaho()
    {
        // A bare Personality: he is the only card without attachments, so he is destroyed (no choice to make).
        var (e, makito, enemy) = MahoRig();
        Accept(e, Offer(e, 1, "makito-maho-destroy", makito.Id, enemy.Id));
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "the Personality without attachments is destroyed");
        Check(e.State.PendingChoice is null, "no choice was needed");
        Eq(e.CardForceForTest(makito.Id), 5, "and his own trait fired: a card was destroyed (+1F)");

        // A unit with attachments: the Personality is no legal card (he has attachments); the acting player chooses one attachment to destroy.
        var (f, makito2, enemy2) = MahoRig((s, host) => { Add(s, "training-follower", 0, Zone.Attached, parent: host.Id).ProvinceIndex = 0; Add(s, "training-item", 0, Zone.Attached, parent: host.Id).ProvinceIndex = 0; });
        var attachments = f.State.Cards.Values.Where(c => c.AttachedTo == enemy2.Id).Select(c => c.Id).ToArray();
        Eq(attachments.Length, 2);
        Accept(f, Offer(f, 1, "makito-maho-destroy", makito2.Id, enemy2.Id));
        Eq(f.State.PendingChoice?.Type, "effectChoice", "the acting player chooses the card to destroy");
        var choices = f.QueryActions(1).Where(a => a.Type == "chooseEffect").ToArray();
        Eq(string.Join(",", choices.Select(c => c.Command!.Choice).Order()), string.Join(",", attachments.Order()), "exactly the two attachments are offered -- not the Personality");
        Accept(f, choices.First(c => c.Command!.Choice == attachments[0]).Command!);
        Eq(f.State.Cards[attachments[0]].Zone, Zone.FateDiscard, "the chosen attachment was destroyed");
        Eq(f.State.Cards[attachments[1]].Zone, Zone.Attached, "the other stays");
        Eq(f.State.Cards[enemy2.Id].Zone, Zone.Battlefield, "and so does the Personality");
        Eq(f.CardForceForTest(makito2.Id), 5, "Makito got +1F for the destroyed card");

        // Legal targets: an enemy Personality with equal or LOWER Chi (current Chi: a modifier counts), not a higher one, not his own player's, not out of the battle.
        var (g, makito3, enemy3) = MahoRig((s, host) => { ChiBoost(host, 1); });   // Chi 4: equal to his own -> legal
        Check(CanUse(g, 1, "makito-maho-destroy", makito3.Id), "equal Chi (4 against 4) is legal");
        var (h, makito4, enemy4) = MahoRig((s, host) => { ChiBoost(host, 2); });   // Chi 5: higher -> not legal
        Check(!Options(h, 1, "makito-maho-destroy", makito4.Id).Any(a => a.Enabled && (a.TargetIds?.Contains(enemy4.Id) ?? false)), "a higher Chi is not a legal target");
        var (i, makito5, _) = MahoRig((s, host) => { Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); });
        var own = i.State.Cards.Values.First(c => c.OwnerId == 1 && c.DefinitionId == "training-lion-samurai");
        Check(!Options(i, 1, "makito-maho-destroy", makito5.Id).Any(a => a.Enabled && (a.TargetIds?.Contains(own.Id) ?? false)), "his own player's Personality is not a legal target");
        var raised = Edit(i, st => ChiBoost(st.Cards[makito5.Id], 3));
        Check(Options(raised, 1, "makito-maho-destroy", makito5.Id).Any(a => a.Enabled), "his own Chi is the current Chi too: with a bonus a Chi 5 target would qualify");
        var (j, makito6, _) = MahoRig();
        j = Edit(j, st => st.Cards[makito6.Id].Bowed = true);
        Check(!CanUse(j, 1, "makito-maho-destroy", makito6.Id), "a bowed Makito cannot take the action");
        // Not a Battle action outside a battle.
        var s2 = Table(Phase.Action, 1); s2.PriorityPlayerId = 1;
        var m2 = Add(s2, Makito, 1, Zone.Home); Add(s2, "training-crane-samurai", 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s2), 1, "makito-maho-destroy", m2.Id), "a Battle action is not taken in the Action phase");
    }

    private static void MakitoNotNegated()
    {
        // Control: Wide Valley negates another action's destruction of a Personality at its battlefield.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        Add(s, WideValley, 1, Zone.Battlefield, province: 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, victim.Id));
        Eq(e.State.Cards[victim.Id].Zone, Zone.Battlefield, "control: Wide Valley negates the other action's destruction");

        // Makito's destruction is not negated: a bare Personality...
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 1;
        Add(s2, WideValley, 1, Zone.Battlefield, province: 0);
        var makito = Add(s2, Makito, 1, Zone.Battlefield, province: 0);
        var bare = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(s2);
        Accept(f, Offer(f, 1, "makito-maho-destroy", makito.Id, bare.Id));
        Eq(f.State.Cards[bare.Id].Zone, Zone.Dead, "Wide Valley cannot negate the destruction of the Personality");
        Check(!Logged(f, "destructionNegated"), "nothing was negated");

        // ... and an attachment chosen through the effect choice (which resumes the paused action).
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 1;
        Add(s3, WideValley, 1, Zone.Battlefield, province: 0);
        var makito3 = Add(s3, Makito, 1, Zone.Battlefield, province: 0);
        var host = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var f1 = Add(s3, "training-follower", 0, Zone.Attached, parent: host.Id); f1.ProvinceIndex = 0;
        var f2 = Add(s3, "training-follower", 0, Zone.Attached, parent: host.Id); f2.ProvinceIndex = 0;
        var g = MatchEngine.FromTestState(s3);
        Accept(g, Offer(g, 1, "makito-maho-destroy", makito3.Id, host.Id));
        Accept(g, g.QueryActions(1).First(a => a.Type == "chooseEffect" && a.Command!.Choice == f1.Id).Command!);
        Eq(g.State.Cards[f1.Id].Zone, Zone.FateDiscard, "the chosen Follower is destroyed although Wide Valley is there");
        Check(!Logged(g, "destructionNegated"), "the resumed action's destruction was not negated either");

        // Not delayed: Halls of the Forgotten's Reaction delays "its effects" on a Berserker -- but not this destruction.
        var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = 1;
        var halls = StrongholdOf(s4, 0, Halls);
        var berserker = Add(s4, "training-berserker", 0, Zone.Battlefield, province: 0); Force(berserker, 10);
        var makito4 = Add(s4, Makito, 1, Zone.Battlefield, province: 0);
        var h = MatchEngine.FromTestState(s4);
        Accept(h, Offer(h, 1, "makito-maho-destroy", makito4.Id, berserker.Id));
        if (h.State.PendingChoice is { Type: "beforeActionReaction" })
        {
            var reaction = h.QueryActions(0).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == "halls-forgotten-delay");
            if (reaction is not null) Accept(h, reaction.Command!);
            for (var guard = 0; h.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++) Accept(h, new GameCommand("declineReaction", h.State.PriorityPlayerId, h.Version));
        }
        Eq(h.State.Cards[berserker.Id].Zone, Zone.Dead, "the Berserker was destroyed at once");
        Check(h.State.DelayedEffects.Count == 0, "nothing was delayed");
        // Control: the same Halls delay does hold back another action's destruction.
        var s5 = Table(Phase.Battle, 1); s5.PriorityPlayerId = 1;
        var halls5 = StrongholdOf(s5, 0, Halls);
        var berserker5 = Add(s5, "training-berserker", 0, Zone.Battlefield, province: 0); Force(berserker5, 10);
        var destroyer5 = Add(s5, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var i = MatchEngine.FromTestState(s5);
        Accept(i, Offer(i, 1, "training-destroy-no-attachments", destroyer5.Id, berserker5.Id));
        Accept(i, i.QueryActions(0).First(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == "halls-forgotten-delay").Command!);
        for (var guard = 0; i.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++) Accept(i, new GameCommand("declineReaction", i.State.PriorityPlayerId, i.Version));
        Eq(i.State.Cards[berserker5.Id].Zone, Zone.Battlefield, "control: another action's destruction is delayed by Halls");
        Check(i.State.DelayedEffects.Any(d => d.Kind == DelayedKinds.Destroy), "and recorded");
    }
}
