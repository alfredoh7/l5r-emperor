using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #501, mechanics group D (records, transfer and timed removal): Mendon's items 28-31 and the engine half of 32. Daidoji Narizane and Daigotsu Yajinden - exp3 (personality-wave-92), Broken
// Cipher, Indecision and Amazing Feat (strategy-wave-78). Every position is built explicitly and driven through QueryActions and the commands it offers (the primitives are in EmperorMechD.cs).
internal static class EmperorMechDTests
{
    private const string Narizane = "oracle-l5r-1663", Yajinden = "oracle-l5r-1783", Cipher = "oracle-l5r-1151", Indecision = "oracle-l5r-3736", Feat = "oracle-l5r-340",
        Ichizo = "oracle-l5r-5104", Keppo = "oracle-l5r-4328";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void ChiBoost(CardInstance card, int amount) => card.Modifiers.Add(new("test-fixture", "test-fixture", "Chi", amount));
    private static bool Logged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);
    private static GameCommand[] Actions(MatchEngine e, int player, string type) => e.QueryActions(player).Where(a => a.Type == type && a.Enabled && a.Command is not null).Select(a => a.Command!).ToArray();
    private static GameCommand PassC(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);

    public static void Run(TestRunner runner) => runner.Run("emperor-mech-D", [
        ("Published shape: Daidoji Narizane and Daigotsu Yajinden - exp3 (personality-wave-92), Broken Cipher, Indecision and Amazing Feat (strategy-wave-78) print their scans' stats, keywords, text and ids, ship their art and are in their manifests", PublishedShape),
        // Item 28: the Recon-action counter and Narizane.
        ("Recon-action counter: each Recon action a player takes counts for him (the ability's own Recon tag, or a Recon Strategy's action); an untagged action does not; both counters restart when the turn ends", ReconCounter),
        ("Daidoji Narizane: after two Recon actions this turn he may bow up to two different unbowed cards of the target enemy's unit (Personality, Follower, Item) -- the acting player chooses; a third is refused", NarizaneBowsUpToCount),
        ("Daidoji Narizane, edges: with no Recon action taken the action is legal and bows nothing; a bowed card is never offered; zero cards is a legal choice; the count is read when the action is taken", NarizaneEdges),
        // Item 29: transfer and the attach log; Broken Cipher.
        ("Attach log: an Equip is recorded (turn, phase, player, card, type, how); the log is kept for the current turn only (a transfer is not an attach: see Broken Cipher's Focus Effect)", AttachLog),
        ("Broken Cipher, Focus Effect: its owner may transfer a Spell from his Personality in the duel to another of his Shugenja at the same location (not one elsewhere), or decline", BrokenCipherTransfer),
        ("Broken Cipher, Battle: after a Spell was attached this turn and with no card put into the hand this phase it draws two; with no Spell attached, or once a card went into the hand this phase, it may not be taken", BrokenCipherDraw),
        // Item 30: cards into hand, Indecision.
        ("Indecision, Reaction: after a player's second action this phase that put cards into his hand, its holder is offered it (out of play, from the hand) and that player discards two cards of his choice", IndecisionDiscards),
        ("Indecision, Reaction, edges: after only one such action it is not offered; a new phase starts the count (and the cards-into-hand record) again; with two cards or fewer they are discarded without a choice", IndecisionEdges),
        ("Indecision, Battle: with enemy units at the current battlefield, his performing Personality at home moves there and straightens; one already there is not straightened; with no enemy units it may not be taken", IndecisionMove),
        // Item 31: timed removal; Yajinden.
        ("Daigotsu Yajinden: brings another player's dead Personality into play under his controller's control, ignoring costs; the pending removal is announced; when the turn ends he is removed from the game (with his unit) and returns to his owner", YajindenRaises),
        ("Daigotsu Yajinden, edges: his own dead Personality comes back too; a Loyal Personality who will not be controlled stays dead; one destroyed again before the turn ends stays in the discard pile; Yajinden loses 4 Honor as he enters play", YajindenEdges),
        // Item 32 (engine half) and Amazing Feat.
        ("Amazing Feat: bowing an Artisan, it creates a copy of a non-Unique Personality with his base Force, Chi, Personal Honor, keywords and base abilities (not his traits); the view marks it as a copy with its conditions; it is removed as its player's next turn begins, not before", AmazingFeatCopies),
        // Mendon's ruling (via the coordinator): a partially negated action counts, a wholly negated one does not.
        ("Negation, Recon counter: a wholly negated Recon action (the whole-action negation path) is not counted; a partially negated one (its destruction of the target negated) is", NegationRecon),
        ("Negation, Indecision's record: a wholly negated draw action is not counted (and opens no window); a partially negated one that still puts a card into the hand (Subtle Games, its dishonor negated) is", NegationHandActions),
        ("Negation, Broken Cipher's records: a wholly negated Equip (Battle) attaches nothing and logs nothing, so Broken Cipher stays unavailable; a wholly negated draw puts no card into the hand, so it stays available", NegationCipherRecords),
        ("Amazing Feat, edges: abilities that copy abilities are not copied; a Unique Personality may not be targeted; without an unbowed Artisan it may not be taken; a copied ability is usable by the copy", AmazingFeatEdges),
    ]);

    // ---- published shape ------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Manifest(string wave, params string[] ids)
        {
            Eq(string.Join(",", File.ReadAllLines(Path.Combine(root, $"tests/L5R.Core.Tests/PublishedWaves/{wave}.txt")).Where(l => l.Length > 0)), string.Join(",", ids), $"{wave}'s manifest");
            Check(File.ReadAllText(Path.Combine(root, $"content/published/{wave}-sources.json")).Contains("image_sha256"), $"{wave}: the source receipt records the scan's sha256");
            foreach (var id in ids)
            {
                Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
                Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
                Check(!CardCatalog.Get(id).ScenarioOnly && !CardCatalog.Get(id).CreatedOnly, $"{id} is playable");
            }
        }
        var narizane = CardCatalog.Get(Narizane);
        Check(narizane.Type == CardType.Personality && narizane.Name == "Daidoji Narizane" && narizane.Clan == "Crane" && narizane.GoldCost == 7 && narizane.Force == 4 && narizane.Chi == 3
            && narizane.PersonalHonor == 2 && narizane.HonorRequirement == 2, "Narizane's stats");
        Eq(string.Join(",", narizane.Keywords ?? []), "Crane Clan,Imperial,Iron Crane,Samurai,Scout,Yojimbo");
        Eq(string.Join(",", narizane.Abilities ?? []), "narizane-bow"); Eq(narizane.Effect, "none");
        Eq(narizane.Text, "Battle: Target an enemy Personality: Bow zero or more different cards in his unit up to the number of Recon actions you have taken this turn.");
        var yajinden = CardCatalog.Get(Yajinden);
        Check(yajinden.Type == CardType.Personality && yajinden.Name == "Daigotsu Yajinden" && yajinden.Clan == "Spider" && yajinden.GoldCost == 12 && yajinden.Force == 6 && yajinden.Chi == 6
            && yajinden.PersonalHonor == 0 && yajinden.HonorRequirement is null && yajinden.EntryHonorLoss == 4, "Yajinden's stats");
        Eq(string.Join(",", (yajinden.Keywords ?? []).Order(StringComparer.Ordinal)), "Artisan,Bloodspeaker,Experienced 3,Khadi,Loyal,Shadowlands,Shugenja,Spider Clan,Unique");
        Eq(string.Join(",", yajinden.Abilities ?? []), "yajinden-raise");
        Eq(yajinden.Text, "After Yajinden enters play: Lose 4 Honor.\nLimited: Target a dead Personality: Yajinden raises him from the dead. Bring him into play (under your control) ignoring costs and Honor Requirements. Remove him from the game when the turn ends.");
        Manifest("personality-wave-92", Narizane, Yajinden);
        var cipher = CardCatalog.Get(Cipher);
        Check(cipher.Type == CardType.Strategy && cipher.Name == "Broken Cipher" && cipher.GoldCost == 0 && cipher.Focus == 1, "Broken Cipher's stats");
        Eq(string.Join(",", cipher.Abilities ?? []), "broken-cipher-focus,broken-cipher-draw");
        Eq(cipher.Text, "As a Focus Effect: You may transfer a Spell from your Personality in this duel to another of your Shugenja at the same location.\nBattle: If you have not put cards into your hand this phase and you have attached a Spell this turn: Draw two cards.");
        var indecision = CardCatalog.Get(Indecision);
        Check(indecision.Type == CardType.Strategy && indecision.Name == "Indecision" && indecision.GoldCost == 0 && indecision.Focus == 4, "Indecision's stats");
        Eq(string.Join(",", indecision.Abilities ?? []), "indecision-discard,indecision-move");
        Eq(indecision.Text, "Reaction: After an action resolves, if its player has resolved two or more actions which put cards into his hand this phase: He discards two cards.\nBattle: If any enemy units are at the current battlefield, choose your performing Personality: Move him there. Straighten him if he moved.");
        var feat = CardCatalog.Get(Feat);
        Check(feat.Type == CardType.Strategy && feat.Name == "Amazing Feat" && feat.GoldCost == 0 && feat.Focus == 4, "Amazing Feat's stats");
        Eq(string.Join(",", feat.Abilities ?? []), "amazing-feat-create");
        Eq(feat.Text, "Limited: Bow your performing Artisan Personality and target a non-Unique Personality: Create a Personality with the same base Force, Chi, Personal Honor and keywords as the targeted Personality, who permanently copies all his base abilities except those that copy abilities. Remove the created Personality from the game when your next turn begins.");
        Manifest("strategy-wave-78", Cipher, Feat, Indecision);
        Eq(AbilityCatalog.All["narizane-bow"].Timing, "Battle"); Eq(AbilityCatalog.All["yajinden-raise"].Timing, "Limited");
        Eq(AbilityCatalog.All["broken-cipher-focus"].Timing, "FocusEffect"); Eq(AbilityCatalog.All["broken-cipher-draw"].Timing, "Battle");
        Eq(AbilityCatalog.All["indecision-discard"].Timing, "Reaction"); Eq(AbilityCatalog.All["indecision-move"].Timing, "Battle"); Check(AbilityCatalog.All["indecision-move"].MovesToBattle, "Indecision moves a Personality to the battle");
        Eq(AbilityCatalog.All["amazing-feat-create"].Timing, "Limited"); Check(AbilityCatalog.All["amazing-feat-create"].CopiesAbilities, "Amazing Feat's ability copies abilities");
    }

    // ---- item 28 --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static int Recon(MatchEngine e, int player) => e.ReconActionsTakenThisTurn(player);

    // Closes any entering-play or reaction window the last command opened, passing for whoever holds it.
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { } pending && guard < 8; guard++)
        {
            var pass = e.QueryActions(pending.PlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction" or "passReaction");
            if (pass is null) return;
            Accept(e, pass.Command!);
        }
    }

    private static void ReconCounter()
    {
        var s = Table(Phase.Action, 1);
        var actor = Add(s, "training-recon-actor", 1, Zone.Home);
        var other = Add(s, "training-recon-actor", 0, Zone.Home);
        var reconStrategy = Add(s, "training-recon-keyword-strategy", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Eq(Recon(e, 1), 0, "nothing taken yet");
        Accept(e, Use(e, 1, "training-recon-open", actor.Id));
        Eq(Recon(e, 1), 1, "a Recon Open counts");
        Accept(e, Use(e, 0, "training-plain-open", other.Id));
        Eq(Recon(e, 0), 0, "an untagged Open does not count");
        Accept(e, Use(e, 1, "training-plain-open", reconStrategy.Id));
        Eq(Recon(e, 1), 2, "the action of a Recon Strategy is a Recon action (its card's keyword)");
        Accept(e, Use(e, 0, "training-recon-open", other.Id));
        Eq(Recon(e, 0), 1, "the other player's Recon action is his own");
        Eq(Recon(e, 1), 2, "and not the first player's");
        var next = Passing(e, x => x.State.TurnNumber > s.TurnNumber);
        Eq(Recon(next, 0), 0, "the turn ended: both counters restart");
        Eq(Recon(next, 1), 0, "the turn ended: both counters restart");
    }

    private static MatchEngine Passing(MatchEngine e, Func<MatchEngine, bool> stop)
    {
        for (var i = 0; i < 300 && !stop(e); i++)
        {
            var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var options = e.QueryActions(mover).Where(a => a.Enabled && a.Command is not null).ToArray();
            var pick = options.FirstOrDefault(a => a.Type == "discardHand") ?? options.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "wait" or "declineReaction" or "declineStraighten" or "declineAttack" or "declinePhase")
                ?? throw new Exception($"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type}): " + string.Join(", ", e.QueryActions(mover).Select(a => a.Type)));
            var command = pick.Command!;
            if (command.Type == "discardHand") command = command with { CardIds = e.State.Players[mover].Hand.Take(e.State.Players[mover].Hand.Count - 8).ToArray() };
            Accept(e, command);
        }
        Check(stop(e), $"did not reach the wanted point (phase {e.State.Phase}, turn {e.State.TurnNumber})");
        return e;
    }

    // Player 1 (the Attacker) with Narizane at the current battlefield; player 0's enemy Personality there with a Follower and an Item (and a bowed Item).
    private static (MatchEngine E, CardInstance Narizane, CardInstance Enemy, CardInstance Follower, CardInstance Item, CardInstance BowedItem) NarizaneTable(int recon)
    {
        var s = Table(Phase.Battle, 1);
        var narizane = Add(s, Narizane, 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: enemy.Id);
        var item = Add(s, "training-item", 0, Zone.Attached, province: 0, parent: enemy.Id);
        var bowedItem = Add(s, "training-costly-weapon", 0, Zone.Attached, bowed: true, province: 0, parent: enemy.Id);
        s.MechD.ReconActionsThisTurn[1] = recon;
        return (MatchEngine.FromTestState(s), narizane, enemy, follower, item, bowedItem);
    }

    private static void NarizaneBowsUpToCount()
    {
        // The count comes from real Recon actions: two taken in the Action Phase, then the battle (the counters are the turn's).
        var a = Table(Phase.Action, 1);
        var scout = Add(a, "training-recon-actor", 1, Zone.Home);
        var strategy = Add(a, "training-recon-keyword-strategy", 1, Zone.Hand);
        var ae = MatchEngine.FromTestState(a);
        Accept(ae, Use(ae, 1, "training-recon-open", scout.Id)); Accept(ae, PassC(ae));
        Accept(ae, Use(ae, 1, "training-plain-open", strategy.Id));
        var (e2, n2, en2, f2, i2, b2) = NarizaneTable(Recon(ae, 1));
        Eq(Recon(e2, 1), 2, "two Recon actions this turn");
        Accept(e2, Offer(e2, 1, "narizane-bow", n2.Id, en2.Id));
        Eq(e2.State.PendingChoice?.Type, "deciderChoice", "the acting player chooses the cards to bow");
        Eq(e2.State.PendingDeciderChoice!.DeciderPlayerId, 1, "Narizane's controller chooses");
        Eq(e2.State.PendingDeciderChoice.Max, 2, "up to the number of Recon actions");
        Eq(string.Join(",", e2.State.PendingDeciderChoice.Candidates.Order(StringComparer.Ordinal)), string.Join(",", new[] { en2.Id, f2.Id, i2.Id }.Order(StringComparer.Ordinal)), "the unbowed cards of his unit (not the bowed Item)");
        Accept(e2, new GameCommand("chooseDecider", 1, e2.Version, CardId: n2.Id, TargetId: en2.Id));
        Accept(e2, new GameCommand("chooseDecider", 1, e2.Version, CardId: n2.Id, TargetId: f2.Id));
        Check(!Actions(e2, 1, "chooseDecider").Any(c => c.TargetId == i2.Id), "a third card is not offered");
        var third = new GameCommand("chooseDecider", 1, e2.Version, CardId: n2.Id, TargetId: i2.Id);
        Check(!e2.Execute(third).Accepted, "a third card is refused");
        Accept(e2, new GameCommand("chooseDecider", 1, e2.Version, CardId: n2.Id, Choice: "done"));
        Check(e2.State.Cards[en2.Id].Bowed && e2.State.Cards[f2.Id].Bowed, "the Personality and his Follower are bowed");
        Check(!e2.State.Cards[i2.Id].Bowed, "the Item was not chosen");
        Check(e2.State.PendingDeciderChoice is null && e2.State.PendingChoice is null, "the action finished");
    }

    private static void NarizaneEdges()
    {
        // No Recon action: the action is legal, and bows nothing.
        var (e, narizane, enemy, follower, item, _) = NarizaneTable(0);
        Check(CanUse(e, 1, "narizane-bow", narizane.Id), "legal with no Recon action taken");
        Accept(e, Offer(e, 1, "narizane-bow", narizane.Id, enemy.Id));
        Check(e.State.PendingDeciderChoice is null, "nothing to choose");
        Check(!e.State.Cards[enemy.Id].Bowed && !e.State.Cards[follower.Id].Bowed && !e.State.Cards[item.Id].Bowed, "nothing bowed");
        Check(Logged(e, "narizaneNothing"), "logged");
        // Zero cards is a legal answer.
        var (z, zn, ze, zf, zi, _) = NarizaneTable(3);
        Accept(z, Offer(z, 1, "narizane-bow", zn.Id, ze.Id));
        Accept(z, new GameCommand("chooseDecider", 1, z.Version, CardId: zn.Id, Choice: "done"));
        Check(!z.State.Cards[ze.Id].Bowed && !z.State.Cards[zf.Id].Bowed && !z.State.Cards[zi.Id].Bowed, "none chosen, none bowed");
        // Three Recon actions and three unbowed cards: all three may be bowed (choosing every candidate ends the choice).
        var (t, tn, te, tf, ti, tb) = NarizaneTable(3);
        Accept(t, Offer(t, 1, "narizane-bow", tn.Id, te.Id));
        foreach (var id in new[] { te.Id, tf.Id, ti.Id }) Accept(t, new GameCommand("chooseDecider", 1, t.Version, CardId: tn.Id, TargetId: id));
        Check(t.State.Cards[te.Id].Bowed && t.State.Cards[tf.Id].Bowed && t.State.Cards[ti.Id].Bowed, "all three bowed");
        Check(t.State.PendingDeciderChoice is null, "choosing every candidate ends the choice");
        // The count is read when the action is taken: raising it afterwards (a Recon action in a window) does not widen the choice.
        var (c, cn, ce, _, _, _) = NarizaneTable(1);
        Accept(c, Offer(c, 1, "narizane-bow", cn.Id, ce.Id));
        Eq(c.State.PendingDeciderChoice!.Max, 1, "one Recon action: one card");
        Eq(c.State.MechD.NarizaneLimits[cn.Id], 1, "the count captured as the action was taken");
    }

    // ---- item 29 --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AttachLog()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0; RealStronghold(s, 0); // the Defender (player 0) attaches
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var spell = Add(s, "training-spell", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        EquipBattle(e, 0, shugenja, spell);
        var log = e.State.MechD.AttachLog;
        Eq(log.Count, 1, "the Equip is recorded");
        Check(log[0].CardId == spell.Id && log[0].PlayerId == 0 && log[0].CardType == CardType.Spell && log[0].Turn == s.TurnNumber && log[0].Phase == Phase.Battle, "turn, phase, player, card and type");
        Eq(log[0].Via, "hand");
        Eq(log[0].HostId, null, "a hand attach is noted as the card leaves the hand");
        var next = Passing(e, x => x.State.TurnNumber > s.TurnNumber);
        Eq(next.State.MechD.AttachLog.Count, 0, "the log is the current turn's");
    }

    // Player 1's Ichizo challenges player 0's Shugenja (with a Spell); player 0 focuses Broken Cipher. Another Shugenja of player 0 stands at the battlefield, one more at home.
    private static (MatchEngine E, CardInstance Dueller, CardInstance Spell, CardInstance There, CardInstance Home, CardInstance Cipher) CipherDuel(int dueller = 20)
    {
        var s = Table();
        var ichizo = Add(s, Ichizo, 1, Zone.Battlefield, province: 0);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var spell = Add(s, "training-spell", 0, Zone.Attached, province: 0, parent: shugenja.Id);
        var there = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var home = Add(s, "training-shugenja", 0, Zone.Home);
        ChiBoost(shugenja, dueller - CardCatalog.Get("training-shugenja").Chi);
        var cipher = Add(s, Cipher, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "ichizo-exp-duel", ichizo.Id, shugenja.Id));
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: cipher.Id));
        for (var guard = 0; e.State.PendingDuel is { Stage: "Focusing" or "DuelistRefocus" } duel && guard < 6; guard++)
            Accept(e, duel.Stage == "Focusing" ? new GameCommand("strike", duel.TurnPlayerId, e.Version) : new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version));
        // One Focus Effect card: nothing to order, it resolves at once (and its choice is open).
        if (e.State.PendingDuel is { Stage: "OrderingEffects" } && e.State.PendingFocusChoice is null)
            Accept(e, new GameCommand("orderDuelFocusEffects", e.State.ActivePlayerId, e.Version, CardIds: [cipher.Id]));
        return (e, shugenja, spell, there, home, cipher);
    }

    private static void BrokenCipherTransfer()
    {
        var (e, dueller, spell, there, home, cipher) = CipherDuel();
        Eq(e.State.PendingChoice?.Type, "focusEffectChoice", "Broken Cipher's Focus Effect asks its owner");
        var choices = Actions(e, 0, "chooseFocusEffect");
        Check(choices.Any(c => c.TargetId == spell.Id + ">" + there.Id), "the Spell to the other Shugenja at the same location is offered");
        Check(!choices.Any(c => c.TargetId is not null && c.TargetId.EndsWith(">" + home.Id)), "a Shugenja elsewhere is not");
        Check(choices.Any(c => c.Choice == "decline"), "it may be declined");
        Accept(e, choices.First(c => c.TargetId == spell.Id + ">" + there.Id));
        Eq(e.State.Cards[spell.Id].AttachedTo, there.Id, "the Spell was transferred");
        Eq(e.State.Cards[spell.Id].Zone, Zone.Attached, "and stays in play (not an entry, no cost)");
        Eq(e.State.MechD.AttachLog.Count, 0, "a transfer is not an attach");
        Check(Logged(e, "attachmentTransferred"), "logged");
        // Declining leaves it.
        var (d, _, dspell, _, _, _) = CipherDuel();
        Accept(d, Actions(d, 0, "chooseFocusEffect").First(c => c.Choice == "decline"));
        Eq(d.State.Cards[dspell.Id].AttachedTo, CipherDuelDueller(d, dspell), "declined: the Spell stays");
    }

    // Equip (Battle): the player's opposed unbowed Shugenja takes a Spell from his hand, paying its cost.
    private static void EquipBattle(MatchEngine e, int player, CardInstance shugenja, CardInstance spell)
    {
        var option = Options(e, player, "equip-battle").FirstOrDefault(a => a.Enabled && a.Command?.PerformerId == shugenja.Id)
            ?? Options(e, player, "equip-battle").FirstOrDefault(a => a.Enabled)
            ?? throw new Exception("equip-battle not offered: " + string.Join("; ", e.QueryActions(player).Select(a => $"{a.Type}:{a.Label} [{a.Enabled}] {a.Reason}")));
        Accept(e, option.Command! with { TargetId = spell.Id, PerformerId = shugenja.Id });
    }

    // The rulebook's player abilities (Equip (Battle) among them) are offered only under a published Stronghold (the economy scenario's is a scenario card): Shinden Asahina (Effect none, 4 Gold).
    private static void RealStronghold(MatchState s, int player) => s.Cards[s.Players[player].StrongholdId].DefinitionId = "oracle-l5r-6855";

    private static string CipherDuelDueller(MatchEngine e, CardInstance spell) => e.State.Cards[spell.Id].AttachedTo!;

    private static void BrokenCipherDraw()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0; RealStronghold(s, 0); // the Defender (player 0) plays it
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var spell = Add(s, "training-spell", 0, Zone.Hand);
        var cipher = Add(s, Cipher, 0, Zone.Hand);
        var second = Add(s, Cipher, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "broken-cipher-draw", cipher.Id), "no Spell attached this turn: not offered");
        EquipBattle(e, 0, shugenja, spell);
        if (e.State.PriorityPlayerId != 0) Accept(e, PassC(e));
        Check(CanUse(e, 0, "broken-cipher-draw", cipher.Id), "a Spell attached this turn and no card into the hand this phase: offered");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Use(e, 0, "broken-cipher-draw", cipher.Id));
        Eq(e.State.Players[0].Hand.Count, hand - 1 + 2, "two cards drawn (the played Strategy left the hand)");
        if (e.State.PriorityPlayerId != 0) Accept(e, PassC(e));
        Check(!CanUse(e, 0, "broken-cipher-draw", second.Id), "cards went into the hand this phase: the second copy may not be taken");
        // A Spell attached in an earlier phase of the same turn still counts; a card drawn in that earlier phase does not.
        var earlier = Edit(e, st => { st.MechD.CardsIntoHandThisPhase = new bool[2]; });
        if (earlier.State.PriorityPlayerId != 0) Accept(earlier, PassC(earlier));
        Check(CanUse(earlier, 0, "broken-cipher-draw", second.Id), "a new phase: the draw of the previous phase does not count, the Spell of this turn does");
    }

    // ---- item 30 --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Player 1 (active) holds two draw Strategies (Limited: draw a card); player 0 holds Indecision.
    private static (MatchEngine E, CardInstance First, CardInstance Second, CardInstance Indecision) IndecisionTable(int extraHand = 3)
    {
        var s = Table(Phase.Action, 1);
        var first = Add(s, "training-bushido-virtue", 1, Zone.Hand);
        var second = Add(s, "training-bushido-virtue", 1, Zone.Hand);
        for (var i = 0; i < extraHand; i++) Add(s, "training-item", 1, Zone.Hand);
        var indecision = Add(s, Indecision, 0, Zone.Hand);
        return (MatchEngine.FromTestState(s), first, second, indecision);
    }

    private static void IndecisionDiscards()
    {
        var (e, first, second, indecision) = IndecisionTable();
        Accept(e, Use(e, 1, "training-limited-draw", first.Id));
        Check(e.State.PendingChoice?.Type != "afterActionResolvedReaction", "one action that put a card into his hand: no window");
        Eq(e.State.MechD.HandActionsThisPhase[1], 1, "counted");
        Accept(e, PassC(e));
        Accept(e, Use(e, 1, "training-limited-draw", second.Id));
        Eq(e.State.MechD.HandActionsThisPhase[1], 2, "two actions put cards into his hand this phase");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens: the holder is notified");
        Eq(e.State.PriorityPlayerId, 0, "Indecision's holder is asked");
        Check(CanUse(e, 0, "indecision-discard", indecision.Id), "Indecision is offered from the hand");
        var hand = e.State.Players[1].Hand.ToArray();
        Accept(e, Use(e, 0, "indecision-discard", indecision.Id));
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "the player of the action chooses what he discards");
        Eq(e.State.PendingDeciderChoice!.DeciderPlayerId, 1, "player 1 decides");
        Eq(e.State.PendingDeciderChoice.Min, 2); Eq(e.State.PendingDeciderChoice.Max, 2);
        Accept(e, new GameCommand("chooseDecider", 1, e.Version, CardId: indecision.Id, TargetId: hand[0]));
        Accept(e, new GameCommand("chooseDecider", 1, e.Version, CardId: indecision.Id, TargetId: hand[1]));
        if (e.State.PendingDeciderChoice is not null) Accept(e, new GameCommand("chooseDecider", 1, e.Version, CardId: indecision.Id, Choice: "done"));
        Eq(e.State.Players[1].Hand.Count, hand.Length - 2, "two cards discarded");
        Check(e.State.Cards[hand[0]].Zone == Zone.FateDiscard && e.State.Cards[hand[1]].Zone == Zone.FateDiscard, "the chosen two");
        Eq(e.State.Cards[indecision.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
    }

    private static void IndecisionEdges()
    {
        // Two cards or fewer in the hand: both discarded without a choice.
        var (e, first, second, indecision) = IndecisionTable(0);
        Accept(e, Use(e, 1, "training-limited-draw", first.Id)); Accept(e, PassC(e));
        Accept(e, Use(e, 1, "training-limited-draw", second.Id));
        Eq(e.State.Players[1].Hand.Count, 2, "two drawn cards in his hand");
        Accept(e, Use(e, 0, "indecision-discard", indecision.Id));
        Eq(e.State.Players[1].Hand.Count, 0, "both discarded, nothing asked");
        Check(e.State.PendingDeciderChoice is null, "no choice");
        // A new phase starts the count again (the Attack Phase is a new phase).
        var (n, nFirst, nSecond, nIndecision) = IndecisionTable();
        Accept(n, Use(n, 1, "training-limited-draw", nFirst.Id)); Accept(n, PassC(n));
        var later = Edit(n, st => { });
        Eq(later.State.MechD.HandActionsThisPhase[1], 1, "one so far");
        var reset = MatchEngine.FromTestState(later.State);
        Passing(reset, x => x.State.Phase != Phase.Action);
        Eq(reset.State.MechD.HandActionsThisPhase[1], 0, "the phase changed: the count restarts");
        Check(!reset.State.MechD.CardsIntoHandThisPhase[1], "and so does the cards-into-hand record");
        // The Reaction is not offered after only one such action.
        var (w, wFirst, _, wIndecision) = IndecisionTable();
        Accept(w, Use(w, 1, "training-limited-draw", wFirst.Id));
        Check(!CanUse(w, 0, "indecision-discard", wIndecision.Id), "not offered after one");
    }

    private static void IndecisionMove()
    {
        var s = Table(Phase.Battle, 1);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // presence is not needed (it moves to the battle), but a unit is there
        var enemy = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var athome = Add(s, "training-crane-samurai", 1, Zone.Home, bowed: true);
        var there = Add(s, "training-crane-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        var indecision = Add(s, Indecision, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "indecision-move", indecision.Id, performerId: athome.Id));
        Eq(e.State.Cards[athome.Id].Zone, Zone.Battlefield, "he moved to the current battlefield");
        Check(!e.State.Cards[athome.Id].Bowed, "and straightened because he moved");
        // Already there: no move, no straightening.
        var s2 = Table(Phase.Battle, 1);
        Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var there2 = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        var ind2 = Add(s2, Indecision, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 1, "indecision-move", ind2.Id, performerId: there2.Id));
        Check(e2.State.Cards[there2.Id].Bowed, "he did not move, so he is not straightened");
        // No enemy units at the current battlefield.
        var s3 = Table(Phase.Battle, 1);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var home3 = Add(s3, "training-crane-samurai", 1, Zone.Home);
        var ind3 = Add(s3, Indecision, 1, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e3, 1, "indecision-move", ind3.Id), "no enemy units there: not offered");
        Check(!e3.Execute(Use(e3, 1, "indecision-move", ind3.Id, performerId: home3.Id)).Accepted, "and refused");
    }

    // ---- item 31 --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void YajindenRaises()
    {
        var s = Table(Phase.Action, 1);
        var yajinden = Add(s, Yajinden, 1, Zone.Home);
        var dead = Add(s, "training-lion-samurai", 0, Zone.Dead);
        var follower = Add(s, "training-follower", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 1);
        Accept(e, Offer(e, 1, "yajinden-raise", yajinden.Id, dead.Id));
        Settle(e);
        var raised = e.State.Cards[dead.Id];
        Eq(raised.Zone, Zone.Home, "he entered play at home");
        Eq(raised.OwnerId, 1, "under Yajinden's controller's control");
        Eq(raised.OriginalOwnerId, 0, "his owner is still player 0");
        Eq(Honor(e, 1), honor, "no cost was paid (Honor unchanged)");
        Check(e.State.Events.Any(x => x.Type == "timedRemovalScheduled" && x.CardId == dead.Id), "the pending removal is announced");
        Check(e.State.MechD.TimedRemovals.Any(r => r.CardId == dead.Id && r.When == TimedRemovalWhen.TurnEnds), "recorded");
        // Give him a Follower (his unit), then let the turn end.
        var withUnit = Edit(e, st => { var f = st.Cards[follower.Id]; st.Players[1].Hand.Remove(f.Id); f.Zone = Zone.Attached; f.AttachedTo = dead.Id; f.FaceUp = true; });
        var turn = withUnit.State.TurnNumber;
        Passing(withUnit, x => x.State.TurnNumber > turn);
        Eq(withUnit.State.Cards[dead.Id].Zone, Zone.RemovedFromGame, "removed from the game when the turn ended");
        Eq(withUnit.State.Cards[dead.Id].OwnerId, 0, "back with his owner");
        Eq(withUnit.State.Cards[follower.Id].Zone, Zone.RemovedFromGame, "his unit left play the same way");
        Check(withUnit.State.MechD.TimedRemovals.Count == 0, "the record is spent");
    }

    private static void YajindenEdges()
    {
        // His own dead Personality.
        var s = Table(Phase.Action, 1);
        var yajinden = Add(s, Yajinden, 1, Zone.Home);
        var own = Add(s, "training-lion-samurai", 1, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "yajinden-raise", yajinden.Id, own.Id));
        Check(e.State.Cards[own.Id].Zone is Zone.Home, "his own dead Personality came back");
        // Destroyed again before the turn ends: he stays in the discard pile.
        var again = Edit(e, st => { st.Cards[own.Id].Zone = Zone.Dead; });
        var turn = again.State.TurnNumber;
        Passing(again, x => x.State.TurnNumber > turn);
        Eq(again.State.Cards[own.Id].Zone, Zone.Dead, "no longer in play when the turn ended: not removed");
        // A Loyal Personality of another Clan will not be controlled: he stays dead.
        var l = Table(Phase.Action, 1);
        var y2 = Add(l, Yajinden, 1, Zone.Home);
        var loyal = Add(l, "training-crane-samurai", 0, Zone.Dead); GiveKeyword(loyal, "Loyal"); // a Crane Personality; player 1 is the Lion player
        var le = MatchEngine.FromTestState(l);
        Accept(le, Offer(le, 1, "yajinden-raise", y2.Id, loyal.Id));
        Eq(le.State.Cards[loyal.Id].Zone, Zone.Dead, "a Loyal Personality who will not be controlled stays dead");
        Check(le.State.MechD.TimedRemovals.Count == 0, "nothing to remove");
        // Not a dead Personality: not a legal target.
        var p = Table(Phase.Action, 1);
        var y3 = Add(p, Yajinden, 1, Zone.Home);
        var alive = Add(p, "training-lion-samurai", 0, Zone.Home);
        var pe = MatchEngine.FromTestState(p);
        Check(!Options(pe, 1, "yajinden-raise", y3.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(alive.Id) ?? false)), "a Personality in play is not a target");
        // After Yajinden enters play: lose 4 Honor (his trait, the shared EntryHonorLoss).
        Eq(CardCatalog.Get(Yajinden).EntryHonorLoss, 4, "the entry trait is EntryHonorLoss 4");
    }

    // ---- whole vs partial negation (Mendon's ruling) -----------------------------------------------------------------------------------------------------------------------------------------

    private static void NegationRecon()
    {
        // Wholly negated: the engine's whole-action negation path (its costs paid, nothing of it resolves).
        var s = Table(Phase.Action, 1);
        var actor = Add(s, "training-recon-actor", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s); e.NegateEveryAction = true;
        Accept(e, Use(e, 1, "training-recon-open", actor.Id));
        Check(Logged(e, "abilityUsed") && e.State.Events.Any(x => x.Message.Contains("(negated)")), "the action was taken, wholly negated");
        Eq(Recon(e, 1), 0, "a wholly negated Recon action is not counted");
        // Partially negated: the Recon Ranged Attack resolves, but its destruction of the target is negated.
        var b = Table(Phase.Battle, 1);
        var scout = Add(b, "training-recon-actor", 1, Zone.Battlefield, province: 0);
        var enemy = Add(b, "training-follower", 0, Zone.Attached, province: 0, parent: Add(b, "training-lion-samurai", 0, Zone.Battlefield, province: 0).Id);
        b.NegatedEffects.Add(new("destroy", enemy.Id));
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 1, "training-recon-attack", scout.Id, enemy.Id));
        for (var guard = 0; be.State.PendingChoice is { } pending && guard < 8; guard++)
        {
            var decline = be.QueryActions(pending.PlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "pass" or "chooseAttackTarget");
            if (decline is null) break;
            Accept(be, decline.Command!);
        }
        Eq(be.State.Cards[enemy.Id].Zone, Zone.Attached, "its destruction was negated: the target survives");
        Eq(Recon(be, 1), 1, "a partially negated Recon action is counted");
    }

    private static void NegationHandActions()
    {
        // Wholly negated second draw: the count stays at one, no Indecision window.
        var (e, first, second, indecision) = IndecisionTable();
        Accept(e, Use(e, 1, "training-limited-draw", first.Id)); Accept(e, PassC(e));
        var hand = e.State.Players[1].Hand.Count;
        var negating = Edit(e, st => { }); negating.NegateEveryAction = true;
        Accept(negating, Use(negating, 1, "training-limited-draw", second.Id));
        Eq(negating.State.Players[1].Hand.Count, hand - 1, "the wholly negated action drew nothing (the Strategy left the hand)");
        Eq(negating.State.MechD.HandActionsThisPhase[1], 1, "a wholly negated action is not counted");
        Check(negating.State.PendingChoice?.Type != "afterActionResolvedReaction", "no Indecision window");
        // Partially negated: Subtle Games ("Dishonor him. Draw a card") with the dishonor negated still draws, so it counts -- the second such action opens Indecision's window.
        var s = Table(Phase.Action, 1);
        var courtier = Add(s, "training-crane-samurai", 1, Zone.Home); GiveKeyword(courtier, "Courtier");
        var victim = Add(s, "training-lion-samurai", 0, Zone.Home);
        var draw = Add(s, "training-bushido-virtue", 1, Zone.Hand);
        var games = Add(s, "oracle-l5r-9932", 1, Zone.Hand);
        Add(s, "training-item", 1, Zone.Hand); Add(s, "training-item", 1, Zone.Hand);
        var holder = Add(s, Indecision, 0, Zone.Hand);
        var p = MatchEngine.FromTestState(s);
        Accept(p, Use(p, 1, "training-limited-draw", draw.Id)); Accept(p, PassC(p));
        var partial = Edit(p, st => st.NegatedEffects.Add(new("dishonor", victim.Id)));
        Accept(partial, Use(partial, 1, "subtle-games-dishonor", games.Id, victim.Id, courtier.Id));
        Check(!partial.State.Cards[victim.Id].Dishonorable, "its dishonor was negated");
        Eq(partial.State.MechD.HandActionsThisPhase[1], 2, "the partially negated action still put a card into the hand: counted");
        Eq(partial.State.PendingChoice?.Type, "afterActionResolvedReaction", "the second such action opens the window");
        Check(CanUse(partial, 0, "indecision-discard", holder.Id), "Indecision is offered");
    }

    private static void NegationCipherRecords()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0; RealStronghold(s, 0);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var spell = Add(s, "training-spell", 0, Zone.Hand);
        var cipher = Add(s, Cipher, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s); e.NegateEveryAction = true;
        EquipBattle(e, 0, shugenja, spell);
        Check(e.State.Cards[spell.Id].Zone != Zone.Attached, "the wholly negated Equip attached nothing");
        Eq(e.State.MechD.AttachLog.Count, 0, "and logged nothing");
        var later = Edit(e, st => st.PriorityPlayerId = 0);
        Check(!CanUse(later, 0, "broken-cipher-draw", cipher.Id), "so Broken Cipher may not be taken");
        // A wholly negated draw puts no card into the hand: with a Spell attached, Broken Cipher stays available.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0; RealStronghold(t, 0);
        var sh2 = Add(t, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var sp2 = Add(t, "training-spell", 0, Zone.Hand);
        var first = Add(t, Cipher, 0, Zone.Hand); var second = Add(t, Cipher, 0, Zone.Hand);
        var te = MatchEngine.FromTestState(t);
        EquipBattle(te, 0, sh2, sp2);
        var negated = Edit(te, st => st.PriorityPlayerId = 0); negated.NegateEveryAction = true;
        Accept(negated, Use(negated, 0, "broken-cipher-draw", first.Id));
        Check(!negated.State.MechD.CardsIntoHandThisPhase[0], "the wholly negated draw put no card into the hand");
        var again = Edit(negated, st => st.PriorityPlayerId = 0);
        Check(CanUse(again, 0, "broken-cipher-draw", second.Id), "so the second copy is still available");
    }

    // ---- item 32 and Amazing Feat -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Artisan, CardInstance Target, CardInstance Feat) FeatTable(string target = Keppo)
    {
        var s = Table(Phase.Action, 1);
        var artisan = Add(s, "training-crane-samurai", 1, Zone.Home); GiveKeyword(artisan, "Artisan");
        var copied = Add(s, target, 0, Zone.Home);
        var feat = Add(s, Feat, 1, Zone.Hand);
        return (MatchEngine.FromTestState(s), artisan, copied, feat);
    }

    private static CardInstance Created(MatchEngine e) => e.State.Cards.Values.Single(c => c.CreatedCopy);

    private static void AmazingFeatCopies()
    {
        var (e, artisan, keppo, feat) = FeatTable();
        var honor = Honor(e, 1);
        Accept(e, Use(e, 1, "amazing-feat-create", feat.Id, keppo.Id, artisan.Id));
        Settle(e);
        Check(e.State.Cards[artisan.Id].Bowed, "the Artisan was bowed as the cost");
        var copy = Created(e);
        Eq(copy.OwnerId, 1, "the creating player's"); Eq(copy.Zone, Zone.Home, "at home");
        var printed = copy.Printed!; var keppoDef = CardCatalog.Get(Keppo);
        Check(printed.Force == keppoDef.Force && printed.Chi == keppoDef.Chi && printed.PersonalHonor == keppoDef.PersonalHonor, "base Force, Chi and Personal Honor");
        Eq(string.Join(",", printed.Keywords!), string.Join(",", keppoDef.Keywords!), "his keywords");
        Eq(string.Join(",", printed.Abilities!), "keppo-destroy", "his base ability");
        Check(printed.EntryHonorLoss is null && printed.Effect == "none", "his trait is not copied");
        Eq(Honor(e, 1), honor, "so no Honor was lost as the copy entered play");
        Check(printed.GoldCost == 0 && printed.HonorRequirement is null, "a created Personality: Gold Cost 0, no Honor Requirement");
        var view = e.GetView(0).Players[1].Home.First(c => c.Id == copy.Id);
        Check(view.CreatedCopy && view.IsCopy, "the view marks it as a created copy");
        Eq(view.CopyOfCardId, keppo.Id, "the view names the copied card");
        Eq(view.CopyOfDefinitionId, Keppo, "and its definition (the scan the duplicate frame shows)");
        Check(view.CopyConditions is { } conditions && conditions.Contains("next turn begins (Amazing Feat)"), $"its conditions: {view.CopyConditions}");
        Eq(view.Definition!.ArtworkKey, keppoDef.ArtworkKey, "the copy shows the copied card's scan");
        // The other player's turn: still in play. Its creator's next turn: removed.
        var start = e.State.TurnNumber;
        Passing(e, x => x.State.TurnNumber == start + 1 && x.State.Phase == Phase.Action);
        Eq(e.State.Cards[copy.Id].Zone, Zone.Home, "still in play during the other player's turn");
        Passing(e, x => x.State.TurnNumber == start + 2);
        Eq(e.State.Cards[copy.Id].Zone, Zone.RemovedFromGame, "removed from the game when its player's next turn began");
        Check(e.State.MechD.TimedRemovals.Count == 0, "the removal is spent");
    }

    private static void AmazingFeatEdges()
    {
        // Abilities that copy abilities are not copied.
        var (e, artisan, receiver, feat) = FeatTable("training-skin-receiver");
        Accept(e, Use(e, 1, "amazing-feat-create", feat.Id, receiver.Id, artisan.Id));
        Eq(string.Join(",", Created(e).Printed!.Abilities!), "training-copy-keyword", "the two abilities that copy abilities are left out");
        // A Unique Personality may not be targeted.
        var (u, ua, _, uf) = FeatTable();
        var unique = Edit(u, st => { });
        var s = unique.State; var uniqueTarget = Add(s, Yajinden, 0, Zone.Home); var ue = MatchEngine.FromTestState(s);
        Check(!ue.Execute(Use(ue, 1, "amazing-feat-create", uf.Id, uniqueTarget.Id, ua.Id)).Accepted, "a Unique Personality is refused");
        // No unbowed Artisan: not offered.
        var (b, ba, bt, bf) = FeatTable();
        var bowed = Edit(b, st => st.Cards[ba.Id].Bowed = true);
        Check(!CanUse(bowed, 1, "amazing-feat-create", bf.Id), "a bowed Artisan cannot pay the cost");
        var (n, na, nt, nf) = FeatTable();
        var notArtisan = Edit(n, st => st.Cards[na.Id].GrantedKeywords.Clear());
        Check(!CanUse(notArtisan, 1, "amazing-feat-create", nf.Id), "no Artisan: not offered");
        // A copied ability is the copy's own: training-recon-actor's Recon Open is usable by the copy (and counts as a Recon action).
        var (r, ra, rt, rf) = FeatTable("training-recon-actor");
        Accept(r, Use(r, 1, "amazing-feat-create", rf.Id, rt.Id, ra.Id));
        Settle(r);
        if (r.State.PriorityPlayerId != 1) Accept(r, PassC(r));
        var copy = Created(r);
        Accept(r, Use(r, 1, "training-recon-open", copy.Id));
        Eq(Recon(r, 1), 1, "the copy's Recon Open was used, a Recon action");
    }
}
