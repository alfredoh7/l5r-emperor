using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// The Shadow's Embrace pass (issue #476), batch 5: the cards of spell-wave-22 and item-wave-14, driven through QueryActions, the commands it offers and the
// turn flow. Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the Lion), with training fixtures (and "test-fixture"
// keyword grants) standing in for the Personalities each card needs.
internal static class ShadowsEmbraceBatch5Tests
{
    private const string BloodOfThePreserver = "oracle-l5r-9875", CrabSword = "oracle-l5r-9876", CraneSword = "oracle-l5r-9877", PhoenixSword = "oracle-l5r-9878",
        StringOfTheColonies = "oracle-l5r-9880", TheVictor = "oracle-l5r-9881", VanquishersArmor = "oracle-l5r-9882", VanquishersBlades = "oracle-l5r-9883",
        BeseechSakkaku = "oracle-l5r-9884", ChannelTheMoon = "oracle-l5r-9885", OurAncestorsCall = "oracle-l5r-9886", PurifyTheSoul = "oracle-l5r-9887",
        SetTheFieldsAflame = "oracle-l5r-9888", ThundersStamina = "oracle-l5r-9891", TouchTheEmptiness = "oracle-l5r-9892", WardOfAir = "oracle-l5r-9893",
        YariOfAir = "oracle-l5r-9894", YourHeartsEnemy = "oracle-l5r-9895", EssenceOfUndeath = "oracle-l5r-2380", SecondCity = "oracle-l5r-8303";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static ActionOption[] Listed(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();
    private static ActionOption Opt(MatchEngine e, int player, string ability, Func<ActionOption, bool>? match = null)
    {
        var found = Listed(e, player, ability).FirstOrDefault(a => a.Enabled && (match?.Invoke(a) ?? true));
        Check(found is not null, $"{ability} for player {player}: " + string.Join("; ", Options(e, player, ability).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return found!;
    }
    private static bool Offered(MatchEngine e, int player, string ability) => Listed(e, player, ability).Any(a => a.Enabled);
    private static void DeclineWindows(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }
    private static void Gold(MatchState s, int owner, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", owner, Zone.Home); }

    public static void Run(TestRunner runner) => runner.Run("shadows-embrace-batch-5", [
        ("Published shape: exactly the fifteen wired cards are published across spell-wave-22 and item-wave-14, with their printed stats, keywords, Effects, ability ids, scan text and shipped art; the three blocked cards are not", PublishedShape),
        ("Celestial Sword of the Crab: attaches only to a Crab Clan Personality, gains 2 Honor as it enters play, +1F per Crab Clan Personality, and straightens its whole unit even while bowed", CrabSwordEquipAndStraighten),
        ("Celestial Sword of the Crane: its Political Open gives -4F, and discarding the Imperial Favor (optional, one shared use) also gains 1 Honor", CraneSwordFavor),
        ("Celestial Sword of the Phoenix: brings an honorably dead Phoenix Clan Personality into play at its location free, and removes him from the game before the turn ends", PhoenixSwordReturn),
        ("The String of the Colonies and Vanquisher's Armor: a bow for a Ranged 5 Attack, which cannot reach a Personality whose Armor gives Attacks targeting him -5 strength", StringAndArmor),
        ("The Victor: equipped, its Personality has Conqueror (and +4F)", VictorGrantsConqueror),
        ("Blood of the Preserver: each player chooses up to two Personalities at the battle; their units leave the game with their cards and tokens and return home after the next turn begins with three +1F Blood tokens", BloodOfThePreserverRemovesAndReturns),
        ("Beseech Sakkaku: bowing its Shugenja gains 1 Honor, or -- destroyed, if he is Earth -- Honor equal to the Spirit's Personal Honor", BeseechSakkakuHonor),
        ("Channel the Moon: +1F to its controller's opposed Nonhumans while its Shugenja is Shadowlands; its Open gives a Nonhuman a Melee Attack of his own Force for the turn, and loses 3 Honor", ChannelTheMoon_),
        ("Purify the Soul: 2 Gold less onto an Air Shugenja; its Reaction negates an action's grant of Shadowlands to a Personality of its controller's", PurifyTheSoul_),
        ("Set the Fields Aflame: +1F to an Air or Fire Shugenja; destroyed, it destroys a Terrain, locks a Region's abilities or bows a Holding", SetTheFieldsAflame_),
        ("Thunder's Stamina: +3F; while its Shugenja is Thunder, the battle's resolution does not bow his unit", ThundersStaminaNoBow),
        ("Touch the Emptiness: -2F, and with a Void Shugenja the target's next action is negated (costs paid)", TouchTheEmptinessNegatesNextAction),
        ("Ward of Air: destroyed to negate the destruction of its Shugenja (or, if he is Air, another of his controller's Personalities) by another player's Battle action; its Battle moves the unit home", WardOfAir_),
        ("Your Heart's Enemy: -4F to an enemy Personality with lower Chi; with an Air Shugenja, the target's actions may not target him", YourHeartsEnemy_),
    ]);

    // ---- Published shape ------------------------------------------------------------------------------------------------------------------------------
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
        var spells = new[] { BeseechSakkaku, ChannelTheMoon, PurifyTheSoul, SetTheFieldsAflame, ThundersStamina, TouchTheEmptiness, WardOfAir, YourHeartsEnemy };
        var items = new[] { BloodOfThePreserver, CrabSword, CraneSword, PhoenixSword, StringOfTheColonies, TheVictor, VanquishersArmor };
        Check(Ids(PublishedContent.SpellWave22).SequenceEqual(spells.OrderBy(i => i, StringComparer.Ordinal)), "spell-wave-22");
        Check(Ids(PublishedContent.ItemWave14).SequenceEqual(items.OrderBy(i => i, StringComparer.Ordinal)), "item-wave-14");
        foreach (var blocked in new[] { VanquishersBlades, OurAncestorsCall, YariOfAir })
            Check(!CardCatalog.Cards.ContainsKey(blocked), $"{blocked} is not published (its created card has no scan)");

        void Spell(string id, int cost, int focus, string keywords, string effect)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Spell && c.GoldCost == cost && c.Focus == focus && Keywords(id) == keywords && c.Effect == effect, $"{c.Name}: {c.Type} {c.GoldCost} {c.Focus} {Keywords(id)} {c.Effect}");
        }
        Spell(BeseechSakkaku, 1, 1, "Earth", "none"); Abilities(BeseechSakkaku, "beseech-sakkaku-honor", "beseech-sakkaku-destroy");
        Check(CardCatalog.Get(BeseechSakkaku).Text.StartsWith("Limited: Bow this Shugenja, you may destroy this card if he is Earth"), "Beseech Sakkaku's text is the scan's");
        Spell(ChannelTheMoon, 2, 2, "Maho", "channel-the-moon"); Abilities(ChannelTheMoon, "channel-moon-grant");
        Check(!CardCatalog.Cards.Values.Any(c => (c.Abilities ?? []).Contains("channel-moon-melee")), "the granted Melee Attack is printed on no card");
        Spell(PurifyTheSoul, 2, 4, "Air", "purify-the-soul"); Abilities(PurifyTheSoul, "purify-soul-negate");
        Spell(SetTheFieldsAflame, 0, 2, "Air,Fire", "set-fields-aflame"); Abilities(SetTheFieldsAflame, "set-fields-terrain", "set-fields-region", "set-fields-holding");
        Spell(ThundersStamina, 2, 1, "Thunder", "thunders-stamina"); Abilities(ThundersStamina);
        Spell(TouchTheEmptiness, 0, 3, "Void", "none"); Abilities(TouchTheEmptiness, "touch-emptiness-weaken");
        Check(CardCatalog.Get(TouchTheEmptiness).Text.Contains("negate the effects of the next action the target performs"), "Touch the Emptiness's text is the scan's");
        Spell(WardOfAir, 0, 4, "Air", "none"); Abilities(WardOfAir, "ward-of-air-negate", "ward-of-air-move-home");
        Check(AbilityCatalog.All["ward-of-air-negate"].Trigger == ReactionTriggers.TargetedByAction, "a targeted-by-action Reaction");
        Spell(YourHeartsEnemy, 0, 3, "Air,Fear", "none"); Abilities(YourHeartsEnemy, "hearts-enemy-weaken");
        Manifest(root, "spell-wave-22", spells);

        void Item(string id, int cost, int force, int chi, int focus, string keywords, string effect)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Item && c.GoldCost == cost && c.Force == force && c.Chi == chi && c.Focus == focus && Keywords(id) == keywords && c.Effect == effect,
                $"{c.Name}: {c.Type} {c.GoldCost} {c.Force}/{c.Chi} {c.Focus} {Keywords(id)} {c.Effect}");
        }
        Item(BloodOfThePreserver, 4, 0, 0, 4, "Unique", "none"); Abilities(BloodOfThePreserver, "blood-preserver-remove");
        Item(CrabSword, 8, 1, 1, 4, "Experienced,Unique,Weapon,Yuruginai", "celestial-sword-force-crab"); Abilities(CrabSword, "celestial-sword-crab-honor", "celestial-sword-crab-straighten");
        Item(CraneSword, 8, 1, 1, 4, "Experienced,Megumi,Unique,Weapon", "celestial-sword-force-crane"); Abilities(CraneSword, "celestial-sword-crane-honor", "celestial-sword-crane-weaken", "celestial-sword-crane-favor");
        Check(CardCatalog.Get(CraneSword).Text.Contains("Political Open: Target a Personality or Follower and you may discard the Imperial Favor"), "the Crane sword's text is the scan's");
        Item(PhoenixSword, 8, 1, 1, 4, "Experienced,Keitaku,Unique,Weapon", "celestial-sword-force-phoenix"); Abilities(PhoenixSword, "celestial-sword-phoenix-honor", "celestial-sword-phoenix-return");
        foreach (var (sword, clan) in new[] { (CrabSword, "Crab Clan"), (CraneSword, "Crane Clan"), (PhoenixSword, "Phoenix Clan") }) Eq(CardCatalog.Get(sword).AttachRequiresKeyword, clan);
        Item(StringOfTheColonies, 3, 0, 0, 3, "Weapon", "none"); Abilities(StringOfTheColonies, "string-colonies-attack");
        Item(TheVictor, 6, 4, 0, 2, "Armor", "none"); Abilities(TheVictor); Eq(CardCatalog.Get(TheVictor).AttachGrantsKeyword, "Conqueror");
        Item(VanquishersArmor, 7, 5, 0, 3, "Armor", "none"); Abilities(VanquishersArmor);
        var armor = CardCatalog.Get(VanquishersArmor);
        Check(armor.AttackDefenseKind == "Both" && armor.AttackDefenseAmount == -5 && armor.AttackDefenseScope == "Wielder", "Melee and Ranged Attacks targeting this Personality have -5 strength");
        Manifest(root, "item-wave-14", items);
    }

    // ---- Items ----------------------------------------------------------------------------------------------------------------------------------------
    private static void CrabSwordEquipAndStraighten()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var crab = Add(s, "training-merchant", 0, Zone.Home); GiveKeyword(crab, "Crab Clan");   // Crab Clan, 2F (a fixture prints no Clan keyword)
        Add(s, "training-merchant", 0, Zone.Home);                                                // a second Crab Clan Personality
        var crane = Add(s, "training-crane-samurai", 0, Zone.Home);
        var sword = Add(s, CrabSword, 0, Zone.Hand);
        Gold(s, 0, 4);
        var e = MatchEngine.FromTestState(s);
        var equip = e.QueryActions(0).Single(a => a.Type == "equip" && a.CardId == sword.Id);
        Check(equip.TargetIds!.Contains(crab.Id) && !equip.TargetIds.Contains(crane.Id), "will only attach to a Crab Clan Personality");
        var honor = Honor(e, 0);
        Accept(e, equip.Command! with { TargetId = crab.Id });
        Accept(e, Opt(e, 0, "celestial-sword-crab-honor").Command!);
        Eq(Honor(e, 0), honor + 2, "gained 2 Honor as it entered play");
        Eq(e.CardForceForTest(crab.Id), 2 + 1 + 2, "printed +1F, and +1F for each of the two Crab Clan Personalities");
        // Even if this card is bowed: straighten this unit.
        var follower = Add(s, "training-follower", 0, Zone.Attached, parent: crab.Id);
        e = Edit(e, st =>
        {
            st.Cards[follower.Id] = follower; follower.Bowed = true;
            st.Cards[crab.Id].Bowed = true; st.Cards[sword.Id].Bowed = true; st.PriorityPlayerId = 0; st.PendingChoice = null;
        });
        Accept(e, Opt(e, 0, "celestial-sword-crab-straighten").Command!);
        Check(!e.State.Cards[crab.Id].Bowed && !e.State.Cards[sword.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "the Personality and every card attached to him straightened");
    }

    private static void CraneSwordFavor()
    {
        MatchEngine Rig(bool favor, out CardInstance wielder, out CardInstance enemy)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            wielder = Add(s, "training-crane-samurai", 0, Zone.Home);
            Add(s, CraneSword, 0, Zone.Attached, parent: wielder.Id);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
            if (favor) s.FavorPlayerId = 0;
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(favor: true, out var wielder, out var enemy);
        Eq(e.CardForceForTest(wielder.Id), 3 + 1 + 1, "printed +1F and +1F for the one Crane Clan Personality");
        var honor = Honor(e, 0);
        Accept(e, Opt(e, 0, "celestial-sword-crane-favor", a => a.TargetIds!.Contains(enemy.Id)).Command! with { TargetId = enemy.Id });
        Eq(e.CardForceForTest(enemy.Id), 4 - 4, "-4F");
        Check(e.State.FavorPlayerId is null, "the Imperial Favor was discarded as the cost");
        Eq(Honor(e, 0), honor + 1, "and 1 Honor gained");
        Check(!Offered(e, 0, "celestial-sword-crane-weaken"), "one use: the plain half shares it");
        // Without the Favor: only the plain -4F, no Honor.
        var e2 = Rig(favor: false, out _, out var enemy2);
        Check(!Offered(e2, 0, "celestial-sword-crane-favor"), "no Favor to discard: that half is not offered");
        var honor2 = Honor(e2, 0);
        Accept(e2, Opt(e2, 0, "celestial-sword-crane-weaken", a => a.TargetIds!.Contains(enemy2.Id)).Command! with { TargetId = enemy2.Id });
        Eq(e2.CardForceForTest(enemy2.Id), 0, "-4F"); Eq(Honor(e2, 0), honor2, "no Honor");
    }

    private static void PhoenixSwordReturn()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var wielder = Add(s, "training-shugenja", 0, Zone.Home);                 // Phoenix Clan
        var sword = Add(s, PhoenixSword, 0, Zone.Attached, parent: wielder.Id);
        var dead = Add(s, "training-shugenja", 0, Zone.Dead);                    // an honorably dead Phoenix Clan Personality
        var dishonored = Add(s, "training-shugenja", 0, Zone.Dead); dishonored.Dishonorable = true;
        var crane = Add(s, "training-crane-samurai", 0, Zone.Dead);
        var enemyPhoenix = Add(s, "training-shugenja", 1, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        var option = Opt(e, 0, "celestial-sword-phoenix-return");
        Check(option.TargetIds!.Contains(dead.Id) && !option.TargetIds.Contains(dishonored.Id) && !option.TargetIds.Contains(crane.Id) && !option.TargetIds.Contains(enemyPhoenix.Id),
            "only the player's own honorably dead Phoenix Clan Personalities: " + string.Join(",", option.TargetIds!));
        var gold = e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed);
        Accept(e, option.Command! with { TargetId = dead.Id });
        Eq(e.State.Cards[dead.Id].Zone, Zone.Home, "in play at the sword's location (home)");
        Eq(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Bowed), gold, "ignoring costs: no Gold was produced");
        Eq(e.CardForceForTest(wielder.Id), 1 + 1 + 2, "+1F for each of the two Phoenix Clan Personalities now in play");
        // Before the turn ends, he is removed from the game.
        e = Edit(e, st => { st.Phase = Phase.Dynasty; st.PendingChoice = null; });
        RunUntil(e, () => e.State.ActivePlayerId == 1);
        Eq(e.State.Cards[dead.Id].Zone, Zone.RemovedFromGame, "removed from the game before the turn ended");
        Eq(e.State.Cards[sword.Id].Zone, Zone.Attached, "the sword stays with its wielder");

        // The Battle half brings him into play at the battlefield.
        var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0;
        var battler = Add(b, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var bSword = Add(b, PhoenixSword, 0, Zone.Attached, province: 0, parent: battler.Id);
        var bDead = Add(b, "training-shugenja", 0, Zone.Dead);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Offer(eb, 0, "celestial-sword-phoenix-return", bSword.Id, bDead.Id));
        Check(eb.State.Cards[bDead.Id] is { Zone: Zone.Battlefield, ProvinceIndex: 0 }, "at the battlefield where the sword stands");
    }

    private static void StringAndArmor()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var archer = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var bow = Add(s, StringOfTheColonies, 1, Zone.Attached, province: 0, parent: archer.Id);
        var armored = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);   // 3F + 5
        Add(s, VanquishersArmor, 0, Zone.Attached, province: 0, parent: armored.Id);
        var bare = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);      // 3F
        var strong = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Force(strong, 2); // 6F
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(armored.Id), 3 + 5, "the Armor's +5F");
        var option = Opt(e, 1, "string-colonies-attack");
        Check(option.TargetIds!.Contains(bare.Id) && !option.TargetIds.Contains(strong.Id), "Ranged 5: a 3F target, not a 6F one");
        Check(!option.TargetIds.Contains(armored.Id), "the Armor's -5 strength leaves the Attack no reach");
        Accept(e, option.Command! with { TargetId = bare.Id });
        DeclineWindows(e);
        Check(e.State.Cards[bow.Id].Bowed && e.State.Cards[bare.Id].Zone != Zone.Battlefield, "bowed; the target was destroyed");
    }

    private static void VictorGrantsConqueror()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var wielder = Add(s, "training-crane-samurai", 0, Zone.Home);
        var victor = Add(s, TheVictor, 0, Zone.Hand);
        Gold(s, 0, 3);
        var e = MatchEngine.FromTestState(s);
        Check(!e.State.Cards[wielder.Id].GrantedKeywords.Any(k => k.Keyword == "Conqueror"), "not a Conqueror before");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "equip" && a.CardId == victor.Id).Command! with { TargetId = wielder.Id });
        Check(e.State.Cards[wielder.Id].GrantedKeywords.Any(k => k.Keyword == "Conqueror" && k.Source == victor.Id), "This Personality has Conqueror");
        Eq(e.CardForceForTest(wielder.Id), 3 + 4, "+4F");
    }

    private static void BloodOfThePreserverRemovesAndReturns()
    {
        // Player 1 (the Attacker) uses it; player 0 defends.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var wielder = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var blood = Add(s, BloodOfThePreserver, 1, Zone.Attached, province: 0, parent: wielder.Id);
        var ally = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var allyFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: ally.Id);
        ally.Tokens.Add(new("tk-1", "Fire", "test", "Force", 1));
        var defenderA = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var defenderB = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Opt(e, 1, "blood-preserver-remove").Command!);
        DeclineWindows(e);
        Eq(e.State.Cards[blood.Id].Zone, Zone.RemovedFromGame, "removed from the game as the cost");
        GameCommand Pick(int player, string id) => e.QueryActions(player).Single(a => a.Type == "chooseDecider" && a.Command!.TargetId == id).Command!;
        GameCommand Done(int player) => e.QueryActions(player).Single(a => a.Type == "chooseDecider" && a.Command!.Choice == "done").Command!;
        Check(e.State.PendingChoice?.Type == "deciderChoice" && e.State.PendingChoice.PlayerId == 1, "the acting player chooses first");
        Accept(e, Pick(1, ally.Id)); Accept(e, Done(1));
        Check(e.State.PendingChoice?.Type == "deciderChoice" && e.State.PendingChoice.PlayerId == 0, "then the other player");
        Check(!e.QueryActions(0).Any(a => a.Type == "chooseDecider" && a.Command!.TargetId == ally.Id), "a Personality already chosen is not offered again");
        Accept(e, Pick(0, defenderA.Id)); Accept(e, Done(0));
        Check(e.State.PendingChoice is null || e.State.PendingChoice.Type != "deciderChoice", "both have chosen");
        foreach (var gone in new[] { ally, allyFollower, defenderA })
            Eq(e.State.Cards[gone.Id].Zone, Zone.RemovedFromGame, $"{gone.Id} is removed from the game");
        Eq(e.State.Cards[allyFollower.Id].AttachedTo, ally.Id, "the Follower stays attached to him");
        Check(e.State.Cards[ally.Id].Tokens.Any(t => t.Type == "Fire"), "his tokens stay on him");
        Eq(e.State.Cards[defenderB.Id].Zone, Zone.Battlefield, "an unchosen Personality stays");
        Eq(e.State.Cards[wielder.Id].Zone, Zone.Battlefield, "the wielder, not chosen, stays");

        // After the next turn begins: each unit enters play in its owner's home, with three +1F Blood tokens.
        e = Edit(e, st => { st.Phase = Phase.Dynasty; st.CurrentBattleProvince = null; st.UnresolvedBattlefields = []; st.PendingChoice = null; st.PriorityPlayerId = 1; });
        Eq(e.State.Cards[ally.Id].Zone, Zone.RemovedFromGame, "still away during this turn");
        RunUntil(e, () => e.State.ActivePlayerId == 0);
        var back = e.State.Cards[ally.Id];
        Eq(back.Zone, Zone.Home, "back in play at home"); Eq(back.OwnerId, 1, "his owner's");
        Check(e.State.Cards[allyFollower.Id] is { Zone: Zone.Attached } f && f.AttachedTo == ally.Id, "with his Follower still attached");
        Eq(back.Tokens.Count(t => t.Type == "Blood" && t.Stat == "Force" && t.Amount == 1), 3, "three +1F Blood tokens");
        Check(back.Tokens.Any(t => t.Type == "Fire"), "and the token he left with");
        Eq(e.State.Cards[defenderA.Id].Zone, Zone.Home, "the defender's unit is back in its owner's home too");
        Eq(e.CardForceForTest(defenderA.Id), 3 + 3, "+3F from the Blood tokens");
    }

    // ---- Spells ---------------------------------------------------------------------------------------------------------------------------------------
    private static void BeseechSakkakuHonor()
    {
        MatchEngine Rig(string shugenjaDef, out CardInstance shugenja, out CardInstance spell, out CardInstance spirit)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            shugenja = Add(s, shugenjaDef, 0, Zone.Home);
            spell = Add(s, BeseechSakkaku, 0, Zone.Attached, parent: shugenja.Id);
            spirit = Add(s, "training-crane-courtier", 0, Zone.Home); GiveKeyword(spirit, "Spirit"); // Personal Honor 3
            Add(s, "training-crane-samurai", 0, Zone.Home);                                           // not a Spirit
            return MatchEngine.FromTestState(s);
        }
        var e = Rig("training-seeds5-earth-alchemist", out var earth, out var spell, out var spirit);
        var destroy = Opt(e, 0, "beseech-sakkaku-destroy");
        Eq(destroy.TargetIds!.Length, 1, "only the Spirit is a target");
        var honor = Honor(e, 0);
        Accept(e, destroy.Command! with { TargetId = spirit.Id });
        Eq(Honor(e, 0), honor + 3, "Honor equal to the Spirit's Personal Honor");
        Check(e.State.Cards[earth.Id].Bowed && e.State.Cards[spell.Id].Zone == Zone.FateDiscard, "the Shugenja bowed and the Spell destroyed");
        Check(!Offered(e, 0, "beseech-sakkaku-honor"), "one use between the two halves");
        // Not destroyed: 1 Honor, the Spell stays.
        var e2 = Rig("training-seeds5-earth-alchemist", out _, out var spell2, out var spirit2);
        var honor2 = Honor(e2, 0);
        Accept(e2, Opt(e2, 0, "beseech-sakkaku-honor").Command! with { TargetId = spirit2.Id });
        Eq(Honor(e2, 0), honor2 + 1, "1 Honor"); Eq(e2.State.Cards[spell2.Id].Zone, Zone.Attached, "the Spell stays");
        // A Shugenja who is not Earth may not destroy it.
        var e3 = Rig("training-seeds5-air-shugenja", out _, out _, out _);
        Check(!Offered(e3, 0, "beseech-sakkaku-destroy") && Offered(e3, 0, "beseech-sakkaku-honor"), "not Earth: only the 1 Honor half");
    }

    private static void ChannelTheMoon_()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var shugenja = Add(s, "training-shugenja", 0, Zone.Home); GiveKeyword(shugenja, "Shadowlands");
        var channel = Add(s, ChannelTheMoon, 0, Zone.Attached, parent: shugenja.Id);
        var opposed = Add(s, "training-nonhuman-personality", 0, Zone.Battlefield, province: 0);   // 2F
        var atHome = Add(s, "training-nonhuman-personality", 0, Zone.Home);
        var enemyNonhuman = Add(s, "training-nonhuman-personality", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(opposed.Id), 3, "an opposed Nonhuman of its controller's: +1F");
        Eq(e.CardForceForTest(atHome.Id), 2, "not opposed: no bonus");
        Eq(e.CardForceForTest(enemyNonhuman.Id), 2, "another player's Nonhuman: no bonus");
        var plain = Edit(e, st => st.Cards[shugenja.Id].GrantedKeywords.Clear());
        Eq(plain.CardForceForTest(opposed.Id), 2, "no bonus while the Shugenja is not Shadowlands");

        // The Open: bow the Spell, the performing Nonhuman gets the Melee Attack for the turn, lose 3 Honor.
        var a = Table(Phase.Action, 0); a.PriorityPlayerId = 0;
        var caster = Add(a, "training-shugenja", 0, Zone.Home);
        var spell = Add(a, ChannelTheMoon, 0, Zone.Attached, parent: caster.Id);
        var beast = Add(a, "training-nonhuman-personality", 0, Zone.Home); Force(beast, 2); // 4F
        Add(a, "training-crane-samurai", 0, Zone.Home);                                       // not Nonhuman: never a performer
        var ea = MatchEngine.FromTestState(a);
        var grant = Opt(ea, 0, "channel-moon-grant");
        Eq(grant.Command!.PerformerId, beast.Id, "the performing Nonhuman");
        Check(Listed(ea, 0, "channel-moon-grant").All(o => o.Command?.PerformerId is null || o.Command.PerformerId == beast.Id), "only a Nonhuman performs it");
        var honor = Honor(ea, 0);
        Accept(ea, grant.Command!);
        Eq(Honor(ea, 0), honor - 3, "lose 3 Honor");
        Check(ea.State.Cards[spell.Id].Bowed && ea.State.Cards[beast.Id].GrantedAbilities.Any(g => g.AbilityId == "channel-moon-melee"), "the Spell bowed; he has the ability");
        // In a battle this turn: a Melee Attack with strength equal to his Force.
        var eb = Edit(ea, st =>
        {
            st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; st.PriorityPlayerId = 0;
            st.Cards[beast.Id].Zone = Zone.Battlefield; st.Cards[beast.Id].ProvinceIndex = 0;
            st.Cards["foe"] = new CardInstance { Id = "foe", DefinitionId = "training-lion-samurai", OwnerId = 1, Zone = Zone.Battlefield, ProvinceIndex = 0, FaceUp = true }; // 4F
        });
        Eq(eb.AttackStrengthForTest("channel-moon-melee", beast.Id), 4, "strength = his Force");
        Accept(eb, Offer(eb, 0, "channel-moon-melee", beast.Id, "foe"));
        DeclineWindows(eb);
        Check(eb.State.Cards["foe"].Zone != Zone.Battlefield, "the 4F target was destroyed");
    }

    private static void PurifyTheSoul_()
    {
        // Equip (Battle): 2 Gold less onto an Air Shugenja.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var air = Add(s, "training-seeds5-air-shugenja", 0, Zone.Battlefield, province: 0);
        var earth = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var purify = Add(s, PurifyTheSoul, 0, Zone.Hand);
        foreach (var player in s.Players) s.Cards[player.StrongholdId].DefinitionId = "oracle-l5r-1218"; // Equip (Battle) is a player ability: a published Stronghold
        var e = MatchEngine.FromTestState(s);
        GoldQuote? Quote(string performer)
        {
            var command = Listed(e, 0, "equip-battle").First(o => o.Command?.PerformerId == performer).Command! with { TargetId = purify.Id };
            return e.QuoteAbilityGold(command);
        }
        Eq(Quote(air.Id)!.Cost, 0, "an Air Shugenja: 2 less");
        Eq(Quote(earth.Id)!.Cost, 2, "anyone else: the printed 2");

        // The Reaction: Essence of Undeath would permanently give the Personality it brings back Shadowlands and Undead; Purify the Soul negates the Shadowlands.
        MatchEngine Rig(bool withPurify, out CardInstance dead, out CardInstance essence)
        {
            var a = Table(Phase.Action, 0); a.PriorityPlayerId = 0;
            var maho = Add(a, "training-shugenja", 0, Zone.Home);
            essence = Add(a, EssenceOfUndeath, 0, Zone.Attached, parent: maho.Id);
            if (withPurify) { var guard = Add(a, "training-seeds5-air-shugenja", 0, Zone.Home); Add(a, PurifyTheSoul, 0, Zone.Attached, parent: guard.Id); }
            dead = Add(a, "training-crane-samurai", 0, Zone.Dead);
            Gold(a, 0, 3);
            return MatchEngine.FromTestState(a);
        }
        var ep = Rig(true, out var deadP, out var essenceP);
        Accept(ep, Offer(ep, 0, "essence-of-undeath-recruit", essenceP.Id, deadP.Id));
        Check(ep.State.PendingChoice?.Type == "beforeActionReaction", "a before-action window opened for Purify the Soul: " + ep.State.PendingChoice?.Type);
        Accept(ep, Opt(ep, 0, "purify-soul-negate").Command!);
        DeclineWindows(ep);
        var back = ep.State.Cards[deadP.Id];
        Check(back.Zone == Zone.Home, "he came back into play");
        Check(!back.GrantedKeywords.Any(k => k.Keyword == "Shadowlands") && back.GrantedKeywords.Any(k => k.Keyword == "Undead"), "Shadowlands negated, Undead still given");
        var ew = Rig(false, out var deadW, out var essenceW);
        Accept(ew, Offer(ew, 0, "essence-of-undeath-recruit", essenceW.Id, deadW.Id));
        DeclineWindows(ew);
        Check(ew.State.Cards[deadW.Id].GrantedKeywords.Any(k => k.Keyword == "Shadowlands"), "without it, Shadowlands is given");
    }

    private static void SetTheFieldsAflame_()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var air = Add(s, "training-seeds5-air-shugenja", 0, Zone.Battlefield, province: 0);    // 2F
        var spell = Add(s, SetTheFieldsAflame, 0, Zone.Attached, province: 0, parent: air.Id);
        var earth = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Home);
        Add(s, SetTheFieldsAflame, 0, Zone.Attached, parent: earth.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var terrain = Add(s, "training-before-resolution-boost-terrain", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(air.Id), 3, "an Air Shugenja: +1F");
        Eq(e.CardForceForTest(earth.Id), 2, "neither Air nor Fire: no bonus");
        Accept(e, Offer(e, 0, "set-fields-terrain", spell.Id, terrain.Id));
        DeclineWindows(e);
        Check(e.State.Cards[terrain.Id].Zone != Zone.Battlefield && e.State.Cards[spell.Id].Zone == Zone.FateDiscard, "the Terrain destroyed; the Spell destroyed as the cost");

        // Open: a Region's abilities may not be used. Limited: bow a Holding.
        var a = Table(Phase.Action, 0); a.PriorityPlayerId = 0;
        var caster = Add(a, "training-seeds5-air-shugenja", 0, Zone.Home);
        var regionSpell = Add(a, SetTheFieldsAflame, 0, Zone.Attached, parent: caster.Id);
        var caster2 = Add(a, "training-seeds5-air-shugenja", 0, Zone.Home);
        var holdingSpell = Add(a, SetTheFieldsAflame, 0, Zone.Attached, parent: caster2.Id);
        var city = Add(a, SecondCity, 1, Zone.Region, province: 0); a.Players[1].Provinces[0].RegionId = city.Id;
        var enemyHolding = Add(a, "training-marketplace", 1, Zone.Home);
        var ea = MatchEngine.FromTestState(a);
        Accept(ea, Offer(ea, 0, "set-fields-region", regionSpell.Id, city.Id));
        DeclineWindows(ea);
        var locked = Edit(ea, st => { st.PriorityPlayerId = 1; st.PendingChoice = null; });
        Check(!Offered(locked, 1, "second-city-reveal") && !Offered(locked, 1, "second-city-reveal-raise"), "the Region's abilities may not be used this turn");
        ea = Edit(ea, st => { st.PriorityPlayerId = 0; st.PendingChoice = null; });
        Accept(ea, Offer(ea, 0, "set-fields-holding", holdingSpell.Id, enemyHolding.Id));
        DeclineWindows(ea);
        Check(ea.State.Cards[enemyHolding.Id].Bowed && ea.State.Cards[holdingSpell.Id].Zone == Zone.FateDiscard, "the Holding bowed; the Spell destroyed");
    }

    private static void ThundersStaminaNoBow()
    {
        MatchEngine Battle(string shugenjaDef, out CardInstance shugenja, out CardInstance follower)
        {
            // Player 1 attacks alone at province 0 with a Shugenja carrying the Spell and a Follower; nobody defends, so he wins.
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            shugenja = Add(s, shugenjaDef, 1, Zone.Battlefield, province: 0);
            Add(s, ThundersStamina, 1, Zone.Attached, province: 0, parent: shugenja.Id);
            follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: shugenja.Id);
            return MatchEngine.FromTestState(s);
        }
        var e = Battle("training-thunder-shugenja", out var thunder, out var follower);
        Eq(e.CardForceForTest(thunder.Id), 2 + 3, "This Shugenja has +3F");
        var id = thunder.Id;
        RunUntil(e, () => e.State.Cards[id].Zone == Zone.Home);
        Check(!e.State.Cards[thunder.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "a Thunder Shugenja's unit is not bowed by the resolution");
        var e2 = Battle("training-shugenja", out var plain, out var follower2);
        var id2 = plain.Id;
        RunUntil(e2, () => e2.State.Cards[id2].Zone == Zone.Home);
        Check(e2.State.Cards[plain.Id].Bowed && e2.State.Cards[follower2.Id].Bowed, "not Thunder: the unit bows as usual");
    }

    private static void TouchTheEmptinessNegatesNextAction()
    {
        MatchEngine Rig(string shugenjaDef, out CardInstance spell, out CardInstance archer, out CardInstance bow, out CardInstance victim)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            var shugenja = Add(s, shugenjaDef, 0, Zone.Battlefield, province: 0);
            spell = Add(s, TouchTheEmptiness, 0, Zone.Attached, province: 0, parent: shugenja.Id);
            victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);  // 3F
            archer = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);   // 4F
            bow = Add(s, StringOfTheColonies, 1, Zone.Attached, province: 0, parent: archer.Id);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig("training-seeds5-void-shugenja", out var spell, out var archer, out var bow, out var victim);
        Accept(e, Offer(e, 0, "touch-emptiness-weaken", spell.Id, archer.Id));
        DeclineWindows(e);
        Eq(e.CardForceForTest(archer.Id), 4 - 2, "-2F");
        Check(e.State.Cards[spell.Id].Bowed, "bowed as the cost");
        // The archer's next action (the bow's Ranged 5 Attack, performed through him) is negated: its cost is paid, the target survives.
        e = Edit(e, st => { st.PriorityPlayerId = 1; st.PendingChoice = null; });
        Accept(e, Offer(e, 1, "string-colonies-attack", bow.Id, victim.Id));
        DeclineWindows(e);
        Check(e.State.Cards[bow.Id].Bowed, "the cost was paid");
        Eq(e.State.Cards[victim.Id].Zone, Zone.Battlefield, "its effects were negated: the target survives");
        // Not Void: only the -2F.
        var e2 = Rig("training-seeds5-air-shugenja", out var spell2, out var archer2, out var bow2, out var victim2);
        Accept(e2, Offer(e2, 0, "touch-emptiness-weaken", spell2.Id, archer2.Id));
        DeclineWindows(e2);
        e2 = Edit(e2, st => { st.PriorityPlayerId = 1; st.PendingChoice = null; });
        Accept(e2, Offer(e2, 1, "string-colonies-attack", bow2.Id, victim2.Id));
        DeclineWindows(e2);
        Check(e2.State.Cards[victim2.Id].Zone != Zone.Battlefield, "not Void: the next action resolves");
    }

    private static void WardOfAir_()
    {
        MatchEngine Rig(string shugenjaDef, out CardInstance shugenja, out CardInstance ward, out CardInstance other, out CardInstance bow)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            shugenja = Add(s, shugenjaDef, 0, Zone.Battlefield, province: 0);                 // 2F
            ward = Add(s, WardOfAir, 0, Zone.Attached, province: 0, parent: shugenja.Id);
            other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);       // 3F
            var archer = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            bow = Add(s, StringOfTheColonies, 1, Zone.Attached, province: 0, parent: archer.Id);
            return MatchEngine.FromTestState(s);
        }
        // Another player's Battle action (a Ranged Attack) targets the Shugenja himself: destroying Ward of Air negates his destruction.
        var e = Rig("training-seeds5-earth-alchemist", out var shugenja, out var ward, out var other, out var bow);
        Accept(e, Offer(e, 1, "string-colonies-attack", bow.Id, shugenja.Id));
        Check(e.State.PendingChoice?.Type == "attackReaction", "the Attack's reaction window is open");
        Accept(e, Opt(e, 0, "ward-of-air-negate").Command!);
        DeclineWindows(e);
        Eq(e.State.Cards[shugenja.Id].Zone, Zone.Battlefield, "his destruction was negated");
        Eq(e.State.Cards[ward.Id].Zone, Zone.FateDiscard, "Ward of Air was destroyed as the cost");
        // Another of his Personalities: only while the Shugenja is Air.
        var e2 = Rig("training-seeds5-earth-alchemist", out _, out _, out var other2, out var bow2);
        Accept(e2, Offer(e2, 1, "string-colonies-attack", bow2.Id, other2.Id));
        Check(!Offered(e2, 0, "ward-of-air-negate"), "not Air: another Personality is not protected");
        DeclineWindows(e2);
        Check(e2.State.Cards[other2.Id].Zone != Zone.Battlefield, "and he was destroyed");
        var e3 = Rig("training-seeds5-air-shugenja", out _, out _, out var other3, out var bow3);
        Accept(e3, Offer(e3, 1, "string-colonies-attack", bow3.Id, other3.Id));
        Accept(e3, Opt(e3, 0, "ward-of-air-negate").Command!);
        DeclineWindows(e3);
        Eq(e3.State.Cards[other3.Id].Zone, Zone.Battlefield, "an Air Shugenja protects any Personality of his controller's");
        // Battle: move this unit home.
        var e4 = Rig("training-seeds5-air-shugenja", out var mover, out var ward4, out _, out _);
        e4 = Edit(e4, st => st.PriorityPlayerId = 0);
        Accept(e4, Opt(e4, 0, "ward-of-air-move-home").Command!);
        DeclineWindows(e4);
        Check(e4.State.Cards[mover.Id].Zone == Zone.Home && e4.State.Cards[ward4.Id].Zone == Zone.Attached, "the Shugenja's unit moved home");
    }

    private static void YourHeartsEnemy_()
    {
        MatchEngine Rig(string shugenjaDef, out CardInstance shugenja, out CardInstance spell, out CardInstance weak, out CardInstance peer, out CardInstance bow)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            shugenja = Add(s, shugenjaDef, 0, Zone.Battlefield, province: 0);                 // Chi 3
            spell = Add(s, YourHeartsEnemy, 0, Zone.Attached, province: 0, parent: shugenja.Id);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            weak = Add(s, "training-nonhuman-personality", 1, Zone.Battlefield, province: 0);  // Chi 2
            bow = Add(s, StringOfTheColonies, 1, Zone.Attached, province: 0, parent: weak.Id);
            peer = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);           // Chi 3
            return MatchEngine.FromTestState(s);
        }
        var e = Rig("training-seeds5-air-shugenja", out var shugenja, out var spell, out var weak, out var peer, out var bow);
        var option = Opt(e, 0, "hearts-enemy-weaken");
        Check(option.TargetIds!.Contains(weak.Id) && !option.TargetIds.Contains(peer.Id), "only an enemy Personality with lower Chi than the Shugenja");
        Accept(e, option.Command! with { TargetId = weak.Id });
        DeclineWindows(e);
        Eq(e.CardForceForTest(weak.Id), 0, "-4F (2F, to no less than 0)");
        e = Edit(e, st => { st.PriorityPlayerId = 1; st.PendingChoice = null; });
        var attack = Opt(e, 1, "string-colonies-attack");
        Check(!attack.TargetIds!.Contains(shugenja.Id), "an Air Shugenja: the target's actions may not target him");
        // Not Air: the target's actions may target him.
        var e2 = Rig("training-seeds5-earth-alchemist", out var shugenja2, out _, out var weak2, out _, out _);
        Accept(e2, Offer(e2, 0, "hearts-enemy-weaken", Opt(e2, 0, "hearts-enemy-weaken").CardId!, weak2.Id));
        DeclineWindows(e2);
        e2 = Edit(e2, st => { st.PriorityPlayerId = 1; st.PendingChoice = null; });
        Check(Opt(e2, 1, "string-colonies-attack").TargetIds!.Contains(shugenja2.Id), "not Air: no bar");
    }
}
