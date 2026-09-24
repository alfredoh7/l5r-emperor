using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Forgotten Legacy pass (issue #467), batch 5: the cards of spell-wave-21, holding-wave-10, item-wave-13, event-wave-6, celestial-wave-6 and region-wave-8,
// driven through QueryActions, the commands it offers and the turn flow. Every scenario is an explicit position on the economy table (player 0 the Crane,
// player 1 the Lion), with training fixtures (and "test-fixture" keyword grants) standing in for the Personalities each card needs.
internal static class ForgottenLegacyBatch5Tests
{
    private const string PearlOfRage = "oracle-l5r-5903", MountainsPower = "oracle-l5r-8228", WindOfTheMoon = "oracle-l5r-9339", ThundersWrath = "oracle-l5r-8475",
        AgelessShrine = "oracle-l5r-166", BambooExp = "oracle-l5r-680", BorderKeepExp = "oracle-l5r-1107", ColonialHarbor = "oracle-l5r-1431",
        BlackPearl = "oracle-l5r-993", CursedRelic = "oracle-l5r-1607",
        AssaultInTheJungle = "oracle-l5r-628", BurningDreams = "oracle-l5r-1185", WillingSpirits = "oracle-l5r-9337",
        RyoshunsGuidance = "oracle-l5r-6425", SecondCity = "oracle-l5r-8303";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static ActionOption[] Listed(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();
    // The first enabled offer of the ability (optionally matching), or a failure naming every offer and its reason.
    private static ActionOption Opt(MatchEngine e, int player, string ability, Func<ActionOption, bool>? match = null)
    {
        var found = Listed(e, player, ability).FirstOrDefault(a => a.Enabled && (match?.Invoke(a) ?? true));
        Check(found is not null, $"{ability} for player {player}: " + string.Join("; ", Options(e, player, ability).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return found!;
    }
    // Player abilities (Assault in the Jungle's grant, the Favor's and the Lobby) are enabled only with a published Stronghold (the economy fixture's are scenario-only).
    private static MatchState Published(MatchState s) { foreach (var player in s.Players) s.Cards[player.StrongholdId].DefinitionId = "oracle-l5r-1218"; return s; }
    private static CardInstance FaceDownIn(MatchState s, string def, int owner, int province)
    {
        var card = Add(s, def, owner, Zone.Province, province: province); card.FaceUp = false; s.Players[owner].Provinces[province].CardId = card.Id;
        return card;
    }

    public static void Run(TestRunner runner) => runner.Run("forgotten-legacy-batch-5", [
        ("Published shape: exactly the fifteen wired cards are published across the six waves, with their printed stats, keywords, Effects and ability ids and shipped art", PublishedShape),
        ("Willing Spirits: revealed, it waits in play; after its owner's next turn begins every one of his provinces permanently gets +2 strength", WillingSpiritsDelayedStrength),
        ("Assault in the Jungle: revealed it stays two turns; while it does every player has the Battle action that bows the performing attacker's whole unit to destroy an enemy card without attachments", AssaultInTheJungleGrant),
        ("Burning Dreams: the Favor is discarded on reveal; a Favor-discarding action against a Nonhuman Personality is taken but negated; Nonhumans may not perform Political actions", BurningDreamsClauses),
        ("Ageless Shrine: 3 Gold when paying for a Nonhuman; its Open straightens a Nonhuman and one of his attachments, or a Samurai alone", AgelessShrine_),
        ("Bamboo Harvesters - experienced: stays bowed until its controller's third turn; its Limited draw forces a discard at 6 or more cards", BambooHarvestersExpDraw),
        ("Border Keep - experienced: once per game, one to three hand cards go to the bottom of the Fate deck and as many are drawn", BorderKeepExpCycle),
        ("Colonial Harbor: produces 5 Gold, and its controller's bowed Stronghold may still use an ability that does not bow it", ColonialHarborTireless),
        ("Black Pearl: destroyed to bring a Personality from the opponent's discard pile into play under its controller's control, 4 Gold less for a Nonhuman", BlackPearlRecruit),
        ("Cursed Relic: each -1C pays for a Ranged 3 Attack, any number of times, until the wielder's last Chi kills him; its bow and -1C remove a card from the game", CursedRelicCosts),
        ("The Second City: +1F to its controller's Courtiers and Magistrates; its Political Open turns a face-down province card face-up, with or without +2 province strength (one use)", SecondCityForceAndReveal),
        ("Pearl of Rage: +1F per Pearl its controller controls or has discarded, and a Ranged Attack of the wielder's Chi plus that count", PearlOfRageCount),
        ("The Mountain's Power: a defending Earth wielder's destruction by another player's card is negated (not while attacking, not for a non-Earth wielder); its Battle bows and gains 1 Honor", MountainsPowerShield),
        ("Thunder's Wrath: -4F for its bow; destroyed, a Ranged 4 Attack (+2 against a Follower) whose destroyed Follower earns a Thunder wielder a second Ranged 4 Attack at a target chosen afterwards -- immediately or after the Attack's reaction window", ThundersWrathAttacks),
        ("Wind of the Moon: its Air wielder straightens in the other player's Straighten Phase too; its Open gives an out-of-play face-up Personality +1PH or +1 Gold Cost (one use)", WindOfTheMoon_),
        ("Ryoshun's Guidance: +1C to its controller's Personalities outside a duel; discarded, it redirects another player's action to another of his cards at the same location, and the lost +1C can kill", RyoshunsGuidance_),
    ]);

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Check(listed.Where(l => l.Length > 0).OrderBy(l => l, StringComparer.Ordinal).SequenceEqual(ids.OrderBy(i => i, StringComparer.Ordinal)), $"{wave} manifest lists exactly the wired cards");
        foreach (var id in ids)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Abilities(string id, params string[] expected)
        {
            var card = CardCatalog.Get(id);
            Check(!card.ScenarioOnly && (card.Abilities ?? []).SequenceEqual(expected), $"{card.Name}: abilities {string.Join(",", card.Abilities ?? [])}");
            foreach (var ability in expected) Check(AbilityCatalog.All.ContainsKey(ability), ability);
        }
        string[] Ids(PublishedSnapshot wave) => wave.Cards.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();
        Check(Ids(PublishedContent.SpellWave21).SequenceEqual(new[] { PearlOfRage, MountainsPower, WindOfTheMoon, ThundersWrath }.OrderBy(i => i, StringComparer.Ordinal)), "spell-wave-21");
        Check(Ids(PublishedContent.HoldingWave10).SequenceEqual(new[] { AgelessShrine, BambooExp, BorderKeepExp, ColonialHarbor }.OrderBy(i => i, StringComparer.Ordinal)), "holding-wave-10");
        Check(Ids(PublishedContent.ItemWave13).SequenceEqual(new[] { BlackPearl, CursedRelic }.OrderBy(i => i, StringComparer.Ordinal)), "item-wave-13");
        Check(Ids(PublishedContent.EventWave6).SequenceEqual(new[] { AssaultInTheJungle, BurningDreams, WillingSpirits }.OrderBy(i => i, StringComparer.Ordinal)), "event-wave-6");
        Check(Ids(PublishedContent.CelestialWave6).SequenceEqual(new[] { RyoshunsGuidance }), "celestial-wave-6");
        Check(Ids(PublishedContent.RegionWave8).SequenceEqual(new[] { SecondCity }), "region-wave-8");

        var pearl = CardCatalog.Get(PearlOfRage);
        Check(pearl.Type == CardType.Spell && pearl.GoldCost == 2 && pearl.Focus == 4 && Keywords(PearlOfRage) == "Pearl,Unique" && pearl.Effect == "pearl-of-rage", "Pearl of Rage");
        Abilities(PearlOfRage, "pearl-of-rage-attack");
        var mountain = CardCatalog.Get(MountainsPower);
        Check(mountain.Type == CardType.Spell && mountain.Focus == 3 && Keywords(MountainsPower) == "Earth" && mountain.Effect == "mountains-power", "The Mountain's Power");
        Abilities(MountainsPower, "mountains-power-bow");
        var wind = CardCatalog.Get(WindOfTheMoon);
        Check(wind.Type == CardType.Spell && wind.Focus == 2 && Keywords(WindOfTheMoon) == "Air,Political" && wind.Effect == "wind-of-the-moon", "Wind of the Moon");
        Abilities(WindOfTheMoon, "wind-moon-honor", "wind-moon-gold");
        var thunder = CardCatalog.Get(ThundersWrath);
        Check(thunder.Type == CardType.Spell && thunder.GoldCost == 0 && thunder.Focus == 2 && Keywords(ThundersWrath) == "Thunder", "Thunder's Wrath");
        Abilities(ThundersWrath, "thunders-wrath-bow", "thunders-wrath-attack");
        Check(!CardCatalog.Cards.Values.Any(c => (c.Abilities ?? []).Contains("thunders-wrath-followup-attack")), "the follow-up Attack is never an action of its own");
        Manifest(root, "spell-wave-21", PearlOfRage, MountainsPower, WindOfTheMoon, ThundersWrath);

        var shrine = CardCatalog.Get(AgelessShrine);
        Check(shrine.Type == CardType.Holding && shrine.GoldCost == 2 && shrine.GoldProduction == 2 && Keywords(AgelessShrine) == "Forest,Temple" && shrine.Effect == "ageless-shrine-gold", "Ageless Shrine");
        Abilities(AgelessShrine, "ageless-shrine-straighten");
        var bamboo = CardCatalog.Get(BambooExp);
        Check(bamboo.Type == CardType.Holding && bamboo.Name == "Bamboo Harvesters" && bamboo.GoldCost == 3 && bamboo.GoldProduction == 2 && Keywords(BambooExp) == "Experienced,Forest,Unique"
            && bamboo.Effect == "bamboo-harvesters-exp", "Bamboo Harvesters - experienced");
        Abilities(BambooExp, "bamboo-exp-draw");
        var keep = CardCatalog.Get(BorderKeepExp);
        Check(keep.Type == CardType.Holding && keep.Name == "Border Keep" && keep.GoldCost == 3 && keep.GoldProduction == 2 && Keywords(BorderKeepExp) == "Experienced,Unique", "Border Keep - experienced");
        Abilities(BorderKeepExp, "border-keep-exp-cycle");
        Check(AbilityCatalog.All["border-keep-exp-cycle"].OncePerGame, "once per game");
        var harbor = CardCatalog.Get(ColonialHarbor);
        Check(harbor.Type == CardType.Holding && harbor.GoldCost == 6 && harbor.GoldProduction == 5 && Keywords(ColonialHarbor) == "Port" && harbor.Effect == "colonial-harbor-tireless", "Colonial Harbor");
        Abilities(ColonialHarbor);
        Manifest(root, "holding-wave-10", AgelessShrine, BambooExp, BorderKeepExp, ColonialHarbor);

        var blackPearl = CardCatalog.Get(BlackPearl);
        Check(blackPearl.Type == CardType.Item && blackPearl.GoldCost == 4 && blackPearl.Force == 0 && blackPearl.Chi == 1 && blackPearl.Focus == 4 && Keywords(BlackPearl) == "Experienced,Pearl,Unique", "Black Pearl");
        Abilities(BlackPearl, "black-pearl-recruit");
        var relic = CardCatalog.Get(CursedRelic);
        Check(relic.Type == CardType.Item && relic.GoldCost == 8 && relic.Force == 4 && relic.Chi == 0 && relic.Focus == 1 && Keywords(CursedRelic) == "Cursed,Weapon", "Cursed Relic");
        Abilities(CursedRelic, "cursed-relic-attack", "cursed-relic-remove");
        Manifest(root, "item-wave-13", BlackPearl, CursedRelic);

        foreach (var (id, effect) in new[] { (AssaultInTheJungle, "assault-in-the-jungle"), (BurningDreams, "burning-dreams"), (WillingSpirits, "willing-spirits") })
        {
            var ev = CardCatalog.Get(id);
            Check(ev.Type == CardType.Event && Keywords(id) == "Unique" && ev.Effect == effect, ev.Name);
            Abilities(id);
        }
        Check(AbilityCatalog.All["assault-jungle-destroy"].PlayerAbility, "Assault in the Jungle's action is a player ability");
        Manifest(root, "event-wave-6", AssaultInTheJungle, BurningDreams, WillingSpirits);

        var ryoshun = CardCatalog.Get(RyoshunsGuidance);
        Check(ryoshun.Type == CardType.Celestial && Keywords(RyoshunsGuidance) == "Kami,Unique" && ryoshun.Effect == "ryoshuns-guidance", "Ryoshun's Guidance");
        Abilities(RyoshunsGuidance, "ryoshun-redirect");
        Check(AbilityCatalog.All["ryoshun-redirect"].Trigger == ReactionTriggers.TargetedByAction, "a targeted-by-action Reaction");
        Manifest(root, "celestial-wave-6", RyoshunsGuidance);

        var city = CardCatalog.Get(SecondCity);
        Check(city.Type == CardType.Region && Keywords(SecondCity) == "Unique" && city.Effect == "second-city-force", "The Second City");
        Abilities(SecondCity, "second-city-reveal", "second-city-reveal-raise");
        Manifest(root, "region-wave-8", SecondCity);
    }

    // ---- Events ---------------------------------------------------------------------------------------------------------------------------------
    private static void WillingSpiritsDelayedStrength()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var spirits = FaceDownIn(s, WillingSpirits, 1, 0);
        var before = s.Players[1].Provinces.Select(p => p.Strength).ToArray();
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        Eq(e.State.Cards[spirits.Id].Zone, Zone.EventInPlay, "revealed, it waits in play");
        Check(e.State.Players[1].Provinces.Select(p => p.Strength).SequenceEqual(before), "no strength yet: the owner's next turn has not begun");
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action && e.State.Players[1].TurnsTaken == s.Players[1].TurnsTaken + 2);
        Eq(e.State.Cards[spirits.Id].Zone, Zone.DynastyDiscard, "its effect happened and it was discarded");
        foreach (var province in e.State.Players[1].Provinces.Where(p => !p.Destroyed))
            Eq(province.Strength, before[province.Index] + 2, $"province {province.Index + 1} permanently +2");
        // Permanent: still there a turn later.
        var afterBonus = e.State.Players[1].Provinces.Select(p => p.Strength).ToArray();
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action && e.State.Players[1].TurnsTaken == s.Players[1].TurnsTaken + 3);
        foreach (var province in e.State.Players[1].Provinces.Where(p => !p.Destroyed)) Eq(province.Strength, afterBonus[province.Index], "unchanged afterwards");
    }

    private static void AssaultInTheJungleGrant()
    {
        // Reveal: it stays until the owner's second turn from now begins.
        var s0 = Table(Phase.Action, 0); s0.PriorityPlayerId = 0;
        var assault0 = FaceDownIn(s0, AssaultInTheJungle, 1, 0);
        var e0 = MatchEngine.FromTestState(s0);
        RunUntil(e0, () => e0.State.ActivePlayerId == 1 && e0.State.Phase == Phase.Action);
        Eq(e0.State.Cards[assault0.Id].Zone, Zone.EventInPlay, "it stays in play");
        Eq(e0.State.Cards[assault0.Id].ExpiresAtTurnsTaken, e0.State.Players[1].TurnsTaken + 2, "until the owner's second turn from now begins");

        // The granted ability, used by the OTHER player (all players have it): the performer's whole unit bows, the enemy card is destroyed.
        var s = Published(Table(Phase.Battle, 1));
        var assault = Add(s, AssaultInTheJungle, 0, Zone.EventInPlay); assault.ExpiresAtTurnsTaken = s.Players[0].TurnsTaken + 2;
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: attacker.Id);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var command = Opt(e, 1, "assault-jungle-destroy").Command! with { PerformerId = attacker.Id, TargetId = enemy.Id };
        Accept(e, command);
        Check(e.State.Cards[attacker.Id].Bowed && e.State.Cards[follower.Id].Bowed, "every unbowed card in the performer's unit bowed as the cost");
        Check(e.State.Cards[enemy.Id].Zone != Zone.Battlefield, "the enemy card without attachments was destroyed");

        // A defending Personality cannot perform it, and without the Event nobody has it at all.
        var s2 = Published(Table(Phase.Battle, 1)); s2.PriorityPlayerId = 0;
        var assault2 = Add(s2, AssaultInTheJungle, 1, Zone.EventInPlay); assault2.ExpiresAtTurnsTaken = s2.Players[1].TurnsTaken + 2;
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!Listed(e2, 0, "assault-jungle-destroy").Any(a => a.Enabled), "the Defender has no attacking performer");
        var e3 = Edit(e2, st => { st.Cards[assault2.Id].Zone = Zone.DynastyDiscard; st.PriorityPlayerId = 1; });
        Check(Listed(e3, 1, "assault-jungle-destroy").Length == 0, "not even listed while no Assault in the Jungle is in effect");
    }

    private static void BurningDreamsClauses()
    {
        // Reveal: the Favor's holder discards it; the Event stays for good.
        var s0 = Table(Phase.Action, 0); s0.PriorityPlayerId = 0; s0.FavorPlayerId = 0;
        var dreams0 = FaceDownIn(s0, BurningDreams, 1, 0);
        var e0 = MatchEngine.FromTestState(s0);
        RunUntil(e0, () => e0.State.ActivePlayerId == 1 && e0.State.Phase == Phase.Action);
        Check(e0.State.FavorPlayerId is null, "the Imperial Favor was discarded");
        Eq(e0.State.Cards[dreams0.Id].Zone, Zone.EventInPlay, "it stays in play (its Political ban lasts until the game ends)");

        // Negation: the Defender discards the Favor to move a Nonhuman attacker home -- taken, Favor spent, no movement.
        var s = Published(Table(Phase.Battle, 1)); s.PriorityPlayerId = 0; s.FavorPlayerId = 0;
        var dreams = Add(s, BurningDreams, 1, Zone.EventInPlay); dreams.AbilityUses["burning-dreams:until"] = s.Players[1].TurnsTaken + 2;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var nonhuman = Add(s, "training-nonhuman-personality", 1, Zone.Battlefield, province: 0);
        var human = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Opt(e, 0, "favor-battle", a => a.TargetIds!.Contains(nonhuman.Id)).Command! with { TargetId = nonhuman.Id });
        Check(e.State.FavorPlayerId is null, "the Favor was still discarded (a cost)");
        Eq(e.State.Cards[nonhuman.Id].Zone, Zone.Battlefield, "its effect on the Nonhuman was negated");
        // Against a human Personality it resolves.
        var e1 = MatchEngine.FromTestState(s);
        Accept(e1, Opt(e1, 0, "favor-battle", a => a.TargetIds!.Contains(human.Id)).Command! with { TargetId = human.Id });
        Eq(e1.State.Cards[human.Id].Zone, Zone.Home, "moved home");
        // After the owner's second turn from now has begun, the negation is over.
        var e1b = Edit(MatchEngine.FromTestState(s), st => st.Players[1].TurnsTaken += 2);
        Accept(e1b, Opt(e1b, 0, "favor-battle", a => a.TargetIds!.Contains(nonhuman.Id)).Command! with { TargetId = nonhuman.Id });
        Eq(e1b.State.Cards[nonhuman.Id].Zone, Zone.Home, "the bounded clause has ended: the Nonhuman moved home");

        // The Political ban: a Nonhuman may not perform a Political action (a Political card's own Open), nor bow to Lobby; a human may.
        var s2 = Published(Table(Phase.Action, 0)); s2.PriorityPlayerId = 0;
        Add(s2, BurningDreams, 1, Zone.EventInPlay);
        s2.Players[0].Honor = s2.Players[1].Honor + 5;
        var courtier = Add(s2, "training-seeds5-political-courtier", 0, Zone.Home);
        var courtier2 = Add(s2, "training-seeds5-political-courtier", 0, Zone.Home); GiveKeyword(courtier2, "Nonhuman");
        var nonhumanLobbyist = Add(s2, "training-nonhuman-personality", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Check(Listed(e2, 0, "training-bow-any").Any(a => a.Enabled && a.CardId == courtier.Id), "a human performs the Political action");
        Check(!Listed(e2, 0, "training-bow-any").Any(a => a.Enabled && a.CardId == courtier2.Id), "a Nonhuman may not");
        var lobby = Opt(e2, 0, "lobby").Command!;
        Check(e2.Execute(lobby with { TargetId = nonhumanLobbyist.Id }) is { Accepted: false }, "a Nonhuman may not perform the Lobby");
        var e2b = MatchEngine.FromTestState(s2);
        Accept(e2b, Opt(e2b, 0, "lobby").Command! with { TargetId = courtier.Id });
        Eq(e2b.State.FavorPlayerId, 0, "a human Lobbies");
    }

    // ---- Holdings -------------------------------------------------------------------------------------------------------------------------------
    private static void AgelessShrine_()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var shrine = Add(s, AgelessShrine, 0, Zone.Home);
        var nonhumanRecruit = Add(s, "training-nonhuman-personality", 0, Zone.Province, province: 0); s.Players[0].Provinces[0].CardId = nonhumanRecruit.Id;
        var humanRecruit = Add(s, "training-crane-samurai", 0, Zone.Province, province: 1); s.Players[0].Provinces[1].CardId = humanRecruit.Id;
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(shrine.Id, 0), 2, "2 Gold ordinarily");
        Eq(e.GoldProduced(shrine.Id, 0, new PaymentSubject(e.State.Cards[nonhumanRecruit.Id], PaymentKind.Recruit)), 3, "3 Gold when paying for a Nonhuman");
        Eq(e.GoldProduced(shrine.Id, 0, new PaymentSubject(e.State.Cards[humanRecruit.Id], PaymentKind.Recruit)), 2, "2 Gold for anyone else");

        // Open: a bowed Nonhuman and one of his bowed attachments straighten.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var shrine2 = Add(s2, AgelessShrine, 0, Zone.Home);
        var him = Add(s2, "training-nonhuman-personality", 1, Zone.Home, bowed: true);
        var item = Add(s2, CursedRelic, 1, Zone.Attached, bowed: true, parent: him.Id);
        var samurai = Add(s2, "training-crane-samurai", 0, Zone.Home, bowed: true); GiveKeyword(samurai, "Samurai");
        var samuraiItem = Add(s2, CursedRelic, 0, Zone.Attached, bowed: true, parent: samurai.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.Execute(Use(e2, 0, "ageless-shrine-straighten", shrine2.Id, selections: [Pick("target", him.Id)])) is { Accepted: false }, "a Nonhuman with an attachment: one of them must be named");
        var e2b = MatchEngine.FromTestState(s2);
        Accept(e2b, Use(e2b, 0, "ageless-shrine-straighten", shrine2.Id, selections: [Pick("target", him.Id), Pick("attachment", item.Id)]));
        Check(e2b.State.Cards[shrine2.Id].Bowed && !e2b.State.Cards[him.Id].Bowed && !e2b.State.Cards[item.Id].Bowed, "bowed as the cost; he and his attachment straightened");
        // A Samurai: straightened, but no attachment of his.
        var e2c = MatchEngine.FromTestState(s2);
        Check(e2c.Execute(Use(e2c, 0, "ageless-shrine-straighten", shrine2.Id, selections: [Pick("target", samurai.Id), Pick("attachment", samuraiItem.Id)])) is { Accepted: false }, "only a Nonhuman's attachment");
        Accept(e2c, Use(e2c, 0, "ageless-shrine-straighten", shrine2.Id, selections: [Pick("target", samurai.Id)]));
        Check(!e2c.State.Cards[samurai.Id].Bowed && e2c.State.Cards[samuraiItem.Id].Bowed, "the Samurai straightened, his Item did not");
        // Neither Nonhuman nor Samurai: not a legal target.
        var s4 = Table(Phase.Action, 0); s4.PriorityPlayerId = 0;
        var shrine4 = Add(s4, AgelessShrine, 0, Zone.Home);
        var neither = Add(s4, "training-lion-samurai", 1, Zone.Home, bowed: true); // no Nonhuman or Samurai keyword
        var e4 = MatchEngine.FromTestState(s4);
        Check(e4.Execute(Use(e4, 0, "ageless-shrine-straighten", shrine4.Id, selections: [Pick("target", neither.Id)])) is { Accepted: false }, "neither Nonhuman nor Samurai: not a target");
    }

    private static void BambooHarvestersExpDraw()
    {
        // The hold: bowed at the end of its controller's first turn, it stays bowed through the second turn and straightens in the third.
        var s = Table(Phase.Action, 0); s.Players[0].TurnsTaken = 1; s.Players[1].TurnsTaken = 0;
        var bamboo = Add(s, BambooExp, 0, Zone.Home, bowed: true);
        var control = Add(s, "training-farm", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.ActivePlayerId == 0 && e.State.Phase == Phase.Action && e.State.Players[0].TurnsTaken == 2);
        Check(e.State.Cards[bamboo.Id].Bowed && !e.State.Cards[control.Id].Bowed, "second turn: still bowed");
        RunUntil(e, () => e.State.ActivePlayerId == 0 && e.State.Phase == Phase.Action && e.State.Players[0].TurnsTaken == 3);
        Check(!e.State.Cards[bamboo.Id].Bowed, "third turn: it straightened");

        // The draw: at 5 cards it draws to 6 and must discard one of its controller's choice.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0; s2.Players[0].TurnsTaken = 3;
        var bamboo2 = Add(s2, BambooExp, 0, Zone.Home);
        for (var i = 0; i < 5; i++) Add(s2, "training-follower", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "bamboo-exp-draw", bamboo2.Id));
        Check(e2.State.Cards[bamboo2.Id].Bowed, "bowed as the cost");
        Eq(e2.State.Players[0].Hand.Count, 6, "drew to six");
        Eq(e2.State.PendingChoice?.Type, "forcedDiscard", "six or more: a discard is owed");
        Accept(e2, new GameCommand("forcedDiscard", 0, e2.Version, TargetId: e2.State.Players[0].Hand[0]));
        Eq(e2.State.Players[0].Hand.Count, 5, "discarded one");
        // Fewer: no discard.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0; s3.Players[0].TurnsTaken = 3;
        var bamboo3 = Add(s3, BambooExp, 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, "bamboo-exp-draw", bamboo3.Id));
        Eq(e3.State.Players[0].Hand.Count, 1, "drew one"); Check(e3.State.PendingChoice?.Type != "forcedDiscard", "no discard owed");
    }

    private static void BorderKeepExpCycle()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var keep = Add(s, BorderKeepExp, 0, Zone.Home);
        var a = Add(s, "training-follower", 0, Zone.Hand); var b = Add(s, "training-follower", 0, Zone.Hand); var c = Add(s, "training-follower", 0, Zone.Hand);
        var deckBefore = s.Players[0].FateDeck.Count;
        var e = MatchEngine.FromTestState(s);
        Check(e.Execute(Use(e, 0, "border-keep-exp-cycle", keep.Id, selections: [Pick("cards", a.Id, b.Id, c.Id, a.Id)])) is { Accepted: false }, "at most three, each once");
        Accept(e, Use(e, 0, "border-keep-exp-cycle", keep.Id, selections: [Pick("cards", a.Id, b.Id)]));
        var deck = e.State.Players[0].FateDeck;
        Check(deck.Count == deckBefore && deck[^2] == a.Id && deck[^1] == b.Id, "the two cards went to the bottom, in the order chosen, and two were drawn");
        Eq(e.State.Players[0].Hand.Count, 3, "two put back, two drawn");
        Check(e.State.Players[0].Hand.Contains(c.Id) && !e.State.Players[0].Hand.Contains(a.Id), "the kept card stays");
        Check(!e.State.Cards[keep.Id].Bowed, "no bow: it is not a cost");
        Check(!e.State.Events.Skip(e.State.Events.Count - 3).Any(ev => ev.Message.Contains("Training ashigaru")), "the cards put back are never named");
        // Once per game: not again, even on a later turn.
        var later = Edit(e, st => { st.TurnNumber += 2; st.Players[0].TurnsTaken += 1; });
        Check(later.Execute(Use(later, 0, "border-keep-exp-cycle", keep.Id, selections: [Pick("cards", c.Id)])) is { Accepted: false }, "once per game");
    }

    private static void ColonialHarborTireless()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var stronghold = s.Cards[s.Players[0].StrongholdId]; stronghold.Bowed = true;
        stronghold.GrantedAbilities.Add(new("training-bow-any", "test-fixture", GrantDuration.Permanent));
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!Listed(e, 0, "training-bow-any").Any(a => a.Enabled && a.CardId == stronghold.Id), "without Colonial Harbor a bowed Stronghold's abilities cannot be used");
        var harbor = Add(s, ColonialHarbor, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s);
        Eq(e2.GoldProduced(harbor.Id, 0), 5, "produces 5 Gold");
        Accept(e2, Listed(e2, 0, "training-bow-any").First(a => a.Enabled && a.CardId == stronghold.Id).Command! with { TargetId = enemy.Id });
        Check(e2.State.Cards[enemy.Id].Bowed, "the bowed Stronghold's ability was used (Tireless)");
        // Another player's Colonial Harbor gives nothing; nor does it reach a bowed Holding.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0;
        var stronghold3 = s3.Cards[s3.Players[0].StrongholdId]; stronghold3.Bowed = true;
        stronghold3.GrantedAbilities.Add(new("training-bow-any", "test-fixture", GrantDuration.Permanent));
        Add(s3, ColonialHarbor, 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!Listed(e3, 0, "training-bow-any").Any(a => a.Enabled && a.CardId == stronghold3.Id), "only its controller's Stronghold");
    }

    // ---- Items ----------------------------------------------------------------------------------------------------------------------------------
    private static void BlackPearlRecruit()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var wielder = Add(s, "training-crane-samurai", 0, Zone.Home);
        var pearl = Add(s, BlackPearl, 0, Zone.Attached, parent: wielder.Id);
        var dead = Add(s, "training-nonhuman-personality", 1, Zone.Dead);       // the opponent's: cost 3, 4 less for a Nonhuman -> 0
        var deadHuman = Add(s, "training-crane-samurai", 1, Zone.Dead);        // cost 5, no discount
        var e = MatchEngine.FromTestState(s);
        Eq(e.PersonalityChiForTest(wielder.Id), CardCatalog.Get("training-crane-samurai").Chi + 1, "+1C from the Pearl");
        var option = Opt(e, 0, "black-pearl-recruit");
        Check(option.TargetIds!.Contains(dead.Id) && option.TargetIds.Contains(deadHuman.Id), "either player's discard pile");
        Eq(option.TargetGold![dead.Id].Cost, 0, "a Nonhuman: 4 less, to no less than 0");
        Eq(option.TargetGold![deadHuman.Id].Cost, 5, "anyone else: his full recruit cost");
        Accept(e, option.Command! with { TargetId = dead.Id, SourceIds = [] });
        var entered = e.State.Cards[dead.Id];
        Check(entered.Zone == Zone.Home && entered.OwnerId == 0 && entered.OriginalOwnerId == 1, "in play at home under the acting player's control, its owner remembered");
        Eq(e.State.Cards[pearl.Id].Zone, Zone.FateDiscard, "the Pearl was destroyed as the cost");
    }

    private static void CursedRelicCosts()
    {
        var s = Table(Phase.Battle, 1);
        var wielder = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Chi 3
        var relic = Add(s, CursedRelic, 1, Zone.Attached, province: 0, parent: wielder.Id);
        var victimA = Add(s, "training-nonhuman-personality", 0, Zone.Battlefield, province: 0); // Force 2
        var victimB = Add(s, "training-nonhuman-personality", 0, Zone.Battlefield, province: 0);
        var victimC = Add(s, "training-nonhuman-personality", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(wielder.Id), CardCatalog.Get("training-lion-samurai").Force + 4, "+4F from the Relic");
        Accept(e, Offer(e, 1, "cursed-relic-attack", relic.Id, victimA.Id));
        Eq(e.PersonalityChiForTest(wielder.Id), 2, "-1C paid");
        Check(e.State.Cards[victimA.Id].Zone != Zone.Battlefield, "the Ranged 3 Attack destroyed a 2F target");
        e = Edit(e, st => st.PriorityPlayerId = 1);
        Accept(e, Offer(e, 1, "cursed-relic-attack", relic.Id, victimB.Id));
        Eq(e.PersonalityChiForTest(wielder.Id), 1, "any number of times per turn: -1C again");
        // The remove: bow the Relic and give him his last Chi -- he dies of 0 Chi, the target is still removed from the game.
        e = Edit(e, st => st.PriorityPlayerId = 1);
        Accept(e, Offer(e, 1, "cursed-relic-remove", relic.Id, victimC.Id));
        Eq(e.State.Cards[victimC.Id].Zone, Zone.RemovedFromGame, "removed from the game");
        Check(e.State.Cards[wielder.Id].Zone != Zone.Battlefield, "0 Chi: the wielder was destroyed");
        // A wielder with no Chi to give cannot pay.
        var s2 = Table(Phase.Battle, 1);
        var weak = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0); weak.Modifiers.Add(new("test-source", "test", "Chi", -3));
        var relic2 = Add(s2, CursedRelic, 1, Zone.Attached, province: 0, parent: weak.Id);
        Add(s2, "training-nonhuman-personality", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!Listed(e2, 1, "cursed-relic-attack").Any(a => a.Enabled && a.CardId == relic2.Id), "no Chi to lose: the cost cannot be paid");
    }

    // ---- Region ---------------------------------------------------------------------------------------------------------------------------------
    private static void SecondCityForceAndReveal()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var city = Add(s, SecondCity, 0, Zone.Region, province: 0); s.Players[0].Provinces[0].RegionId = city.Id;
        var courtier = Add(s, "training-courtier-personality", 0, Zone.Home);
        var magistrate = Add(s, "training-magistrate-personality", 0, Zone.Home);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var enemyCourtier = Add(s, "training-courtier-personality", 1, Zone.Home);
        var hidden = FaceDownIn(s, "training-crane-samurai", 1, 1);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(courtier.Id), CardCatalog.Get("training-courtier-personality").Force + 1, "a Courtier +1F");
        Eq(e.CardForceForTest(magistrate.Id), CardCatalog.Get("training-magistrate-personality").Force + 1, "a Magistrate +1F");
        Eq(e.CardForceForTest(plain.Id), CardCatalog.Get("training-crane-samurai").Force, "anyone else: no bonus");
        Eq(e.CardForceForTest(enemyCourtier.Id), CardCatalog.Get("training-courtier-personality").Force, "another player's Courtier: no bonus");
        var strength = e.State.Players[1].Provinces[1].Strength;
        GameCommand Reveal(MatchEngine engine, string ability, int owner, int province) => Opt(engine, 0, ability).Command! with { TargetPlayerId = owner, Province = province };
        var revealOption = Opt(e, 0, "second-city-reveal-raise", a => a.Label.EndsWith("an enemy Province"));
        Check(revealOption.Provinces!.Contains(1), "the enemy province holding a face-down card is offered");
        Accept(e, Reveal(e, "second-city-reveal-raise", 1, 1));
        Check(e.State.Cards[hidden.Id].FaceUp && e.State.Cards[city.Id].Bowed, "bowed; the face-down card is face-up");
        Eq(e.State.Players[1].Provinces[1].Strength, strength + 2, "the Province's strength +2");
        Check(!Listed(e, 0, "second-city-reveal").Any(a => a.Enabled), "one use: the plain reveal shares it");
        // The plain reveal: no strength.
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Reveal(e2, "second-city-reveal", 1, 1));
        Check(e2.State.Cards[hidden.Id].FaceUp, "turned face-up"); Eq(e2.State.Players[1].Provinces[1].Strength, strength, "no strength given");
        // A province whose card is face-up is refused, and the face-down card's identity never appears in the offer.
        var s3 = s; s3.Cards[hidden.Id].FaceUp = true;
        var e3 = MatchEngine.FromTestState(s3);
        Check(e3.Execute(Reveal(e3, "second-city-reveal", 1, 1)) is { Accepted: false }, "a face-up province card is not a target");
    }

    // ---- Spells ---------------------------------------------------------------------------------------------------------------------------------
    private static void PearlOfRageCount()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0); // 1F, 3C
        var rage = Add(s, PearlOfRage, 0, Zone.Attached, province: 0, parent: shugenja.Id);
        Add(s, BlackPearl, 0, Zone.FateDiscard);   // a Pearl in the controller's discard pile
        Add(s, BlackPearl, 1, Zone.FateDiscard);   // the opponent's: does not count
        var victim = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); victim.Modifiers.Add(new("test-source", "test", "Force", 1)); // 5F
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(shugenja.Id), 1 + 2, "+1F per Pearl: itself and the discarded Black Pearl");
        Eq(e.AttackStrengthForTest("pearl-of-rage-attack", rage.Id), 3 + 2, "Chi 3 plus 2 Pearls");
        Accept(e, Offer(e, 0, "pearl-of-rage-attack", rage.Id, victim.Id));
        Check(e.State.Cards[rage.Id].Bowed && e.State.Cards[victim.Id].Zone != Zone.Battlefield, "bowed; the Ranged 5 Attack destroyed a 5F target");
    }

    private static void MountainsPowerShield()
    {
        // The attacker's Ranged 3 Attack against a defending Earth Shugenja wielding The Mountain's Power: negated.
        MatchEngine Attack(string shugenjaDef, bool defending, out CardInstance shugenja)
        {
            // The Shugenja is always player 0's; he defends when player 1 is the active player (the Attacker), and attacks otherwise. Player 1 holds the Relic.
            var s = Table(Phase.Battle, defending ? 1 : 0); s.PriorityPlayerId = 1;
            var attackerSide = 1; var shugenjaSide = 0;
            var wielder = Add(s, "training-lion-samurai", attackerSide, Zone.Battlefield, province: 0);
            var relic = Add(s, CursedRelic, attackerSide, Zone.Attached, province: 0, parent: wielder.Id);
            shugenja = Add(s, shugenjaDef, shugenjaSide, Zone.Battlefield, province: 0);
            Add(s, MountainsPower, shugenjaSide, Zone.Attached, province: 0, parent: shugenja.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, attackerSide, "cursed-relic-attack", relic.Id, shugenja.Id));
            return e;
        }
        var shielded = Attack("training-seeds5-earth-alchemist", defending: true, out var earth);
        Eq(shielded.State.Cards[earth.Id].Zone, Zone.Battlefield, "a defending Earth wielder's destruction is negated");
        var attacking = Attack("training-seeds5-earth-alchemist", defending: false, out var earthAttacking);
        Check(attacking.State.Cards[earthAttacking.Id].Zone != Zone.Battlefield, "not while he is attacking");
        var notEarth = Attack("training-seeds5-air-shugenja", defending: true, out var air);
        Check(notEarth.State.Cards[air.Id].Zone != Zone.Battlefield, "not for a wielder who is not Earth");

        // Its Battle: bow an enemy card without attachments, gain 1 Honor.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var caster = Add(s2, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0);
        var spell = Add(s2, MountainsPower, 0, Zone.Attached, province: 0, parent: caster.Id);
        var enemy = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        var honor = Honor(e2, 0);
        Accept(e2, Offer(e2, 0, "mountains-power-bow", spell.Id, enemy.Id));
        Check(e2.State.Cards[spell.Id].Bowed && e2.State.Cards[enemy.Id].Bowed, "bowed itself and the enemy");
        Eq(Honor(e2, 0), honor + 1, "gained 1 Honor");
    }

    private static void ThundersWrathAttacks()
    {
        // Player 1 (the Attacker) wields it; player 0 defends with a followed Samurai and a bare Personality.
        (MatchState S, CardInstance Shugenja, CardInstance Spell, CardInstance Followed, CardInstance Follower, CardInstance Bare) Rig(string shugenjaDef)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            var shugenja = Add(s, shugenjaDef, 1, Zone.Battlefield, province: 0);
            var spell = Add(s, ThundersWrath, 1, Zone.Attached, province: 0, parent: shugenja.Id);
            var followed = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);           // 3F
            var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: followed.Id); // 2F
            var bare = Add(s, "training-nonhuman-personality", 0, Zone.Battlefield, province: 0);         // 2F
            return (s, shugenja, spell, followed, follower, bare);
        }
        // The bow: -4F.
        var r0 = Rig("training-thunder-shugenja");
        var e0 = MatchEngine.FromTestState(r0.S);
        Accept(e0, Offer(e0, 1, "thunders-wrath-bow", r0.Spell.Id, r0.Followed.Id));
        Check(e0.State.Cards[r0.Spell.Id].Bowed, "bowed as the cost");
        Eq(e0.CardForceForTest(r0.Followed.Id), 0, "-4F (3F, to no less than 0)");

        // The Attack at a Follower: Ranged 6; destroyed, and the Thunder wielder's second Ranged 4 Attack takes a target chosen now (the Personality it shielded).
        var r = Rig("training-thunder-shugenja");
        var e = MatchEngine.FromTestState(r.S);
        Eq(e.AttackStrengthForTest("thunders-wrath-attack", r.Shugenja.Id), 4, "Ranged 4 with no Follower target");
        Accept(e, Offer(e, 1, "thunders-wrath-attack", r.Spell.Id, r.Follower.Id, r.Shugenja.Id));
        Eq(e.State.Cards[r.Spell.Id].Zone, Zone.FateDiscard, "the Spell was destroyed as the cost");
        Check(e.State.Cards[r.Follower.Id].Zone != Zone.Attached, "the Follower was destroyed");
        Eq(e.State.PendingChoice?.Type, "sequencedTarget", "the second Attack's target is chosen now");
        var offered = e.QueryActions(1).Where(a => a.Type == "chooseSequencedTarget").Select(a => a.Command?.TargetId).ToArray();
        Check(offered.Contains(r.Followed.Id), "the Personality the Follower shielded is now a legal target");
        Accept(e, new GameCommand("chooseSequencedTarget", 1, e.Version, CardId: r.Spell.Id, TargetId: r.Followed.Id));
        Check(e.State.Cards[r.Followed.Id].Zone != Zone.Battlefield, "the second Ranged 4 Attack destroyed him (3F)");
        Check(e.State.PendingChoice is null, "the action finished");

        // A wielder who is not Thunder: no second Attack.
        var r2 = Rig("training-shugenja");
        var e2 = MatchEngine.FromTestState(r2.S);
        Accept(e2, Offer(e2, 1, "thunders-wrath-attack", r2.Spell.Id, r2.Follower.Id, r2.Shugenja.Id));
        Check(e2.State.Cards[r2.Follower.Id].Zone != Zone.Attached && e2.State.PendingChoice?.Type != "sequencedTarget", "the Follower died, but no second Attack");
        // A Personality target: Ranged 4, and no second Attack even when it dies.
        var r3 = Rig("training-thunder-shugenja");
        var e3 = MatchEngine.FromTestState(r3.S);
        Accept(e3, Offer(e3, 1, "thunders-wrath-attack", r3.Spell.Id, r3.Bare.Id, r3.Shugenja.Id));
        Check(e3.State.Cards[r3.Bare.Id].Zone != Zone.Battlefield && e3.State.PendingChoice?.Type != "sequencedTarget", "a Personality destroyed: no second Attack");

        // The first Attack waits for the Defender's Fallow Lands (-2): still 4 against a 2F Follower, destroyed after the window, then the second target is chosen.
        var r4 = Rig("training-thunder-shugenja");
        var fallow = Add(r4.S, "oracle-l5r-9865", 0, Zone.Region, province: 0); r4.S.Players[0].Provinces[0].RegionId = fallow.Id;
        var e4 = MatchEngine.FromTestState(r4.S);
        Accept(e4, Offer(e4, 1, "thunders-wrath-attack", r4.Spell.Id, r4.Follower.Id, r4.Shugenja.Id));
        Eq(e4.State.PendingChoice?.Type, "attackReaction", "the Attack waits for the Defender's reaction");
        Accept(e4, new GameCommand("useAbility", 0, e4.Version, CardId: fallow.Id, AbilityId: "fallow-lands-reaction"));
        for (var guard = 0; e4.State.PendingChoice is { Type: "attackReaction" } && guard < 6; guard++)
            Accept(e4, new GameCommand("declineReaction", e4.State.PriorityPlayerId, e4.Version));
        Check(e4.State.Cards[r4.Follower.Id].Zone != Zone.Attached, "the Follower was destroyed once the window closed");
        Eq(e4.State.PendingChoice?.Type, "sequencedTarget", "the deferred Attack still earns the second one");
        Accept(e4, new GameCommand("chooseSequencedTarget", 1, e4.Version, CardId: r4.Spell.Id, TargetId: r4.Bare.Id));
        Check(e4.State.Cards[r4.Bare.Id].Zone != Zone.Battlefield, "the second Attack destroyed its chosen target");
    }

    private static void WindOfTheMoon_()
    {
        // The trait: player 0's bowed Air Shugenja straightens in player 1's Straighten Phase; a non-Air wielder does not.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var air = Add(s, "training-seeds5-air-shugenja", 0, Zone.Home, bowed: true);
        Add(s, WindOfTheMoon, 0, Zone.Attached, parent: air.Id);
        var other = Add(s, "training-shugenja", 0, Zone.Home, bowed: true);
        Add(s, WindOfTheMoon, 0, Zone.Attached, parent: other.Id);
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        Check(!e.State.Cards[air.Id].Bowed, "the Air wielder straightened in the other player's Straighten Phase");
        Check(e.State.Cards[other.Id].Bowed, "a wielder who is not Air did not");

        // The Open: +1 Gold Cost (or +1PH) on a face-up Personality out of play; one use for both.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var caster = Add(s2, "training-seeds5-air-shugenja", 0, Zone.Home);
        var wind = Add(s2, WindOfTheMoon, 0, Zone.Attached, parent: caster.Id);
        var recruit = Add(s2, "training-crane-samurai", 1, Zone.Province, province: 0); s2.Players[1].Provinces[0].CardId = recruit.Id;
        var dead = Add(s2, "training-lion-samurai", 1, Zone.Dead);
        var inPlay = Add(s2, "training-lion-samurai", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var targets = Opt(e2, 0, "wind-moon-gold").TargetIds!;
        Check(targets.Contains(recruit.Id) && targets.Contains(dead.Id) && !targets.Contains(inPlay.Id), "a face-up Personality out of play, not one in play");
        var costBefore = e2.QuotePurchase(1, recruit.Id).Cost;
        Accept(e2, Offer(e2, 0, "wind-moon-gold", wind.Id, recruit.Id));
        Check(e2.State.Cards[caster.Id].Bowed, "the Shugenja bowed as the cost");
        Eq(e2.QuotePurchase(1, recruit.Id).Cost, costBefore + 1, "+1 Gold Cost");
        Check(!Listed(e2, 0, "wind-moon-honor").Any(a => a.Enabled), "one use for both choices");
        var e3 = MatchEngine.FromTestState(s2);
        var honorBefore = e3.PersonalHonorForTest(recruit.Id);
        Accept(e3, Offer(e3, 0, "wind-moon-honor", wind.Id, recruit.Id));
        Eq(e3.PersonalHonorForTest(recruit.Id), honorBefore + 1, "+1PH");
    }

    // ---- Celestial ------------------------------------------------------------------------------------------------------------------------------
    private static void RyoshunsGuidance_()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var ryoshun = Add(s, RyoshunsGuidance, 0, Zone.Celestial);
        var targeted = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var standIn = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var atHome = Add(s, "training-crane-samurai", 0, Zone.Home);
        var fragile = Add(s, "training-crane-samurai", 0, Zone.Home); fragile.Modifiers.Add(new("test-source", "test", "Chi", -3)); // 0C + 1 = 1C
        var enemyAtHome = Add(s, "training-lion-samurai", 1, Zone.Home);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var bv = Add(s, "training-bushido-virtue", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Eq(e.PersonalityChiForTest(targeted.Id), 4, "+1C (Chi 3)");
        Eq(e.PersonalityChiForTest(enemyAtHome.Id), 3, "another player's Personality: no bonus");
        Eq(e.PersonalityChiForTest(fragile.Id), 1, "kept alive by the +1C");
        // Involved in a duel: no bonus.
        var dueling = Edit(e, st => st.PendingDuel = new PendingDuel { ChallengerId = st.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield).Id, ChallengedId = targeted.Id });
        Eq(dueling.PersonalityChiForTest(targeted.Id), 3, "no bonus while involved in a duel");
        Eq(dueling.PersonalityChiForTest(standIn.Id), 4, "the bonus stays for everyone else");

        // The redirect: player 1's Battle action targets one of player 0's Personalities; Ryoshun's Guidance is discarded to send it to another at the same location.
        Accept(e, Offer(e, 1, "training-bow-at-battle", bv.Id, targeted.Id));
        Check(e.State.PendingChoice is not null, "a reaction window opened: " + e.State.PendingChoice?.Type + " / " + string.Join("; ", Options(e, 0, "ryoshun-redirect").Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        var offers = Listed(e, 0, "ryoshun-redirect").Where(o => o.Enabled).Select(o => o.Command!.TargetId).ToArray();
        Check(offers.SequenceEqual(new[] { standIn.Id }), "only another of his cards at the same location: " + string.Join(",", offers) + " | " + e.State.PendingChoice?.Type + " | " + string.Join("; ", Options(e, 0, "ryoshun-redirect").Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        Accept(e, Listed(e, 0, "ryoshun-redirect").First(o => o.Enabled).Command!);
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.Cards[standIn.Id].Bowed && !e.State.Cards[targeted.Id].Bowed, "the action now bowed the stand-in");
        Eq(e.State.Cards[ryoshun.Id].Zone, Zone.DynastyDiscard, "discarded as the cost");
        Eq(e.PersonalityChiForTest(targeted.Id), 3, "the +1C left with it");
        Check(e.State.Cards[fragile.Id].Zone != Zone.Home, "a Personality left at 0 Chi was destroyed");
        Check(e.State.Cards[atHome.Id].Zone == Zone.Home, "the others are unharmed");
    }
}
