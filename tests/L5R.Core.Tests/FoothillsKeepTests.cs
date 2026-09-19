using static RegressionAssertions;
using static Support337;
using L5R.Core;

// Foothills Keep (oracle-l5r-2626, issue #352): "Equipping Weapons is a Battle/Open action for you. Battle/Open: Choose your performing Kensai and target a
// Weapon in your discard pile: Attach it to him, paying all costs." Two things: a PLAYER-LEVEL TIMING GRANT (TimingGrants.cs) that ADDS a Battle/Open timing
// for equipping Weapons from hand and leaves Equip (Limited) alone, and the Battle/Open attach from your own discard pile (AttachAction.cs). Every reading
// is in docs/decisions/2026-09-19-alternative-costs-attach-from-discard.md. Player 0 owns the Keep and acts unless a test says otherwise.
internal static class FoothillsKeepTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Foothills Keep is the printed Stronghold (Province Strength 7, Gold Production 4, Starting Honor 5, Dragon), whole -- its Effect is the timing grant and its one ability the Battle/Open attach; the text is the scan's (\"paying all costs\", no cost symbol), and the art is shipped", PublishedShape),
            ("The attach as a Battle action: your performing Kensai AT the current battlefield, a Weapon from YOUR pile, full cost through the Gold flow; the Weapon joins his battlefield; a Kensai at home, a non-Kensai, a bowed Keep and the enemy's pile are all refused", AttachInBattle),
            ("The attach as an Open action: out of battle, in the Action phase, the Kensai at Home -- and in ANY player's action segment, so the player who is not the active player may take it when they hold priority; a bowed Keep is refused there too", AttachAsOpenInAnySegment),
            ("Paying with the Keep's own production bows it (the Gold flow); paying with other sources leaves it unbowed and the ability usable again", KeepBowsOnlyWhenItPays),
            ("The timing grant ADDS to Equip (Limited): on your own turn a Weapon or any other Item is equipped exactly as before, with or without the Keep", EquipLimitedIsUnchanged),
            ("The grant's Open timing: in the Action phase a player who is NOT the active player may equip a Weapon from hand (when they hold priority), an Item that is not a Weapon still may not, and without the Keep neither may; the opponent's Keep gives you nothing", OpenEquipInAnySegment),
            ("The grant's Battle timing: a Weapon from hand to your Personality AT the current battlefield, the Keep unbowed; not to one at Home, not a non-Weapon, not with the Keep bowed, not without a Keep; the Weapon stands at its wielder's battlefield", BattleEquip),
            ("A Battle equip is a Battle action like any other: it uses an unscoped Additional Action grant (keeping priority while more remain), forfeits the rest when none applies, and is barred where a Battle action is (the first-action must-pass)", BattleEquipIsABattleAction),
            ("Control and ownership: a Weapon from your own pile changes no one's control, is a normal attachment (the Weapon cap applies), and goes back to your pile when destroyed", OwnWeaponControl),
            ("Every offered Foothills Keep command (the attach, and the equips the grant adds) validates on a fresh engine", OfferedCommandsValidateAll),
        ];
        runner.Run("foothills-keep", tests);
    }

    private const string Keep = "oracle-l5r-2626", Ability = "foothills-equip-weapon";

    private static MatchState WithKeep(Phase phase, int owner = 0, int actor = 0)
    {
        var s = Table(phase, actor);
        s.Cards[s.Players[owner].StrongholdId].DefinitionId = Keep;
        return s;
    }

    // Sources that really pay `cost` for player 0, so a refusal is about the TIMING and not about an unpaid cost.
    private static string[] Src(MatchEngine e, int cost) => e.PaymentOptions(0, cost).First().SourceIds;

    private static CardInstance KeepOf(MatchState s, int player = 0) => s.Cards[s.Players[player].StrongholdId];

    private static CardInstance Farm(MatchState s, int player = 0) => Put(s, "training-farm", player, Zone.Home);

    private static void PublishedShape()
    {
        var d = CardCatalog.Get(Keep);
        Expect(!d.ScenarioOnly && d.Type == CardType.Stronghold && d.Name == "Foothills Keep" && d.Clan == "Dragon");
        // Verified against the scan: Province Strength 7, Gold Production 4, Starting Honor 5.
        Eq(d.ProvinceStrength, 7); Eq(d.GoldProduction, 4); Eq(d.StartingHonor, 5);
        Eq(d.Text, "Equipping Weapons is a Battle/Open action for you.\nBattle/Open: Choose your performing Kensai and target a Weapon in your discard pile: Attach it to him, paying all costs.");
        Eq(d.Effect, "equip-weapons-battle-open"); Expect(d.Abilities!.SequenceEqual([Ability]), "one ability");
        var a = AbilityCatalog.All[Ability];
        Eq(a.Timing, "BattleOrOpen"); Eq(a.Target, "attachment-in-discard"); Eq(a.Performer, "own-personality-with-keyword-at-battle"); Expect(a.RequiredKeywords!.SequenceEqual(["Kensai"]));
        Eq(a.Cost!.Basis, GoldCostBasis.AttachCost); Eq(a.Attach!.Pile, AttachPile.Own); Eq(a.Attach.Wielder, AttachWielder.Performer);
        Expect(a.Attach.Types!.SequenceEqual([CardType.Item]) && a.Attach.Keywords!.SequenceEqual(["Weapon"]) && a.Attach.ExcludedKeywords is null, "a Weapon (Unique or not)");
        Expect(ArtShipped(Keep), "art is shipped"); Eq(d.ArtworkKey, $"res://Content/Published/{Keep}.jpg");
    }

    private static void AttachInBattle()
    {
        var s = WithKeep(Phase.Battle);
        var kensai = Put(s, "training-kensai", 0, Zone.Battlefield, province: 0);
        var atHome = Put(s, "training-kensai", 0, Zone.Home);
        var samurai = Put(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var mine = Put(s, "training-weapon", 0, Zone.FateDiscard);
        var theirs = Put(s, "training-weapon", 1, Zone.FateDiscard);
        Put(s, "training-item", 0, Zone.FateDiscard);                       // not a Weapon
        var farm = Farm(s);
        var e = Engine(s);
        var offers = Offers(e, 0, Ability).Where(o => o.Enabled).ToArray();
        Expect(offers.Length == 1 && offers[0].Command!.PerformerId == kensai.Id && offers[0].CardId == s.Players[0].StrongholdId, "one performer: the Kensai at the battlefield");
        Expect(offers[0].TargetIds!.SequenceEqual([mine.Id]), "only a Weapon in MY pile");
        var command = Offer(e, 0, Ability, s.Players[0].StrongholdId, mine.Id, kensai.Id) with { SourceIds = [farm.Id] };
        foreach (var bad in new[] { atHome.Id, samurai.Id }) Reject(e, command with { PerformerId = bad });
        Reject(e, command with { TargetId = theirs.Id });
        Accept(e, command);
        var w = e.State.Cards[mine.Id];
        Expect(w.Zone == Zone.Attached && w.AttachedTo == kensai.Id && w.ProvinceIndex == 0, "attached to the Kensai, at his battlefield");
        Expect(e.State.Events.Any(x => x.Type == "payment" && x.CardId == mine.Id), "the full cost went through the Gold flow");
        Expect(e.State.Cards[farm.Id].Bowed && !KeepOf(e.State).Bowed, "paid by the Farm; the Keep is still unbowed");
        Eq(e.State.PriorityPlayerId, 1, "priority passed");
        // A bowed Keep cannot use its ability.
        var bowed = WithKeep(Phase.Battle); KeepOf(bowed).Bowed = true;
        Put(bowed, "training-kensai", 0, Zone.Battlefield, province: 0); Put(bowed, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Put(bowed, "training-weapon", 0, Zone.FateDiscard); Farm(bowed);
        var be = Engine(bowed);
        Expect(Offers(be, 0, Ability).All(o => !o.Enabled), "the Keep is bowed: not offered");
        // No unit of yours at the battlefield: no Battle action at all.
        var absent = WithKeep(Phase.Battle);
        Put(absent, "training-kensai", 0, Zone.Home); Put(absent, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Put(absent, "training-weapon", 0, Zone.FateDiscard); Farm(absent);
        Expect(Offers(Engine(absent), 0, Ability).All(o => !o.Enabled), "no Kensai at the battlefield: not offered");
    }

    private static void AttachAsOpenInAnySegment()
    {
        // The active player, out of battle: the Kensai is at Home (there is no battlefield).
        var s = WithKeep(Phase.Action);
        var kensai = Put(s, "training-kensai", 0, Zone.Home);
        var weapon = Put(s, "training-weapon", 0, Zone.FateDiscard); var farm = Farm(s);
        var e = Engine(s);
        var open = Offer(e, 0, Ability, s.Players[0].StrongholdId, weapon.Id, kensai.Id) with { SourceIds = [farm.Id] };
        Accept(e, open);
        Expect(e.State.Cards[weapon.Id].AttachedTo == kensai.Id && e.State.Cards[weapon.Id].ProvinceIndex is null, "equipped at Home");
        // A player who is NOT the active player, holding priority in the Action phase: Open is usable in ANY player's action segment.
        var t = WithKeep(Phase.Action, actor: 1); t.PriorityPlayerId = 0;
        var kensai2 = Put(t, "training-kensai", 0, Zone.Home);
        var weapon2 = Put(t, "training-weapon", 0, Zone.FateDiscard); var farm2 = Farm(t);
        var te = Engine(t);
        Eq(te.State.ActivePlayerId, 1); Eq(te.State.PriorityPlayerId, 0);
        Accept(te, Offer(te, 0, Ability, t.Players[0].StrongholdId, weapon2.Id, kensai2.Id) with { SourceIds = [farm2.Id] });
        Expect(te.State.Cards[weapon2.Id].AttachedTo == kensai2.Id, "equipped in the opponent's action segment");
        // Bowed Keep: refused in the Action phase as well (the ability is the Keep's).
        var b = WithKeep(Phase.Action); KeepOf(b).Bowed = true;
        Put(b, "training-kensai", 0, Zone.Home); Put(b, "training-weapon", 0, Zone.FateDiscard); Farm(b);
        Expect(Offers(Engine(b), 0, Ability).All(o => !o.Enabled), "a bowed Keep cannot use it");
        // Only a Kensai performs; only your own pile.
        var n = WithKeep(Phase.Action);
        var plain = Put(n, "training-crane-samurai", 0, Zone.Home); Put(n, "training-weapon", 1, Zone.FateDiscard); Farm(n);
        var ne = Engine(n);
        Expect(Offers(ne, 0, Ability).All(o => !o.Enabled), "no Kensai, and the only Weapon is in the enemy pile");
        Reject(ne, new GameCommand("useAbility", 0, ne.Version, n.Players[0].StrongholdId, TargetId: n.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.FateDiscard && c.DefinitionId == "training-weapon").Id, AbilityId: Ability, PerformerId: plain.Id));
    }

    private static void KeepBowsOnlyWhenItPays()
    {
        var s = WithKeep(Phase.Action);
        var kensai = Put(s, "training-kensai", 0, Zone.Home);
        var weapon = Put(s, "training-costly-weapon", 0, Zone.FateDiscard);              // cost 3: the Keep alone (4) pays it
        var e = Engine(s);
        var keep = s.Players[0].StrongholdId;
        var command = Offer(e, 0, Ability, keep, weapon.Id, kensai.Id);
        Expect(command.SourceIds is { Length: > 0 }, "the engine suggests a payment for the quoted 3 Gold");
        var withKeep = command with { SourceIds = [keep] };
        Accept(e, withKeep);
        Expect(KeepOf(e.State).Bowed && e.State.Cards[weapon.Id].AttachedTo == kensai.Id, "paid with the Keep's own production: the Keep is bowed (a cost of the Gold flow, not of the ability)");
    }

    private static void EquipLimitedIsUnchanged()
    {
        foreach (var keep in new[] { true, false })
            foreach (var definition in new[] { "training-weapon", "training-item", "training-follower" })
            {
                var s = keep ? WithKeep(Phase.Action) : Table(Phase.Action);
                var wielder = Put(s, "training-lion-samurai", 0, Zone.Home);
                var card = Put(s, definition, 0, Zone.Hand);
                var e = Engine(s);
                var offer = e.QueryActions(0).Single(a => a.Type == "equip" && a.CardId == card.Id);
                Expect(offer.Enabled && !offer.Label.Contains("Open") && !offer.Label.Contains("Battle"), $"{definition} ({(keep ? "with" : "without")} the Keep): the ordinary Equip (Limited), labelled as before");
                Accept(e, offer.Command!);
                Expect(e.State.Cards[card.Id].AttachedTo == wielder.Id && e.State.Cards[card.Id].ProvinceIndex is null, $"{definition}: equipped at home");
            }
    }

    private static void OpenEquipInAnySegment()
    {
        // Player 0 is not the active player but holds priority.
        MatchState Rig(bool keep, int keepOwner)
        {
            var s = Table(Phase.Action, actor: 1); s.PriorityPlayerId = 0;
            if (keep) s.Cards[s.Players[keepOwner].StrongholdId].DefinitionId = Keep;
            Put(s, "training-lion-samurai", 0, Zone.Home);
            Put(s, "training-weapon", 0, Zone.Hand); Put(s, "training-item", 0, Zone.Hand);
            return s;
        }
        var with = Engine(Rig(true, 0));
        var equips = with.QueryActions(0).Where(a => a.Type == "equip").ToArray();
        Expect(equips.Length == 1 && equips[0].Enabled && equips[0].Label.Contains("Open") && with.State.Cards[equips[0].CardId!].DefinitionId == "training-weapon", "exactly the Weapon is offered, as an Open equip");
        var item = with.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Hand && c.DefinitionId == "training-item");
        Reject(with, new GameCommand("equip", 0, with.Version, item.Id, Src(with, 2), TargetId: equips[0].TargetIds![0]));
        Accept(with, equips[0].Command!);
        Eq(with.State.PriorityPlayerId, 1, "priority passed as after any action");
        foreach (var (keep, owner, label) in new[] { (false, 0, "without a Keep"), (true, 1, "with only the OPPONENT's Keep") })
        {
            var e = Engine(Rig(keep, owner));
            Expect(e.QueryActions(0).All(a => a.Type != "equip"), $"{label}: nothing to equip");
            var weapon = e.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Hand && c.DefinitionId == "training-weapon");
            Reject(e, new GameCommand("equip", 0, e.Version, weapon.Id, Src(e, 1), TargetId: e.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == "training-lion-samurai").Id));
        }
    }

    private static void BattleEquip()
    {
        MatchState Rig(bool keep, bool bowedKeep = false)
        {
            var s = keep ? WithKeep(Phase.Battle) : Table(Phase.Battle);
            if (bowedKeep) KeepOf(s).Bowed = true;
            Put(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            Put(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            Put(s, "training-weapon", 0, Zone.Hand); Put(s, "training-item", 0, Zone.Hand);
            return s;
        }
        var s = Rig(true);
        var home = Put(s, "training-lion-courtier", 0, Zone.Home);
        var e = Engine(s);
        var equips = e.QueryActions(0).Where(a => a.Type == "equip").ToArray();
        var wielder = s.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && c.DefinitionId == "training-lion-samurai");
        Expect(equips.Length == 1 && equips[0].Label.Contains("Battle") && equips[0].TargetIds!.SequenceEqual([wielder.Id]), "the Weapon, as a Battle equip, to the Personality at the battlefield only");
        var weapon = e.State.Cards[equips[0].CardId!];
        var item = e.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Hand && c.DefinitionId == "training-item");
        Reject(e, equips[0].Command! with { TargetId = home.Id });                                        // a wielder at Home
        Reject(e, new GameCommand("equip", 0, e.Version, item.Id, equips[0].Command!.SourceIds, TargetId: wielder.Id));   // not a Weapon
        Accept(e, equips[0].Command!);
        var w = e.State.Cards[weapon.Id];
        Expect(w.Zone == Zone.Attached && w.AttachedTo == wielder.Id && w.ProvinceIndex == 0, "attached, at the battlefield with its wielder");
        Eq(e.State.PriorityPlayerId, 1, "priority passed");
        // The Keep bowed, or no Keep at all: not offered and refused.
        foreach (var (keep, bowed, label) in new[] { (true, true, "a bowed Keep"), (false, false, "no Keep") })
        {
            var x = Engine(Rig(keep, bowed));
            Expect(x.QueryActions(0).All(a => a.Type != "equip"), $"{label}: not offered");
            var ww = x.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Hand && c.DefinitionId == "training-weapon");
            var target = x.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
            Reject(x, new GameCommand("equip", 0, x.Version, ww.Id, Src(x, 1), TargetId: target.Id));
        }
        // ...while the same bowed Keep still grants the OPEN timing out of battle (it is a trait there): Mendon's "During Battle ... the Foothills Keep unbowed".
        var open = WithKeep(Phase.Action); KeepOf(open).Bowed = true;
        Put(open, "training-lion-samurai", 0, Zone.Home); Put(open, "training-weapon", 0, Zone.Hand);
        var oe = Engine(open); open.PriorityPlayerId = 0;
        Expect(oe.QueryActions(0).Any(a => a.Type == "equip" && a.Enabled), "an ordinary Equip (Limited) of course still works; the grant needs nothing of the Keep out of battle");
        var notActive = WithKeep(Phase.Action, actor: 1); notActive.PriorityPlayerId = 0; KeepOf(notActive).Bowed = true;
        Put(notActive, "training-lion-samurai", 0, Zone.Home); Put(notActive, "training-weapon", 0, Zone.Hand);
        Expect(Engine(notActive).QueryActions(0).Any(a => a.Type == "equip" && a.Enabled), "and the Open timing is granted even by a bowed Keep (a trait)");
    }

    private static void BattleEquipIsABattleAction()
    {
        MatchState Rig()
        {
            var s = WithKeep(Phase.Battle);
            Put(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            Put(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            Put(s, "training-weapon", 0, Zone.Hand);
            return s;
        }
        // Two unscoped grants: the first equip uses one and keeps priority; with none, a normal action hands priority over and forfeits nothing left.
        foreach (var (grants, keeps) in new[] { (2, true), (1, false), (0, false) })
        {
            var s = Rig();
            for (var i = 0; i < grants; i++) s.AdditionalActions.Add(new(0, null));
            var e = Engine(s);
            Accept(e, e.QueryActions(0).Single(a => a.Type == "equip" && a.Enabled).Command!);
            Eq(e.State.AdditionalActions.Count, Math.Max(0, grants - 1), $"{grants} grants");
            Eq(e.State.PriorityPlayerId, keeps ? 0 : 1, $"{grants} grants: who holds priority");
        }
        // A grant scoped to some other card is not this action's to use: it is forfeited like by any Battle action.
        var scoped = Rig(); var other = scoped.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        scoped.AdditionalActions.Add(new(0, [other.Id]));
        var se = Engine(scoped);
        Accept(se, se.QueryActions(0).Single(a => a.Type == "equip" && a.Enabled).Command!);
        Expect(se.State.AdditionalActions.Count == 0 && se.State.PriorityPlayerId == 1, "a scoped grant is forfeited, priority passes");
        // Barred where a Battle action is: the first-action "must pass" limit (Rebuilding Kyuden Hida).
        var limited = Rig(); limited.FirstActionRestriction = new(0, null, MustPass: true);
        var le = Engine(limited);
        var w = le.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Hand);
        var t = le.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        Reject(le, new GameCommand("equip", 0, le.Version, w.Id, Src(le, 1), TargetId: t.Id));
    }

    private static void OwnWeaponControl()
    {
        var s = WithKeep(Phase.Action);
        var kensai = Put(s, "training-kensai", 0, Zone.Home);
        var w1 = Put(s, "training-weapon", 0, Zone.FateDiscard); var w2 = Put(s, "training-weapon", 0, Zone.FateDiscard); var w3 = Put(s, "training-weapon", 0, Zone.FateDiscard);
        Farm(s); Farm(s); Farm(s);
        var e = Engine(s);
        var keep = s.Players[0].StrongholdId;
        Accept(e, Offer(e, 0, Ability, keep, w1.Id, kensai.Id));
        Expect(e.State.Cards[w1.Id].OwnerId == 0 && e.State.Cards[w1.Id].OriginalOwnerId is null && !e.State.Events.Any(x => x.Type == "controlTaken"), "no change of control from your own pile");
        var second = Edit(e, st => { st.PriorityPlayerId = 0; st.AbilityUses(keep).Clear(); foreach (var c in st.Cards.Values.Where(c => c.DefinitionId == "training-farm")) c.Bowed = false; });
        Accept(second, Offer(second, 0, Ability, keep, w2.Id, kensai.Id));
        var third = Edit(second, st => { st.PriorityPlayerId = 0; st.AbilityUses(keep).Clear(); foreach (var c in st.Cards.Values.Where(c => c.DefinitionId == "training-farm")) c.Bowed = false; });
        Expect(Offers(third, 0, Ability).All(o => !o.Enabled), "a Kensai wields two Weapons: the third is not offered");
        // Destroyed, it is back in its owner's (your own) pile.
        var gone = Edit(third, st => { st.Cards[w1.Id].Zone = Zone.FateDiscard; st.Cards[w1.Id].AttachedTo = null; });
        Expect(gone.State.Cards[w1.Id].OwnerId == 0 && gone.GetView(0).Players[0].Discard.Any(c => c.Id == w1.Id), "in your own pile");
        Expect(w3.Id.Length > 0);
    }

    private static void OfferedCommandsValidateAll()
    {
        var s = WithKeep(Phase.Battle);
        Put(s, "training-kensai", 0, Zone.Battlefield, province: 0);
        Put(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Put(s, "training-weapon", 0, Zone.FateDiscard); Put(s, "training-unique-weapon", 0, Zone.FateDiscard); Put(s, "training-weapon", 0, Zone.Hand);
        Farm(s);
        var e = Engine(s);
        OfferedCommandsValidate(e, () => Engine(s), alternateTargets: true);
        var t = WithKeep(Phase.Action, actor: 1); t.PriorityPlayerId = 0;
        Put(t, "training-kensai", 0, Zone.Home); Put(t, "training-weapon", 0, Zone.FateDiscard); Put(t, "training-weapon", 0, Zone.Hand); Farm(t);
        OfferedCommandsValidate(Engine(t), () => Engine(t), alternateTargets: true);
    }
}
