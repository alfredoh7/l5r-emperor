using L5R.Core;
using static RegressionAssertions;

// Primitive P10 (issue #695): the keyword-rule registry (KeywordRules695.cs). Engine only: nothing here is a catalogue card. Every rule below is a fixture rule whose source is a card Effect starting with
// "training-p10-" (set on a per-instance definition, CardInstance.Printed) and is unregistered when its test ends; the four live conditionals that moved onto the channel are tested against the Effects
// they always used. Positions are explicit (Batch336Support.Table: player 0 the Crane, player 1 the Lion; province 0 is the battlefield).
internal static class KeywordRules695Tests
{
    private const string Samurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", Follower = "training-follower", Item = "training-item", Holding = "training-border-keep";
    private const string RingAir = "training-ring-of-air", RingWater = "training-ring-of-water", RingFire = "training-ring-of-fire-stub";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner) => Add(s, def, owner, Zone.Battlefield, false, 0);
    private static CardInstance Attach(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, province: host.ProvinceIndex, parent: host.Id);
    private static MatchState Table() => Batch336Support.Table();
    private static MatchEngine Engine(MatchState s) => MatchEngine.FromTestState(s);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);

    /// <summary>A per-instance definition: the card's own, with the Effect (the trait name a rule's source matches) and extra keywords.</summary>
    private static CardInstance Fix(CardInstance card, string effect, params string[] keywords)
    {
        var def = CardCatalog.Get(card.DefinitionId);
        card.Printed = def with { Effect = effect, Keywords = (def.Keywords ?? []).Concat(keywords).ToArray() };
        return card;
    }

    private static void Give(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));

    /// <summary>Registers fixture rules for the length of one test.</summary>
    private static void WithRules(KeywordRule[] rules, Action test)
    {
        KeywordRuleCatalog.Register(rules);
        try { test(); }
        finally { KeywordRuleCatalog.Unregister(rules.Select(r => r.Id).ToArray()); }
    }

    private static KeywordCondition C(KeywordConditionKind kind, string? arg = null) => new(kind, arg);

    public static void Run(TestRunner runner) => runner.Run("keyword-rules-695", [
        ("The registry: the hook is registered at priority 16500 under a P10 id, the four legacy rules are in the catalogue, and a malformed rule is refused", RegistryShape),
        ("Self rules: \"has Cavalry while Shadowlands\" and \"has Naval while Crane Clan\" apply and lapse with their condition, live", SelfRules),
        ("\"While its controller is Clan X\" and \"while another player is Clan X\" (P9's condition, read on the source)", ControllerAndOtherPlayerClan),
        ("Army scope: \"Human Personalities in Chun's army have Naval\" -- not Nonhumans, enemies, cards at home, or after Chun leaves", ArmyScope),
        ("Unit scope: \"cards in this unit have Cavalry\" -- every card in the unit, so IsCavalryUnit sees it; another unit's cards do not", UnitScope),
        ("Controller scope: \"your Personalities have Conqueror\" -- not the opponent's, not a Follower; the source leaving ends it", ControllerScope),
        ("\"The element keywords of every Ring you control\": one keyword per Ring, live as Rings come and go, never the opponent's", RingElements),
        ("A Suppress rule beats a printed keyword but not a permanent removal; rules that read each other's keywords (or their own) neither loop nor grant", SuppressAndReentrancy),
        ("The common case allocates nothing: a keyword no rule names costs the consult zero bytes", NoAllocation),
        ("Gain blocks: GrantKeyword refuses Fallen and Shadowlands, grants others; printed keywords stay; a live granted one is suppressed", GainBlocksGrantKeyword),
        ("Gain blocks end when their source leaves play and do not revive when it returns", GainBlockSourceLeaves),
        ("Gain blocks: permanent copies (a Spell's and a Personality's) skip a blocked keyword, and a Clan block skips Clan keywords", GainBlocksPermanentCopies),
        ("Gain blocks: \"will not gain a Clan Alignment\" blocks the clan-alignment grant paths (a static rule, a permanent grant) and a created card's controller Clan", GainBlocksClanPaths),
        ("Element replacement: Keyword() maps Fire to Water, Spell eligibility follows, chains map in order, and it ends when its Spell leaves", ElementReplacement),
        ("Permanent Clan alignment: granted, read by Keyword and ClanAlignments (Unaligned, Loyal), removed by the existing removal, regranted, refused by a block", PermanentClanAlignment),
        ("The four live conditionals on the channel: Naval while it has a Spell, Jun-Ni's unit Cavalry, Yayu's Cavalry, Watari's sole Follower", LegacyConditionals),
        ("Save and replay: the stores survive Copy, Clone and a JSON round trip, and the engine rebuilt from the saved state answers identically", SaveAndReplay),
    ]);

    private static void RegistryShape()
    {
        Check(EngineHooks.KeywordRule.Names.Contains("P10KeywordRules"), "the P10 hook is registered");
        foreach (var id in new[] { MatchEngine.LegacyKeywordRules.KojiroNaval, MatchEngine.LegacyKeywordRules.JunNiCavalry, MatchEngine.LegacyKeywordRules.YayuCavalry, MatchEngine.LegacyKeywordRules.WatariCavalry })
            Check(KeywordRuleCatalog.All.Any(r => r.Id == id), $"{id} is in the catalogue");
        static bool Refused(KeywordRule rule)
        {
            try { KeywordRuleCatalog.Register(rule); KeywordRuleCatalog.Unregister(rule.Id); return false; }
            catch (InvalidOperationException) { return true; }
        }
        Check(Refused(new("training-p10-bad", KeywordRuleScope.Self, [], SourceEffect: "training-p10-x")), "a rule names a keyword");
        Check(Refused(new("training-p10-bad", KeywordRuleScope.Self, ["Naval"])), "a rule names its source");
        Check(Refused(new("training-p10-bad", KeywordRuleScope.Self, ["Naval"], SourceEffect: "training-p10-x", SourceCardId: "training-crane-samurai")), "and not two");
        Check(Refused(new("training-p10-bad", KeywordRuleScope.Self, ["Naval"], SourceEffect: "none")), "the Effect none is no source");
        Check(Refused(new(MatchEngine.LegacyKeywordRules.YayuCavalry, KeywordRuleScope.Self, ["Naval"], SourceEffect: "training-p10-x")), "ids are unique");
    }

    private static void SelfRules()
    {
        WithRules([
            new("training-p10-cav-shadowlands", KeywordRuleScope.Self, ["Cavalry"], SourceEffect: "training-p10-cav-shadowlands", AllOf: [C(KeywordConditionKind.SubjectHasKeyword, "Shadowlands")]),
            new("training-p10-naval-crane", KeywordRuleScope.Self, ["Naval"], SourceEffect: "training-p10-naval-crane", AllOf: [C(KeywordConditionKind.SubjectIsClan, "Crane")]),
        ], () =>
        {
            var s = Table();
            var shadow = Fix(Add(s, Samurai, 0, Zone.Home), "training-p10-cav-shadowlands");
            var crane = Fix(Add(s, Samurai, 0, Zone.Home), "training-p10-naval-crane");
            var lion = Fix(Add(s, LionSamurai, 1, Zone.Home), "training-p10-naval-crane");
            var plain = Add(s, Samurai, 0, Zone.Home);
            Give(plain, "Shadowlands");
            var e = Engine(s);
            Check(!e.HasKeyword(shadow.Id, "Cavalry"), "no Shadowlands, no Cavalry");
            Check(!e.HasKeyword(plain.Id, "Cavalry"), "a card without the Effect has no rule");
            Check(e.HasKeyword(crane.Id, "Naval"), "a Crane Clan card has Naval");
            Check(!e.HasKeyword(lion.Id, "Naval"), "a Lion Clan card does not");
            e = Edit(e, st => Give(st.Cards[shadow.Id], "Shadowlands"));
            Check(e.HasKeyword(shadow.Id, "Cavalry"), "it has Cavalry while Shadowlands");
            e = Edit(e, st => st.Cards[shadow.Id].GrantedKeywords.Clear());
            Check(!e.HasKeyword(shadow.Id, "Cavalry"), "and loses it the moment Shadowlands goes: the rule is live, nothing was stored");
            Check(e.State.Cards[shadow.Id].GrantedKeywords.Count == 0, "no keyword was written onto the card");
            e = Edit(e, st => st.Cards[crane.Id].RemovedClanAffiliations.Add("Crane"));
            Check(!e.HasKeyword(crane.Id, "Naval"), "an Unaligned card is no longer Crane Clan, so the rule lapses");
        });
    }

    private static void ControllerAndOtherPlayerClan()
    {
        WithRules([
            new("training-p10-own-clan", KeywordRuleScope.Self, ["Scout"], SourceEffect: "training-p10-own-clan", AllOf: [C(KeywordConditionKind.ControllerIsClan, "Crane")]),
            new("training-p10-other-clan", KeywordRuleScope.Self, ["Tactician"], SourceEffect: "training-p10-other-clan", SourceAnyOf: [new(TermConditionKind.OtherPlayerIsClan, "Crane")]),
        ], () =>
        {
            var s = Table();
            var mine = Fix(Add(s, Samurai, 0, Zone.Home), "training-p10-own-clan");
            var theirs = Fix(Add(s, LionSamurai, 1, Zone.Home), "training-p10-own-clan");
            var other0 = Fix(Add(s, Samurai, 0, Zone.Home), "training-p10-other-clan");
            var other1 = Fix(Add(s, LionSamurai, 1, Zone.Home), "training-p10-other-clan");
            var e = Engine(s);
            Check(e.HasKeyword(mine.Id, "Scout"), "its controller is Crane");
            Check(!e.HasKeyword(theirs.Id, "Scout"), "the Lion player's card: its controller is not Crane");
            Check(!e.HasKeyword(other0.Id, "Tactician"), "another player must be Crane: the Crane card's own controller does not count");
            Check(e.HasKeyword(other1.Id, "Tactician"), "the Lion's card sees another player who is Crane (P9's OtherPlayerIsClan)");
        });
    }

    private static void ArmyScope()
    {
        WithRules([
            new("training-p10-chun", KeywordRuleScope.Army, ["Naval"], SourceEffect: "training-p10-chun",
                AllOf: [C(KeywordConditionKind.SubjectIsType, "Personality"), C(KeywordConditionKind.SubjectIsHuman)]),
        ], () =>
        {
            var s = Table();
            var chun = Fix(Bf(s, Samurai, 0), "training-p10-chun");
            var human = Bf(s, Samurai, 0);
            var nonhuman = Fix(Bf(s, Samurai, 0), "none", "Nonhuman");
            var enemy = Bf(s, LionSamurai, 1);
            var home = Add(s, Samurai, 0, Zone.Home);
            var follower = Attach(s, Follower, human);
            var e = Engine(s);
            Check(e.HasKeyword(human.Id, "Naval"), "a Human Personality in his army has Naval");
            Check(e.HasKeyword(chun.Id, "Naval"), "so does Chun himself (he is Human and in his own army)");
            Check(!e.HasKeyword(nonhuman.Id, "Naval"), "a Nonhuman does not");
            Check(!e.HasKeyword(enemy.Id, "Naval"), "an enemy Personality does not");
            Check(!e.HasKeyword(home.Id, "Naval"), "a Personality at home is in no army");
            Check(!e.HasKeyword(follower.Id, "Naval"), "a Follower is not a Personality");
            e = Edit(e, st => { st.Cards[chun.Id].Zone = Zone.Home; st.Cards[chun.Id].ProvinceIndex = null; });
            Check(!e.HasKeyword(human.Id, "Naval"), "Chun at home is in no army: the rule lapses");
            e = Edit(e, st => { st.Cards[chun.Id].Zone = Zone.Battlefield; st.Cards[chun.Id].ProvinceIndex = 0; });
            Check(e.HasKeyword(human.Id, "Naval"), "and returns when he is back");
            e = Edit(e, st => st.Cards[chun.Id].Zone = Zone.DynastyDiscard);
            Check(!e.HasKeyword(human.Id, "Naval"), "Chun out of play: nothing");
        });
    }

    private static void UnitScope()
    {
        WithRules([
            new("training-p10-elephant", KeywordRuleScope.Unit, ["Cavalry"], SourceEffect: "training-p10-elephant"),
        ], () =>
        {
            var s = Table();
            var host = Add(s, Samurai, 0, Zone.Battlefield, province: 0);
            var follower = Attach(s, Follower, host);
            var elephant = Fix(Attach(s, Item, host), "training-p10-elephant");
            var otherHost = Add(s, Samurai, 0, Zone.Battlefield, province: 0);
            var otherFollower = Attach(s, Follower, otherHost);
            var e = Engine(s);
            foreach (var card in new[] { host, follower, elephant })
                Check(e.HasKeyword(card.Id, "Cavalry"), $"{card.Id} is in the unit and has Cavalry");
            Check(!e.HasKeyword(otherHost.Id, "Cavalry") && !e.HasKeyword(otherFollower.Id, "Cavalry"), "another unit's cards do not");
            Check(e.IsCavalryUnitForTest(host.Id), "IsCavalryUnit sees a unit-scoped grant: the Personality and every Follower have Cavalry");
            Check(!e.IsCavalryUnitForTest(otherHost.Id), "the other unit stays Infantry");
            e = Edit(e, st => { st.Cards[elephant.Id].Zone = Zone.FateDiscard; st.Cards[elephant.Id].AttachedTo = null; });
            Check(!e.HasKeyword(host.Id, "Cavalry") && !e.HasKeyword(follower.Id, "Cavalry") && !e.IsCavalryUnitForTest(host.Id), "the source leaving ends it for the whole unit");
        });
    }

    private static void ControllerScope()
    {
        WithRules([
            new("training-p10-conqueror", KeywordRuleScope.Controller, ["Conqueror"], SourceEffect: "training-p10-conqueror", AllOf: [C(KeywordConditionKind.SubjectIsType, "Personality")]),
        ], () =>
        {
            var s = Table();
            var celestial = Fix(Add(s, Holding, 0, Zone.Celestial), "training-p10-conqueror");
            var mine = Bf(s, Samurai, 0); var home = Add(s, Samurai, 0, Zone.Home);
            var theirs = Bf(s, LionSamurai, 1);
            var follower = Attach(s, Follower, mine);
            var inDiscard = Add(s, Samurai, 0, Zone.DynastyDiscard);
            var e = Engine(s);
            Check(e.HasKeyword(mine.Id, "Conqueror") && e.HasKeyword(home.Id, "Conqueror"), "your Personalities in play have Conqueror");
            Check(!e.HasKeyword(theirs.Id, "Conqueror"), "not the opponent's");
            Check(!e.HasKeyword(follower.Id, "Conqueror"), "not a Follower");
            Check(!e.HasKeyword(inDiscard.Id, "Conqueror"), "not a Personality out of play (the rule is read in play)");
            e = Edit(e, st => st.Cards[celestial.Id].Zone = Zone.DynastyDiscard);
            Check(!e.HasKeyword(mine.Id, "Conqueror"), "the Celestial leaving ends it");
        });
        WithRules([
            new("training-p10-fudo-all", KeywordRuleScope.Controller, ["Monk"], SourceEffect: "training-p10-fudo-all", InAndOutOfPlay: true,
                AllOf: [C(KeywordConditionKind.SubjectIsType, "Personality"), C(KeywordConditionKind.SubjectLacksKeyword, "Unique")]),
        ], () =>
        {
            var s = Table();
            var source = Fix(Add(s, Holding, 0, Zone.Home), "training-p10-fudo-all");
            var inHand = Add(s, Samurai, 0, Zone.DynastyDiscard); var unique = Add(s, Samurai, 0, Zone.DynastyDiscard); Give(unique, "Unique");
            var enemyOut = Add(s, LionSamurai, 1, Zone.DynastyDiscard);
            var e = Engine(s);
            Check(e.HasKeyword(inHand.Id, "Monk"), "in and out of play: a Personality in a discard pile has it");
            Check(!e.HasKeyword(unique.Id, "Monk") && !e.HasKeyword(enemyOut.Id, "Monk"), "non-Unique only, and only the controller's cards");
        });
    }

    private static void RingElements()
    {
        WithRules([
            new("training-p10-rings", KeywordRuleScope.Self, ["Air", "Earth", "Fire", "Water", "Void"], SourceEffect: "training-p10-rings", AllOf: [C(KeywordConditionKind.ControllerControlsRingWithAskedKeyword)]),
        ], () =>
        {
            var s = Table();
            var shugenja = Fix(Add(s, Samurai, 0, Zone.Home), "training-p10-rings", "Shugenja");
            var air = Add(s, RingAir, 0, Zone.Home); var water = Add(s, RingWater, 0, Zone.Home);
            var theirFire = Add(s, RingFire, 1, Zone.Home);
            var e = Engine(s);
            Check(e.HasKeyword(shugenja.Id, "Air") && e.HasKeyword(shugenja.Id, "Water"), "an Air Ring and a Water Ring: Air and Water");
            Check(!e.HasKeyword(shugenja.Id, "Fire") && !e.HasKeyword(shugenja.Id, "Earth") && !e.HasKeyword(shugenja.Id, "Void"), "no other element, and never the opponent's Ring");
            e = Edit(e, st => st.Cards[air.Id].Zone = Zone.FateDiscard);
            Check(!e.HasKeyword(shugenja.Id, "Air") && e.HasKeyword(shugenja.Id, "Water"), "a Ring leaving play takes its element with it");
            e = Edit(e, st => Add(st, RingFire, 0, Zone.Home));
            Check(e.HasKeyword(shugenja.Id, "Fire"), "a new Ring adds its element");
            Check(theirFire.OwnerId == 1, "fixture sanity");
        });
    }

    private static void SuppressAndReentrancy()
    {
        WithRules([
            new("training-p10-suppress", KeywordRuleScope.Self, ["Cavalry"], SourceEffect: "training-p10-suppress", Effect: KeywordRuleEffect.Suppress, AllOf: [C(KeywordConditionKind.SubjectHasKeyword, "Shadowlands")]),
            new("training-p10-loop-a", KeywordRuleScope.Self, ["Scout"], SourceEffect: "training-p10-loop", AllOf: [C(KeywordConditionKind.SubjectHasKeyword, "Tactician")]),
            new("training-p10-loop-b", KeywordRuleScope.Self, ["Tactician"], SourceEffect: "training-p10-loop", AllOf: [C(KeywordConditionKind.SubjectHasKeyword, "Scout")]),
            new("training-p10-self-read", KeywordRuleScope.Self, ["Duelist"], SourceEffect: "training-p10-self-read", AllOf: [C(KeywordConditionKind.SubjectHasKeyword, "Duelist")]),
        ], () =>
        {
            var s = Table();
            var suppressed = Fix(Add(s, Samurai, 0, Zone.Home), "training-p10-suppress", "Cavalry", "Shadowlands");
            var kept = Fix(Add(s, Samurai, 0, Zone.Home), "training-p10-suppress", "Cavalry");
            var removed = Fix(Add(s, Samurai, 0, Zone.Home), "training-p10-suppress", "Cavalry"); removed.RemovedKeywords.Add("Cavalry");
            var loop = Fix(Add(s, Samurai, 0, Zone.Home), "training-p10-loop");
            var selfRead = Fix(Add(s, Samurai, 0, Zone.Home), "training-p10-self-read");
            var e = Engine(s);
            Check(!e.HasKeyword(suppressed.Id, "Cavalry"), "a Suppress rule beats the printed keyword while its condition holds");
            Check(e.HasKeyword(kept.Id, "Cavalry"), "and not otherwise");
            Check(!e.HasKeyword(removed.Id, "Cavalry"), "a permanent removal still removes");
            Check(!e.HasKeyword(loop.Id, "Scout") && !e.HasKeyword(loop.Id, "Tactician"), "two rules that read each other grant neither and do not loop");
            Check(!e.HasKeyword(selfRead.Id, "Duelist"), "a rule that reads its own keyword grants nothing from itself");
            Give(loop, "Tactician");
            e = Engine(s);
            Check(e.HasKeyword(loop.Id, "Scout"), "a real Tactician makes the first rule grant Scout, with no loop");
        });
    }

    private static void NoAllocation()
    {
        var s = Table();
        var card = Add(s, Samurai, 0, Zone.Home);
        var e = Engine(s);
        for (var n = 0; n < 200; n++) e.ConsultKeywordRulesAnswersForTest(card.Id, "Courtier");
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var n = 0; n < 5000; n++) e.ConsultKeywordRulesAnswersForTest(card.Id, "Courtier");
        var after = GC.GetAllocatedBytesForCurrentThread();
        Check(after - before < 1024, $"the consult for a keyword no rule names allocated {after - before} bytes over 5000 calls (only the test seam's own lookup is allowed)");
        // With a gain block stored on ANOTHER card the consult is still free of allocation for this one.
        s = Table(); card = Add(s, Samurai, 0, Zone.Home); var other = Add(s, Samurai, 0, Zone.Home);
        e = Engine(s);
        var id = other.Id;
        e = Edit(e, st => st.KeywordRules.Blocks.Add(new("b", null, id, null, ["Fallen"], [])));
        for (var n = 0; n < 200; n++) e.ConsultKeywordRulesAnswersForTest(card.Id, "Courtier");
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var n = 0; n < 5000; n++) e.ConsultKeywordRulesAnswersForTest(card.Id, "Courtier");
        after = GC.GetAllocatedBytesForCurrentThread();
        Check(after - before < 1024, $"a stored block on another card costs no allocation either ({after - before} bytes)");
    }

    private static void GainBlocksGrantKeyword()
    {
        var s = Table();
        var ayumu = Add(s, Samurai, 0, Zone.Home); var printed = Fix(Add(s, Samurai, 0, Zone.Home), "none", "Shadowlands");
        var e = Engine(s);
        e.AddKeywordGainBlockForTest(null, ayumu.Id, null, ["Fallen", "Shadowlands"]);
        e.GrantKeywordForTest(ayumu.Id, "Fallen", "src", GrantDuration.Permanent);
        e.GrantKeywordForTest(ayumu.Id, "Shadowlands", "src", GrantDuration.Turn);
        e.GrantKeywordForTest(ayumu.Id, "Naval", "src", GrantDuration.Permanent);
        Check(!e.HasKeyword(ayumu.Id, "Fallen") && !e.HasKeyword(ayumu.Id, "Shadowlands"), "Fallen and Shadowlands are not granted");
        Check(e.HasKeyword(ayumu.Id, "Naval"), "another keyword is");
        Check(!e.State.Cards[ayumu.Id].GrantedKeywords.Any(g => g.Keyword is "Fallen" or "Shadowlands"), "nothing was recorded on the card");
        e.GrantKeywordForTest(printed.Id, "Fallen", "src", GrantDuration.Permanent);
        Check(e.HasKeyword(printed.Id, "Fallen"), "a card the block is not on is unaffected");

        // A live-granted keyword (a rule) is a gain too: suppressed; a printed one stays.
        WithRules([new("training-p10-live-shadow", KeywordRuleScope.Self, ["Shadowlands"], SourceEffect: "training-p10-live-shadow")], () =>
        {
            var t = Table();
            var blocked = Fix(Add(t, Samurai, 0, Zone.Home), "training-p10-live-shadow");
            var free = Fix(Add(t, Samurai, 0, Zone.Home), "training-p10-live-shadow");
            var printedBlocked = Fix(Add(t, Samurai, 0, Zone.Home), "none", "Fallen");
            var f = Engine(t);
            f.AddKeywordGainBlockForTest(null, blocked.Id, null, ["Shadowlands"]);
            f.AddKeywordGainBlockForTest(null, printedBlocked.Id, null, ["Fallen"]);
            Check(!f.HasKeyword(blocked.Id, "Shadowlands"), "a live-granted keyword is suppressed by the block");
            Check(f.HasKeyword(free.Id, "Shadowlands"), "the same rule on an unblocked card works");
            Check(f.HasKeyword(printedBlocked.Id, "Fallen"), "a block never takes a printed keyword away");
        });

        // A player-level block: "your Personalities will not gain ..." covers a Personality he controls, not his Followers or the other player's.
        var u = Table();
        var mine = Add(u, Samurai, 0, Zone.Home); var theirs = Add(u, LionSamurai, 1, Zone.Home); var follower = Attach(u, Follower, Add(u, Samurai, 0, Zone.Home));
        var g = Engine(u);
        g.AddKeywordGainBlockForTest(null, null, 0, classes: ["element"]);
        foreach (var card in new[] { mine, theirs, follower }) g.GrantKeywordForTest(card.Id, "Fire", "src", GrantDuration.Turn);
        Check(!g.HasKeyword(mine.Id, "Fire") && g.HasKeyword(theirs.Id, "Fire") && g.HasKeyword(follower.Id, "Fire"), "an element-class block on player 0's Personalities only");
    }

    private static void GainBlockSourceLeaves()
    {
        var s = Table();
        var target = Add(s, Samurai, 0, Zone.Home); var source = Add(s, Item, 0, Zone.Home);
        var e = Engine(s);
        e.AddKeywordGainBlockForTest(source.Id, target.Id, null, ["Fallen"]);
        e.GrantKeywordForTest(target.Id, "Fallen", "src", GrantDuration.Permanent);
        Check(!e.HasKeyword(target.Id, "Fallen"), "blocked while the source is in play");
        e = Edit(e, st => st.Cards[source.Id].Zone = Zone.FateDiscard);
        e.GrantKeywordForTest(target.Id, "Fallen", "src", GrantDuration.Permanent);
        Check(e.HasKeyword(target.Id, "Fallen"), "the source left play: the block is gone");
        Check(e.State.KeywordRules.Blocks.Count == 0, "and removed from the store");
        // Revival: the block must not return when the source comes back.
        s = Table(); target = Add(s, Samurai, 0, Zone.Home); source = Add(s, Item, 0, Zone.Home);
        e = Engine(s);
        e.AddKeywordGainBlockForTest(source.Id, target.Id, null, ["Fallen"]);
        var (tid, sid) = (target.Id, source.Id);
        e = Edit(e, st => st.Cards[sid].Zone = Zone.FateDiscard);
        _ = e.HasKeyword(tid, "Fallen"); // any keyword read sweeps
        e = Edit(e, st => st.Cards[sid].Zone = Zone.Home);
        e.GrantKeywordForTest(tid, "Fallen", "src", GrantDuration.Permanent);
        Check(e.HasKeyword(tid, "Fallen"), "a source that returns does not revive the block it lost");
        // RemoveKeywordGainBlocks by source.
        s = Table(); target = Add(s, Samurai, 0, Zone.Home); source = Add(s, Item, 0, Zone.Home);
        e = Engine(s);
        e.AddKeywordGainBlockForTest(source.Id, target.Id, null, ["Fallen"]);
        e.AddKeywordGainBlockForTest(null, target.Id, null, ["Naval"]);
        e.RemoveKeywordGainBlocksForTest(source.Id);
        e.GrantKeywordForTest(target.Id, "Fallen", "src", GrantDuration.Permanent); e.GrantKeywordForTest(target.Id, "Naval", "src", GrantDuration.Permanent);
        Check(e.HasKeyword(target.Id, "Fallen") && !e.HasKeyword(target.Id, "Naval"), "removing a source's blocks leaves the permanent one");
    }

    private static void GainBlocksPermanentCopies()
    {
        var s = Table();
        var copier = Add(s, Samurai, 0, Zone.Home);
        var spell = Fix(Add(s, Item, 0, Zone.Home), "none", "Fire", "Maho", "Spell");
        var from = Fix(Add(s, Samurai, 0, Zone.Home), "none", "Fallen", "Tactician", "Crane Clan");
        var e = Engine(s);
        e.AddKeywordGainBlockForTest(null, copier.Id, null, ["Maho", "Fallen"]);
        e.PermanentlyCopySpellForTest(copier.Id, spell.Id);
        e.PermanentlyCopyPersonalityForTest(copier.Id, from.Id);
        Check(e.HasKeyword(copier.Id, "Fire") && e.HasKeyword(copier.Id, "Tactician"), "unblocked keywords are copied");
        Check(!e.HasKeyword(copier.Id, "Maho") && !e.HasKeyword(copier.Id, "Fallen"), "blocked keywords are not copied, by either copy");
        Check(!(e.State.Cards[copier.Id].Printed?.Keywords ?? []).Contains("Maho") && !(e.State.Cards[copier.Id].Printed?.Keywords ?? []).Contains("Fallen"), "nor written into the copier's printed values");
        // A Clan block skips Clan keywords.
        var s2 = Table(); var copier2 = Add(s2, Samurai, 0, Zone.Home); var from2 = Fix(Add(s2, Samurai, 0, Zone.Home), "none", "Lion Clan", "Fallen");
        var f = Engine(s2);
        f.AddKeywordGainBlockForTest(null, copier2.Id, null, classes: ["clan"]);
        f.PermanentlyCopyPersonalityForTest(copier2.Id, from2.Id);
        Check(!f.HasKeyword(copier2.Id, "Lion Clan") && f.HasKeyword(copier2.Id, "Fallen"), "a clan-class block skips \"Lion Clan\" and copies Fallen");
    }

    private static void GainBlocksClanPaths()
    {
        // The static path: Seppun Nagi's "your other non-Loyal Magistrate Personalities have your Clan alignment".
        var s = Table();
        var nagi = Fix(Add(s, Samurai, 0, Zone.Home), "nagi-magistrate-clan");
        var magistrate = Fix(Add(s, LionSamurai, 0, Zone.Home), "none", "Magistrate");
        var blocked = Fix(Add(s, LionSamurai, 0, Zone.Home), "none", "Magistrate");
        var e = Engine(s);
        e.AddKeywordGainBlockForTest(null, blocked.Id, null, classes: ["clan"]);
        Check(e.ClanAlignmentsForTest(magistrate.Id).Contains("Crane") && e.HasKeyword(magistrate.Id, "Crane Clan"), "Nagi's rule gives the Magistrate Crane (control case)");
        Check(!e.ClanAlignmentsForTest(blocked.Id).Contains("Crane") && !e.HasKeyword(blocked.Id, "Crane Clan"), "a card that will not gain a Clan alignment does not gain Nagi's");
        Check(e.ClanAlignmentsForTest(blocked.Id).Contains("Lion"), "its printed alignment stays");
        Check(nagi.OwnerId == 0, "fixture sanity");

        // The permanent grant path.
        var t = Table(); var target = Add(t, LionSamurai, 0, Zone.Home);
        var f = Engine(t);
        f.AddKeywordGainBlockForTest(null, target.Id, null, ["Crane Clan"]);
        Check(!f.GrantClanAlignmentForTest(target.Id, "Crane", "src"), "the grant is refused");
        Check(!f.ClanAlignmentsForTest(target.Id).Contains("Crane"), "and nothing was gained");

        // A created card's controller Clan (WithControllerClan).
        var u = Table();
        var g = Engine(u);
        var scan = CardCatalog.Get(LionSamurai) with { Clan = "Unaligned", Clans = null, Keywords = ["Samurai"] };
        var created = g.WithControllerClanForTest(scan, 0);
        Check(created.Clan == "Crane" && created.Keywords!.Contains("Crane Clan"), "control: the created Personality takes his creator's Clan");
        g.AddKeywordGainBlockForTest(null, null, 0, ["Crane Clan"]);
        var blockedCreated = g.WithControllerClanForTest(scan, 0);
        Check(blockedCreated.Clan == "Unaligned" && !blockedCreated.Keywords!.Contains("Crane Clan"), "a player-level block: the created card takes no Clan alignment");
        Check(g.WithControllerClanForTest(scan, 1).Clan == "Lion", "the other player's creation is unaffected");
    }

    private static void ElementReplacement()
    {
        var s = Table();
        var shugenja = Fix(Add(s, Samurai, 0, Zone.Home), "none", "Shugenja", "Fire", "Void");
        var spell = Add(s, Item, 0, Zone.Attached, parent: shugenja.Id);
        var waterSpell = Fix(Add(s, Item, 0, Zone.Home), "none", "Spell");
        waterSpell.Printed = waterSpell.Printed! with { AttachRequiresKeyword = "Water" };
        var fireSpell = Fix(Add(s, Item, 0, Zone.Home), "none", "Spell");
        fireSpell.Printed = fireSpell.Printed! with { AttachRequiresKeyword = "Fire" };
        var e = Engine(s);
        Check(e.HasKeyword(shugenja.Id, "Fire") && !e.HasKeyword(shugenja.Id, "Water"), "control: a Fire Shugenja");
        Check(e.AttachmentLegalTargetForTest(fireSpell.Id, shugenja.Id) && !e.AttachmentLegalTargetForTest(waterSpell.Id, shugenja.Id), "control: a Fire Spell attaches, a Water Spell does not");
        e.ReplaceElementKeywordForTest(shugenja.Id, "Fire", "Water", spell.Id);
        Check(!e.HasKeyword(shugenja.Id, "Fire") && e.HasKeyword(shugenja.Id, "Water"), "Fire is replaced by Water");
        Check(e.HasKeyword(shugenja.Id, "Void") && e.HasKeyword(shugenja.Id, "Shugenja"), "other keywords are untouched");
        Check(!e.HasKeyword(shugenja.Id, "Air") && !e.HasKeyword(shugenja.Id, "Earth"), "nothing else appears");
        Check(!e.AttachmentLegalTargetForTest(fireSpell.Id, shugenja.Id) && e.AttachmentLegalTargetForTest(waterSpell.Id, shugenja.Id), "Spell eligibility follows the replaced element");
        // Chains and rule-granted elements are mapped too.
        e.ReplaceElementKeywordForTest(shugenja.Id, "Water", "Air", spell.Id);
        Check(!e.HasKeyword(shugenja.Id, "Fire") && !e.HasKeyword(shugenja.Id, "Water") && e.HasKeyword(shugenja.Id, "Air"), "a second replacement maps the result of the first");
        // The source leaves play: the elements come back and the replacements are gone.
        var (sid, hid) = (spell.Id, shugenja.Id);
        e = Edit(e, st => { st.Cards[sid].Zone = Zone.FateDiscard; st.Cards[sid].AttachedTo = null; });
        Check(e.HasKeyword(hid, "Fire") && !e.HasKeyword(hid, "Air") && !e.HasKeyword(hid, "Water"), "the Spell leaves play: Fire is Fire again");
        Check(e.State.KeywordRules.ElementReplacements.Count == 0, "the replacements were swept");
        // A Spell moved to another host no longer replaces on the first.
        s = Table(); shugenja = Fix(Add(s, Samurai, 0, Zone.Home), "none", "Fire"); var other = Add(s, Samurai, 0, Zone.Home); spell = Add(s, Item, 0, Zone.Attached, parent: shugenja.Id);
        e = Engine(s);
        e.ReplaceElementKeywordForTest(shugenja.Id, "Fire", "Earth", spell.Id);
        (sid, hid) = (spell.Id, shugenja.Id); var oid = other.Id;
        e = Edit(e, st => st.Cards[sid].AttachedTo = oid);
        Check(e.HasKeyword(hid, "Fire") && !e.HasKeyword(hid, "Earth"), "a Spell attached elsewhere no longer replaces");
        Check(Throws(() => e.ReplaceElementKeywordForTest(hid, "Fire", "Fire", null)) && Throws(() => e.ReplaceElementKeywordForTest(hid, "Fire", "Naval", null)), "a replacement maps one element to a different element");
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
    }

    private static void PermanentClanAlignment()
    {
        var s = Table();
        var unaligned = Add(s, Samurai, 0, Zone.Home); unaligned.Printed = CardCatalog.Get(Samurai) with { Clan = "Unaligned", Keywords = ["Samurai"] };
        var loyal = Add(s, Samurai, 0, Zone.Home); loyal.Printed = CardCatalog.Get(Samurai) with { Clan = "Lion", Keywords = ["Samurai", "Loyal", "Lion Clan"] };
        var e = Engine(s);
        Check(e.HasKeyword(unaligned.Id, "Unaligned") && !e.HasKeyword(unaligned.Id, "Crane Clan"), "control: Unaligned");
        Check(e.GrantClanAlignmentForTest(unaligned.Id, "Crane", "suikihime"), "the grant is made");
        Check(e.HasKeyword(unaligned.Id, "Crane Clan") && e.ClanAlignmentsForTest(unaligned.Id).SequenceEqual(["Crane"]) && !e.HasKeyword(unaligned.Id, "Unaligned"), "Keyword, ClanAlignments and Unaligned all agree");
        Check(e.GrantClanAlignmentForTest(unaligned.Id, "Crane", "again") && e.State.KeywordRules.ClanGrants.Count == 1, "granting what he has is idempotent");
        e.RemoveClanAlignmentsForTest(unaligned.Id);
        Check(!e.HasKeyword(unaligned.Id, "Crane Clan") && e.HasKeyword(unaligned.Id, "Unaligned") && e.ClanAlignmentsForTest(unaligned.Id).Length == 0, "the existing removal takes a granted alignment too");
        Check(e.GrantClanAlignmentForTest(unaligned.Id, "Lion", "regrant") && e.HasKeyword(unaligned.Id, "Lion Clan"), "and a new alignment can be given after");
        e.RemoveClanAlignmentForTest(unaligned.Id, "Lion");
        Check(!e.HasKeyword(unaligned.Id, "Lion Clan") && e.State.KeywordRules.ClanGrants.Count == 0, "removing the one alignment drops its grant");
        // A printed alignment removed, then given back.
        e.RemoveClanAlignmentForTest(loyal.Id, "Lion");
        Check(!e.HasKeyword(loyal.Id, "Lion Clan") && e.HasKeyword(loyal.Id, "Unaligned"), "the printed alignment is removed");
        Check(e.GrantClanAlignmentForTest(loyal.Id, "Lion", "back") && e.HasKeyword(loyal.Id, "Lion Clan") && e.ClanAlignmentsForTest(loyal.Id).Contains("Lion"), "given back");
        // A second, different alignment is added to the first (a card may have several).
        e.GrantClanAlignmentForTest(loyal.Id, "Crane", "second");
        Check(e.ClanAlignmentsForTest(loyal.Id).OrderBy(c => c).SequenceEqual(["Crane", "Lion"]), "alignments accumulate");
    }

    private static void LegacyConditionals()
    {
        var s = Table();
        var kojiro = Fix(Add(s, Samurai, 0, Zone.Home), "naval-while-has-spell");
        var junni = Fix(Add(s, Samurai, 0, Zone.Home), "unit-followers-cavalry");
        var followerOfJunni = Attach(s, Follower, junni); var itemOfJunni = Attach(s, Item, junni);
        var strayFollower = Attach(s, Follower, Add(s, Samurai, 0, Zone.Home));
        var yayu = Fix(Add(s, Samurai, 0, Zone.Home), "cavalry-force-while-not-your-turn");
        var watari = Fix(Add(s, Samurai, 0, Zone.Home), "watari-sole-follower-cavalry");
        var onlyFollower = Attach(s, Follower, watari);
        var spellOnKojiro = Fix(Attach(s, Item, kojiro), "none", "Spell");
        spellOnKojiro.Printed = spellOnKojiro.Printed! with { Type = CardType.Spell };
        s.ActivePlayerId = 0;
        var e = Engine(s);
        Check(e.HasKeyword(kojiro.Id, "Naval"), "Kojiro has Naval while he has a Spell");
        Check(e.HasKeyword(followerOfJunni.Id, "Cavalry") && !e.HasKeyword(itemOfJunni.Id, "Cavalry") && !e.HasKeyword(junni.Id, "Cavalry") && !e.HasKeyword(strayFollower.Id, "Cavalry"), "Jun-Ni's Followers, and only they, have Cavalry");
        Check(!e.HasKeyword(yayu.Id, "Cavalry"), "Yayu on her own turn: no Cavalry");
        Check(e.HasKeyword(onlyFollower.Id, "Cavalry"), "Watari's sole Follower has Cavalry");
        var (kid, wid, sid) = (kojiro.Id, watari.Id, spellOnKojiro.Id);
        e = Edit(e, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 1; Attach(st, Follower, st.Cards[wid]); st.Cards[sid].Zone = Zone.FateDiscard; st.Cards[sid].AttachedTo = null; });
        Check(!e.HasKeyword(kid, "Naval"), "the Spell leaves: Kojiro loses Naval");
        Check(e.HasKeyword(yayu.Id, "Cavalry"), "Yayu on the other player's turn: Cavalry");
        Check(!e.HasKeyword(onlyFollower.Id, "Cavalry"), "Watari with two Followers: neither has it");
        Check(!e.HasKeyword(kid, "Cavalry"), "and nothing leaks across keywords");
    }

    private static void SaveAndReplay()
    {
        var s = Table();
        var a = Add(s, Samurai, 0, Zone.Home); var b = Add(s, Samurai, 0, Zone.Attached, parent: a.Id); var unaligned = Add(s, Samurai, 0, Zone.Home);
        unaligned.Printed = CardCatalog.Get(Samurai) with { Clan = "Unaligned", Keywords = ["Samurai"] };
        Fix(a, "none", "Fire");
        var e = Engine(s);
        e.AddKeywordGainBlockForTest(b.Id, a.Id, null, ["Fallen"], ["clan"]);
        e.AddKeywordGainBlockForTest(null, null, 1, ["Naval"]);
        e.ReplaceElementKeywordForTest(a.Id, "Fire", "Earth", b.Id);
        e.GrantClanAlignmentForTest(unaligned.Id, "Lion", "src");
        var json = System.Text.Json.JsonSerializer.Serialize(e.State);
        Check(json.Contains("\"KeywordRules\""), "the store is part of the saved state");
        var restored = System.Text.Json.JsonSerializer.Deserialize<MatchState>(json)!;
        Eq(System.Text.Json.JsonSerializer.Serialize(restored), json, "a JSON round trip is byte for byte");
        Eq(restored.KeywordRules.Blocks.Count, 2); Eq(restored.KeywordRules.ElementReplacements.Count, 1); Eq(restored.KeywordRules.ClanGrants.Count, 1);
        var rebuilt = Engine(restored);
        Check(rebuilt.HasKeyword(a.Id, "Earth") && !rebuilt.HasKeyword(a.Id, "Fire"), "the rebuilt engine reads the replacement");
        Check(rebuilt.HasKeyword(unaligned.Id, "Lion Clan"), "and the Clan grant");
        rebuilt.GrantKeywordForTest(a.Id, "Fallen", "src", GrantDuration.Permanent);
        Check(!rebuilt.HasKeyword(a.Id, "Fallen"), "and the block");
        var clone = rebuilt.Clone();
        Eq(System.Text.Json.JsonSerializer.Serialize(clone.State), System.Text.Json.JsonSerializer.Serialize(rebuilt.State), "Clone carries the store");
        // The state is a function of the commands: two engines driven through the same calls end byte for byte alike.
        MatchEngine Drive()
        {
            var t = Table();
            var x = Add(t, Samurai, 0, Zone.Home); var y = Add(t, Samurai, 0, Zone.Attached, parent: x.Id); Fix(x, "none", "Fire");
            var f = Engine(t);
            f.AddKeywordGainBlockForTest(y.Id, x.Id, null, ["Fallen"]); f.ReplaceElementKeywordForTest(x.Id, "Fire", "Earth", y.Id); f.GrantClanAlignmentForTest(x.Id, "Lion", "src");
            return f;
        }
        Eq(System.Text.Json.JsonSerializer.Serialize(Drive().State), System.Text.Json.JsonSerializer.Serialize(Drive().State), "the same calls give the same saved state");
    }
}
