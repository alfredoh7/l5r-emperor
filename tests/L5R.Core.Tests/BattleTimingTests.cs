using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// Battle Phase timing (issues #309/#310): the Engage window, the first-action claim (with Daidoji Yuki and Naval
// Invasion), Rebuilding Kyuden Hida's interaction, the generalized "before this battle resolves" window, and the
// public BattleStepView. Every test builds an explicit position (no shuffled deck) and asserts both the accepted
// path and the rejected ones; docs/decisions/2026-09-18-battle-timing-primitives.md lists each ruling covered here.
internal static class BattleTimingTests
{
    private const string Yuki = "oracle-l5r-1696", Isas = "oracle-l5r-8840", Kojiro = "oracle-l5r-3850", Doho = "oracle-l5r-9539";
    private const string Claim = "training-engage-claim-strategy", Honor = "training-engage-honor-strategy";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Daidoji Yuki, Experienced: published shape, Recon tag and the wired Naval carrier set", PublishedShape),
            ("Battle start: nothing playable opens no window and the Defender still acts first (event stream unchanged)", DefaultFlowUnchanged),
            ("Engage window: the turn player is first, reactions resolve immediately, alternation, two passes close, no Battle action inside", EngageWindowMechanics),
            ("Engage window: QueryActions lists the legal reactions, 'wait' for the other player, and every offered command validates", EngageQueryActions),
            ("First-action claim: an attacker-side claim (Sneak Attack shape) flips the first opportunity to the Attacker", AttackerClaimFlips),
            ("First-action claim: two and three attacker-side claims (incl. an attacker-controlled Yuki) are still exactly one opportunity", ClaimsNeverStack),
            ("First-action claim: a defending Yuki overrides the attacker's claims in either order, and the Defender acts first", DefenderClaimWinsAnyOrder),
            ("Daidoji Yuki: Reconnaissance for his controller as Attacker OR Defender (own province), never the opponent, resets after the battle", YukiReconnaissance),
            ("Daidoji Yuki: the all-Scouts condition decides the grant on either side, and only his controller's Personalities count", YukiReconnaissanceScoutCondition),
            ("Reconnaissance readers ask 'does this player have it at the current battlefield': a defender's Yuki-granted Recon serves Sniper, Keppo, Kinto and a fixture; the opponent never gains it", ReconnaissanceReadersServeDefenders),
            ("Daidoji Yuki: every Personality of his controller there must be a Scout, he must be assigned to the engaged battlefield, unbowed, once", YukiConditions),
            ("Engage window: opens once per battle, only when something is playable at the SELECTED battlefield, and never again in that battle", WindowOncePerBattle),
            ("Engage window state is plain data: a snapshot taken mid-window continues identically on two engines", EngageDeterministic),
            ("Naval Invasion: strictly more Naval cards claims the first opportunity, bound to its performer, negated by a defender claim", NavalInvasion),
            ("Naval Invasion: ties and fewer never claim, only the Attacker may use it, once per battle, fresh each battle", NavalIllegal),
            ("Naval Invasion: counts printed Naval, granted Naval, Kojiro's live conditional Naval and Naval Followers", NavalCounting),
            ("Rebuilding Kyuden Hida: an Attacker who holds the first opportunity may only pass; the default flow is untouched", KyudenHida),
            ("Before this battle resolves: a mandatory effect applies once both players have passed and before Force is totalled", BeforeResolutionMandatory),
            ("Before this battle resolves: two simultaneous sources are ordered by the turn player and the order changes who fights", BeforeResolutionOrdering),
            ("Before this battle resolves: an optional ('you may') effect asks its own controller, who may decline or name the target", BeforeResolutionOptional),
            ("Before this battle resolves: only cards at the fought battlefield participate; Encircled Terrain is registered through the same path", BeforeResolutionScope),
            ("BattleStepView: every segment and step, the decision player, both viewers identical", StepViewSegments),
            ("BattleStepView: first-action holder and reason, claims, consecutive passes, before-resolution sources; JSON round trip", StepViewDetails),
        ];
        runner.Run("battle-timing", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);

    // The Battle Selection moment: player 0 attacks; both Strongholds are a published one, which is what enables player
    // abilities (Naval Invasion). Battlefields are the defender's provinces 0.. as given.
    private static MatchState Table(params int[] battlefields)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218";
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null;
        s.UnresolvedBattlefields = (battlefields.Length == 0 ? [0] : battlefields).ToList();
        return s;
    }
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"bt-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }
    // A plain unit at a battlefield for each side.
    private static (CardInstance A, CardInstance D) Units(MatchState s, int province = 0, string attacker = "training-crane-samurai", string defender = "training-lion-samurai")
        => (Add(s, attacker, 0, Zone.Battlefield, province: province), Add(s, defender, 1, Zone.Battlefield, province: province));
    private static GameCommand Select(MatchEngine e, int province = 0) => new("selectBattle", 0, e.Version, Province: province);
    private static GameCommand PassC(MatchEngine e, int? player = null) => new("pass", player ?? e.State.PriorityPlayerId, e.Version);
    private static GameCommand Decline(MatchEngine e, int? player = null) => new("declineReaction", player ?? e.State.PriorityPlayerId, e.Version);
    private static GameCommand Use(MatchEngine e, int player, string ability, string? card = null, string? performer = null, string? target = null)
        => new("useAbility", player, e.Version, CardId: card, AbilityId: ability, PerformerId: performer, TargetId: target);
    private static BattleStepView Step(MatchEngine e, int viewer = 0) => e.GetView(viewer).BattleStep!;
    // Both players pass in succession, closing the Engage window whoever holds priority.
    private static void CloseWindow(MatchEngine e) { while (e.State.PendingChoice?.Type == "engageReaction") Accept(e, Decline(e)); }
    // The battle's Combat Segment ends: both players pass in succession (Pass), resolving it.
    private static void ResolveByPassing(MatchEngine e) { Accept(e, PassC(e)); Accept(e, PassC(e)); }
    private static string ReasonOf(MatchEngine e) => Step(e).FirstActionReason!;

    // ---- published shape ---------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        var yuki = CardCatalog.Get(Yuki);
        Eq(PublishedContent.PersonalityWave25.Cards.Length, 1, "personality-wave-25 is exactly Yuki");
        Check(!yuki.ScenarioOnly && yuki.Type == CardType.Personality, "Published Personality");
        Eq(yuki.Name, "Daidoji Yuki"); Eq(yuki.Clan, "Crane");
        Eq(yuki.GoldCost, 8); Eq(yuki.Force, 4); Eq(yuki.Chi, 3); Eq(yuki.PersonalHonor, 2); Eq(yuki.HonorRequirement, (int?)0);
        foreach (var k in new[] { "Crane Clan", "Samurai", "Iron Crane", "Scout", "Experienced", "Unique" }) Check(yuki.Keywords!.Contains(k), $"printed keyword {k}");
        Eq(yuki.Text, "Recon Reaction: After engaging at Yuki's battlefield, if all your Personalities there are Scouts: You have Reconnaissance there. You have the first opportunity in this battle to take a Battle action or pass.");
        Eq(string.Join(",", yuki.Abilities!), "yuki-engage-recon");
        var ability = AbilityCatalog.All["yuki-engage-recon"];
        Eq(ability.Timing, "Reaction"); Eq(ability.Trigger, "after-engaging-at-own-battlefield");
        Check(ability.ActionKeywords is ["Recon"], "Yuki's ability carries the printed Recon action keyword");
        // Nothing else already wired has a printed Recon prefix (the whole published text was searched), so Yuki's (and, since issue #364, Kyuden Hida - exp's) are the only Recon-tagged
        // ability of any PUBLISHED card (other action keywords, e.g. Political since issue #338, are tagged too and are not what is counted here) (later synthetic training fixtures may carry tags of their own).
        Eq(string.Join(",", AbilityCatalog.All.Where(a => a.Value.ActionKeywords?.Contains("Recon") == true && CardCatalog.Cards.Values.Any(c => !c.ScenarioOnly && c.Abilities!.Contains(a.Key))).Select(a => a.Key).OrderBy(x => x, StringComparer.Ordinal)), "akeha-recon-search,golden-plains-recon,hidden-falls-recon,jades-roar-recon,koshin-keep-recon,kyuden-hida-exp-scouts,qalyar-recon-minus,qalyar-recon-plus,wasteland-scout-recon,wl-forest-cover-attach,wl-forest-cover-recon,yuki-engage-recon"); // + jades-roar-recon (issue #461 Gempukku pass: Jade's Roar - exp, a Recon Reaction); + wasteland-scout-recon (issue #476 batch 6: Wasteland Scout); + hidden-falls-recon and koshin-keep-recon (issue #486 Stronghold pass, batch 1) + akeha-recon-search (issue #503 group P3: Daidoji Akeha, a Recon Limited)
        foreach (var card in CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly))
            Check(!System.Text.RegularExpressions.Regex.IsMatch(card.Text, @"\bRecon (Battle|Open|Limited|Reaction)") || card.Id == Yuki || card.Id == "oracle-l5r-4638" && AbilityCatalog.All["kyuden-hida-exp-scouts"].ActionKeywords!.Contains("Recon") || card.Id == "oracle-l5r-10223" && AbilityCatalog.All["jades-roar-recon"].ActionKeywords!.Contains("Recon")
                || card.Id == "oracle-l5r-6117" && AbilityCatalog.All["qalyar-recon-plus"].ActionKeywords!.Contains("Recon") && AbilityCatalog.All["qalyar-recon-minus"].ActionKeywords!.Contains("Recon")
                || card.Id == "oracle-l5r-9874" && AbilityCatalog.All["wasteland-scout-recon"].ActionKeywords!.Contains("Recon")
                || card.Id == "oracle-l5r-8105" && AbilityCatalog.All["golden-plains-recon"].ActionKeywords!.Contains("Recon") || card.Id == "oracle-l5r-3257" && AbilityCatalog.All["hidden-falls-recon"].ActionKeywords!.Contains("Recon") || card.Id == "oracle-l5r-4541" && AbilityCatalog.All["koshin-keep-recon"].ActionKeywords!.Contains("Recon")
                || card.Id == "oracle-l5r-1621" && AbilityCatalog.All["akeha-recon-search"].ActionKeywords!.Contains("Recon"), // Daidoji Akeha (issue #503 group P3) // Hidden Falls Dojo, Koshin Keep (issue #486 Stronghold pass, batch 1)
                $"{card.Name} prints a Recon action but is not tagged"); // Qalyar (Emperor Edition Personality pass, issue #477 batch 6)
        // Naval Invasion changes existing published play for exactly these cards (printed Naval, a granter, or a live conditional grant);
        // none is in the pinned Lion/Crane decks.
        var navalCarriers = CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && (c.Keywords!.Contains("Naval") || c.AttachGrantsKeyword == "Naval" || c.Effect == "naval-while-has-spell"))
            .Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        navalCarriers = navalCarriers.Where(id => !WlPersTornTests.NavalCarriers.Contains(id)).ToArray(); // issue #674 (personality-wave-131): Moshi Ira and Yoritomo Tansen, printed Naval, are asserted in WlPersTornTests
        // + oracle-l5r-1726 (issue #336: Daigotsu Hotako, Experienced 3, printed Naval).
        // + oracle-l5r-10067 (issue #503, group P3: Thunder Dragon - exp2, printed Naval).
        // + oracle-l5r-9544 (issue #264 Second City pass: Yoritomo Emoto, printed Naval).
        // + oracle-l5r-10049, -10066, -10068, -10070, -10071 (issue #453 Seeds of Decay batch 3: Doji Mitsuru, Moshi Ino, Tsuruchi Kohrogi, Yoritomo Akukiho, Yoritomo Iwashi, each printed Naval).
        // + oracle-l5r-10069 (issue #453 Seeds of Decay pass, batch 4: Tsuruchi Nobukatsu - exp, printed Naval).
        // + oracle-l5r-5286, -8894, -9579 (issue #460 Embers of War pass, batch 2: Moshi Tomiko, Tsuruchi Tomaru - exp, Yoritomo Kanaye, each
        //   printed Naval -- Tsuruchi Shichiro also prints Naval but is not published at all, unwired and per this pass's own convention never
        //   entering the catalogue, so he is correctly absent here).
        // + oracle-l5r-5279, -9556 (issue #467 Forgotten Legacy pass, batch 2: Moshi Sasako, Yoritomo Haruhiko, each printed Naval).
        // + oracle-l5r-9830, -9833 (issue #476 The Shadow's Embrace pass, batch 2: Moshi Rukia, Yoritomo Kanahashi, each printed Naval).
        // + oracle-l5r-9552, -9558, -9611 (issue #477 Emperor Edition Personality pass, batch 3: Yoritomo Hama, Yoritomo Hiromi, Yoritomo Naoto, each printed Naval).
        // + oracle-l5r-9630 (issue #501, mechanics group E: Yoritomo Singh - exp2, printed Naval).
        // + oracle-l5r-8878 (issue #503, group P1: Tsuruchi Shichiro, printed Naval -- now wired and published).
        // + oracle-l5r-10642, -10644, -10646 (issue #674, personality-wave-134: Moshi Rukia - exp, Tsuruchi Gombei, Yoritomo Hiromi - exp, each printed Naval).
        string[] expected = ["oracle-l5r-10465", "oracle-l5r-10466", "oracle-l5r-10471", "oracle-l5r-10642", "oracle-l5r-10644", "oracle-l5r-10646", "oracle-l5r-8878", "oracle-l5r-10067", "oracle-l5r-9630", "oracle-l5r-9552", "oracle-l5r-9558", "oracle-l5r-9611", "oracle-l5r-5279", "oracle-l5r-9556", "oracle-l5r-10049", "oracle-l5r-10066", "oracle-l5r-10068", "oracle-l5r-10069", "oracle-l5r-10070", "oracle-l5r-10071", "oracle-l5r-10133", "oracle-l5r-10299", "oracle-l5r-10853", "oracle-l5r-10857", "oracle-l5r-1726", "oracle-l5r-3850", "oracle-l5r-5285", "oracle-l5r-5286", "oracle-l5r-5287", "oracle-l5r-6501", "oracle-l5r-8110", "oracle-l5r-8840", "oracle-l5r-8879", "oracle-l5r-8894", "oracle-l5r-8895", "oracle-l5r-9539", "oracle-l5r-9544", "oracle-l5r-9579", "oracle-l5r-9605", "oracle-l5r-9610", "oracle-l5r-9642", "oracle-l5r-9659", "oracle-l5r-9832", "oracle-l5r-9834", "oracle-l5r-9830", "oracle-l5r-9833", "oracle-l5r-5290" /* Moshi Yokohime, issue #622 */];
        // + oracle-l5r-10643 (issue #672, primitive P16: Moshi Sasako - exp, printed Naval). Added on its own line so parallel waves do not collide on the list above.
        expected = [.. expected, "oracle-l5r-10643", "oracle-l5r-10859"]; // 10859: Yoritomo Toganin (P9), printed Naval
        expected = [.. expected, "oracle-l5r-10300"]; // issue #736 (personality-wave-159): Yoritomo Naoto - exp, printed Naval
        expected = [.. expected, "oracle-l5r-10467", "oracle-l5r-10470"]; // issue #689 (personality-wave-9501): Okazaki, Breaker of Wills and Yoritomo Kanaye - exp, each printed Naval
        Eq(string.Join(",", navalCarriers), string.Join(",", expected.Append("oracle-l5r-4464").OrderBy(x => x, StringComparer.Ordinal)), "Every wired Naval carrier");
        var pinned = PublishedContent.Snapshot.Cards.Concat(PublishedContent.TestSnapshot.Cards).Select(c => c.Id).ToHashSet();
        Check(!navalCarriers.Any(pinned.Contains), "No Naval card is in the pinned Lion/Crane decks");
    }

    // ---- default flow and the window's mechanics -------------------------------------------------------------
    private static void DefaultFlowUnchanged()
    {
        var s = Table(); Units(s);
        var e = MatchEngine.FromTestState(s);
        var choose = Step(e);
        Eq(choose.Segment, BattleSteps.FightBattles); Eq(choose.Step, BattleSteps.ChooseBattle); Eq(choose.DecisionPlayerId, (int?)0); Eq(choose.FirstActionPlayerId, (int?)null);
        Accept(e, Select(e));
        Check(e.State.PendingChoice is null && e.State.PendingEngage is null, "Nothing anyone could play: no window opens");
        Eq(e.State.Phase, Phase.Battle); Eq(e.State.PriorityPlayerId, 1, "Spec 4.4: the Defender acts first by default");
        var view = Step(e);
        Eq(view.Step, BattleSteps.BattleActions); Eq(view.FirstActionPlayerId, (int?)1); Eq(view.FirstActionReason, BattleSteps.ReasonDefault); Check(view.FirstActionResolved);
        Check(e.GetView(0).Events.Any(ev => ev.Type == "battleStarted" && ev.Message == "Battle at province 1: defender acts first."), "The pre-#310 event is emitted unchanged");
        Check(!e.GetView(0).Events.Any(ev => ev.Type is "reactionWindow" or "firstOpportunity"), "No window, no first-opportunity event");
        // Spec 4.5: alternating actions, two passes in succession end the segment; after the battle the state is clean again.
        Accept(e, PassC(e)); Eq(Step(e).ConsecutivePasses, 1); Eq(e.State.PriorityPlayerId, 0);
        Accept(e, PassC(e)); Eq(e.State.Phase, Phase.Dynasty, "Last battlefield resolved");
        Check(e.State.FirstActionPlayerId is null && e.State.FirstActionReason is null && e.State.FirstActionRestriction is null, "First-action state is battle-scoped");
    }

    private static void EngageWindowMechanics()
    {
        var s = Table(); var (a, d) = Units(s);
        var mine = Add(s, Honor, 0, Zone.Hand); var theirs = Add(s, Honor, 1, Zone.Hand);
        var harmless = Add(s, "training-harmless-battle-action", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var honor0 = e.State.Players[0].Honor; var honor1 = e.State.Players[1].Honor;
        Accept(e, Select(e));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "Something playable: the window opens as the Attacker selects the battle");
        Eq(e.State.PriorityPlayerId, 0, "The turn player has the first opportunity");
        Eq(Step(e).Segment, BattleSteps.FightBattles); Eq(Step(e).Step, BattleSteps.Engage); Eq(Step(e).DecisionPlayerId, (int?)0);
        Eq(Step(e).Window, "engageReaction");
        // Only Engage actions and "After engaging" Reactions: a Battle action, a foreign command and the wrong player are all refused, atomically.
        Reject(e, Use(e, 0, "kouki-force", harmless.Id, target: d.Id)); // a Battle action
        Reject(e, Select(e));
        Reject(e, Use(e, 1, "training-engage-gain-honor", theirs.Id)); // not your opportunity
        Reject(e, new GameCommand("playStrategy", 0, e.Version, CardId: mine.Id));
        // A reaction resolves the moment it is played (no queue), and hands the opportunity to the other player.
        Accept(e, Use(e, 0, "training-engage-gain-honor", mine.Id));
        Eq(e.State.Players[0].Honor, honor0 + 1, "Resolved immediately");
        Eq(e.State.Cards[mine.Id].Zone, Zone.FateDiscard); Eq(e.State.PriorityPlayerId, 1); Eq(e.State.PendingChoice?.Type, "engageReaction");
        Accept(e, Use(e, 1, "training-engage-gain-honor", theirs.Id));
        Eq(e.State.Players[1].Honor, honor1 + 1, "The Defender may react too, after the Attacker");
        Eq(e.State.PriorityPlayerId, 0);
        // Passing does not close it (one pass), playing resets the count, and it closes only on two passes in succession.
        Accept(e, Decline(e)); Eq(e.State.PendingChoice?.Type, "engageReaction"); Eq(Step(e).ConsecutivePasses, 1); Eq(e.State.PriorityPlayerId, 1);
        Accept(e, Decline(e));
        Check(e.State.PendingChoice is null && e.State.PendingEngage is null, "Two consecutive passes closed the window");
        Eq(e.State.PriorityPlayerId, 1, "No claim: the Defender acts first");
        Eq(ReasonOf(e), BattleSteps.ReasonDefault);
        // The window is over: an Engage reaction can no longer be played, and Battle actions now can.
        Check(e.State.Cards.Values.All(c => c.DefinitionId != Honor || c.Zone == Zone.FateDiscard));
        Reject(e, Use(e, 1, "training-engage-gain-honor", theirs.Id)); // spent, and the window is over
        Accept(e, PassC(e)); // the Defender's first opportunity: pass
        Accept(e, Use(e, 0, "kouki-force", harmless.Id, target: d.Id)); // a Battle action is legal now
    }

    private static void EngageQueryActions()
    {
        var s = Table(); Units(s);
        var mine = Add(s, Honor, 0, Zone.Hand); Add(s, Honor, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Select(e));
        var offered = e.QueryActions(0);
        Check(offered.Any(o => o.Type == "useAbility" && o.Enabled && o.Command!.AbilityId == "training-engage-gain-honor" && o.Command.CardId == mine.Id), "The Attacker is offered the reaction in hand");
        Check(offered.Any(o => o.Type == "declineReaction" && o.Enabled), "and the pass");
        Check(!offered.Any(o => o.Type is "pass" or "playStrategy" or "recruitReserve" or "selectBattle"), "no Battle-action shaped options inside the window");
        foreach (var option in offered.Where(o => o.Enabled && o.Command is not null)) Accept(MatchEngine.FromTestState(e.State), option.Command!);
        var waiting = e.QueryActions(1);
        Check(waiting.Any(o => o.Type == "wait") && !waiting.Any(o => o.Type == "useAbility"), "The other player waits for their opportunity");
        Accept(e, Decline(e));
        var defender = e.QueryActions(1);
        Check(defender.Any(o => o.Type == "useAbility" && o.Enabled && o.Command!.AbilityId == "training-engage-gain-honor"), "then the Defender's own");
        foreach (var option in defender.Where(o => o.Enabled && o.Command is not null)) Accept(MatchEngine.FromTestState(e.State), option.Command!);
    }

    // ---- claims ------------------------------------------------------------------------------------------------
    private static void AttackerClaimFlips()
    {
        var s = Table(); Units(s);
        var claim = Add(s, Claim, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Select(e));
        Eq(e.State.PendingChoice?.Type, "engageReaction");
        Accept(e, Use(e, 0, "training-engage-attacker-first", claim.Id));
        Eq(e.State.Cards[claim.Id].Zone, Zone.FateDiscard);
        var during = Step(e);
        Eq(during.FirstActionPlayerId, (int?)0, "Provisional holder while the window is open"); Check(!during.FirstActionResolved); Eq(during.EngageClaims.Length, 1);
        Eq(during.EngageClaims[0].PlayerId, 0);
        CloseWindow(e);
        Eq(e.State.PriorityPlayerId, 0, "The Attacker has the first opportunity");
        var after = Step(e);
        Eq(after.FirstActionPlayerId, (int?)0); Eq(after.FirstActionReason, BattleSteps.ReasonEngageGrant); Check(after.FirstActionResolved); Eq(after.Step, BattleSteps.BattleActions);
        Check(after.EngageClaims.Length == 0, "Claims are only listed while the window is open");
        // The opportunity is "to take a Battle action or pass": passing hands the turn to the Defender, who may pass to end it.
        Accept(e, PassC(e)); Eq(e.State.PriorityPlayerId, 1);
        Accept(e, PassC(e)); Eq(e.State.Phase, Phase.Dynasty);
        // The synthetic beneficiary is the Attacker whoever plays it: a Defender who plays it grants THEM the opportunity, and the
        // Defender has it by default anyway.
        var s2 = Table(); Units(s2);
        var theirs = Add(s2, Claim, 1, Zone.Hand); Add(s2, Honor, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Select(e2)); Accept(e2, Decline(e2));
        Accept(e2, Use(e2, 1, "training-engage-attacker-first", theirs.Id));
        CloseWindow(e2);
        Eq(e2.State.PriorityPlayerId, 0, "The card names the Attacker as the beneficiary, whoever played it");
    }

    private static void ClaimsNeverStack()
    {
        var s = Table();
        Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var yuki = Add(s, Yuki, 0, Zone.Battlefield, province: 0);
        var c1 = Add(s, Claim, 0, Zone.Hand); var c2 = Add(s, Claim, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Select(e));
        Accept(e, Use(e, 0, "training-engage-attacker-first", c1.Id)); Accept(e, Decline(e));
        Accept(e, Use(e, 0, "training-engage-attacker-first", c2.Id)); Accept(e, Decline(e));
        Accept(e, Use(e, 0, "yuki-engage-recon", yuki.Id)); // an attacker-controlled Yuki: the third claim
        Eq(e.State.PendingEngage!.Claims.Count, 3, "All three claims were recorded");
        Accept(e, Decline(e)); Accept(e, Decline(e));
        Eq(e.State.PriorityPlayerId, 0); Eq(ReasonOf(e), BattleSteps.ReasonEngageGrant);
        // Exactly ONE first opportunity: the Attacker passes it, then the Defender (not the Attacker again) has priority, and their pass ends the segment.
        Accept(e, PassC(e)); Eq(e.State.PriorityPlayerId, 1, "No second first opportunity for the Attacker");
        Eq(e.State.Phase, Phase.Battle);
        Accept(e, PassC(e)); Eq(e.State.Phase, Phase.Dynasty);
        Check(e.GetView(0).Events.Count(ev => ev.Type == "firstOpportunityClaimed") == 3);
        Check(e.GetView(0).Events.Count(ev => ev.Type == "firstOpportunity") == 1, "Decided once");
    }

    private static void DefenderClaimWinsAnyOrder()
    {
        // Order 1: the Attacker claims (twice), then the Defender's Yuki.
        {
            var s = Table(); Units(s, defender: "training-scout-samurai");
            var yuki = Add(s, Yuki, 1, Zone.Battlefield, province: 0);
            var c1 = Add(s, Claim, 0, Zone.Hand); var c2 = Add(s, Claim, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e));
            Accept(e, Use(e, 0, "training-engage-attacker-first", c1.Id));
            Accept(e, Use(e, 1, "yuki-engage-recon", yuki.Id));
            Accept(e, Use(e, 0, "training-engage-attacker-first", c2.Id)); // a claim AFTER the override changes nothing
            Accept(e, Decline(e)); Accept(e, Decline(e));
            Eq(e.State.PriorityPlayerId, 1, "The Defender acts first"); Eq(ReasonOf(e), BattleSteps.ReasonDefenderOverride);
            Eq(Step(e).FirstActionPlayerId, (int?)1);
            Check(e.State.FirstActionRestriction is null);
        }
        // Order 2: the Defender's Yuki first (the Attacker passes to let them), then the Attacker's claims.
        {
            var s = Table(); Units(s, defender: "training-scout-samurai");
            var yuki = Add(s, Yuki, 1, Zone.Battlefield, province: 0);
            var c1 = Add(s, Claim, 0, Zone.Hand); var c2 = Add(s, Claim, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e));
            Accept(e, Decline(e));
            Accept(e, Use(e, 1, "yuki-engage-recon", yuki.Id));
            Accept(e, Use(e, 0, "training-engage-attacker-first", c1.Id)); Accept(e, Decline(e));
            Accept(e, Use(e, 0, "training-engage-attacker-first", c2.Id));
            CloseWindow(e);
            Eq(e.State.PriorityPlayerId, 1, "Same result whatever order the claims were made in"); Eq(ReasonOf(e), BattleSteps.ReasonDefenderOverride);
        }
        // A defender claim with nothing to override is just the default made explicit.
        {
            var s = Table(); Units(s, defender: "training-scout-samurai");
            var yuki = Add(s, Yuki, 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e)); Accept(e, Decline(e)); Accept(e, Use(e, 1, "yuki-engage-recon", yuki.Id));
            CloseWindow(e);
            Eq(e.State.PriorityPlayerId, 1); Eq(ReasonOf(e), BattleSteps.ReasonDefault);
        }
    }

    // ---- Daidoji Yuki ------------------------------------------------------------------------------------------
    private static void YukiReconnaissance()
    {
        // An Attacker's Yuki gains Reconnaissance at the battlefield (the Defender's province) and the first opportunity.
        var s = Table(); Units(s, attacker: "training-scout-samurai");
        var yuki = Add(s, Yuki, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Select(e));
        Accept(e, Use(e, 0, "yuki-engage-recon", yuki.Id));
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "The Attacker holds Reconnaissance at the battlefield");
        Check(!e.State.Players[1].Provinces[0].Reconnaissance[1] && !e.State.Players[0].Provinces[0].Reconnaissance[0]);
        Check(e.GetView(0).Events.Any(ev => ev.Type == "reconnaissanceGained"));
        CloseWindow(e);
        Eq(e.State.PriorityPlayerId, 0); Eq(ReasonOf(e), BattleSteps.ReasonEngageGrant);
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "Still held during the battle");
        ResolveByPassing(e);
        Check(e.State.Players[1].Provinces[0].Reconnaissance.All(r => !r), "Reconnaissance resets for both players once the battle resolves");
        Check(!e.State.Players[0].Provinces[0].Reconnaissance.Any(r => r), "Nothing is set at the Attacker's own province row");
        Check(!e.GetView(0).Events.Any(ev => ev.Type == "reconnaissanceNotGained"));
        // A Defender's Yuki (rules lead's ruling): the Scout condition alone decides the grant, so his controller has Reconnaissance
        // "there" -- the battlefield is the Defender's OWN province -- exactly as an Attacker's controller does at the enemy's.
        var s2 = Table(); Units(s2, defender: "training-scout-samurai");
        var yuki2 = Add(s2, Yuki, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Select(e2)); Accept(e2, Decline(e2));
        Accept(e2, Use(e2, 1, "yuki-engage-recon", yuki2.Id));
        Check(e2.State.Players[1].Provinces[0].Reconnaissance[1], "The Defender holds Reconnaissance at their own battlefield province");
        Check(!e2.State.Players[1].Provinces[0].Reconnaissance[0], "The opponent (the Attacker) does not gain it");
        Check(!e2.State.Players[0].Provinces[0].Reconnaissance.Any(r => r), "Nothing is set at the Attacker's own province of the same index");
        Check(e2.GetView(0).Events.Any(ev => ev.Type == "reconnaissanceGained" && ev.Message.Contains(e2.State.Players[1].Name)), "Announced as the Defender's gain");
        Check(!e2.GetView(0).Events.Any(ev => ev.Type == "reconnaissanceNotGained"), "Never reported as a no-op any more");
        Eq(e2.State.PendingEngage!.Claims.Count, 1, "The first-opportunity claim is recorded exactly as before");
        CloseWindow(e2);
        Eq(e2.State.PriorityPlayerId, 1); Eq(ReasonOf(e2), BattleSteps.ReasonDefault, "A defender claim with nothing to override is still just the default");
        Check(e2.State.Players[1].Provinces[0].Reconnaissance[1], "Still held during the battle");
        ResolveByPassing(e2);
        Check(e2.State.Players.SelectMany(p => p.Provinces).All(p => p.Reconnaissance.All(r => !r)), "Reset for both players when the battle resolves, as for an Attacker's");
        // Each side's own Yuki grants its own controller (never the other's), and the Defender's claim still overrides.
        var s3 = Table(); Units(s3, attacker: "training-scout-samurai", defender: "training-scout-samurai");
        var yA = Add(s3, Yuki, 0, Zone.Battlefield, province: 0); var yD = Add(s3, Yuki, 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Select(e3));
        Accept(e3, Use(e3, 0, "yuki-engage-recon", yA.Id)); Accept(e3, Use(e3, 1, "yuki-engage-recon", yD.Id));
        Check(e3.State.Players[1].Provinces[0].Reconnaissance[0] && e3.State.Players[1].Provinces[0].Reconnaissance[1], "Each controller holds their own");
        CloseWindow(e3);
        Eq(e3.State.PriorityPlayerId, 1); Eq(ReasonOf(e3), BattleSteps.ReasonDefenderOverride);
    }

    // The all-Scouts condition (checked in ValidateAbility) is the ONLY thing deciding the grant, on either side. It reads
    // "all your Personalities there", so only his controller's Personalities count, never the opponent's.
    private static void YukiReconnaissanceScoutCondition()
    {
        foreach (var side in new[] { 0, 1 })
        {
            // A non-Scout beside a Yuki of THIS side, with the other side entirely Scouts: refused, nobody gains anything, no claim.
            var s = Table();
            Units(s, attacker: side == 0 ? "training-crane-samurai" : "training-scout-samurai", defender: side == 1 ? "training-lion-samurai" : "training-scout-samurai");
            var yuki = Add(s, Yuki, side, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e));
            Check(e.State.PendingChoice is null, $"side {side}: Yuki cannot be used and nothing else is playable: no window");
            // Same position with a playable Strategy so the window opens and the refusal itself is exercised.
            var s2 = Table();
            Units(s2, attacker: side == 0 ? "training-crane-samurai" : "training-scout-samurai", defender: side == 1 ? "training-lion-samurai" : "training-scout-samurai");
            var yuki2 = Add(s2, Yuki, side, Zone.Battlefield, province: 0); Add(s2, Honor, side, Zone.Hand);
            var e2 = MatchEngine.FromTestState(s2);
            Accept(e2, Select(e2));
            Eq(e2.State.PendingChoice?.Type, "engageReaction", $"side {side}");
            if (e2.State.PriorityPlayerId != side) Accept(e2, Decline(e2));
            var refused = e2.Execute(Use(e2, side, "yuki-engage-recon", yuki2.Id));
            Check(!refused.Accepted && refused.Error!.Contains("Scouts"), $"side {side}: refused for the Scout condition ({refused.Error})");
            Check(e2.State.Players.SelectMany(p => p.Provinces).All(p => p.Reconnaissance.All(r => !r)), $"side {side}: nobody gained Reconnaissance");
            Check(e2.State.PendingEngage!.Claims.Count == 0, $"side {side}: and no claim was made");
        }
        // Only his controller's Personalities count: the OTHER side's non-Scout does not stop either Yuki (Attacker's, Defender's).
        {
            var s = Table(); Units(s, attacker: "training-crane-samurai", defender: "training-scout-samurai"); var yuki = Add(s, Yuki, 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); Accept(e, Select(e)); Accept(e, Decline(e)); Accept(e, Use(e, 1, "yuki-engage-recon", yuki.Id));
            Check(e.State.Players[1].Provinces[0].Reconnaissance[1], "Defender's Yuki with an all-Scout army gains it against a non-Scout Attacker");
            var s2 = Table(); Units(s2, attacker: "training-scout-samurai", defender: "training-lion-samurai"); var yuki2 = Add(s2, Yuki, 0, Zone.Battlefield, province: 0);
            var e2 = MatchEngine.FromTestState(s2); Accept(e2, Select(e2)); Accept(e2, Use(e2, 0, "yuki-engage-recon", yuki2.Id));
            Check(e2.State.Players[1].Provinces[0].Reconnaissance[0], "Attacker's Yuki with an all-Scout army gains it against a non-Scout Defender");
        }
    }

    // Every reader asks the one question -- does THIS player have Reconnaissance at the CURRENT battlefield, whoever owns that
    // province -- so a Defender who gained it (here through Yuki) satisfies each Recon-dependent card, and one who did not is refused.
    private static void ReconnaissanceReadersServeDefenders()
    {
        // Common shape: player 0 attacks with a plain Personality; player 1 defends with Yuki (a Scout) and the card under test.
        // Yuki is played in the Engage window (or not, for the control) and the window is then closed; the Defender acts first.
        MatchEngine Defence(Action<MatchState, CardInstance, CardInstance> arrange, bool playYuki, out CardInstance yuki)
        {
            var s = Table();
            var attacker = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            yuki = Add(s, Yuki, 1, Zone.Battlefield, province: 0);
            arrange(s, attacker, yuki);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e));
            if (playYuki) { Accept(e, Decline(e)); Accept(e, Use(e, 1, "yuki-engage-recon", yuki.Id)); }
            CloseWindow(e);
            Eq(e.State.PriorityPlayerId, 1, "the Defender acts first");
            return e;
        }
        string? Error(MatchEngine e, GameCommand c) => e.Execute(c) is { Accepted: false } r ? r.Error : null;
        // Hiruma Sniper (a Follower on Yuki): "Battle: If you have Reconnaissance: Ranged 4 Attack." Requirement, not effect-gated.
        foreach (var recon in new[] { true, false })
        {
            CardInstance sniper = null!, target = null!;
            var e = Defence((s, a, y) => { target = a; sniper = Add(s, "oracle-l5r-3325", 1, Zone.Attached, province: 0, parent: y.Id); }, recon, out _);
            var cmd = Use(e, 1, "hiruma-sniper-attack", sniper.Id, target: target.Id);
            if (recon)
            {
                Accept(e, cmd);
                Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "A defending Sniper's Ranged 4 Attack kills the Force-3 attacker: Yuki gave the Defender Reconnaissance");
            }
            else Check(Error(e, cmd)?.Contains("Reconnaissance") == true, "Without Reconnaissance the Defender's Sniper is refused for the requirement");
        }
        // Keppo (a Scout): "If you have Reconnaissance, target an enemy attachment: Destroy it" -- effect-gated, so it fizzles without.
        foreach (var recon in new[] { true, false })
        {
            CardInstance keppo = null!, item = null!;
            var e = Defence((s, a, y) => { keppo = Add(s, "oracle-l5r-4328", 1, Zone.Battlefield, province: 0); item = Add(s, "training-item", 0, Zone.Attached, province: 0, parent: a.Id); }, recon, out _);
            Accept(e, Use(e, 1, "keppo-destroy", keppo.Id, target: item.Id));
            Eq(e.State.Cards[item.Id].Zone == Zone.Attached, !recon, recon ? "a defending Keppo with Reconnaissance destroys the attachment" : "without it the action resolves and nothing is destroyed");
        }
        // Shinjo Kinto (a Scout): "This movement will not be negated if you have Reconnaissance." The Attacker's Tamori Tamoko
        // (a movement-negation watcher) only gets its window when the Defender's Kinto lacks Reconnaissance.
        foreach (var recon in new[] { true, false })
        {
            CardInstance kinto = null!, target = null!;
            var e = Defence((s, a, y) => { kinto = Add(s, "oracle-l5r-6906", 1, Zone.Battlefield, province: 0); target = a; Add(s, "training-tamoko", 0, Zone.Battlefield, province: 0); }, recon, out _);
            Accept(e, Use(e, 1, "kinto-move", kinto.Id, target: target.Id));
            if (recon)
            {
                Check(e.State.PendingChoice is null, "a defending Kinto with Reconnaissance: the movement is never offered for negation");
                Eq(e.State.Cards[target.Id].Zone, Zone.Home, "and it resolves");
            }
            else Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "without Reconnaissance the Attacker's Tamoko gets a negation window");
        }
        // The opponent does NOT gain it: the Attacker's own Sniper, with the Defender's Yuki having granted Reconnaissance, is refused.
        {
            CardInstance sniper = null!, host = null!;
            var e = Defence((s, a, y) => { host = a; sniper = Add(s, "oracle-l5r-3325", 0, Zone.Attached, province: 0, parent: a.Id); Add(s, "training-scout-samurai", 1, Zone.Battlefield, province: 0); }, true, out _);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[1] && !e.State.Players[1].Provinces[0].Reconnaissance[0]);
            Accept(e, PassC(e)); // the Defender passes; the Attacker's turn
            var defenderUnit = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.DefinitionId == "training-scout-samurai");
            Check(Error(e, Use(e, 0, "hiruma-sniper-attack", sniper.Id, target: defenderUnit.Id))?.Contains("Reconnaissance") == true, "The Attacker still has none: their Sniper is refused");
        }
        // Training fixtures: the printed "You have Reconnaissance at the current battlefield" (either side) satisfies the Recon-gated strike.
        foreach (var recon in new[] { true, false })
        {
            var s = Table();
            var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var granter = Add(s, "training-scout", 1, Zone.Battlefield, province: 0); var striker = Add(s, "training-scout", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e)); Eq(e.State.PriorityPlayerId, 1);
            var honor = e.State.Players[0].Honor;
            if (recon) { Accept(e, Use(e, 1, "training-gain-reconnaissance-here", granter.Id)); Accept(e, PassC(e)); }
            Check(e.State.Players[1].Provinces[0].Reconnaissance[1] == recon && !e.State.Players[1].Provinces[0].Reconnaissance[0]);
            Accept(e, Use(e, 1, "training-reconnaissance-strike", striker.Id, target: enemy.Id));
            Eq(e.State.Players[0].Honor, recon ? honor - 2 : honor, recon ? "a defender's grant sits at their own province and lets the strike land" : "without it the strike does nothing");
        }
    }

    private static void YukiConditions()
    {
        // Another Personality of his controller there is not a Scout: the action cannot be taken at all (a constraints-block condition).
        {
            var s = Table(); Units(s);
            var yuki = Add(s, Yuki, 0, Zone.Battlefield, province: 0);
            Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); // not a Scout
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e));
            Check(e.State.PendingChoice is null, "Yuki cannot be used, and nothing else is playable: no window at all");
            Eq(e.State.PriorityPlayerId, 1);
            // The same position with something else playable opens the window, and Yuki is refused inside it.
            var s2 = Table(); Units(s2);
            var yuki2 = Add(s2, Yuki, 0, Zone.Battlefield, province: 0); Add(s2, "training-crane-veteran", 0, Zone.Battlefield, province: 0); Add(s2, Honor, 0, Zone.Hand);
            var e2 = MatchEngine.FromTestState(s2);
            Accept(e2, Select(e2));
            Eq(e2.State.PendingChoice?.Type, "engageReaction");
            Reject(e2, Use(e2, 0, "yuki-engage-recon", yuki2.Id));
            var listed = e2.QueryActions(0).Where(o => o.Type == "useAbility" && o.CardId == yuki2.Id).ToArray();
            Check(listed.Length == 1 && !listed[0].Enabled && listed[0].Reason!.Contains("Scouts"), "Listed but disabled, with the reason");
        }
        // "Your Personalities": the enemy's non-Scouts (and Followers) are irrelevant; his own Scouts and Followers on Scouts are fine.
        {
            var s = Table(); Units(s, attacker: "training-scout-samurai", defender: "training-crane-veteran");
            var yuki = Add(s, Yuki, 0, Zone.Battlefield, province: 0);
            Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: yuki.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e));
            Accept(e, Use(e, 0, "yuki-engage-recon", yuki.Id));
            Accept(e, Decline(e)); // the Defender passes; the Attacker's turn again
            Reject(e, Use(e, 0, "yuki-engage-recon", yuki.Id)); // a card's ability is usable once per turn
        }
        // He must be assigned to THE battlefield being engaged: at Home, or at another battlefield, he cannot react.
        foreach (var where in new[] { "home", "other" })
        {
            var s = Table(0, 1);
            Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
            var yuki = where == "home" ? Add(s, Yuki, 0, Zone.Home) : Add(s, Yuki, 0, Zone.Battlefield, province: 1);
            Add(s, Honor, 0, Zone.Hand); // so the window opens and the refusal is what is tested
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e, 0));
            Eq(e.State.PendingChoice?.Type, "engageReaction", where);
            Reject(e, Use(e, 0, "yuki-engage-recon", yuki.Id));
        }
        // A bowed Yuki (abilities on bowed cards cannot be used) and one that is not yours.
        {
            var s = Table(); Units(s, attacker: "training-scout-samurai");
            var yuki = Add(s, Yuki, 0, Zone.Battlefield, bowed: true, province: 0); Add(s, Honor, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e));
            Reject(e, Use(e, 0, "yuki-engage-recon", yuki.Id));
            var s2 = Table(); Units(s2, defender: "training-scout-samurai");
            var theirYuki = Add(s2, Yuki, 1, Zone.Battlefield, province: 0); Add(s2, Honor, 0, Zone.Hand);
            var e2 = MatchEngine.FromTestState(s2);
            Accept(e2, Select(e2));
            Reject(e2, Use(e2, 0, "yuki-engage-recon", theirYuki.Id)); // the Attacker cannot use the Defender's card
        }
    }

    private static void WindowOncePerBattle()
    {
        var s = Table(0, 1);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var yuki = Add(s, Yuki, 0, Zone.Battlefield, province: 1); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 1); // Yuki alone (a Scout) at province 1
        var e = MatchEngine.FromTestState(s);
        Accept(e, Select(e, 0));
        Check(e.State.PendingChoice is null && e.State.PendingEngage is null, "Yuki is at the OTHER battlefield: selecting province 0 opens no window");
        Eq(e.State.PriorityPlayerId, 1);
        ResolveByPassing(e);
        Eq(e.State.Phase, Phase.BattleSelection, "A second battlefield remains");
        Accept(e, Select(e, 1));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "Yuki's own battlefield: the window opens");
        Eq(e.State.PendingEngage!.ProvinceIndex, 1);
        Accept(e, Use(e, 0, "yuki-engage-recon", yuki.Id));
        CloseWindow(e);
        Eq(e.State.PriorityPlayerId, 0);
        // Once per battle: after it closed there is no second window, and the Reaction is no longer available.
        Check(e.State.PendingEngage is null);
        Reject(e, Use(e, 0, "yuki-engage-recon", yuki.Id));
        Check(!e.QueryActions(0).Any(o => o.Type == "useAbility" && o.Command?.AbilityId == "yuki-engage-recon"), "not listed once the window is over");
        ResolveByPassing(e);
        Eq(e.State.Phase, Phase.Dynasty);
        // A later, unrelated battle starts from the default again (no leftover claim).
        Check(e.State.FirstActionPlayerId is null && e.State.PendingEngage is null);
    }

    private static void EngageDeterministic()
    {
        var s = Table(); Units(s, defender: "training-scout-samurai");
        var yuki = Add(s, Yuki, 1, Zone.Battlefield, province: 0); var c1 = Add(s, Claim, 0, Zone.Hand);
        var first = MatchEngine.FromTestState(s);
        Accept(first, Select(first));
        Accept(first, Use(first, 0, "training-engage-attacker-first", c1.Id));
        var snapshot = first.State; // taken mid-window, with a claim already recorded
        Check(snapshot.PendingEngage is not null && snapshot.PendingEngage.Claims.Count == 1);
        string Continue(MatchState from)
        {
            var e = MatchEngine.FromTestState(from);
            Accept(e, Use(e, 1, "yuki-engage-recon", yuki.Id));
            CloseWindow(e);
            Accept(e, PassC(e));
            return Json(e.State) + Json(e.GetView(0).BattleStep);
        }
        var a = Continue(snapshot); var b = Continue(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(snapshot))!);
        Eq(a, b, "Identical continuations from the same mid-window state");
        Check(a.Contains("DefenderOverride"), "and it is the override case");
    }

    // ---- Naval Invasion ----------------------------------------------------------------------------------------
    // Attacker: Isas (printed Naval, has a Battle ability) plus `extraNaval` Naval samurai; Defender: `defenderNaval` Naval samurai.
    private static (MatchState S, CardInstance Isas, CardInstance Other) NavalTable(int extraNaval, int defenderNaval, params int[] battlefields)
    {
        var s = Table(battlefields);
        var isas = Add(s, Isas, 0, Zone.Battlefield, province: 0);
        var other = Add(s, "training-harmless-battle-action", 0, Zone.Battlefield, province: 0);
        for (var i = 0; i < extraNaval; i++) Add(s, "training-naval-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        for (var i = 0; i < defenderNaval; i++) Add(s, "training-naval-samurai", 1, Zone.Battlefield, province: 0);
        return (s, isas, other);
    }

    private static void NavalInvasion()
    {
        var (s, isas, other) = NavalTable(extraNaval: 0, defenderNaval: 0); // 1 Naval card (Isas) against 0
        var e = MatchEngine.FromTestState(s);
        Accept(e, Select(e));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "Naval Invasion is a legal reaction: the window opens on its own");
        var offered = e.QueryActions(0).Where(o => o.Type == "useAbility" && o.Enabled && o.Command!.AbilityId == "naval-invasion").ToArray();
        Eq(offered.Length, 1); Eq(offered[0].Command!.PerformerId, isas.Id, "Only the Naval Personality can be the performer");
        Check(offered[0].Command!.CardId is null, "A player ability has no source card");
        Reject(e, Use(e, 0, "naval-invasion", null, other.Id)); // "choose your performing NAVAL Personality"
        Reject(e, Use(e, 0, "naval-invasion", null, null));
        Accept(e, Use(e, 0, "naval-invasion", null, isas.Id));
        Eq(e.State.PendingEngage!.NavalPerformerId, isas.Id);
        CloseWindow(e);
        Eq(e.State.PriorityPlayerId, 0); Eq(ReasonOf(e), BattleSteps.ReasonNaval);
        var view = Step(e);
        Eq(view.FirstActionLimit, BattleSteps.LimitPerformerOnly); Eq(view.FirstActionPerformerId, isas.Id);
        // "Which he must perform": the first Battle action is Isas's own (core rules, Choose Performers) -- anything else is refused, atomically...
        Reject(e, Use(e, 0, "kouki-force", other.Id, target: e.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield).Id));
        Reject(e, new GameCommand("recruitReserve", 0, e.Version, CardId: other.Id));
        Check(!e.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command!.AbilityId == "kouki-force"), "and not offered");
        Check(e.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command!.AbilityId == "tsuruchi-isas-attack"), "Isas's own ability is offered");
        var defenderId = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield).Id;
        Accept(e, Use(e, 0, "tsuruchi-isas-attack", isas.Id)); // an Attack needs no target up front
        Check(e.State.FirstActionRestriction is null, "The limit binds only that first action");
        Eq(e.GetView(0).BattleStep!.FirstActionLimit, (string?)null);
        Accept(e, PassC(e)); // the Defender passes...
        Accept(e, Use(e, 0, "kouki-force", other.Id, target: defenderId)); // ...and the Attacker's next action may be anyone's
        // Passing is a legal way to use the opportunity, and it too ends the limit.
        var (s2, _, _) = NavalTable(0, 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Select(e2)); Accept(e2, Use(e2, 0, "naval-invasion", null, e2.State.Cards.Values.First(c => c.DefinitionId == Isas).Id)); CloseWindow(e2);
        Accept(e2, PassC(e2)); Check(e2.State.FirstActionRestriction is null); Eq(e2.State.PriorityPlayerId, 1);
        // A Defender's claim negates it: the Defender acts first, and the limit never applies to the Attacker.
        var (s3, _, _) = NavalTable(0, 0);
        var yuki = Add(s3, Yuki, 1, Zone.Battlefield, province: 0); // (a Scout, alone): remove the fixture's plain Lion so "all your Personalities there are Scouts" holds
        s3.Cards.Remove(s3.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield && c.DefinitionId == "training-lion-samurai").Id);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Select(e3));
        Accept(e3, Use(e3, 0, "naval-invasion", null, e3.State.Cards.Values.First(c => c.DefinitionId == Isas).Id));
        Accept(e3, Use(e3, 1, "yuki-engage-recon", yuki.Id));
        Accept(e3, Decline(e3)); Accept(e3, Decline(e3));
        Eq(e3.State.PriorityPlayerId, 1); Eq(ReasonOf(e3), BattleSteps.ReasonDefenderOverride); Check(e3.State.FirstActionRestriction is null);
        // Naval together with another engage grant is still one opportunity; the printed "which he must perform" is kept (documented reading).
        var (s4, _, _) = NavalTable(0, 0); var grant = Add(s4, Claim, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Select(e4));
        Accept(e4, Use(e4, 0, "training-engage-attacker-first", grant.Id)); Accept(e4, Decline(e4));
        Accept(e4, Use(e4, 0, "naval-invasion", null, e4.State.Cards.Values.First(c => c.DefinitionId == Isas).Id));
        CloseWindow(e4);
        Eq(e4.State.PriorityPlayerId, 0); Eq(ReasonOf(e4), BattleSteps.ReasonEngageGrant); Eq(Step(e4).FirstActionLimit, BattleSteps.LimitPerformerOnly);
    }

    private static void NavalIllegal()
    {
        // A tie and fewer claim nothing: with nothing else playable there is not even a window. (Attacker = Isas + extra; Defender = their Naval samurai.)
        foreach (var (own, theirs) in new[] { (0, 1), (0, 2), (1, 2), (1, 3), (2, 3) })
        {
            var (s, _, _) = NavalTable(own, theirs);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e));
            Check(e.State.PendingChoice is null, $"{own + 1} v {theirs} Naval cards: not strictly more");
            Eq(e.State.PriorityPlayerId, 1); Eq(ReasonOf(e), BattleSteps.ReasonDefault);
        }
        {
            var (s, _, _) = NavalTable(1, 1); // 2 v 1: strictly more
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e));
            Eq(e.State.PendingChoice?.Type, "engageReaction");
        }
        // Even at 1 v 0, with the window open for another reason, the illegal uses are refused: the Defender (whose army has MORE Naval cards)...
        {
            var s = Table(); var isas = Add(s, Isas, 0, Zone.Battlefield, province: 0);
            var navalD1 = Add(s, "training-naval-samurai", 1, Zone.Battlefield, province: 0); Add(s, "training-naval-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, Honor, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e));
            Reject(e, Use(e, 0, "naval-invasion", null, isas.Id)); // 1 v 2
            Accept(e, Decline(e));
            Reject(e, Use(e, 1, "naval-invasion", null, navalD1.Id)); // the Defender is not "the Attacker"
        }
        // ...and once per battle, but once per BATTLE and not per turn: it is available again in the next battle of the same turn.
        {
            var s = Table(0, 1);
            var isas0 = Add(s, Isas, 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var naval1 = Add(s, "training-naval-samurai", 0, Zone.Battlefield, province: 1); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Select(e, 0));
            Accept(e, Use(e, 0, "naval-invasion", null, isas0.Id));
            Accept(e, Decline(e)); // the Defender passes; the Attacker's turn again
            Reject(e, Use(e, 0, "naval-invasion", null, isas0.Id)); // once per battle
            Check(!e.QueryActions(0).Any(o => o.Enabled && o.Command?.AbilityId == "naval-invasion"), "and no longer offered");
            Accept(e, Decline(e)); // two passes in succession: the window closes
            Check(e.State.PendingChoice is null);
            Accept(e, PassC(e)); Accept(e, PassC(e)); // resolve battle 0 (the Attacker passes their limited first opportunity, then the Defender)
            Eq(e.State.Phase, Phase.BattleSelection);
            Accept(e, Select(e, 1));
            Eq(e.State.PendingChoice?.Type, "engageReaction", "A fresh battle: Naval Invasion is available again");
            Accept(e, Use(e, 0, "naval-invasion", null, naval1.Id));
        }
    }

    private static void NavalCounting()
    {
        // Printed Naval on a Personality (Yoritomo Doho, a vanilla real card).
        {
            var s = Table(); Add(s, Doho, 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); Accept(e, Select(e));
            Eq(e.State.PendingChoice?.Type, "engageReaction", "printed Naval counts");
        }
        // Isawa Kojiro: Naval only while he has a Spell (a live conditional keyword, never a durable grant).
        {
            var s = Table(); Add(s, Kojiro, 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); Accept(e, Select(e));
            Check(e.State.PendingChoice is null, "Kojiro without a Spell has no Naval");
            var s2 = Table(); var kojiro = Add(s2, Kojiro, 0, Zone.Battlefield, province: 0); Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s2, "training-spell", 0, Zone.Attached, province: 0, parent: kojiro.Id);
            var e2 = MatchEngine.FromTestState(s2); Accept(e2, Select(e2));
            Eq(e2.State.PendingChoice?.Type, "engageReaction", "Kojiro with a Spell is Naval");
            Check(e2.QueryActions(0).Any(o => o.Enabled && o.Command?.AbilityId == "naval-invasion" && o.Command.PerformerId == kojiro.Id));
        }
        // A granted Naval keyword (Suitengu's Path / Scourge of the Sea grant it to their wielder for as long as they stay attached).
        {
            var s = Table(); var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); Accept(e, Select(e));
            Check(e.State.PendingChoice is null, "no Naval yet");
            var s2 = Table(); var host2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s2, "oracle-l5r-6501", 0, Zone.Attached, province: 0, parent: host2.Id); host2.GrantedKeywords.Add(new("Naval", "oracle-l5r-6501", "Attachment"));
            var e2 = MatchEngine.FromTestState(s2); Accept(e2, Select(e2));
            Eq(e2.State.PendingChoice?.Type, "engageReaction", "the wielder has Naval");
            Check(e2.QueryActions(0).Any(o => o.Enabled && o.Command?.AbilityId == "naval-invasion" && o.Command.PerformerId == host2.Id));
        }
        // A Naval Follower is a Naval card too: a Naval Personality with a Naval Follower is 2, beating a defender's 1 -- 1 v 1 would not,
        // and the performer must be a Naval PERSONALITY (a Naval Follower on a non-Naval host cannot perform it).
        {
            var s = Table(); var host = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-naval-follower", 0, Zone.Attached, province: 0, parent: host.Id);
            Add(s, "training-naval-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); Accept(e, Select(e));
            Check(e.State.PendingChoice is null, "a Naval Follower (1) against 1 is a tie");
            var s1 = Table(); var host1 = Add(s1, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
            Add(s1, "training-naval-follower", 0, Zone.Attached, province: 0, parent: host1.Id); Add(s1, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e1 = MatchEngine.FromTestState(s1); Accept(e1, Select(e1));
            Check(e1.State.PendingChoice is null, "1 v 0 by count, but no Naval Personality can perform it: not playable");
            var s2 = Table(); var host2 = Add(s2, "training-naval-samurai", 0, Zone.Battlefield, province: 0);
            Add(s2, "training-naval-follower", 0, Zone.Attached, province: 0, parent: host2.Id);
            Add(s2, "training-naval-samurai", 1, Zone.Battlefield, province: 0);
            var e2 = MatchEngine.FromTestState(s2); Accept(e2, Select(e2));
            Eq(e2.State.PendingChoice?.Type, "engageReaction", "Naval Personality + Naval Follower = 2 v 1");
        }
        // Only the fought battlefield's armies count.
        {
            var s = Table(0, 1); Add(s, Doho, 0, Zone.Battlefield, province: 1); Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s); Accept(e, Select(e, 0));
            Check(e.State.PendingChoice is null, "a Naval Personality at ANOTHER battlefield does not count");
        }
    }

    // ---- Rebuilding Kyuden Hida --------------------------------------------------------------------------------
    private static void KyudenHida()
    {
        var s = Table(); var (a, d) = Units(s);
        var harmless = Add(s, "training-harmless-battle-action", 0, Zone.Battlefield, province: 0);
        Add(s, "oracle-l5r-6200", 1, Zone.EventInPlay); // Rebuilding Kyuden Hida, in play for the Defender
        var claim = Add(s, Claim, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Select(e));
        Accept(e, Use(e, 0, "training-engage-attacker-first", claim.Id));
        CloseWindow(e);
        Eq(e.State.PriorityPlayerId, 0, "The Attacker still holds the first opportunity...");
        Eq(Step(e).FirstActionLimit, BattleSteps.LimitMustPass, "...but may only pass it");
        Check(!e.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled), "no Battle action is offered");
        Reject(e, Use(e, 0, "kouki-force", harmless.Id, target: d.Id));
        Accept(e, PassC(e));
        Eq(e.State.PriorityPlayerId, 1, "the Defender's first normal opportunity"); Check(e.State.FirstActionRestriction is null);
        Accept(e, PassC(e)); Eq(e.State.Phase, Phase.Dynasty, "the pass just made counts: the Defender's pass ends the segment (documented reading)");
        // With no claim the event changes nothing at all: the Defender acts first, unrestricted, and the Attacker acts after them.
        var s2 = Table(); var (a2, d2) = Units(s2); var harmless2 = Add(s2, "training-harmless-battle-action", 0, Zone.Battlefield, province: 0);
        Add(s2, "oracle-l5r-6200", 1, Zone.EventInPlay);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Select(e2));
        Eq(e2.State.PriorityPlayerId, 1); Eq(Step(e2).FirstActionLimit, (string?)null);
        Accept(e2, PassC(e2));
        Accept(e2, Use(e2, 0, "kouki-force", harmless2.Id, target: d2.Id));
    }

    // ---- Before this battle resolves ---------------------------------------------------------------------------
    // A battle already in its Combat Segment with both units in place (the Defender to act); both players pass to resolve it.
    private static (MatchState S, CardInstance A, CardInstance D) ResolveTable(string attacker, string defender)
    {
        var s = Table(); s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.PriorityPlayerId = 1;
        var a = Add(s, attacker, 0, Zone.Battlefield, province: 0); var d = Add(s, defender, 1, Zone.Battlefield, province: 0);
        return (s, a, d);
    }
    private static bool Fought(MatchEngine e, string message) => e.GetView(0).Events.Any(ev => ev.Type == "battleResolved" && ev.Message.Contains(message));
    private static void BeforeResolutionMandatory()
    {
        // Without the trait: Force 2 loses to Force 3.
        var (s0, x0, _) = ResolveTable("training-scout-samurai", "training-crane-samurai");
        var e0 = MatchEngine.FromTestState(s0); ResolveByPassing(e0);
        Eq(e0.State.Cards[x0.Id].Zone, Zone.Dead, "2 v 3: the Attacker loses");
        // With a boost trait in play at the battlefield (controlled by the Attacker): applied after both passed, before the totals -> 4 v 3.
        var (s, x, y) = ResolveTable("training-scout-samurai", "training-crane-samurai");
        var terrain = Add(s, "training-before-resolution-boost-terrain", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, PassC(e));
        var midway = Step(e);
        Eq(midway.Step, BattleSteps.BattleActions, "Still the Combat Segment after one pass"); Eq(midway.ConsecutivePasses, 1);
        Check(e.State.Cards[x.Id].Modifiers.Count == 0, "Nothing applied while the players may still act");
        Accept(e, PassC(e));
        Check(Fought(e, "attacking Force 4, defending Force 3"), "The boost was applied before Force was totalled");
        Eq(e.State.Cards[y.Id].Zone, Zone.Dead, "4 v 3: the Defender loses");
        Check(e.State.Cards[terrain.Id].Zone != Zone.Battlefield, "A Terrain is discarded when its battle ends");
        Check(e.State.PendingBeforeBattleResolve is null && e.State.PendingChoice is null);
        // A trait that changes who fights: everyone with 3 or less Force goes home BEFORE the Armies are read.
        var (s2, _, d2) = ResolveTable("training-crane-veteran", "training-crane-samurai");
        Add(s2, "training-before-resolution-send-home-terrain", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        ResolveByPassing(e2);
        Check(Fought(e2, "attacking Force 5, defending Force 0"), "the Defender's unit never fought");
        Check(e2.State.Cards[d2.Id].Zone == Zone.Home && !e2.State.Cards[d2.Id].Bowed, "sent home (not destroyed, not bowed)");
    }

    private static void BeforeResolutionOrdering()
    {
        // Two sources at one battlefield never happen through ordinary play (one Terrain per battlefield) but the rule is written for
        // several; the fixtures prove the ordering primitive. X (Force 2) fights Y (Force 3): "home" sends home Force <= 3, "boost" gives the Attacker's +2F.
        (MatchEngine E, CardInstance Boost, CardInstance Home, CardInstance X, CardInstance Y) Build()
        {
            var (s, x, y) = ResolveTable("training-scout-samurai", "training-crane-samurai");
            var boost = Add(s, "training-before-resolution-boost-terrain", 0, Zone.Battlefield, province: 0); // controlled by the ATTACKER (boosts X)
            var home = Add(s, "training-before-resolution-send-home-terrain", 1, Zone.Battlefield, province: 0); // controlled by the DEFENDER
            return (MatchEngine.FromTestState(s), boost, home, x, y);
        }
        {
            var (e, boost, home, x, y) = Build();
            ResolveByPassing(e);
            Eq(e.State.PendingChoice?.Type, "beforeBattleOrder"); Eq(e.State.PriorityPlayerId, 0, "The player whose turn it is orders them, whoever controls the sources");
            var step = Step(e);
            Eq(step.Segment, BattleSteps.Resolution); Eq(step.Step, BattleSteps.BeforeResolution); Eq(step.DecisionPlayerId, (int?)0);
            Eq(step.BeforeResolutionSourceIds.Length, 2); Check(step.BeforeResolutionSourceIds.Contains(boost.Id) && step.BeforeResolutionSourceIds.Contains(home.Id));
            Check(e.QueryActions(0).Count(o => o.Type == "beforeBattleOrder" && o.Enabled) == 2, "Both orders are offered");
            foreach (var option in e.QueryActions(0).Where(o => o.Type == "beforeBattleOrder")) Accept(MatchEngine.FromTestState(e.State), option.Command!);
            Reject(e, new GameCommand("beforeBattleOrder", 1, e.Version, TargetId: boost.Id)); // not the active player
            Accept(e, new GameCommand("beforeBattleOrder", 0, e.Version, TargetId: boost.Id)); // boost first...
            Check(e.State.PendingChoice is null, "the last source resolved directly, with no second ordering choice");
            // ...X is 4, so "home" sends only Y (Force 3) away: the Attacker fights alone and wins by default, returning home BOWED.
            Check(e.State.Cards[x.Id].Zone == Zone.Home && e.State.Cards[x.Id].Bowed, "boost then home: X fought (and bowed after)");
            Check(e.State.Cards[y.Id].Zone == Zone.Home && !e.State.Cards[y.Id].Bowed, "Y was sent home before the Force comparison");
            Check(Fought(e, "attacking Force 4, defending Force 0"));
        }
        {
            var (e, boost, home, x, y) = Build();
            ResolveByPassing(e);
            Accept(e, new GameCommand("beforeBattleOrder", 0, e.Version, TargetId: home.Id)); // home first...
            // ...both X (2) and Y (3) are sent away, so the boost applies to nobody: nobody fights.
            Check(e.State.Cards[x.Id].Zone == Zone.Home && !e.State.Cards[x.Id].Bowed, "home then boost: X never fought");
            Check(e.State.Cards[y.Id].Zone == Zone.Home && !e.State.Cards[y.Id].Bowed);
            Check(Fought(e, "attacking Force 0, defending Force 0"));
            Check(e.GetView(0).Events.Where(ev => ev.Type == "beforeBattleEffect").Select(ev => ev.CardId).SequenceEqual([home.Id, boost.Id]), "Applied in the chosen order");
        }
    }

    private static void BeforeResolutionOptional()
    {
        (MatchEngine E, CardInstance Terrain, CardInstance X, CardInstance Y) Build()
        {
            var (s, x, y) = ResolveTable("training-crane-veteran", "training-lion-samurai"); // Force 5 v 4: the Attacker wins unless X is bowed
            var terrain = Add(s, "training-before-resolution-may-bow-terrain", 1, Zone.Battlefield, province: 0); // controlled by the DEFENDER (not the turn player)
            return (MatchEngine.FromTestState(s), terrain, x, y);
        }
        {
            var (e, terrain, x, y) = Build();
            ResolveByPassing(e);
            Eq(e.State.PendingChoice?.Type, "beforeBattleOptional"); Eq(e.State.PriorityPlayerId, 1, "The effect's OWN controller answers, not the turn player");
            Eq(Step(e).DecisionPlayerId, (int?)1); Eq(Step(e).ActiveBeforeResolutionSourceId, terrain.Id);
            Reject(e, new GameCommand("beforeBattleOptional", 0, e.Version, Choice: "decline")); // wrong player
            var offered = e.QueryActions(1);
            Check(offered.Any(o => o.Type == "beforeBattleOptional" && o.Command!.Choice == "decline") && offered.Count(o => o.Type == "beforeBattleOptional" && o.Command!.Choice == "use" && o.Command.TargetId == x.Id) == 1);
            foreach (var option in offered.Where(o => o.Type == "beforeBattleOptional")) Accept(MatchEngine.FromTestState(e.State), option.Command!);
            Reject(e, new GameCommand("beforeBattleOptional", 1, e.Version, Choice: "use")); // "use" needs a target
            Reject(e, new GameCommand("beforeBattleOptional", 1, e.Version, Choice: "use", TargetId: y.Id)); // a Personality of the source's OWN controller
            Reject(e, new GameCommand("beforeBattleOptional", 1, e.Version, Choice: "maybe"));
            Accept(e, new GameCommand("beforeBattleOptional", 1, e.Version, Choice: "use", TargetId: x.Id));
            Check(Fought(e, "attacking Force 0, defending Force 4"), "the bowed Attacker contributes nothing");
            Eq(e.State.Cards[x.Id].Zone, Zone.Dead, "and the Defender wins");
        }
        {
            var (e, _, x, _) = Build();
            ResolveByPassing(e);
            Accept(e, new GameCommand("beforeBattleOptional", 1, e.Version, Choice: "decline"));
            Check(Fought(e, "attacking Force 5, defending Force 4"), "declined: nothing changed");
            Check(e.State.Cards[x.Id].Zone == Zone.Home && e.State.Cards[x.Id].Bowed, "the Attacker won and returned home bowed");
        }
    }

    private static void BeforeResolutionScope()
    {
        // A trait card that is not at THIS battlefield (Home, another battlefield, or in a hand) never participates.
        var s = Table(0, 1); s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.PriorityPlayerId = 1;
        Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-before-resolution-boost-terrain", 0, Zone.Battlefield, province: 1); // the other battlefield
        Add(s, "training-before-resolution-boost-terrain", 0, Zone.Home);
        Add(s, "training-before-resolution-boost-terrain", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        ResolveByPassing(e);
        Check(e.State.PendingBeforeBattleResolve is null && e.State.PendingChoice is null);
        Check(e.GetView(0).Events.Any(ev => ev.Type == "battleResolved" && ev.Message.Contains("attacking Force 2, defending Force 3")), "No trait applied");
        Check(!e.GetView(0).Events.Any(ev => ev.Type == "beforeBattleEffect"));
        // Encircled Terrain is registered through the same lookup as the fixtures: its Effect names a BeforeResolution ability.
        Eq(CardCatalog.Get("oracle-l5r-2332").Effect, "encircled-before-battle");
        Eq(AbilityCatalog.All["encircled-before-battle"].Timing, "BeforeResolution");
        Check(AbilityCatalog.All.Where(a => a.Value.Timing == "BeforeResolution").All(a => a.Value.Trigger == "before-battle-resolves"));
        // ...and a BeforeResolution ability is never a player-usable action.
        var (s2, _, _) = ResolveTable("training-scout-samurai", "training-crane-samurai");
        var terrain = Add(s2, "training-before-resolution-boost-terrain", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, new GameCommand("useAbility", 1, e2.Version, CardId: terrain.Id, AbilityId: "training-before-resolution-boost"));
        Check(!e2.QueryActions(1).Any(o => o.Type == "useAbility" && o.Command?.AbilityId == "training-before-resolution-boost"), "and never offered");
    }

    // ---- BattleStepView ----------------------------------------------------------------------------------------
    private static void StepViewSegments()
    {
        void At(Phase phase, int priority, string segment, string step, int? decision, Action<MatchState>? arrange = null)
        {
            var s = Table(); Units(s); s.Phase = phase; s.PriorityPlayerId = priority; arrange?.Invoke(s);
            var e = MatchEngine.FromTestState(s);
            var v0 = Step(e, 0); var v1 = Step(e, 1);
            Eq(v0.Segment, segment, $"{phase} segment"); Eq(v0.Step, step, $"{phase} step"); Eq(v0.DecisionPlayerId, decision, $"{phase} decision");
            Eq(Json(v0), Json(v1), $"{phase}: both viewers see the same public step");
            if (segment != BattleSteps.None) Eq(v0.AttackerPlayerId, (int?)0);
        }
        At(Phase.Action, 0, BattleSteps.None, BattleSteps.None, null);
        At(Phase.AttackDeclaration, 0, BattleSteps.Declaration, BattleSteps.DeclareOrDecline, 0);
        At(Phase.InfantryDeclaration, 0, BattleSteps.InfantryManeuvers, BattleSteps.AttackerAssigns, 0);
        At(Phase.DefenseAssignment, 1, BattleSteps.InfantryManeuvers, BattleSteps.DefenderAssigns, 1);
        At(Phase.CavalryDeclaration, 0, BattleSteps.CavalryManeuvers, BattleSteps.AttackerAssigns, 0);
        At(Phase.CavalryDefense, 1, BattleSteps.CavalryManeuvers, BattleSteps.DefenderAssigns, 1);
        At(Phase.BattleSelection, 0, BattleSteps.FightBattles, BattleSteps.ChooseBattle, 0);
        At(Phase.Battle, 1, BattleSteps.FightBattles, BattleSteps.BattleActions, 1, s => s.CurrentBattleProvince = 0);
        At(Phase.Dynasty, 0, BattleSteps.None, BattleSteps.None, null);
        // Aftermath: after the Force comparison a Conqueror choice / draw window is still part of Resolution.
        At(Phase.Battle, 0, BattleSteps.Resolution, BattleSteps.Aftermath, 0, s => { s.CurrentBattleProvince = null; });
        // The Engage window, and the Resolution's before-resolution step, are covered with real transitions below.
        var t = Table(); Units(t); Add(t, Honor, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(t);
        Accept(e, Select(e));
        Eq(Step(e).Segment, BattleSteps.FightBattles); Eq(Step(e).Step, BattleSteps.Engage); Eq(Step(e).BattlefieldProvince, (int?)0); Eq(Step(e).DecisionPlayerId, (int?)0);
        Accept(e, Decline(e));
        Eq(Step(e).DecisionPlayerId, (int?)1, "the decision follows priority through the window");
        Accept(e, Decline(e));
        Eq(Step(e).Step, BattleSteps.BattleActions); Eq(Step(e).DecisionPlayerId, (int?)1);
        // A finished match has no decision.
        Accept(e, new GameCommand("concede", 1, e.Version));
        Eq(Step(e).DecisionPlayerId, (int?)null);
    }

    private static void StepViewDetails()
    {
        var s = Table(); Units(s, defender: "training-scout-samurai");
        var yuki = Add(s, Yuki, 1, Zone.Battlefield, province: 0); var claim = Add(s, Claim, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Select(e));
        Accept(e, Use(e, 0, "training-engage-attacker-first", claim.Id));
        var v = Step(e);
        Eq(v.EngageClaims.Length, 1); Eq(v.EngageClaims[0].Kind, "Engage"); Eq(v.EngageClaims[0].SourceId, claim.Id);
        Eq(v.FirstActionPlayerId, (int?)0); Eq(v.FirstActionReason, BattleSteps.ReasonEngageGrant); Check(!v.FirstActionResolved, "provisional until the window closes");
        Accept(e, Use(e, 1, "yuki-engage-recon", yuki.Id));
        Eq(Step(e).FirstActionPlayerId, (int?)1); Eq(Step(e).FirstActionReason, BattleSteps.ReasonDefenderOverride); Eq(Step(e).EngageClaims.Length, 2);
        CloseWindow(e);
        var closed = Step(e);
        Eq(closed.FirstActionPlayerId, (int?)1); Check(closed.FirstActionResolved); Eq(closed.EngageClaims.Length, 0);
        Eq(closed.ConsecutivePasses, 0);
        // The whole view survives JSON exactly as it crosses the wire (the server's own options are the same shape, plus strict members).
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true };
        var view = e.GetView(0);
        var round = JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(view, options), options)!;
        Eq(Json(round.BattleStep), Json(view.BattleStep), "BattleStep round-trips");
        Eq(Json(round.BattleStep), Json(e.GetView(1).BattleStep), "identical for both viewers");
        // An older payload without the field still deserializes (the new field is an optional trailing member).
        var old = JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(view with { BattleStep = null }, options), options)!;
        Check(old.BattleStep is null);
        // Every step name the view can emit is one of the published constants.
        var known = typeof(BattleSteps).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        Check(known.Contains(v.Segment) && known.Contains(v.Step) && known.Contains(closed.FirstActionReason!));
    }
}
