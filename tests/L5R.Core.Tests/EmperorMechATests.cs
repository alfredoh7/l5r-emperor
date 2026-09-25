using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #501, mechanics group A (additional actions and phases; src/L5R.Core/EmperorMechA.cs): Mendon's items 1-5 and the cards they unblock -- Isawa Momoko and Mirumoto Inokuchi (personality-wave-83),
// Beyond the Line, One Action, Two Strikes and String of Victories (strategy-wave-69), Aramasu's Legacy (stronghold-wave-23). Every position is built explicitly and driven through QueryActions and
// the commands it offers.
internal static class EmperorMechATests
{
    private const string Momoko = "oracle-l5r-10076", Inokuchi = "oracle-l5r-5106", BeyondTheLine = "oracle-l5r-967", OneActionTwoStrikes = "oracle-l5r-5706", StringOfVictories = "oracle-l5r-7588",
        Aramasu = "oracle-l5r-10184", HiddenFalls = "oracle-l5r-3257", Snare = "oracle-l5r-3738", Winds = "oracle-l5r-9344", SetTheFields = "oracle-l5r-9888", Sawaken = "oracle-l5r-6804",
        RingOfEarth = "oracle-l5r-6334", RingOfAir = "oracle-l5r-6333";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static void RunUntil(MatchEngine e, Func<bool> stop) => Batch336Support.RunUntil(e, stop);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void GiveAbility(CardInstance card, string ability) => card.GrantedAbilities.Add(new(ability, "test-fixture", GrantDuration.Permanent));
    private static CardInstance StrongholdOf(MatchState s, int player, string definition) { var card = s.Cards[s.Players[player].StrongholdId]; card.DefinitionId = definition; card.Bowed = false; return card; }
    private static bool Logged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);
    private static GameCommand Cmd(MatchEngine e, string type, int player) => new(type, player, e.Version);
    private static bool Offered(MatchEngine e, int player, string type) => e.QueryActions(player).Any(a => a.Type == type && a.Enabled);
    private static int Grants(MatchEngine e, int player) => e.State.AdditionalActions.Count(g => g.PlayerId == player);
    // Removes every Personality the economy scenario put in play, so each test decides who is there.
    private static void ClearPersonalities(MatchState s)
    {
        foreach (var id in s.Cards.Values.Where(c => c.Zone is Zone.Home or Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality).Select(c => c.Id).ToArray()) s.Cards.Remove(id);
    }

    public static void Run(TestRunner runner) => runner.Run("emperor-mech-A", [
        ("Published shape: Isawa Momoko and Mirumoto Inokuchi (personality-wave-83), Beyond the Line, One Action, Two Strikes and String of Victories (strategy-wave-69), Aramasu's Legacy (stronghold-wave-23) print their scans' stats, keywords, text and ability ids, ship their art and are in their manifests", PublishedShape),
        ("Item 2, pass: a player holding two additional Battle actions takes them one pass at a time (he keeps the priority after the first); his two passes count once, and the battle resolves only when the other player passes too", PassTakesOneGrant),
        ("Item 2, pass in the Action Phase: two additional actions taken as passes do not end the phase; the other player's pass after them does", PassInActionPhase),
        ("Item 2, decline: an optional additional action may be declined -- it is gone, the priority passes, and it is not a pass (the next two passes, one by each player, are needed); with nothing held it is neither offered nor accepted", DeclineIsNotPassing),
        ("Isawa Momoko, Battle: the next action on one of her Spells this turn ignores its bowing cost (the Spell stays unbowed); she may take an additional Battle action, which that action is; a second Spell action, and a Spell on another Shugenja, pay as usual", MomokoBattle),
        ("Isawa Momoko, Open: in the Action Phase the waiver covers \"Bow this Shugenja\" (Winds of Dismissal leaves her unbowed) and the additional action is a Limited one on her own turn; a destruction cost is still paid (Set the Fields Aflame is destroyed); the waiver lasts this turn only", MomokoOpen),
        ("Mirumoto Inokuchi: even if bowed he straightens and may take an additional Battle action that must be Iaijutsu and that he performs -- one he performs (a granted Iaijutsu Battle) is taken and he stays unbowed; a non-Iaijutsu action of his and another Personality's Iaijutsu action are refused", InokuchiIaijutsu),
        ("Mirumoto Inokuchi, \"If you don't, bow him\": declining the additional action bows him, and so does taking it as a pass; the priority then passes", InokuchiIfYouDont),
        ("Beyond the Line: a mandatory additional action to use a Limited or Open Recon ability as a Battle action targeting the current battlefield's province (Hidden Falls Dojo's Recon Open); the player draws a card after it resolves; another province, a Recon ability with no province target, a non-Recon ability and an ordinary Battle action are refused; it may not be declined", BeyondTheLineRecon),
        ("Mendon's ruling on item 2 (issue #501): a player may always pass -- holding a mandatory grant (Beyond the Line), a restricted optional one (Inokuchi's) or several with no legal action to take, the pass is offered and accepted, and each pass takes one grant", PassAlwaysAvailable),
        ("Mendon's ruling on Beyond the Line (issue #501): either the Attacker or the Defender may play it, and only the player who played it gets the additional action -- the other player holds no grant and cannot use his own Recon Open as a Battle action", BeyondTheLineOnlyItsPlayer),
        ("Beyond the Line, edges: without it no Limited or Open ability is a Battle action; taken as a pass the grant is spent and nothing is drawn", BeyondTheLineEdges),
        ("One Action, Two Strikes: after the enemy leader took a Battle action this battle it grants two additional Battle actions; no other additional action is created for its player this battle (training-grant-bow's grant is not), and the priority passes after the two", OneActionTwoStrikesGrants),
        ("One Action, Two Strikes, edges: not offered when the other player has taken no Battle action this battle (a pass is not one), nor in a later battle; its lock and the record are per battle", OneActionTwoStrikesEdges),
        ("String of Victories, Open: its player's bowed Rings straighten, he may put a Ring from his Fate discard pile into his hand, and he may take an additional action appropriate to the phase -- on the other player's turn an Open one, not a Limited one", StringOfVictoriesOpen),
        ("String of Victories, Battle: the additional action is a Battle action; with no Ring in the discard pile nothing is asked; a Battle/Open action taken in a battle spends a grant", StringOfVictoriesBattle),
        ("Aramasu's Legacy: a whole Attack Phase inside the Action Phase -- maneuvers, battles and resolution as normal, but a win destroys no defending army and no province, and the controller's Mantis Clan Magistrate and attachments are not bowed by resolution; afterwards the Action Phase resumes and he bows a Personality whose straightening is negated until after his next Straighten Phase ends", AramasuWin),
        ("Aramasu's Legacy, edges: with no battle won nothing is offered and the Action Phase resumes with the other player's opportunity; a tie destroys the attacking army only; it is a Limited action (not on the other player's turn, once a turn)", AramasuEdges),
    ]);

    // ---- published shape ---------------------------------------------------------------------------------------------------------------------------------

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
                Check(!CardCatalog.Get(id).ScenarioOnly, $"{id} is playable");
            }
        }
        var momoko = CardCatalog.Get(Momoko);
        Check(momoko.Type == CardType.Personality && momoko.Name == "Isawa Momoko" && momoko.Force == 5 && momoko.Chi == 3 && momoko.GoldCost == 9 && momoko.PersonalHonor == 2 && momoko.HonorRequirement == 0, $"Momoko: {momoko.Force}/{momoko.Chi}/{momoko.GoldCost}/{momoko.PersonalHonor}/{momoko.HonorRequirement}");
        Eq(string.Join(",", momoko.Keywords ?? []), "Fire,Phoenix Clan,Shugenja"); Eq(string.Join(",", momoko.Abilities ?? []), "momoko-spell-waiver");
        Eq(momoko.Text, "Battle/Open: After the next time this turn you announce an action on one of Momoko's Spells, ignore the action's non-destruction costs. You may take an additional action.");
        var inokuchi = CardCatalog.Get(Inokuchi);
        Check(inokuchi.Type == CardType.Personality && inokuchi.Force == 4 && inokuchi.Chi == 4 && inokuchi.GoldCost == 8 && inokuchi.PersonalHonor == 2 && inokuchi.HonorRequirement == 4, $"Inokuchi: {inokuchi.Force}/{inokuchi.Chi}/{inokuchi.GoldCost}");
        Eq(string.Join(",", inokuchi.Keywords ?? []), "Duelist,Dragon Clan,Samurai,Unbreakable"); Eq(string.Join(",", inokuchi.Abilities ?? []), "inokuchi-straighten");
        Eq(inokuchi.Text, "Battle: Even if Inokuchi is bowed: Straighten him. You may take an additional Battle action, which must be Iaijutsu and which Inokuchi must perform. If you don't, bow him.");
        Manifest("personality-wave-83", Inokuchi, Momoko);

        var btl = CardCatalog.Get(BeyondTheLine);
        Check(btl.Type == CardType.Strategy && btl.GoldCost == 0 && btl.Focus == 2, $"Beyond the Line: {btl.Type} {btl.GoldCost} {btl.Focus}");
        Eq(string.Join(",", btl.Abilities ?? []), "beyond-the-line-recon");
        Eq(btl.Text, "Battle: Take an additional action to use a Limited or Open Recon ability as a Battle action, which must target the current battlefield's province. If you do, draw a card after the action's resolution.");
        var oats = CardCatalog.Get(OneActionTwoStrikes);
        Check(oats.Type == CardType.Strategy && oats.GoldCost == 0 && oats.Focus == 1 && oats.Name == "One Action, Two Strikes", $"One Action, Two Strikes: {oats.Focus}");
        Eq(string.Join(",", oats.Abilities ?? []), "one-action-two-strikes");
        Eq(oats.Text, "Battle: If the enemy leader has taken any Battle actions during this battle: You may take two additional Battle actions after this one. You will not take any other additional Battle actions this battle.");
        var sov = CardCatalog.Get(StringOfVictories);
        Check(sov.Type == CardType.Strategy && sov.GoldCost == 0 && sov.Focus == 4, $"String of Victories: {sov.Focus}");
        Eq(string.Join(",", sov.Abilities ?? []), "string-of-victories");
        Eq(sov.Text, "Battle/Open: Straighten all of your Rings. You may put a Ring in your Fate discard pile into your hand. You may take an additional action appropriate to this phase.");
        Manifest("strategy-wave-69", BeyondTheLine, OneActionTwoStrikes, StringOfVictories);

        var aramasu = CardCatalog.Get(Aramasu);
        Check(aramasu.Type == CardType.Stronghold && aramasu.Name == "Aramasu's Legacy" && aramasu.ProvinceStrength == 7 && aramasu.GoldProduction == 4 && aramasu.StartingHonor == 2 && aramasu.Clan == "Mantis",
            $"Aramasu's Legacy: {aramasu.ProvinceStrength}/{aramasu.GoldProduction}/{aramasu.StartingHonor}/{aramasu.Clan}");
        Eq(string.Join(",", aramasu.Abilities ?? []), "aramasus-legacy-attack-phase");
        Eq(aramasu.Text, "Limited: Create an additional Attack Phase, during which battle resolution does not destroy defending armies or provinces and does not bow your Mantis Clan Magistrates or your attachments. After it ends, if you won any of its battles (even against no units at resolution), you may target a Personality or Holding, bow it, and negate its straightening until after your next Straighten Phase ends.");
        Manifest("stronghold-wave-23", Aramasu);
        Eq(PublishedContent.StrategyWave69.Cards.Length, 3); Eq(PublishedContent.PersonalityWave83.Cards.Length, 2); Eq(PublishedContent.StrongholdWave23.Cards.Length, 1);
        Eq(AbilityCatalog.All["momoko-spell-waiver"].Timing, "BattleOrOpen"); Eq(AbilityCatalog.All["string-of-victories"].Timing, "BattleOrOpen");
        Eq(AbilityCatalog.All["inokuchi-straighten"].Timing, "Battle"); Eq(AbilityCatalog.All["aramasus-legacy-attack-phase"].Timing, "Limited");
    }

    // ---- item 2 ---------------------------------------------------------------------------------------------------------------------------------------------

    // Player 1 attacks at province 0 with a bower; player 0 defends. Player 1 to act, holding `grants` optional additional actions.
    private static MatchEngine BurstRig(int grants, Phase phase = Phase.Battle)
    {
        var s = Table(phase, 1); ClearPersonalities(s);
        if (phase == Phase.Battle)
        {
            Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        }
        for (var i = 0; i < grants; i++) s.AdditionalActions.Add(new(1, null, Id: $"test-grant-{i}"));
        return MatchEngine.FromTestState(s);
    }

    private static void PassTakesOneGrant()
    {
        var e = BurstRig(2);
        Accept(e, Cmd(e, "pass", 1));
        Eq(Grants(e, 1), 1, "the first pass takes one of the two additional actions");
        Eq(e.State.PriorityPlayerId, 1, "the holder keeps the priority for the other");
        Check(Logged(e, "additionalActionPassed"), "logged as an additional action taken as a pass");
        Accept(e, Cmd(e, "pass", 1));
        Eq(Grants(e, 1), 0, "the second pass takes the other");
        Eq(e.State.PriorityPlayerId, 0, "the priority passes");
        Eq(e.State.Phase, Phase.Battle, "one player passing twice in a row does not resolve the battle");
        Eq(e.State.ConsecutivePasses, 1, "his two passes count as one");
        Accept(e, Cmd(e, "pass", 0));
        Check(e.State.Phase != Phase.Battle || e.State.CurrentBattleProvince is null || Logged(e, "battleResolved"), "the other player's pass resolves the battle");
        Check(Logged(e, "battleResolved"), "the battle resolved");
    }

    private static void PassInActionPhase()
    {
        var e = BurstRig(2, Phase.Action);
        Accept(e, Cmd(e, "pass", 1));
        Eq(e.State.PriorityPlayerId, 1); Eq(Grants(e, 1), 1);
        Accept(e, Cmd(e, "pass", 1));
        Eq(e.State.Phase, Phase.Action, "the Action Phase goes on after one player's two passes");
        Eq(e.State.PriorityPlayerId, 0);
        Accept(e, Cmd(e, "pass", 0));
        Eq(e.State.Phase, Phase.AttackDeclaration, "the other player's pass ends it");
    }

    private static void DeclineIsNotPassing()
    {
        var e = BurstRig(1);
        var decline = e.QueryActions(1).SingleOrDefault(a => a.Type == "declineAdditionalAction");
        Check(decline is { Enabled: true }, "declining is offered to the holder");
        Check(!Offered(e, 0, "declineAdditionalAction"), "never to the other player");
        Accept(e, decline!.Command!);
        Eq(Grants(e, 1), 0, "the additional action is gone");
        Eq(e.State.PriorityPlayerId, 0, "the priority passes as after his last action");
        Eq(e.State.ConsecutivePasses, 0, "declining is not a pass");
        Check(Logged(e, "additionalActionDeclined") && !e.State.Events.Any(x => x.Type == "passed" && x.PlayerId == 1), "logged as a decline, not a pass");
        Accept(e, Cmd(e, "pass", 0));
        Eq(e.State.Phase, Phase.Battle, "one pass after the decline does not resolve the battle");
        Accept(e, Cmd(e, "pass", 1));
        Check(Logged(e, "battleResolved"), "two passes, one by each player, do");
        // Nothing held: not offered, and refused.
        var plain = BurstRig(0);
        Check(!plain.QueryActions(1).Any(a => a.Type == "declineAdditionalAction"), "with no additional action held, nothing to decline");
        Rejection(plain, Cmd(plain, "declineAdditionalAction", 1));
    }

    // ---- Isawa Momoko -----------------------------------------------------------------------------------------------------------------------------------------

    private static void MomokoBattle()
    {
        var s = Table(Phase.Battle, 1); ClearPersonalities(s);
        var momoko = Add(s, Momoko, 1, Zone.Battlefield, province: 0);
        var snare = Add(s, Snare, 1, Zone.Attached, province: 0, parent: momoko.Id);
        var snare2 = Add(s, Snare, 1, Zone.Attached, province: 0, parent: momoko.Id);
        var other = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0); GiveKeyword(other, "Shugenja");
        var otherSnare = Add(s, Snare, 1, Zone.Attached, province: 0, parent: other.Id);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy2 = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 1, "momoko-spell-waiver", momoko.Id).Command!);
        Eq(Grants(e, 1), 1, "she may take an additional action");
        Eq(e.State.PriorityPlayerId, 1, "the priority stays with her player");
        // The Spell's action: its "Bow this card" cost is ignored.
        Accept(e, Offer(e, 1, "snare-bow", snare.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "the Snare's effect happened");
        Check(!e.State.Cards[snare.Id].Bowed, "its bowing cost was ignored: the Snare is unbowed");
        Check(Logged(e, "momokoWaiver") && Logged(e, "costIgnored"), "logged");
        Eq(Grants(e, 1), 0, "the Spell action was the additional action");
        Eq(e.State.PriorityPlayerId, 0, "the burst is over: the priority passes");
        // Only the NEXT action: the second Snare pays its cost; so does a Spell on another Shugenja.
        e = Edit(e, st => st.PriorityPlayerId = 1);
        Accept(e, Offer(e, 1, "snare-bow", snare2.Id, enemy2.Id));
        Check(e.State.Cards[snare2.Id].Bowed, "a second Spell action pays its bowing cost");
        var s2 = Table(Phase.Battle, 1); ClearPersonalities(s2);
        var momoko2 = Add(s2, Momoko, 1, Zone.Battlefield, province: 0);
        var shugenja = Add(s2, "training-battle-bower", 1, Zone.Battlefield, province: 0); GiveKeyword(shugenja, "Shugenja");
        var foreign = Add(s2, Snare, 1, Zone.Attached, province: 0, parent: shugenja.Id);
        var target = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 1, "momoko-spell-waiver", momoko2.Id).Command!);
        Accept(e2, Offer(e2, 1, "snare-bow", foreign.Id, target.Id));
        Check(e2.State.Cards[foreign.Id].Bowed, "a Spell on another Shugenja is not one of Momoko's: it pays");
        Check(!Logged(e2, "momokoWaiver"), "and the watch is not spent");
    }

    private static void MomokoOpen()
    {
        var s = Table(Phase.Action, 1); ClearPersonalities(s);
        var momoko = Add(s, Momoko, 1, Zone.Home);
        var winds = Add(s, Winds, 1, Zone.Attached, parent: momoko.Id);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        // Control: without the waiver Winds of Dismissal bows her.
        var control = MatchEngine.FromTestState(s);
        Accept(control, Offer(control, 1, "winds-of-dismissal-choice", winds.Id, enemy.Id));
        Check(control.State.Cards[momoko.Id].Bowed, "control: \"Bow this Shugenja\" is paid");
        Accept(e, Enabled(e, 1, "momoko-spell-waiver", momoko.Id).Command!);
        Eq(Grants(e, 1), 1); Eq(e.State.PriorityPlayerId, 1);
        Accept(e, Offer(e, 1, "winds-of-dismissal-choice", winds.Id, enemy.Id));
        Check(!e.State.Cards[momoko.Id].Bowed, "the waiver: \"Bow this Shugenja\" is ignored");
        Eq(Grants(e, 1), 0, "the Limited Spell action (her own turn) was the additional action");
        Eq(e.State.PriorityPlayerId, 0);
        // A destruction cost is still paid: Set the Fields Aflame ("destroy this card").
        var s2 = Table(Phase.Action, 1); ClearPersonalities(s2);
        var momoko2 = Add(s2, Momoko, 1, Zone.Home);
        var fields = Add(s2, SetTheFields, 1, Zone.Attached, parent: momoko2.Id);
        var holding = s2.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Holding && !c.Bowed);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 1, "momoko-spell-waiver", momoko2.Id).Command!);
        Accept(e2, Offer(e2, 1, "set-fields-holding", fields.Id, holding.Id));
        Eq(e2.State.Cards[fields.Id].Zone, Zone.FateDiscard, "its destruction cost is paid");
        Check(e2.State.Cards[holding.Id].Bowed, "and the effect happened");
        // This turn only: the watch is gone when the turn is over.
        var e3 = MatchEngine.FromTestState(s);
        Accept(e3, Enabled(e3, 1, "momoko-spell-waiver", momoko.Id).Command!);
        e3 = Edit(e3, st => { st.TurnNumber++; st.AdditionalActions.Clear(); });
        Accept(e3, Offer(e3, 1, "winds-of-dismissal-choice", winds.Id, enemy.Id));
        Check(e3.State.Cards[momoko.Id].Bowed, "on a later turn the Spell action pays");
    }

    // ---- Mirumoto Inokuchi ----------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Inokuchi, CardInstance Enemy, CardInstance Sawaken) InokuchiRig(bool bowed = true, string? granted = "kataken-bow")
    {
        var s = Table(Phase.Battle, 1); ClearPersonalities(s);
        var inokuchi = Add(s, Inokuchi, 1, Zone.Battlefield, bowed: bowed, province: 0);
        if (granted is not null) GiveAbility(inokuchi, granted);
        GiveAbility(inokuchi, "training-bow-at-battle"); // a non-Iaijutsu Battle action he performs
        var sawaken = Add(s, Sawaken, 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        return (MatchEngine.FromTestState(s), inokuchi, enemy, sawaken);
    }

    private static void InokuchiIaijutsu()
    {
        var (e, inokuchi, enemy, sawaken) = InokuchiRig();
        Check(e.State.Cards[inokuchi.Id].Bowed, "he starts bowed");
        Accept(e, Enabled(e, 1, "inokuchi-straighten", inokuchi.Id).Command!);
        Check(!e.State.Cards[inokuchi.Id].Bowed, "even if bowed: straightened");
        var grant = e.State.AdditionalActions.Single();
        Check(grant.PerformerId == inokuchi.Id && grant.RequiredActionKeyword == "Iaijutsu" && !grant.Mandatory, "an optional additional action, Iaijutsu, his");
        Eq(e.State.PriorityPlayerId, 1);
        // Refused: a non-Iaijutsu action he performs, and another Personality's Iaijutsu action.
        var plain = Options(e, 1, "training-bow-at-battle", inokuchi.Id);
        Check(plain.Length > 0 && plain.All(a => !a.Enabled && (a.Reason ?? "").Contains("Iaijutsu")), "a non-Iaijutsu action is refused: " + string.Join("; ", plain.Select(a => a.Reason)));
        var theirs = Options(e, 1, "sawaken-weaken", sawaken.Id);
        Check(theirs.Length > 0 && theirs.All(a => !a.Enabled), "another Personality's Iaijutsu action is refused");
        // His own Iaijutsu Battle action is the additional action.
        Accept(e, Offer(e, 1, "kataken-bow", inokuchi.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "the Iaijutsu action resolved");
        Eq(Grants(e, 1), 0, "it spent the grant");
        Check(!e.State.Cards[inokuchi.Id].Bowed && !Logged(e, "inokuchiBowed"), "he took it, so he is not bowed");
        Eq(e.State.PriorityPlayerId, 0, "the priority passes");
    }

    private static void InokuchiIfYouDont()
    {
        var (e, inokuchi, _, _) = InokuchiRig(bowed: false);
        Accept(e, Enabled(e, 1, "inokuchi-straighten", inokuchi.Id).Command!);
        Accept(e, e.QueryActions(1).Single(a => a.Type == "declineAdditionalAction").Command!);
        Check(e.State.Cards[inokuchi.Id].Bowed && Logged(e, "inokuchiBowed"), "declined: he bows");
        Eq(e.State.PriorityPlayerId, 0);
        var (p, pInokuchi, _, _) = InokuchiRig(bowed: false);
        Accept(p, Enabled(p, 1, "inokuchi-straighten", pInokuchi.Id).Command!);
        Accept(p, Cmd(p, "pass", 1));
        Check(p.State.Cards[pInokuchi.Id].Bowed, "taken as a pass (not an Iaijutsu action): he bows");
        Eq(p.State.PriorityPlayerId, 0);
    }

    // ---- Beyond the Line ---------------------------------------------------------------------------------------------------------------------------------------

    // Player 1 attacks at province 0 of player 0 with Hidden Falls Dojo as his Stronghold and a Recon actor; player 1 to act.
    private static (MatchEngine E, CardInstance Strategy, CardInstance Dojo, CardInstance Actor, CardInstance Enemy) BeyondRig(bool withStrategy = true)
    {
        var s = Table(Phase.Battle, 1); ClearPersonalities(s);
        var dojo = StrongholdOf(s, 1, HiddenFalls);
        var actor = Add(s, "training-recon-actor", 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strategy = withStrategy ? Add(s, BeyondTheLine, 1, Zone.Hand) : Add(s, "training-plain-strategy", 1, Zone.FateDiscard);
        return (MatchEngine.FromTestState(s), strategy, dojo, actor, enemy);
    }

    private static void BeyondTheLineRecon()
    {
        var (e, strategy, dojo, actor, enemy) = BeyondRig();
        Accept(e, Enabled(e, 1, "beyond-the-line-recon", strategy.Id).Command!);
        var grant = e.State.AdditionalActions.Single();
        Check(grant.Tag == "beyond-the-line" && grant.Mandatory, "a mandatory additional action of its own kind");
        Eq(e.State.PriorityPlayerId, 1);
        Check(!e.QueryActions(1).Any(a => a.Type == "declineAdditionalAction"), "\"Take an additional action\" has no \"may\": it cannot be declined");
        // Hidden Falls Dojo's Recon Open, as a Battle action: only the current battlefield's province is a legal target.
        var falls = Options(e, 1, "hidden-falls-recon", dojo.Id);
        Check(falls.Any(a => a.Enabled && a.Command!.Provinces is [0]), "the Recon Open is offered as a Battle action, at province 1 (index 0)");
        Check(falls.Where(a => a.Command?.Provinces is not [0] && a.Label.Contains("province")).All(a => !a.Enabled) && falls.Any(a => !a.Enabled && (a.Reason ?? "").Contains("current battlefield's province")),
            "any other province set is refused: " + string.Join("; ", falls.Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        // Refused: a Recon Open with no province target, a non-Recon Open, an ordinary Battle action.
        Check(!CanUse(e, 1, "training-recon-open", actor.Id), "a Recon Open that targets no province is refused");
        Check(!CanUse(e, 1, "training-plain-open", actor.Id), "a non-Recon Open is not a Battle action");
        Check(!CanUse(e, 1, "training-recon-attack", actor.Id), "an ordinary Battle action is not what the grant allows");
        var hand = e.State.Players[1].Hand.Count;
        Accept(e, falls.Single(a => a.Enabled && a.Command!.Provinces is [0]).Command!);
        Check(e.State.Players[0].Provinces[0].Reconnaissance[1], "the Recon Open resolved: Reconnaissance at the battlefield");
        Eq(Grants(e, 1), 0, "it was the additional action");
        Eq(e.State.Players[1].Hand.Count, hand + 1, "a card was drawn after its resolution");
        Check(Logged(e, "beyondTheLineDraw"), "logged");
        Eq(e.State.PriorityPlayerId, 0, "the priority passes");
        Check(e.State.Cards[dojo.Id].AbilityUses.ContainsKey("hidden-falls-recon"), "the Dojo's ability is spent for the turn");
    }

    private static void BeyondTheLineEdges()
    {
        // Without Beyond the Line the Recon Open is not a Battle action.
        var (plain, _, plainDojo, _, _) = BeyondRig(withStrategy: false);
        Check(!Options(plain, 1, "hidden-falls-recon", plainDojo.Id).Any(), "without it the Dojo's Open is not on offer in a battle");
        // Taken as a pass: spent, nothing drawn.
        var (e, strategy, _, _, _) = BeyondRig();
        Accept(e, Enabled(e, 1, "beyond-the-line-recon", strategy.Id).Command!);
        var hand = e.State.Players[1].Hand.Count;
        Accept(e, Cmd(e, "pass", 1));
        Eq(Grants(e, 1), 0, "the pass took the additional action");
        Eq(e.State.Players[1].Hand.Count, hand, "no card is drawn");
        Eq(e.State.PriorityPlayerId, 0);
        Rejection(e, Cmd(e, "declineAdditionalAction", 0));
    }

    private static void PassAlwaysAvailable()
    {
        static void PassOffered(MatchEngine e, int player, string what)
        {
            var pass = e.QueryActions(player).SingleOrDefault(a => a.Type == "pass");
            Check(pass is { Enabled: true }, $"{what}: the pass is offered");
        }
        // A mandatory, restricted grant (Beyond the Line) with no Recon ability to take: pass it.
        var s = Table(Phase.Battle, 1); ClearPersonalities(s);
        Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var btl = Add(s, BeyondTheLine, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 1, "beyond-the-line-recon", btl.Id).Command!);
        Check(!e.QueryActions(1).Any(a => a.Type == "useAbility" && a.Enabled && AbilityCatalog.All[a.Command!.AbilityId!].Timing != "Reaction"), "no legal action to take with the grant");
        PassOffered(e, 1, "mandatory grant, nothing to take");
        Accept(e, Cmd(e, "pass", 1));
        Eq(Grants(e, 1), 0); Eq(e.State.PriorityPlayerId, 0);
        // A restricted optional grant (Inokuchi's Iaijutsu) plus a mandatory one and an unrestricted one: three passes, one grant each, the priority kept until the last.
        var (i, inokuchi, _, _) = InokuchiRig(bowed: false, granted: null);
        Accept(i, Enabled(i, 1, "inokuchi-straighten", inokuchi.Id).Command!); // an ordinary action: it would forfeit grants held before it, so the other two are added after
        i = Edit(i, st => { st.AdditionalActions.Add(new(1, null, Id: "extra-1")); st.AdditionalActions.Add(new(1, null, Tag: "beyond-the-line", RequiredActionKeyword: "Recon", Id: "extra-2", Mandatory: true)); });
        Eq(Grants(i, 1), 3, "three additional actions held");
        for (var left = 2; left >= 0; left--)
        {
            PassOffered(i, 1, $"{left + 1} grants held");
            Accept(i, Cmd(i, "pass", 1));
            Eq(Grants(i, 1), left, "each pass takes one grant");
            Eq(i.State.PriorityPlayerId, left > 0 ? 1 : 0, "the priority is kept while a grant is held");
        }
        Eq(i.State.Phase, Phase.Battle, "one player's passes never end the battle");
        Check(i.State.Cards[inokuchi.Id].Bowed, "Inokuchi's grant was passed, so he bows (the ruling on item 2 of the report)");
    }

    private static void BeyondTheLineOnlyItsPlayer()
    {
        foreach (var player in new[] { 1, 0 }) // the Attacker (player 1), then the Defender (player 0)
        {
            var s = Table(Phase.Battle, 1); ClearPersonalities(s); s.PriorityPlayerId = player;
            var dojos = new[] { StrongholdOf(s, 0, HiddenFalls), StrongholdOf(s, 1, HiddenFalls) };
            Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var btl = Add(s, BeyondTheLine, player, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Enabled(e, player, "beyond-the-line-recon", btl.Id).Command!);
            Eq(Grants(e, player), 1, $"player {player} played it and holds the grant");
            Eq(Grants(e, 1 - player), 0, "the other player holds none");
            Check(CanUse(e, player, "hidden-falls-recon", dojos[player].Id), $"player {player} may take the granted action");
            var other = Edit(e, st => st.PriorityPlayerId = 1 - player);
            Check(!Options(other, 1 - player, "hidden-falls-recon", dojos[1 - player].Id).Any(a => a.Enabled), "the other player cannot use his own Recon Open as a Battle action");
            Rejection(other, new GameCommand("useAbility", 1 - player, other.Version, dojos[1 - player].Id, AbilityId: "hidden-falls-recon", Province: 0, Provinces: [0]));
            var hand = e.State.Players[player].Hand.Count;
            Accept(e, Options(e, player, "hidden-falls-recon", dojos[player].Id).Single(a => a.Enabled).Command!);
            Eq(e.State.Players[player].Hand.Count, hand + 1, "its player took it and drew");
            Check(e.State.Players[0].Provinces[0].Reconnaissance[player] && !e.State.Players[0].Provinces[0].Reconnaissance[1 - player], "Reconnaissance is his alone");
        }
    }

    // ---- One Action, Two Strikes -----------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Strategy, CardInstance Bower, CardInstance Granter, CardInstance EnemyBower, CardInstance[] Enemies) OatsRig()
    {
        var s = Table(Phase.Battle, 1); ClearPersonalities(s);
        s.PriorityPlayerId = 0; // the Defender acts first
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var granter = Add(s, "training-grant-bower", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, OneActionTwoStrikes, 1, Zone.Hand);
        var enemyBower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var enemies = new[] { Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0), Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0) };
        return (MatchEngine.FromTestState(s), strategy, bower, granter, enemyBower, enemies);
    }

    private static void OneActionTwoStrikesGrants()
    {
        var (e, strategy, bower, granter, enemyBower, enemies) = OatsRig();
        Check(!CanUse(Edit(e, st => st.PriorityPlayerId = 1), 1, "one-action-two-strikes", strategy.Id), "before the enemy leader acts it is not playable");
        Accept(e, Offer(e, 0, "training-bow-at-battle", enemyBower.Id, bower.Id)); // the enemy leader's Battle action
        Eq(e.State.PriorityPlayerId, 1);
        Accept(e, Enabled(e, 1, "one-action-two-strikes", strategy.Id).Command!);
        Eq(Grants(e, 1), 2, "two additional Battle actions");
        Eq(e.State.PriorityPlayerId, 1);
        // The first: training-grant-bow would create another additional action -- "you will not take any other additional Battle actions this battle".
        Accept(e, Offer(e, 1, "training-grant-bow", granter.Id, enemies[0].Id));
        Check(e.State.Cards[enemies[0].Id].Bowed, "the first additional action resolved");
        Eq(Grants(e, 1), 1, "its own grant was not created: one of the two is left");
        Eq(e.State.PriorityPlayerId, 1);
        // The second: the Defender's action flows back only after it (the bower is bowed, so take the Strategy's second use through a pass).
        Accept(e, Cmd(e, "pass", 1));
        Eq(Grants(e, 1), 0); Eq(e.State.PriorityPlayerId, 0, "after the two, the priority passes");
    }

    private static void OneActionTwoStrikesEdges()
    {
        // The Defender only passed: not a Battle action.
        var (e, strategy, _, _, _, _) = OatsRig();
        Accept(e, Cmd(e, "pass", 0));
        Eq(e.State.PriorityPlayerId, 1);
        var refused = Options(e, 1, "one-action-two-strikes", strategy.Id);
        Check(refused.Length > 0 && refused.All(a => !a.Enabled && (a.Reason ?? "").Contains("enemy leader")), "a pass is not a Battle action: " + string.Join("; ", refused.Select(a => a.Reason)));
        // The record is per battle: a Battle action in an earlier battle does not count.
        var (e2, strategy2, bower2, _, enemyBower2, _) = OatsRig();
        Accept(e2, Offer(e2, 0, "training-bow-at-battle", enemyBower2.Id, bower2.Id));
        var later = Edit(e2, st => st.BattleNumber++);
        Check(!CanUse(later, 1, "one-action-two-strikes", strategy2.Id), "in a later battle the earlier action does not count");
        Check(CanUse(e2, 1, "one-action-two-strikes", strategy2.Id), "control: in this battle it does");
        // The lock is per battle too.
        var (e3, strategy3, bower3, granter3, enemyBower3, enemies3) = OatsRig();
        Accept(e3, Offer(e3, 0, "training-bow-at-battle", enemyBower3.Id, bower3.Id));
        Accept(e3, Enabled(e3, 1, "one-action-two-strikes", strategy3.Id).Command!);
        var next = Edit(e3, st => { st.BattleNumber++; st.AdditionalActions.Clear(); });
        Accept(next, Offer(next, 1, "training-grant-bow", granter3.Id, enemies3[1].Id));
        Eq(Grants(next, 1), 1, "in another battle new additional actions are created again");
    }

    // ---- String of Victories ---------------------------------------------------------------------------------------------------------------------------

    private static void StringOfVictoriesOpen()
    {
        // Player 1 plays it in the Action Phase on player 0's turn: an Open action.
        var s = Table(Phase.Action, 0); ClearPersonalities(s); s.PriorityPlayerId = 1;
        var bowedRing = Add(s, RingOfEarth, 1, Zone.Home, bowed: true);
        var discarded = Add(s, RingOfAir, 1, Zone.FateDiscard);
        var strategy = Add(s, StringOfVictories, 1, Zone.Hand);
        var actor = Add(s, "training-recon-actor", 1, Zone.Home); // training-plain-open: an Open action
        var limited = Add(s, Winds, 1, Zone.Attached, parent: actor.Id); GiveKeyword(actor, "Shugenja");
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 1, "string-of-victories", strategy.Id).Command!);
        Check(!e.State.Cards[bowedRing.Id].Bowed, "his bowed Ring straightened");
        Eq(e.State.PendingChoice?.Type, "effectChoice", "he may put a Ring from the discard pile into his hand");
        var choices = e.QueryActions(1).Where(a => a.Type == "chooseEffect").ToArray();
        Check(choices.Any(a => a.Command!.Choice == discarded.Id) && choices.Any(a => a.Command!.Choice == "none"), "the Ring, or none");
        Accept(e, choices.Single(a => a.Command!.Choice == discarded.Id).Command!);
        Check(e.State.Players[1].Hand.Contains(discarded.Id) && e.State.Cards[discarded.Id].Zone == Zone.Hand, "the Ring is in his hand");
        Eq(Grants(e, 1), 1, "an additional action appropriate to this phase");
        Eq(e.State.PriorityPlayerId, 1);
        Check(!CanUse(e, 1, "winds-of-dismissal-choice", limited.Id), "not his turn: a Limited action is not appropriate");
        Accept(e, Enabled(e, 1, "training-plain-open", actor.Id).Command!);
        Eq(Grants(e, 1), 0, "the Open action was the additional action");
        Eq(e.State.PriorityPlayerId, 0);
    }

    private static void StringOfVictoriesBattle()
    {
        var s = Table(Phase.Battle, 1); ClearPersonalities(s);
        var strategy = Add(s, StringOfVictories, 1, Zone.Hand);
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var momoko = Add(s, Momoko, 1, Zone.Battlefield, province: 0); // her Battle/Open action
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 1, "string-of-victories", strategy.Id).Command!);
        Check(e.State.PendingChoice is null, "no Ring in the discard pile: nothing is asked");
        Eq(Grants(e, 1), 1); Eq(e.State.PriorityPlayerId, 1);
        // A Battle/Open action in a battle is a Battle action: it spends the grant (and grants its own).
        Accept(e, Enabled(e, 1, "momoko-spell-waiver", momoko.Id).Command!);
        Eq(Grants(e, 1), 1, "Momoko's Battle/Open action spent String of Victories' grant and created her own");
        Eq(e.State.PriorityPlayerId, 1);
        Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, enemy.Id));
        Eq(Grants(e, 1), 0); Eq(e.State.PriorityPlayerId, 0, "the burst is over");
        Check(e.State.Cards[enemy.Id].Bowed);
    }

    // ---- Aramasu's Legacy ----------------------------------------------------------------------------------------------------------------------------------

    // Player 0's turn, Action Phase, Aramasu's Legacy his Stronghold. His home: a Mantis Clan Magistrate (Force 4) with a Follower (Force 3), and a plain Personality (Force 3); player 1's home: one
    // defender (Force 3 by default).
    private static (MatchEngine E, CardInstance Stronghold, CardInstance Magistrate, CardInstance Follower, CardInstance Plain, CardInstance Defender) AramasuRig(bool withMagistrate = true, string defenderDef = "training-crane-samurai")
    {
        var s = Table(Phase.Action, 0); ClearPersonalities(s);
        var stronghold = StrongholdOf(s, 0, Aramasu);
        var magistrate = Add(s, "training-lion-samurai", 0, Zone.Home); GiveKeyword(magistrate, "Mantis Clan"); GiveKeyword(magistrate, "Magistrate");
        var follower = Add(s, "training-bow-follower", 0, Zone.Attached, parent: magistrate.Id);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        if (!withMagistrate) { s.Cards.Remove(magistrate.Id); s.Cards.Remove(follower.Id); }
        var defender = Add(s, defenderDef, 1, Zone.Home);
        return (MatchEngine.FromTestState(s), stronghold, magistrate, follower, plain, defender);
    }

    private static void AramasuWin()
    {
        var (e, stronghold, magistrate, follower, plain, defender) = AramasuRig();
        var provinces = e.State.Players[1].Provinces.Count(p => !p.Destroyed);
        Accept(e, Enabled(e, 0, "aramasus-legacy-attack-phase", stronghold.Id).Command!);
        Eq(e.State.Phase, Phase.InfantryDeclaration, "an Attack Phase begins at once, with Infantry Maneuvers");
        Eq(e.State.ActivePlayerId, 0, "its controller is the Attacker");
        Eq(e.State.UnresolvedBattlefields.Count, provinces, "a battlefield at each of the Defender's provinces");
        Check(e.QueryActions(0).Any(a => a.Type == "attack" && a.Enabled), "he assigns attackers");
        // Maneuvers, battles and resolution as normal (every unit to province 1 = index 0, the Defender's too).
        RunUntil(e, () => e.State.Phase == Phase.Action);
        Check(Logged(e, "aramasuBattleWon"), "a battle was won (10 Force against 3)");
        Check(!e.State.Players[1].Provinces[0].Destroyed, "the province was not destroyed, although the attackers had the Force for it");
        Check(e.State.Cards[defender.Id].Zone is Zone.Home or Zone.Battlefield && e.State.Cards[defender.Id].Zone != Zone.Dead, "the defending army was not destroyed");
        Eq(e.State.Cards[magistrate.Id].Zone, Zone.Home, "the attackers went home");
        Check(!e.State.Cards[magistrate.Id].Bowed, "resolution did not bow the Mantis Clan Magistrate");
        Check(!e.State.Cards[follower.Id].Bowed, "nor his attachment");
        Check(e.State.Cards[plain.Id].Bowed, "another Personality of his is bowed by resolution as usual");
        // The Action Phase resumes and he may target a Personality or Holding.
        Eq(e.State.Phase, Phase.Action); Eq(e.State.ActivePlayerId, 0);
        Eq(e.State.PendingChoice?.Type, "effectChoice", "the choice after the phase");
        var options = e.QueryActions(0).Where(a => a.Type == "chooseEffect").ToArray();
        Check(options.Any(a => a.Command!.Choice == "none") && options.Any(a => a.Command!.Choice == defender.Id)
            && options.Any(a => e.State.Cards.TryGetValue(a.Command!.Choice!, out var c) && CardCatalog.Get(c.DefinitionId).Type == CardType.Holding), "a Personality, a Holding, or nothing");
        Accept(e, options.Single(a => a.Command!.Choice == defender.Id).Command!);
        var target = e.State.Cards[defender.Id];
        Check(target.Bowed, "bowed");
        var hold = target.StraightenHolds.Single();
        Check(hold.Owner == 0 && hold.Until == StraightenPoint.StraightenPhaseEnds && hold.Skip == 0, "its straightening negated until after his next Straighten Phase ends");
        Eq(e.State.PriorityPlayerId, 1, "the Limited action is done: the other player's opportunity");
        Eq(e.State.Phase, Phase.Action);
        Check(!CanUse(Edit(e, st => st.PriorityPlayerId = 0), 0, "aramasus-legacy-attack-phase", stronghold.Id), "once a turn");
        // The Action Phase goes on: two passes end it and the ordinary Attack Phase follows.
        Accept(e, Cmd(e, "pass", 1)); Accept(e, Cmd(e, "pass", 0));
        Eq(e.State.Phase, Phase.AttackDeclaration, "the normal Attack Phase is still to come");
    }

    private static void AramasuEdges()
    {
        // Nothing won: the defender wins at the battlefield (Force 3 against 4), the attacking army is destroyed as usual, nothing is offered after.
        var (e, stronghold, _, _, plain, defender) = AramasuRig(withMagistrate: false, defenderDef: "training-lion-samurai");
        Accept(e, Enabled(e, 0, "aramasus-legacy-attack-phase", stronghold.Id).Command!);
        RunUntil(e, () => e.State.Phase == Phase.Action);
        Check(!Logged(e, "aramasuBattleWon"), "no battle won");
        Eq(e.State.Cards[plain.Id].Zone, Zone.Dead, "the losing attacker was destroyed by the defender's win as usual");
        Check(e.State.PendingChoice is null, "nothing to target");
        Eq(e.State.PriorityPlayerId, 1, "the Action Phase resumes with the other player's opportunity");
        // A tie: the attacking army is destroyed, the defending one is not.
        var (t, tStronghold, _, _, tPlain, tDefender) = AramasuRig(withMagistrate: false);
        Accept(t, Enabled(t, 0, "aramasus-legacy-attack-phase", tStronghold.Id).Command!);
        RunUntil(t, () => t.State.Phase == Phase.Action);
        Eq(t.State.Cards[tPlain.Id].Zone, Zone.Dead, "tie: the attacker is destroyed");
        Check(t.State.Cards[tDefender.Id].Zone != Zone.Dead, "tie: the defender is not");
        // A Limited action: not on the other player's turn.
        var (o, oStronghold, _, _, _, _) = AramasuRig();
        var theirs = Edit(o, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 0; });
        Check(!CanUse(theirs, 0, "aramasus-legacy-attack-phase", oStronghold.Id), "not on the other player's turn");
    }
}
