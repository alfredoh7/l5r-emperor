using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Seeds of Decay pass (issue #453), batch 5: item-wave-11, spell-wave-18 and follower-wave-25, and the three general pieces behind them --
// after-command traits ("After this card bows/attaches", SeedsOfDecayBatch5.cs), "from other players' cards/actions" (Aka-beko, Peace of the
// Kami) and the keyword-conditional "may perform this card's Battle action even if it is in your home" (SourceUnitMayBeAwayIfWielder).
internal static class SeedsOfDecayBatch5Tests
{
    private const string OYoroi = "oracle-l5r-10107", Ambushers = "oracle-l5r-10108", AkaBeko = "oracle-l5r-10110", Blades = "oracle-l5r-10111",
        MantisSword = "oracle-l5r-10113", ScorpionSword = "oracle-l5r-10114", Hato = "oracle-l5r-10115", Kama = "oracle-l5r-10116", KiUso = "oracle-l5r-10117",
        Moksha = "oracle-l5r-10121", Okesa = "oracle-l5r-10122", Sandalwood = "oracle-l5r-10123", Extend = "oracle-l5r-10126", Immolation = "oracle-l5r-10127",
        Beasts = "oracle-l5r-10128", Wrath = "oracle-l5r-10129", Peace = "oracle-l5r-10130", Scent = "oracle-l5r-10131", Strength = "oracle-l5r-10134";

    public static void Run(TestRunner runner) => runner.Run("seeds-batch-5", [
        ("Published shape: item-wave-11, spell-wave-18 and follower-wave-25 carry the printed stats, keywords, ability ids, Effects and shipped art", PublishedShape),
        ("O-Yoroi Troops: after another player's action targets a card in its unit, its Reaction negates the action's effects on that card", OYoroiTroops),
        ("Ronin Ambushers: after it bows it straightens and, without a Terrain, holds until the phase ends; with a Terrain no hold; the hold is released when the phase changes", RoninAmbushers),
        ("Aka-beko: a new Force penalty from another player's action is not applied to its wielder, and the ones already on him from other players' cards are purged when it attaches", AkaBekoNegates),
        ("Blades of the Black Dragon: a Melee Attack with the wielder's live Force, bowing him unless he is Nonhuman", BladesOfTheBlackDragon),
        ("Celestial Sword of the Mantis - exp: +1F per Mantis Personality, the entry Honor, and one Battle action with two halves sharing one use, priced by the Gold paid", CelestialSwordOfTheMantis),
        ("Celestial Sword of the Scorpion - exp: only attaches to a Scorpion; bows a bare enemy card, the optional province discard, and the conditional destroy (Personality discarded or a Courtier controlled)", CelestialSwordOfTheScorpion),
        ("Hato-gurama: may not be chosen for a hand discard while other cards are in hand, and each copy lowers the Fate deck minimum by one", HatoGurama),
        ("Kama: attaches beside one Weapon (a second Weapon without Kensai), dishonors a non-Mantis wielder on attaching and not a Mantis one", KamaTraits),
        ("Ki-Uso: a Political action its wielder performs cannot be negated; a non-Political one still can", KiUsoProtects),
        ("Moksha Patam: a +1F token after an action whose effects gained a player Honor; its Battle action moves a Personality home and negates his movement back while the Item is at the battlefield", MokshaPatam),
        ("Okesa-goma: bow it to move a Nonhuman or Shadowlands Personality home; a plain Personality is not a legal target", OkesaGoma),
        ("Sandalwood Incense: the Open action puts it on top of the Fate deck and straightens its wielder; its Focus Effect offers 2 Honor or a draw only when the focusing player's Personality won", SandalwoodIncense),
        ("Immolation: +3F, or +4F while the Shugenja is Fire", ImmolationForce),
        ("Master of Beasts: even away from the battle, bows the Shugenja to straighten one or two Shadowlands Nonhumans; a Human Shadowlands is no candidate", MasterOfBeasts),
        ("Nature's Wrath: moves a Nonhuman Personality to the battlefield if he would be opposed, straightening him; unopposed, nothing moves", NaturesWrath),
        ("Peace of the Kami: usable from home while the Shugenja is Water; purges other players' effects, keeps its controller's own, and straightens a Spirit", PeaceOfTheKami),
        ("Scent of Dishonor: an Air wielder is not bowed, the Spell is destroyed as its cost, the dishonorable target is bowed; an honorable plain Personality is illegal", ScentOfDishonor),
        ("Extend the Soul's Boundaries: enters paying 1 less per Ring; its Battle action bows the wielder, destroys the Spell and a non-Void wielder, then the target's controller shuffles him away or concedes two additional Battle actions", ExtendTheSoulsBoundaries),
        ("The Earth's Strength: straightens an Earth or Water Shugenja (1 Honor if an opposed Alchemist) and its Reaction negates a defending Personality's movement from another player's action", TheEarthsStrength),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void Decline(MatchEngine e) => Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Type == "declineReaction" && a.Enabled).Command!);
    private static void CloseWindows(MatchEngine e) { for (var guard = 0; guard < 10 && e.State.PendingChoice is { Type: "beforeActionReaction" or "entryReaction" }; guard++) Decline(e); }
    private static int ForceOf(MatchEngine e, CardInstance c) => e.GetView(0).Players.SelectMany(p => p.Home.Concat(p.Army)).Single(x => x.Id == c.Id).Force;
    private static void ArtShipped(string id)
    {
        Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), id + ": art is shipped");
        Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
    }
    private static GameCommand EquipCommand(MatchEngine e, int player, CardInstance item, CardInstance host) =>
        new("equip", player, e.Version, CardId: item.Id, SourceIds: CardCatalog.Get(item.DefinitionId).GoldCost == 0 ? [] : e.PaymentOptions(player, CardCatalog.Get(item.DefinitionId).GoldCost).First().SourceIds, TargetId: host.Id);

    private static void PublishedShape()
    {
        void Item(string id, string name, int cost, int force, int chi, int focus, string[] keywords, string[] abilities, string effect)
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == CardType.Item && c.GoldCost == cost && c.Force == force && c.Chi == chi && c.Focus == focus, $"{name}: printed box");
            Check((c.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(keywords.OrderBy(k => k, StringComparer.Ordinal)), $"{name}: keywords " + string.Join(",", c.Keywords ?? []));
            Check((c.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities " + string.Join(",", c.Abilities ?? []));
            Eq(c.Effect, effect, name + ": Effect");
            ArtShipped(id);
        }
        Item(AkaBeko, "Aka-beko", 0, 1, 0, 3, [], [], "aka-beko-negate-penalties");
        Item(Blades, "Blades of the Black Dragon", 4, 3, 1, 3, ["Unique", "Weapon"], ["blades-of-black-dragon-attack"], "none");
        Item(MantisSword, "Celestial Sword of the Mantis", 8, 1, 1, 4, ["Unique", "Weapon", "Experienced", "Nobori Raiu"], ["celestial-sword-mantis-honor", "celestial-sword-mantis-force", "celestial-sword-mantis-attack"], "celestial-sword-force-mantis");
        Eq(CardCatalog.Get(MantisSword).AttachRequiresKeyword, "Mantis Clan");
        Item(ScorpionSword, "Celestial Sword of the Scorpion", 8, 1, 1, 4, ["Unique", "Weapon", "Experienced", "Churetsu"], ["celestial-sword-scorpion-lose", "celestial-sword-scorpion-bow"], "celestial-sword-force-scorpion");
        Eq(CardCatalog.Get(ScorpionSword).AttachRequiresKeyword, "Scorpion Clan");
        Item(Hato, "Hato-gurama", 1, 0, 0, 3, [], [], "hato-gurama");
        Item(Kama, "Kama", 1, 3, 0, 1, ["Weapon"], [], "kama-dishonor-unless-mantis");
        Item(KiUso, "Ki-Uso", 0, 0, 0, 2, [], [], "ki-uso-protect-political");
        Item(Moksha, "Moksha Patam", 2, 0, 0, 3, [], ["moksha-patam-move"], "moksha-patam-token");
        Item(Okesa, "Okesa-goma", 0, 0, 0, 3, [], ["okesa-goma-move"], "none");
        Item(Sandalwood, "Sandalwood Incense", 0, 0, 0, 4, [], ["sandalwood-incense-focus", "sandalwood-incense-straighten"], "none");
        void Spell(string id, string name, int cost, int focus, string[] keywords, string[] abilities, string effect)
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == CardType.Spell && c.GoldCost == cost && c.Focus == focus, $"{name}: printed box");
            Check((c.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(keywords.OrderBy(k => k, StringComparer.Ordinal)), $"{name}: keywords");
            Check((c.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities " + string.Join(",", c.Abilities ?? []));
            Eq(c.Effect, effect, name + ": Effect");
            ArtShipped(id);
        }
        Spell(Extend, "Extend the Soul's Boundaries", 2, 1, ["Void"], ["extend-soul-shuffle"], "discount-per-ring");
        Spell(Immolation, "Immolation", 4, 2, ["Fire"], [], "immolation-force");
        Spell(Beasts, "Master of Beasts", 0, 3, ["Maho"], ["master-of-beasts-straighten"], "none");
        Spell(Wrath, "Nature's Wrath", 0, 2, ["Earth"], ["natures-wrath-move"], "none");
        Spell(Peace, "Peace of the Kami", 2, 3, ["Kami", "Water"], ["peace-of-the-kami-negate"], "none");
        Spell(Scent, "Scent of Dishonor", 0, 2, ["Air"], ["scent-of-dishonor-bow"], "none");
        Spell(Strength, "The Earth's Strength", 0, 2, ["Earth", "Water"], ["earths-strength-straighten", "earths-strength-negate-move"], "none");
        var oyoroi = CardCatalog.Get(OYoroi);
        Check(oyoroi.Name == "O-Yoroi Troops" && oyoroi.Type == CardType.Follower && oyoroi.GoldCost == 9 && oyoroi.Force == 7 && oyoroi.Chi == 0 && oyoroi.HonorRequirement == 2 && oyoroi.Focus == 4
            && (oyoroi.Abilities ?? []).SequenceEqual(["champion-of-thunder-negate"]), "O-Yoroi Troops");
        ArtShipped(OYoroi);
        var ambushers = CardCatalog.Get(Ambushers);
        Check(ambushers.Name == "Ronin Ambushers" && ambushers.Type == CardType.Follower && ambushers.GoldCost == 2 && ambushers.Force == 2 && ambushers.HonorRequirement == 0 && ambushers.Focus == 3
            && (ambushers.Keywords ?? []).OrderBy(k => k).SequenceEqual(["Ronin", "Scout"]) && (ambushers.Abilities ?? []).Length == 0 && ambushers.Effect == "ronin-ambushers-rebound", "Ronin Ambushers");
        ArtShipped(Ambushers);
        Check(AbilityCatalog.All["natures-wrath-move"].SourceUnitMayBeAwayIfWielder == "Earth" && AbilityCatalog.All["peace-of-the-kami-negate"].SourceUnitMayBeAwayIfWielder == "Water", "the keyword-conditional home exemptions");
    }

    private static void OYoroiTroops()
    {
        var s = Table(Phase.Battle, 1);
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var troops = Add(s, OYoroi, 0, Zone.Attached, province: 0, parent: host.Id);
        var bower = Add(s, "training-card-bower", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-bow-enemy-card-at-battle", bower.Id, host.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the targeted unit's controller may react");
        var reaction = e.QueryActions(0).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "champion-of-thunder-negate" && a.Command.TargetId == host.Id && a.CardId == troops.Id);
        Check(reaction is not null, "O-Yoroi Troops' Reaction is offered for the targeted host");
        Accept(e, reaction!.Command!);
        CloseWindows(e);
        Check(!e.State.Cards[host.Id].Bowed, "the bow was negated");
        // Without the Reaction (already spent this turn) a second bow lands.
        var e2 = Edit(e, st => { Add(st, "training-card-bower", 1, Zone.Battlefield, province: 0); st.PriorityPlayerId = 1; });
        var bower2 = e2.State.Cards.Values.Single(c => c.DefinitionId == "training-card-bower" && c.Id != bower.Id);
        Accept(e2, Offer(e2, 1, "training-bow-enemy-card-at-battle", bower2.Id, host.Id));
        CloseWindows(e2);
        Check(e2.State.Cards[host.Id].Bowed, "spent this turn: the second action bows him");
    }

    private static void RoninAmbushers()
    {
        var s = Table(Phase.Battle, 1);
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var ambushers = Add(s, Ambushers, 0, Zone.Attached, province: 0, parent: host.Id);
        var bower = Add(s, "training-card-bower", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-bow-enemy-card-at-battle", bower.Id, ambushers.Id));
        CloseWindows(e);
        var after = e.State.Cards[ambushers.Id];
        Check(!after.Bowed, "after it bowed, the trait straightened it");
        Check(after.StraightenHolds.Any(h => h.Until == StraightenPoint.PhaseEnds), "no Terrain controlled: its future straightening is negated until the phase ends");
        // Bowed again this phase: the hold refuses the trait's straighten.
        var again = Edit(e, st => { Add(st, "training-card-bower", 1, Zone.Battlefield, province: 0); st.PriorityPlayerId = 1; });
        var bowerB = again.State.Cards.Values.Single(c => c.DefinitionId == "training-card-bower" && c.Id != bower.Id);
        Accept(again, Offer(again, 1, "training-bow-enemy-card-at-battle", bowerB.Id, ambushers.Id));
        CloseWindows(again);
        Check(again.State.Cards[ambushers.Id].Bowed, "a second bow this phase sticks");
        // With a Terrain controlled: straightened, no hold.
        var t = Table(Phase.Battle, 1);
        var host2 = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var ambushers2 = Add(t, Ambushers, 0, Zone.Attached, province: 0, parent: host2.Id);
        Add(t, "training-terrain", 0, Zone.Battlefield, province: 0);
        var bower2 = Add(t, "training-card-bower", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 1, "training-bow-enemy-card-at-battle", bower2.Id, ambushers2.Id));
        CloseWindows(e2);
        Check(!e2.State.Cards[ambushers2.Id].Bowed && e2.State.Cards[ambushers2.Id].StraightenHolds.Count == 0, "a Terrain controlled: straightened with no hold");
        // The hold is released when the phase changes (Action phase -> Attack declaration on two passes).
        var u = Table(Phase.Action, 0);
        var host3 = Add(u, "training-crane-samurai", 1, Zone.Home);
        var ambushers3 = Add(u, Ambushers, 1, Zone.Attached, parent: host3.Id);
        var bower3 = Add(u, "training-card-bower", 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(u);
        Accept(e3, Offer(e3, 0, "training-bow-enemy-card-open", bower3.Id, ambushers3.Id));
        CloseWindows(e3);
        Check(!e3.State.Cards[ambushers3.Id].Bowed && e3.State.Cards[ambushers3.Id].StraightenHolds.Count == 1, "straightened and held in the Action phase");
        for (var guard = 0; guard < 4 && e3.State.Phase == Phase.Action; guard++) Accept(e3, e3.QueryActions(e3.State.PriorityPlayerId).First(a => a.Type == "pass" && a.Enabled).Command!);
        Check(e3.State.Phase != Phase.Action, "the Action phase ended");
        Eq(e3.State.Cards[ambushers3.Id].StraightenHolds.Count, 0, "the hold was released when the phase ended");
    }

    private static void AkaBekoNegates()
    {
        var s = Table(Phase.Battle, 1);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var beko = Add(s, AkaBeko, 0, Zone.Attached, province: 0, parent: mine.Id);
        var penalizer = Add(s, "training-penalty-source", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = ForceOf(e, mine);
        Accept(e, Offer(e, 1, "training-penalty-force", penalizer.Id, mine.Id));
        CloseWindows(e);
        Eq(ForceOf(e, mine), before, "the enemy's Force penalty was negated by Aka-beko");
        Check(e.State.Events.Any(ev => ev.Type == "modifierNegated" && ev.Message.Contains("Aka-beko")), "the negation is narrated");
        // A wielder's own controller's penalty is not from another player's action: it applies.
        var t = Table(Phase.Battle, 1);
        var theirs = Add(t, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Add(t, AkaBeko, 1, Zone.Attached, province: 0, parent: theirs.Id);
        var ownPenalizer = Add(t, "training-penalty-source", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(t);
        var force2 = ForceOf(e2, theirs);
        var ownPenalty = Options(e2, 1, "training-penalty-force-own", ownPenalizer.Id).FirstOrDefault(a => a.Enabled && (a.TargetIds?.Contains(theirs.Id) ?? false));
        if (ownPenalty is not null)
        {
            Accept(e2, ownPenalty.Command! with { TargetId = theirs.Id }); CloseWindows(e2);
            Check(ForceOf(e2, theirs) < force2, "its own controller's penalty still applies");
        }
        // Attaching: the penalties already on him from other players' cards are purged, one from his own side stays.
        var u = Table(Phase.Action, 0);
        var home = Add(u, "training-crane-samurai", 0, Zone.Home);
        var enemyCard = Add(u, "training-lion-samurai", 1, Zone.Home);
        var ownCard = Add(u, "training-lion-samurai", 0, Zone.Home);
        home.Modifiers.Add(new(enemyCard.Id, "test", "Force", -2));
        home.Modifiers.Add(new(ownCard.Id, "test", "Chi", -1));
        var item = Add(u, AkaBeko, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(u);
        Accept(e3, EquipCommand(e3, 0, item, home));
        var purged = e3.State.Cards[home.Id];
        Check(purged.Modifiers.All(m => m.Source != enemyCard.Id) && purged.Modifiers.Any(m => m.Source == ownCard.Id), "the other player's penalty was purged, the own one kept");
    }

    private static void BladesOfTheBlackDragon()
    {
        var s = Table(Phase.Battle, 1);
        var wielder = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);      // F4 + Blades' +3 = 7
        var blades = Add(s, Blades, 1, Zone.Attached, province: 0, parent: wielder.Id);
        var target = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0);       // F6
        var e = MatchEngine.FromTestState(s);
        Eq(ForceOf(e, wielder), 7);
        Accept(e, Use(e, 1, "blades-of-black-dragon-attack", blades.Id, target.Id));
        CloseWindows(e);
        Check(e.State.Cards[wielder.Id].Bowed, "the Human wielder bowed as the cost");
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "Melee 7 destroyed the Force-6 target");
        // A Nonhuman wielder pays no bow.
        var t = Table(Phase.Battle, 1);
        var oni = Add(t, "training-seeds5-shadowlands-oni", 1, Zone.Battlefield, province: 0);
        var blades2 = Add(t, Blades, 1, Zone.Attached, province: 0, parent: oni.Id);
        var target2 = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Use(e2, 1, "blades-of-black-dragon-attack", blades2.Id, target2.Id));
        CloseWindows(e2);
        Check(!e2.State.Cards[oni.Id].Bowed, "a Nonhuman wielder is not bowed");
        Eq(e2.State.Cards[target2.Id].Zone, Zone.Dead);
        // A bowed Human wielder cannot pay.
        var u = Table(Phase.Battle, 1);
        var tired = Add(u, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        var blades3 = Add(u, Blades, 1, Zone.Attached, province: 0, parent: tired.Id);
        Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(u), 1, "blades-of-black-dragon-attack", blades3.Id), "a bowed Human wielder cannot pay the bow");
    }

    private static void CelestialSwordOfTheMantis()
    {
        var s = Table(Phase.Action, 0);
        var mantis = Add(s, "training-seeds5-mantis-samurai", 0, Zone.Home);   // F2
        Add(s, "training-seeds5-mantis-samurai", 0, Zone.Home);
        var crane = Add(s, "training-crane-samurai", 0, Zone.Home);
        var sword = Add(s, MantisSword, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, EquipCommand(e, 0, sword, crane));
        var honor = Honor(e, 0);
        Accept(e, EquipCommand(e, 0, sword, mantis));
        Eq(ForceOf(e, mantis), 2 + 1 + 2, "printed +1F and +1F for each of the two Mantis Personalities controlled");
        Eq(e.State.PendingChoice?.Type, "entryReaction", "the entry Honor is offered");
        Accept(e, e.QueryActions(0).First(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "celestial-sword-mantis-honor").Command!);
        Eq(Honor(e, 0), honor + 2, "gained 2 Honor on entering play");
        // Battle: the Force half at zero Gold gives +2F, then the attack half is spent with it (one printed action).
        var t = Table(Phase.Battle, 1);
        var host = Add(t, "training-seeds5-mantis-samurai", 1, Zone.Battlefield, province: 0);
        var sword2 = Add(t, MantisSword, 1, Zone.Attached, province: 0, parent: host.Id);
        var target = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(t);
        var forceBefore = ForceOf(e2, host);
        Accept(e2, Enabled(e2, 1, "celestial-sword-mantis-force", sword2.Id).Command!);
        Eq(ForceOf(e2, host), forceBefore + 2, "zero Gold paid: +2F");
        Check(!CanUse(e2, 1, "celestial-sword-mantis-attack", sword2.Id), "the two halves share one use");
        // The attack half: strength twice the Gold paid.
        var e3 = MatchEngine.FromTestState(t);
        var option = Enabled(e3, 1, "celestial-sword-mantis-attack", sword2.Id);
        var quote = option.Gold!;
        Eq(quote.Kind, GoldCostKind.Variable);
        var amount = Math.Min(2, quote.MaxAmount ?? 0);
        var sources = amount == 0 ? [] : quote.PaymentsByAmount![amount][0].SourceIds;
        Accept(e3, option.Command! with { TargetId = target.Id, GoldAmount = amount, SourceIds = sources });
        CloseWindows(e3);
        if (amount >= 2) Eq(e3.State.Cards[target.Id].Zone, Zone.Dead, "Melee 4 destroyed the Force-3 target");
        else Check(e3.State.Cards[target.Id].Zone != Zone.Dead, "a zero-strength Attack destroys nothing");
    }

    private static void CelestialSwordOfTheScorpion()
    {
        // Attach restriction and the entry loss.
        var s = Table(Phase.Action, 0);
        var scorpion = Add(s, "training-lion-samurai", 0, Zone.Home); scorpion.GrantedKeywords.Add(new("Scorpion Clan", "test-fixture", GrantDuration.Permanent));
        var crane = Add(s, "training-crane-samurai", 0, Zone.Home);
        var sword = Add(s, ScorpionSword, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, EquipCommand(e, 0, sword, crane));
        var theirHonor = Honor(e, 1);
        Accept(e, EquipCommand(e, 0, sword, scorpion));
        var lose = e.QueryActions(0).First(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "celestial-sword-scorpion-lose" && a.Command.TargetPlayerId == 1);
        Accept(e, lose.Command!);
        Eq(Honor(e, 1), theirHonor - 2, "the chosen player lost 2 Honor");
        // Battle: the attacker bows a bare defending card; the defender's province holds a Personality.
        MatchEngine Battle(bool courtier, out CardInstance target, out CardInstance provinceCard)
        {
            var t = Table(Phase.Battle, 1);
            var wielder = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(t, ScorpionSword, 1, Zone.Attached, province: 0, parent: wielder.Id);
            if (courtier) Add(t, "training-seeds5-political-courtier", 1, Zone.Home);
            target = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var armed = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(t, "training-weapon", 0, Zone.Attached, province: 0, parent: armed.Id);
            provinceCard = Add(t, "training-lion-samurai", 0, Zone.Province, province: 0);
            t.Players[0].Provinces[0].CardId = provinceCard.Id;
            var eng = MatchEngine.FromTestState(t);
            var offered = Enabled(eng, 1, "celestial-sword-scorpion-bow").TargetIds ?? [];
            Check(offered.Contains(target.Id) && !offered.Contains(armed.Id), "only a card without attachments");
            return eng;
        }
        var e2 = Battle(false, out var target2, out var province2);
        Accept(e2, Offer(e2, 1, "celestial-sword-scorpion-bow", e2.State.Cards.Values.Single(c => c.DefinitionId == ScorpionSword).Id, target2.Id));
        CloseWindows(e2);
        Eq(e2.State.PendingChoice?.Type, "effectChoice", "the optional province discard is asked");
        Accept(e2, e2.QueryActions(1).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "discard").Command!);
        Eq(e2.State.Cards[province2.Id].Zone, Zone.DynastyDiscard, "the province card was discarded");
        Eq(e2.State.Cards[target2.Id].Zone, Zone.Dead, "a Personality was discarded: the bowed target is destroyed");
        var e3 = Battle(false, out var target3, out _);
        Accept(e3, Offer(e3, 1, "celestial-sword-scorpion-bow", e3.State.Cards.Values.Single(c => c.DefinitionId == ScorpionSword).Id, target3.Id));
        CloseWindows(e3);
        Accept(e3, e3.QueryActions(1).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "keep").Command!);
        Check(e3.State.Cards[target3.Id].Bowed && e3.State.Cards[target3.Id].Zone == Zone.Battlefield, "nothing discarded and no Courtier: bowed, not destroyed");
        var e4 = Battle(true, out var target4, out _);
        Accept(e4, Offer(e4, 1, "celestial-sword-scorpion-bow", e4.State.Cards.Values.Single(c => c.DefinitionId == ScorpionSword).Id, target4.Id));
        CloseWindows(e4);
        Accept(e4, e4.QueryActions(1).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "keep").Command!);
        Eq(e4.State.Cards[target4.Id].Zone, Zone.Dead, "a Courtier controlled: destroyed even with nothing discarded");
    }

    private static void HatoGurama()
    {
        var s = Table(Phase.Action, 0);
        var source = Add(s, "training-slots-source", 0, Zone.Home);
        var hato = Add(s, Hato, 0, Zone.Hand);
        var other = Add(s, "training-weapon", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var candidates = Enabled(e, 0, "training-slots-discard", source.Id).Slots!.Single(x => x.Slot == "card").CardIds;
        Check(candidates.Contains(other.Id) && !candidates.Contains(hato.Id), "with another card in hand Hato-gurama may not be chosen: " + string.Join(",", candidates));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: source.Id, AbilityId: "training-slots-discard", Selections: [Pick("card", hato.Id)]));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: source.Id, AbilityId: "training-slots-discard", Selections: [Pick("card", other.Id)]));
        // Alone in hand it may be chosen.
        var t = Table(Phase.Action, 0);
        var source2 = Add(t, "training-slots-source", 0, Zone.Home);
        var hato2 = Add(t, Hato, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Check(Enabled(e2, 0, "training-slots-discard", source2.Id).Slots!.Single(x => x.Slot == "card").CardIds.Contains(hato2.Id), "alone in hand it may be discarded");
        // The Fate deck minimum: each copy lowers it by one.
        Eq(DeckWorkshop.FateMinimumReductionOf(CardCatalog.Get(Hato)), 1);
        Eq(DeckWorkshop.FateMinimumReductionOf(CardCatalog.Get(Kama)), 0);
        var hatoInfo = new DeckCardInfo(Hato, "Hato-gurama", ["Item"], ["Fate"], [], true, FateMinimumReduction: 1);
        var filler = new DeckCardInfo("training-weapon", "Training weapon", ["Item"], ["Fate"], ["Weapon"], true);
        var workshop = new DeckWorkshop([hatoInfo, filler]);
        workshop.Adjust(Hato, 2); workshop.Adjust("training-weapon", 36);
        var review = workshop.Review();
        Check(review.Fate == 38 && !review.Warnings.Any(w => w.StartsWith("Fate needs")), "38 Fate cards with two Hato-gurama meet the lowered minimum: " + string.Join(" | ", review.Warnings));
        workshop.Adjust("training-weapon", -1);
        Check(workshop.Review().Warnings.Any(w => w.Contains("38 minimum")), "one short of the lowered minimum warns with the lowered number");
    }

    private static void KamaTraits()
    {
        var s = Table(Phase.Action, 0);
        var crane = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, "training-weapon", 0, Zone.Attached, parent: crane.Id);
        var kama = Add(s, Kama, 0, Zone.Hand);
        var third = Add(s, "training-weapon", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, EquipCommand(e, 0, kama, crane));
        Check(e.State.Cards[crane.Id].Dishonorable, "a non-Mantis wielder is dishonored when the Kama attaches");
        Eq(e.State.Cards[kama.Id].AttachedTo, crane.Id, "attached beside the one Weapon he already had");
        Reject(e, EquipCommand(e, 0, third, crane));
        var t = Table(Phase.Action, 0);
        var mantis = Add(t, "training-seeds5-mantis-samurai", 0, Zone.Home);
        var kama2 = Add(t, Kama, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, EquipCommand(e2, 0, kama2, mantis));
        Check(!e2.State.Cards[mantis.Id].Dishonorable, "a Mantis Clan wielder is not dishonored");
    }

    private static void KiUsoProtects()
    {
        MatchEngine Rig(bool kiUso, string performerDef, out CardInstance target)
        {
            var s = Table(Phase.Action, 0);
            var performer = Add(s, performerDef, 0, Zone.Home);
            if (kiUso) Add(s, KiUso, 0, Zone.Attached, parent: performer.Id);
            target = Add(s, "training-lion-samurai", 1, Zone.Home);
            Add(s, "training-action-negator", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "training-bow-any", performer.Id, target.Id));
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction");
            Accept(e, e.QueryActions(1).Single(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "training-negate-action").Command!);
            CloseWindows(e);
            return e;
        }
        var protectedPolitical = Rig(true, "training-seeds5-political-courtier", out var t1);
        Check(protectedPolitical.State.Cards[t1.Id].Bowed, "a Political action by Ki-Uso's wielder was not negated");
        var unprotected = Rig(false, "training-seeds5-political-courtier", out var t2);
        Check(!unprotected.State.Cards[t2.Id].Bowed, "without Ki-Uso the same Political action is negated");
        var nonPolitical = Rig(true, "training-bow-any-source", out var t3);
        Check(!nonPolitical.State.Cards[t3.Id].Bowed, "a non-Political action is not protected by Ki-Uso");
    }

    private static void MokshaPatam()
    {
        // The trait: an action whose effects gained Honor gives the wielder a +1F token.
        var s = Table(Phase.Action, 0);
        var wielder = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, Moksha, 0, Zone.Attached, parent: wielder.Id);
        var other = Add(s, "training-lion-samurai", 0, Zone.Home);
        var item = Add(s, "training-item-entry-reaction", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var force = ForceOf(e, wielder);
        Accept(e, EquipCommand(e, 0, item, other));
        Eq(e.State.Cards[wielder.Id].Tokens.Count, 0, "an equip is not an action: no token yet");
        Accept(e, e.QueryActions(0).First(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "training-item-entry-reaction").Command!);
        Check(e.State.Cards[wielder.Id].Tokens.Any(t => t.Type == "Moksha" && t.Stat == "Force" && t.Amount == 1), "the Reaction's effects gained 1 Honor: a +1F token");
        Eq(ForceOf(e, wielder), force + 1);
        // An action that gains no Honor gives nothing.
        var bower = Add(s, "training-card-bower", 0, Zone.Home);
        var victim = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e1 = MatchEngine.FromTestState(s);
        Accept(e1, Offer(e1, 0, "training-bow-enemy-card-open", bower.Id, victim.Id));
        CloseWindows(e1);
        Eq(e1.State.Cards[wielder.Id].Tokens.Count, 0, "no Honor gained: no token");
        // Battle: move him home, negate his movement back while the Item stays at the battlefield, lose 1 Honor.
        var t = Table(Phase.Battle, 1);
        var attacker = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var moksha = Add(t, Moksha, 1, Zone.Attached, province: 0, parent: attacker.Id);
        var beast = Add(t, "training-nonhuman-personality", 0, Zone.Battlefield, province: 0);
        var shugenja = Add(t, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0);
        var wrath = Add(t, Wrath, 0, Zone.Attached, province: 0, parent: shugenja.Id);
        var e2 = MatchEngine.FromTestState(t);
        var honor = Honor(e2, 1);
        Accept(e2, Offer(e2, 1, "moksha-patam-move", moksha.Id, beast.Id));
        CloseWindows(e2);
        Eq(e2.State.Cards[beast.Id].Zone, Zone.Home, "moved home");
        Eq(Honor(e2, 1), honor - 1, "lost 1 Honor");
        Accept(e2, Offer(e2, 0, "natures-wrath-move", wrath.Id, beast.Id));
        CloseWindows(e2);
        Eq(e2.State.Cards[beast.Id].Zone, Zone.Home, "his movement to the battlefield is negated while Moksha Patam is there");
        // Once the Item has left the battlefield the lock no longer holds.
        var e3 = Edit(e2, st => { st.Cards[attacker.Id].Zone = Zone.Home; st.Cards[attacker.Id].ProvinceIndex = null; st.Cards[moksha.Id].ProvinceIndex = null; st.Cards[wrath.Id].Bowed = false; st.Cards[shugenja.Id].AbilityUses.Clear(); st.Cards[wrath.Id].AbilityUses.Clear(); st.PriorityPlayerId = 0; Add(st, "training-lion-samurai", 1, Zone.Battlefield, province: 0); });
        Accept(e3, Offer(e3, 0, "natures-wrath-move", wrath.Id, beast.Id));
        CloseWindows(e3);
        Eq(e3.State.Cards[beast.Id].Zone, Zone.Battlefield, "the lock held only while the Item was at the battlefield");
    }

    private static void OkesaGoma()
    {
        var s = Table(Phase.Battle, 1);
        var wielder = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var okesa = Add(s, Okesa, 1, Zone.Attached, province: 0, parent: wielder.Id);
        var oni = Add(s, "training-seeds5-shadowlands-oni", 0, Zone.Battlefield, province: 0);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 1, "okesa-goma-move", okesa.Id).TargetIds ?? [];
        Check(targets.Contains(oni.Id) && !targets.Contains(plain.Id), "a Nonhuman or Shadowlands Personality only");
        Accept(e, Offer(e, 1, "okesa-goma-move", okesa.Id, oni.Id));
        CloseWindows(e);
        Check(e.State.Cards[okesa.Id].Bowed && e.State.Cards[oni.Id].Zone == Zone.Home, "bowed as its cost; the Oni went home");
    }

    private static void SandalwoodIncense()
    {
        var s = Table(Phase.Action, 0);
        var wielder = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var incense = Add(s, Sandalwood, 0, Zone.Attached, parent: wielder.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "sandalwood-incense-straighten", incense.Id).Command!);
        Check(!e.State.Cards[wielder.Id].Bowed, "the wielder straightened");
        Eq(e.State.Cards[incense.Id].Zone, Zone.FateDeck); Eq(e.State.Players[0].FateDeck[0], incense.Id, "on top of the Fate deck");
        // The Focus Effect: player 0 challenges and wins; the choice is offered and taken.
        MatchEngine Duel(bool challengerWins)
        {
            var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
            var mine = Add(t, challengerWins ? "training-crane-veteran" : "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var theirs = Add(t, challengerWins ? "training-lion-veteran" : "training-paneki-exp3", 1, Zone.Battlefield, province: 0);
            var duel = Add(t, "training-plain-duel-strategy", 0, Zone.Hand);
            var focused = Add(t, Sandalwood, 0, Zone.Hand);
            var top = Add(t, "training-focus-strategy", 1, Zone.FateDeck); t.Players[1].FateDeck.Insert(0, top.Id);
            var eng = MatchEngine.FromTestState(t);
            Accept(eng, Offer(eng, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
            Accept(eng, new GameCommand("focus", 1, eng.Version, Choice: "deck"));
            Accept(eng, new GameCommand("focus", 0, eng.Version, CardId: focused.Id));
            Accept(eng, new GameCommand("strike", 1, eng.Version));
            return eng;
        }
        var won = Duel(true);
        Eq(won.State.PendingChoice?.Type, "focusEffectChoice", "the winner's Focus Effect asks");
        var picks = won.QueryActions(0).Where(x => x.Type == "chooseFocusEffect").Select(x => x.Label).ToArray();
        Check(picks.Contains("Gain 2 Honor") && picks.Contains("Draw a card"), "the two effects are offered: " + string.Join(",", picks));
        var honor = Honor(won, 0);
        Accept(won, won.QueryActions(0).Single(x => x.Type == "chooseFocusEffect" && x.Label == "Gain 2 Honor").Command!);
        Eq(Honor(won, 0), honor + 2, "gained 2 Honor");
        Check(won.State.PendingFocusChoice is null, "the duel went on");
        var drew = Duel(true);
        var hand = drew.State.Players[0].Hand.Count;
        Accept(drew, drew.QueryActions(0).Single(x => x.Type == "chooseFocusEffect" && x.Label == "Draw a card").Command!);
        Eq(drew.State.Players[0].Hand.Count, hand + 1, "drew a card");
        var lost = Duel(false);
        Check(lost.State.PendingChoice?.Type != "focusEffectChoice", "the loser's Personality did not win: nothing is asked");
    }

    private static void ImmolationForce()
    {
        var s = Table(Phase.Action, 0);
        var plain = Add(s, "training-water-shugenja", 0, Zone.Home);       // F2
        Add(s, Immolation, 0, Zone.Attached, parent: plain.Id);
        var fire = Add(s, "training-seeds5-fire-shugenja", 0, Zone.Home);  // F2
        Add(s, Immolation, 0, Zone.Attached, parent: fire.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(ForceOf(e, plain), 5, "+3F");
        Eq(ForceOf(e, fire), 6, "+4F while Fire");
    }

    private static void MasterOfBeasts()
    {
        var s = Table(Phase.Action, 0);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Home);
        var beasts = Add(s, Beasts, 0, Zone.Attached, parent: shugenja.Id);
        var oni1 = Add(s, "training-seeds5-shadowlands-oni", 0, Zone.Home, bowed: true);
        var oni2 = Add(s, "training-seeds5-shadowlands-oni", 1, Zone.Home, bowed: true);
        var human = Add(s, "training-undead-personality", 0, Zone.Home, bowed: true);     // Shadowlands, but Human
        var e = MatchEngine.FromTestState(s);
        var candidates = Enabled(e, 0, "master-of-beasts-straighten", beasts.Id).Slots!.Single(x => x.Slot == "targets").CardIds;
        Check(candidates.Contains(oni1.Id) && candidates.Contains(oni2.Id) && !candidates.Contains(human.Id), "Shadowlands Nonhumans of either player, not a Human: " + string.Join(",", candidates));
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: beasts.Id, AbilityId: "master-of-beasts-straighten", Selections: [Pick("targets", oni1.Id, oni2.Id)]));
        Check(!e.State.Cards[oni1.Id].Bowed && !e.State.Cards[oni2.Id].Bowed && e.State.Cards[shugenja.Id].Bowed, "both straightened; the Shugenja bowed as the cost");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: beasts.Id, AbilityId: "master-of-beasts-straighten", Selections: [Pick("targets", human.Id)]));
        // Three targets is too many.
        var e2 = MatchEngine.FromTestState(s);
        var oni3 = Add(e2.State, "training-seeds5-shadowlands-oni", 0, Zone.Home, bowed: true);
        var e3 = MatchEngine.FromTestState(e2.State);
        Reject(e3, new GameCommand("useAbility", 0, e3.Version, CardId: beasts.Id, AbilityId: "master-of-beasts-straighten", Selections: [Pick("targets", oni1.Id, oni2.Id, oni3.Id)]));
    }

    private static void NaturesWrath()
    {
        MatchEngine Rig(bool opposed, out CardInstance beast, out CardInstance wrath)
        {
            var s = Table(Phase.Battle, 1);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (opposed) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var shugenja = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Home);
            wrath = Add(s, Wrath, 0, Zone.Attached, parent: shugenja.Id);
            beast = Add(s, "training-nonhuman-personality", 0, Zone.Home, bowed: true);
            s.PriorityPlayerId = 0;
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var beast, out var wrath);
        Accept(e, Offer(e, 0, "natures-wrath-move", wrath.Id, beast.Id));
        CloseWindows(e);
        Check(e.State.Cards[beast.Id].Zone == Zone.Battlefield && e.State.Cards[beast.Id].ProvinceIndex == 0 && !e.State.Cards[beast.Id].Bowed, "opposed: moved to the battlefield and straightened");
        Check(e.State.Cards[wrath.Id].Bowed, "the Spell bowed as its cost");
        var e2 = Rig(false, out var beast2, out var wrath2);
        Accept(e2, Offer(e2, 0, "natures-wrath-move", wrath2.Id, beast2.Id));
        CloseWindows(e2);
        Check(e2.State.Cards[beast2.Id].Zone == Zone.Home && e2.State.Cards[beast2.Id].Bowed, "he would not be opposed: nothing moved, nothing straightened");
        // A Human Personality is not a legal target.
        var e3 = Rig(true, out _, out var wrath3);
        var human = Add(e3.State, "training-crane-samurai", 0, Zone.Home);
        var e4 = MatchEngine.FromTestState(e3.State);
        Check(!(Enabled(e4, 0, "natures-wrath-move", wrath3.Id).TargetIds ?? []).Contains(human.Id), "your Nonhuman Personality only");
    }

    private static void PeaceOfTheKami()
    {
        MatchEngine Rig(string shugenjaDef, out CardInstance peace, out CardInstance spirit, out CardInstance enemyCard, out CardInstance ownCard)
        {
            var s = Table(Phase.Battle, 1);
            spirit = Add(s, "training-seeds5-spirit", 0, Zone.Battlefield, bowed: true, province: 0);
            enemyCard = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            ownCard = Add(s, "training-crane-samurai", 0, Zone.Home);
            spirit.Modifiers.Add(new(enemyCard.Id, "test", "Force", -2));
            spirit.Modifiers.Add(new(ownCard.Id, "test", "Force", 1));
            spirit.Tokens.Add(new("token-x1", "Cultist", enemyCard.Id, "Chi", -1));
            spirit.GrantedKeywords.Add(new("Ninja", enemyCard.Id, GrantDuration.Turn));
            var shugenja = Add(s, shugenjaDef, 0, Zone.Home);
            peace = Add(s, Peace, 0, Zone.Attached, parent: shugenja.Id);
            s.PriorityPlayerId = 0;
            return MatchEngine.FromTestState(s);
        }
        var e = Rig("training-water-shugenja", out var peace, out var spirit, out var enemyCard, out var ownCard);
        Accept(e, Offer(e, 0, "peace-of-the-kami-negate", peace.Id, spirit.Id));
        CloseWindows(e);
        var after = e.State.Cards[spirit.Id];
        Check(after.Modifiers.All(m => m.Source != enemyCard.Id) && after.Modifiers.Any(m => m.Source == ownCard.Id), "the other player's modifier is gone, the own one stays");
        Check(after.Tokens.Count == 0 && after.GrantedKeywords.Count == 0, "the other player's token and granted keyword are gone");
        Check(!after.Bowed, "a Spirit is straightened");
        Eq(after.ForcePenaltiesNegatedTurn, e.State.TurnNumber, "new Force penalties are negated for the turn");
        // A non-Water Shugenja at home cannot perform it.
        var e2 = Rig("training-shugenja", out var peace2, out _, out _, out _);
        Check(!CanUse(e2, 0, "peace-of-the-kami-negate", peace2.Id), "not Water: the Shugenja must be at the battlefield");
    }

    private static void ScentOfDishonor()
    {
        var s = Table(Phase.Action, 0);
        var air = Add(s, "training-seeds5-air-shugenja", 0, Zone.Home);
        var scent = Add(s, Scent, 0, Zone.Attached, parent: air.Id);
        var crook = Add(s, "training-lion-samurai", 1, Zone.Home); crook.Dishonorable = true;
        var ninja = Add(s, "training-ninja-source", 1, Zone.Home);
        var honest = Add(s, "training-crane-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "scent-of-dishonor-bow", scent.Id).TargetIds ?? [];
        Check(targets.Contains(crook.Id) && targets.Contains(ninja.Id) && !targets.Contains(honest.Id), "dishonorable or Ninja, not an honorable plain Personality");
        Accept(e, Offer(e, 0, "scent-of-dishonor-bow", scent.Id, crook.Id));
        CloseWindows(e);
        Check(e.State.Cards[crook.Id].Bowed, "the target bowed");
        Check(!e.State.Cards[air.Id].Bowed, "an Air Shugenja is not bowed");
        Eq(e.State.Cards[scent.Id].Zone, Zone.FateDiscard, "the Spell was destroyed as its cost");
        var t = Table(Phase.Action, 0);
        var water = Add(t, "training-water-shugenja", 0, Zone.Home);
        var scent2 = Add(t, Scent, 0, Zone.Attached, parent: water.Id);
        var crook2 = Add(t, "training-lion-samurai", 1, Zone.Home); crook2.Dishonorable = true;
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 0, "scent-of-dishonor-bow", scent2.Id, crook2.Id));
        CloseWindows(e2);
        Check(e2.State.Cards[water.Id].Bowed, "a non-Air Shugenja bows");
    }

    private static void ExtendTheSoulsBoundaries()
    {
        // The per-Ring discount on Equip (Battle).
        var s = Table(Phase.Battle, 1);
        s.Cards[s.Players[1].StrongholdId].DefinitionId = "oracle-l5r-1218"; // a real Stronghold: player abilities (Equip (Battle)) are not offered under the scenario-only one
        var shugenja = Add(s, "training-seeds5-void-shugenja", 1, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var spell = Add(s, Extend, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        static int EquipCost(MatchEngine eng, CardInstance spell)
        {
            var option = eng.QueryActions(1).FirstOrDefault(a => a.Type == "useAbility" && (a.Command?.AbilityId == "equip-battle" || a.Label.StartsWith(AbilityCatalog.All["equip-battle"].Label)));
            Check(option is not null, "Equip (Battle) is offered");
            return option!.TargetGold![spell.Id].Cost;
        }
        Eq(EquipCost(e, spell), 2, "no Ring: the printed 2");
        Add(s, "training-false-ring-of-earth", 1, Zone.Home); Add(s, "training-false-ring-of-air", 1, Zone.Home);
        var e1 = MatchEngine.FromTestState(s);
        Eq(EquipCost(e1, spell), 0, "two Rings controlled: 2 less");
        // The Battle action: the target's controller shuffles him away.
        MatchEngine Rig(string shugenjaDef, out CardInstance extend, out CardInstance target, out CardInstance follower, out CardInstance caster)
        {
            var t = Table(Phase.Battle, 1);
            caster = Add(t, shugenjaDef, 1, Zone.Battlefield, province: 0);
            extend = Add(t, Extend, 1, Zone.Attached, province: 0, parent: caster.Id);
            target = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            follower = Add(t, "training-follower-human-only", 0, Zone.Attached, province: 0, parent: target.Id);
            return MatchEngine.FromTestState(t);
        }
        var e2 = Rig("training-seeds5-void-shugenja", out var extend2, out var target2, out var follower2, out var caster2);
        Accept(e2, Offer(e2, 1, "extend-soul-shuffle", extend2.Id, target2.Id));
        CloseWindows(e2);
        Check(e2.State.Cards[caster2.Id].Bowed && e2.State.Cards[caster2.Id].Zone == Zone.Battlefield, "a Void Shugenja is bowed, not destroyed");
        Eq(e2.State.Cards[extend2.Id].Zone, Zone.FateDiscard, "the Spell was destroyed as its cost");
        Eq(e2.State.PendingChoice?.Type, "eitherOr"); Eq(e2.State.PendingChoice?.PlayerId, 0, "the target's controller decides");
        Accept(e2, e2.QueryActions(0).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "shuffle").Command!);
        Check(e2.State.Cards[target2.Id].Zone == Zone.DynastyDeck && e2.State.Players[0].DynastyDeck.Contains(target2.Id), "the Personality went into his owner's Dynasty deck");
        Check(e2.State.Cards[follower2.Id].Zone == Zone.FateDeck && e2.State.Players[0].FateDeck.Contains(follower2.Id), "his Follower into its owner's Fate deck");
        Eq(e2.State.AdditionalActions.Count, 0, "no additional actions when he chose the shuffle");
        // Refused: two additional Battle actions for the acting player, and a non-Void Shugenja is destroyed.
        var e3 = Rig("training-water-shugenja", out var extend3, out var target3, out _, out var caster3);
        Accept(e3, Offer(e3, 1, "extend-soul-shuffle", extend3.Id, target3.Id));
        CloseWindows(e3);
        Eq(e3.State.Cards[caster3.Id].Zone, Zone.Dead, "a non-Void Shugenja is destroyed");
        Accept(e3, e3.QueryActions(0).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "refuse").Command!);
        Eq(e3.State.Cards[target3.Id].Zone, Zone.Battlefield, "he stayed");
        Eq(e3.State.AdditionalActions.Count(g => g.PlayerId == 1), 2, "two additional Battle actions");
        Eq(e3.State.PriorityPlayerId, 1, "the acting player keeps priority for them");
    }

    private static void TheEarthsStrength()
    {
        var s = Table(Phase.Battle, 1);
        var alchemist = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, bowed: true, province: 0);
        var strength = Add(s, Strength, 0, Zone.Attached, province: 0, parent: alchemist.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var water = Add(s, "training-water-shugenja", 0, Zone.Battlefield, bowed: true, province: 0);
        var plain = Add(s, "training-shugenja", 0, Zone.Battlefield, bowed: true, province: 0);
        s.PriorityPlayerId = 0;
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "earths-strength-straighten", strength.Id).TargetIds ?? [];
        Check(targets.Contains(alchemist.Id) && targets.Contains(water.Id) && !targets.Contains(plain.Id), "an Earth or Water Shugenja only");
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "earths-strength-straighten", strength.Id, alchemist.Id));
        CloseWindows(e);
        Check(!e.State.Cards[alchemist.Id].Bowed && e.State.Cards[strength.Id].Bowed, "straightened; the Spell bowed as its cost");
        Eq(Honor(e, 0), honor + 1, "an opposed Alchemist: 1 Honor");
        // The Reaction: negate a defending Personality's movement from the attacker's Moksha Patam.
        var t = Table(Phase.Battle, 1);
        var defender = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var shugenja = Add(t, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0);
        var strength2 = Add(t, Strength, 0, Zone.Attached, province: 0, parent: shugenja.Id);
        var attacker = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var moksha = Add(t, Moksha, 1, Zone.Attached, province: 0, parent: attacker.Id);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 1, "moksha-patam-move", moksha.Id, defender.Id));
        Eq(e2.State.PendingChoice?.Type, "beforeActionReaction");
        var reaction = e2.QueryActions(0).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "earths-strength-negate-move" && a.Command.TargetId == defender.Id);
        Check(reaction is not null, "the defending Personality the action would move is offered");
        Accept(e2, reaction!.Command!);
        CloseWindows(e2);
        Check(e2.State.Cards[defender.Id].Zone == Zone.Battlefield, "his movement was negated");
        Check(e2.State.Cards[strength2.Id].Bowed, "the Spell bowed as the Reaction's cost");
        // The attacker's own moved Personality is not a defender: nothing to react with.
        var u = Table(Phase.Battle, 1);
        Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var shugenja3 = Add(u, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0);
        Add(u, Strength, 0, Zone.Attached, province: 0, parent: shugenja3.Id);
        var attacker3 = Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var moksha3 = Add(u, Moksha, 1, Zone.Attached, province: 0, parent: attacker3.Id);
        var ally = Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(u);
        Accept(e3, Offer(e3, 1, "moksha-patam-move", moksha3.Id, ally.Id));
        Check(!e3.QueryActions(0).Any(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "earths-strength-negate-move"), "an attacking Personality is not a defending one");
        CloseWindows(e3);
        Eq(e3.State.Cards[ally.Id].Zone, Zone.Home);
    }
}
