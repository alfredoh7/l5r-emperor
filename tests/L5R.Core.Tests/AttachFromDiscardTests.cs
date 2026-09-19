using static RegressionAssertions;
using static Support337;
using L5R.Core;

// Attach a card from a discard pile (issue #337, AttachAction.cs) and Moto Jun-Ni (oracle-l5r-5337), its first card: "Followers in this unit have
// Cavalry. Limited: Target a Follower in any player's discard pile: Attach it to Jun-Ni, paying all costs." The three cost modes (full, discounted, free)
// and the Foothills Keep shape (a performing Kensai equips a Weapon from your own pile, Battle/Open) are exercised on training fixtures so each parameter is
// proved before any card needs it. Every reading is in docs/decisions/2026-09-19-alternative-costs-attach-from-discard.md. Player 0 acts.
internal static class AttachFromDiscardTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Moto Jun-Ni is the printed card (Force 4, Chi 2, Honor Requirement 2, Cost 8, Personal Honor 2, Unicorn Cavalry Commander Demon Slayer Samurai), whole -- her Effect is the live Cavalry grant and her one ability a Limited attach from any discard pile paying all costs", PublishedShape),
            ("Offered targets are exactly the Followers in EITHER player's discard pile (not Items, hand cards, cards in play or removed from the game), each with its own derived Gold quote at the printed cost and a suggested payment", OffersEveryFollowerInEitherDiscardPile),
            ("Own Follower: the Gold is paid FIRST (the payment is logged before the card is attached), the Follower leaves the pile and attaches to Jun-Ni face up, priority passes, and the Limited use is spent", AttachOwnFollowerPaysThenAttaches),
            ("An opponent's Follower may be attached too: it stays THEIR card (owner unchanged, so it returns to their pile when destroyed) and the discard piles are public, so both players see it move", AttachOpponentsFollower),
            ("The cost is quoted and paid through the Gold path before the card leaves the pile: an underpaid command is refused with the card still in the pile and nothing bowed, and with no Gold left the action is listed disabled", CostIsPaidBeforeTheCardLeaves),
            ("Legality is Equip's legality: the Honor Requirement (a dishonorable Jun-Ni has no Personal Honor), \"Will only attach to\" restrictions, and Unique/Singular in-play copies each keep a discarded Follower out of the offered targets and refuse it if submitted", LegalityIsEquipLegality),
            ("A card returning from a discard pile is a NEW entry into play: stale modifiers, tokens, granted keywords, once-per-turn stamps and bowed state are gone", ReEntryIsFresh),
            ("Its \"after this enters play\" effects fire on the attach (a Destined Follower draws a card), like an ordinary Equip", EntryEffectsFire),
            ("\"Followers in this unit have Cavalry\" is live: an attached Follower has Cavalry while it is in her unit and not in a discard pile or another unit, and her unit is a Cavalry unit in Cavalry Maneuvers although the Follower is Infantry (a control unit is not)", CavalryGrantIsLive),
            ("Cost modes are data: full cost (\"paying all costs\": the attachment's Equip cost), discounted (\"paying 1 less Gold\", never below 0) and free (\"ignoring costs\": no Gold at all)", CostModes),
            ("Foothills Keep's shape: Battle/Open, a performing Kensai equips a Weapon from YOUR OWN pile to himself, paying its cost -- only Kensai perform, only Weapons and only your pile qualify, the Weapon cap applies, and in a battle the Weapon joins the wielder's battlefield", FoothillsShape),
            ("A discount printed for attachments \"from your hand\" (Mirumoto Katagi's) does not apply to an attach from a discard pile", KatagiDiscountOnlyFromHand),
            ("Hidden information: the opponent is only told to wait, sees the attached card (a public pile becomes a public unit) and still cannot see your hand", HiddenInformation),
            ("Every offered Jun-Ni command validates on a fresh engine, with every alternate target", OfferedJunNiCommandsValidate),
        ];
        runner.Run("attach-from-discard", tests);
    }

    private const string JunNiAbility = "jun-ni-attach";
    private sealed record Rig(MatchState S, CardInstance JunNi);

    private static Rig ActionPhase()
    {
        var s = Table(Phase.Action);
        var jun = Put(s, Support337.JunNi, 0, Zone.Home);
        return new(s, jun);
    }

    private static string[] Sources(MatchEngine e, int player) => e.State.Cards.Values.Where(c => c.OwnerId == player && c.Bowed && c.Zone is Zone.Home or Zone.Stronghold).Select(c => c.Id).ToArray();

    private static void PublishedShape()
    {
        var d = CardCatalog.Get(Support337.JunNi);
        Expect(!d.ScenarioOnly && d.Type == CardType.Personality && d.Name == "Moto Jun-Ni" && d.Clan == "Unicorn");
        // Verified against the scan: Force 4, Chi 2, Honor Requirement 2, Gold Cost 8, Personal Honor 2.
        Eq(d.Force, 4); Eq(d.Chi, 2); Eq(d.GoldCost, 8); Eq(d.PersonalHonor, 2); Eq(d.HonorRequirement, 2);
        foreach (var k in new[] { "Unicorn Clan", "Samurai", "Cavalry", "Commander", "Demon Slayer" }) Expect(d.Keywords!.Contains(k), k);
        Eq(d.Text, "Followers in this unit have Cavalry.\nLimited: Target a Follower in any player's discard pile: Attach it to Jun-Ni, paying all costs.");
        Eq(d.Effect, "unit-followers-cavalry"); Expect(d.Abilities!.SequenceEqual([JunNiAbility]), "one ability");
        var a = AbilityCatalog.All[JunNiAbility];
        Eq(a.Timing, "Limited"); Eq(a.Target, "attachment-in-discard");
        Eq(a.Cost!.Basis, GoldCostBasis.AttachCost); Eq(a.Cost.Modifier, 0); Eq(a.Cost.Kind, GoldCostKind.Derived);
        Eq(a.Attach!.Card, AttachSource.Target); Eq(a.Attach.Pile, AttachPile.Any); Eq(a.Attach.Wielder, AttachWielder.Source); Expect(a.Attach.Types!.SequenceEqual([CardType.Follower]));
        Expect(ArtShipped(Support337.JunNi), "art is shipped"); Eq(d.ArtworkKey, $"res://Content/Published/{Support337.JunNi}.jpg");
    }

    private static void OffersEveryFollowerInEitherDiscardPile()
    {
        var r = ActionPhase();
        var enemy = Put(r.S, "training-lion-samurai", 1, Zone.Home);
        var own = Put(r.S, "training-follower", 0, Zone.FateDiscard);
        var theirs = Put(r.S, "training-follower", 1, Zone.FateDiscard);
        Put(r.S, "training-item", 0, Zone.FateDiscard);                                  // not a Follower
        Put(r.S, "training-follower", 0, Zone.Hand);                                     // in hand
        Put(r.S, "training-follower", 1, Zone.Attached, parent: enemy.Id);               // in play
        Put(r.S, "training-follower", 0, Zone.RemovedFromGame);                          // removed from the game
        var e = Engine(r.S);
        var offer = Offers(e, 0, JunNiAbility).Single(o => o.Enabled);
        Expect(offer.TargetIds!.ToHashSet().SetEquals([own.Id, theirs.Id]), $"exactly the two discarded Followers, got {string.Join(", ", offer.TargetIds!)}");
        Expect(offer.TargetGold is { Count: 2 }, "one quote per legal target");
        foreach (var id in new[] { own.Id, theirs.Id })
        {
            var quote = offer.TargetGold![id];
            Eq(quote.Kind, GoldCostKind.Derived); Eq(quote.Cost, 2, "the Follower's printed cost, no discount applies");
            Expect(quote.Error is null && quote.Payments.Length > 0 && quote.Basis.Contains("Training ashigaru"), $"a payable quote: {quote.Basis}");
        }
    }

    private static void AttachOwnFollowerPaysThenAttaches()
    {
        var r = ActionPhase();
        var follower = Put(r.S, "training-follower", 0, Zone.FateDiscard);
        var e = Engine(r.S);
        var command = Offer(e, 0, JunNiAbility, r.JunNi.Id, follower.Id);
        Expect(command.SourceIds is { Length: > 0 }, "the engine suggested a payment for the quoted cost");
        Accept(e, command);
        var s = e.State;
        var attached = s.Cards[follower.Id];
        Expect(attached.Zone == Zone.Attached && attached.AttachedTo == r.JunNi.Id && attached.FaceUp, "attached to Jun-Ni, face up");
        Expect(s.Cards.Values.Count(c => c.Zone == Zone.FateDiscard && c.OwnerId == 0 && c.Id == follower.Id) == 0, "no longer in the discard pile");
        var payment = s.Events.FindIndex(x => x.Type == "payment" && x.CardId == follower.Id);
        var equipped = s.Events.FindIndex(x => x.Type == "equipped" && x.CardId == follower.Id);
        Expect(payment >= 0 && equipped > payment, "the Gold was paid before the card was attached");
        Expect(s.Events[equipped].Message.Contains("from a discard pile"), "the log says where it came from");
        Eq(s.Events[payment].Message.Contains("paid 2 Gold"), true, "the whole cost was charged");
        Expect(command.SourceIds!.All(id => s.Cards[id].Bowed), "the chosen sources are bowed");
        Eq(s.PriorityPlayerId, 1, "priority passed");
        Expect(Offers(e, 0, JunNiAbility).All(o => !o.Enabled), "Limited: once per turn");
    }

    private static void AttachOpponentsFollower()
    {
        var r = ActionPhase();
        var theirs = Put(r.S, "training-follower", 1, Zone.FateDiscard);
        var e = Engine(r.S);
        Accept(e, Offer(e, 0, JunNiAbility, r.JunNi.Id, theirs.Id));
        var moved = e.State.Cards[theirs.Id];
        Expect(moved.Zone == Zone.Attached && moved.AttachedTo == r.JunNi.Id, "attached to MY Personality");
        Eq(moved.OwnerId, 1, "still the opponent's card: it goes back to their discard pile when it leaves play");
        Expect(e.GetView(1).Players[1].Discard.All(c => c.Id != theirs.Id), "their pile no longer holds it (the pile is public)");
        Expect(e.GetView(1).Players[0].Home.Any(c => c.Id == theirs.Id && c.AttachedTo == r.JunNi.Id), "and both players see it in Jun-Ni's unit");
        Expect(e.GetView(0).Players[0].Home.Any(c => c.Id == theirs.Id && c.AttachedTo == r.JunNi.Id));
        Expect(e.GetView(1).Players[1].Home.All(c => c.Id != theirs.Id), "it is listed with the unit's controller, not with its owner, so a client can group the unit");
    }

    private static void CostIsPaidBeforeTheCardLeaves()
    {
        var r = ActionPhase();
        var follower = Put(r.S, "training-follower", 0, Zone.FateDiscard);
        var e = Engine(r.S);
        var command = Offer(e, 0, JunNiAbility, r.JunNi.Id, follower.Id);
        Reject(e, command with { SourceIds = [] });                 // nothing paid
        Reject(e, command with { SourceIds = null });
        Reject(e, command with { SourceIds = ["no-such-card"] });
        Expect(e.State.Cards[follower.Id].Zone == Zone.FateDiscard && e.State.Cards.Values.All(c => !c.Bowed || c.OwnerId == 1), "refused: still in the pile, nothing bowed");
        // With every Gold source bowed there is nothing to pay with: listed, disabled, and the reason names the payment.
        var tapped = Edit(e, st => { foreach (var c in st.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true; });
        var offer = Offers(tapped, 0, JunNiAbility).Single();
        Expect(!offer.Enabled && offer.Reason is { Length: > 0 }, $"no Gold: disabled with a reason ({offer.Reason})");
        Reject(tapped, command);
    }

    private static void LegalityIsEquipLegality()
    {
        var r = ActionPhase();
        var ok = Put(r.S, "training-follower", 0, Zone.FateDiscard);                       // Honor Requirement 1
        var craneOnly = Put(r.S, "training-follower-crane-guard", 0, Zone.FateDiscard);    // will only attach to a Crane Clan Personality
        var named = Put(r.S, "training-follower-named", 0, Zone.FateDiscard);              // attaches only to a named Personality
        var e = Engine(r.S);
        Expect(Offers(e, 0, JunNiAbility).Single(o => o.Enabled).TargetIds!.SequenceEqual([ok.Id]), "only the Follower that will attach to a Unicorn with Personal Honor 2");
        foreach (var bad in new[] { craneOnly.Id, named.Id })
        {
            var fresh = Engine(r.S);
            Reject(fresh, Offer(fresh, 0, JunNiAbility, r.JunNi.Id, ok.Id) with { TargetId = bad });
        }
        // The Honor Requirement reads her CURRENT Personal Honor: dishonorable is 0.
        var dishonored = Edit(e, st => st.Cards[r.JunNi.Id].Dishonorable = true);
        Expect(Offers(dishonored, 0, JunNiAbility).All(o => !o.Enabled), "a dishonorable Jun-Ni cannot take a Follower with an Honor Requirement");
        // Unique / Singular: a copy already in play under my control keeps the discarded one out.
        foreach (var (definition, word) in new[] { ("training-unique-follower", "Unique"), ("training-singular-follower", "Singular") })
        {
            var u = ActionPhase();
            Put(u.S, definition, 0, Zone.Attached, parent: u.JunNi.Id);
            var copy = Put(u.S, definition, 0, Zone.FateDiscard);
            var ue = Engine(u.S);
            Expect(Offers(ue, 0, JunNiAbility).All(o => !o.Enabled), $"a second {word} copy is not offered");
            Reject(ue, new GameCommand("useAbility", 0, ue.Version, u.JunNi.Id, TargetId: copy.Id, AbilityId: JunNiAbility, SourceIds: Sources(ue, 0)));
        }
        {
            var u = ActionPhase();
            var copy = Put(u.S, "training-unique-follower", 0, Zone.FateDiscard);
            var ue = Engine(u.S);
            Accept(ue, Offer(ue, 0, JunNiAbility, u.JunNi.Id, copy.Id));
        }
    }

    private static void ReEntryIsFresh()
    {
        var r = ActionPhase();
        var follower = Put(r.S, "training-follower", 0, Zone.FateDiscard);
        follower.Modifiers.Add(new("test", "test", "Force", 5)); follower.GrantedKeywords.Add(new("Elite", "test", "Permanent")); follower.RemovedKeywords.Add("Samurai");
        follower.Tokens.Add(new("token-x", "Fire", "test", "Force", 1)); follower.AbilityUses["stamp"] = 1; follower.Bowed = true; follower.Protections.Add(new("test", "Turn"));
        var e = Engine(r.S);
        Accept(e, Offer(e, 0, JunNiAbility, r.JunNi.Id, follower.Id));
        var c = e.State.Cards[follower.Id];
        Expect(c.Modifiers.Count == 0 && c.GrantedKeywords.Count == 0 && c.RemovedKeywords.Count == 0 && c.Tokens.Count == 0 && c.AbilityUses.Count == 0 && c.Protections.Count == 0 && !c.Bowed,
            "nothing it carried out of its last life follows it back in");
    }

    private static void EntryEffectsFire()
    {
        var r = ActionPhase();
        var destined = Put(r.S, "training-destined-follower", 0, Zone.FateDiscard);
        var e = Engine(r.S);
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Offer(e, 0, JunNiAbility, r.JunNi.Id, destined.Id));
        Eq(e.State.Players[0].Hand.Count, hand + 1, "Destined: a card drawn as it entered play");
    }

    private static void CavalryGrantIsLive()
    {
        var s = Table(Phase.Action);
        var jun = Put(s, Support337.JunNi, 0, Zone.Home);
        var follower = Put(s, "training-follower", 0, Zone.FateDiscard);
        var other = Put(s, "training-cavalry", 0, Zone.Home);
        var otherFollower = Put(s, "training-follower", 0, Zone.Attached, parent: other.Id);
        var e = Engine(s);
        Expect(!e.HasKeyword(follower.Id, "Cavalry"), "not in her unit yet: a Follower in a discard pile");
        Expect(!e.HasKeyword(otherFollower.Id, "Cavalry"), "not in another unit");
        Accept(e, Offer(e, 0, JunNiAbility, jun.Id, follower.Id));
        Expect(e.HasKeyword(follower.Id, "Cavalry"), "in her unit: Cavalry");
        Expect(!e.HasKeyword(otherFollower.Id, "Cavalry"), "another unit's Follower still has none");
        // Cavalry Maneuvers: a unit is Cavalry only if the Personality AND every attached Follower is. Hers is; the control unit (a Cavalry
        // Personality with the same Infantry Follower) is not.
        var maneuvers = Edit(e, st => { st.Phase = Phase.CavalryDeclaration; st.PriorityPlayerId = 0; st.ActivePlayerId = 0; st.PendingChoice = null; foreach (var c in st.Cards.Values) c.Bowed = false; });
        var attack = maneuvers.QueryActions(0).First(a => a.Type == "attack");
        Expect(attack.TargetIds!.Contains(jun.Id) && !attack.TargetIds.Contains(other.Id), "Jun-Ni's unit assigns in Cavalry Maneuvers; the control unit does not");
        var infantry = Edit(maneuvers, st => { st.Phase = Phase.InfantryDeclaration; });
        var infantryAttack = infantry.QueryActions(0).First(a => a.Type == "attack");
        Expect(!infantryAttack.TargetIds!.Contains(jun.Id) && infantryAttack.TargetIds.Contains(other.Id), "and the reverse in Infantry Maneuvers");
        // Gone the moment it leaves her unit.
        var gone = Edit(e, st => { st.Cards[follower.Id].Zone = Zone.FateDiscard; st.Cards[follower.Id].AttachedTo = null; });
        Expect(!gone.HasKeyword(follower.Id, "Cavalry"), "detached: no longer Cavalry");
    }

    private static void CostModes()
    {
        var s = Table(Phase.Action);
        var source = Put(s, "training-discard-attacher", 0, Zone.Home);
        var weapon = Put(s, "training-costly-weapon", 0, Zone.FateDiscard);                 // cost 3
        var cheap = Put(s, "training-weapon", 0, Zone.FateDiscard);                         // cost 1
        var unique = Put(s, "training-unique-weapon", 0, Zone.FateDiscard);                 // Unique: excluded by the discount ability
        var follower = Put(s, "training-follower", 1, Zone.FateDiscard);                    // free ability takes any Follower, either pile
        var e = Engine(s);
        // Discounted: printed cost - 1, never below 0.
        var discount = Offers(e, 0, "training-attach-weapon-discount").Single(o => o.Enabled);
        Expect(discount.TargetIds!.ToHashSet().SetEquals([weapon.Id, cheap.Id]), "non-Unique Weapons of my own pile only");
        Eq(discount.TargetGold![weapon.Id].Cost, 2, "3 - 1"); Eq(discount.TargetGold[cheap.Id].Cost, 0, "1 - 1, never below 0");
        Expect(discount.TargetGold[weapon.Id].Basis.Contains("− 1"), "the basis shows the discount");
        Accept(e, Offer(e, 0, "training-attach-weapon-discount", source.Id, weapon.Id));
        Eq(e.State.Events.Single(x => x.Type == "payment" && x.CardId == weapon.Id).Message.Contains("paid 2 Gold"), true, "the discounted price was charged");
        // Free: no Gold quote at all, no payment event, no sources.
        var later = Edit(e, st => st.PriorityPlayerId = 0);
        var freeOffer = Offers(later, 0, "training-attach-follower-free").Single(o => o.Enabled);
        Expect(freeOffer.Gold is null && freeOffer.TargetGold is null, "ignoring costs: nothing to quote");
        var command = Offer(later, 0, "training-attach-follower-free", source.Id, follower.Id);
        Expect(command.SourceIds is null or { Length: 0 }, "no sources");
        var before = later.State.Events.Count(x => x.Type == "payment");
        Accept(later, command);
        Eq(later.State.Events.Count(x => x.Type == "payment"), before, "no payment was made");
        Eq(later.State.Cards[follower.Id].AttachedTo, source.Id);
        // Full: the Foothills Keep shape charges the whole printed cost.
        var armory = Table(Phase.Action);
        var kensai = Put(armory, "training-kensai", 0, Zone.Home); Put(armory, "training-discard-armory", 0, Zone.Home);
        var costly = Put(armory, "training-costly-weapon", 0, Zone.FateDiscard);
        var full = Engine(armory);
        Eq(Offers(full, 0, "training-attach-weapon-performer").Single(o => o.Enabled).TargetGold![costly.Id].Cost, 3, "paying all costs");
    }

    private static void FoothillsShape()
    {
        const string Ability = "training-attach-weapon-performer";
        var s = Table(Phase.Action);
        var armory = Put(s, "training-discard-armory", 0, Zone.Home);
        var kensai = Put(s, "training-kensai", 0, Zone.Home);
        var samurai = Put(s, "training-crane-samurai", 0, Zone.Home);                       // not a Kensai
        var mine = Put(s, "training-weapon", 0, Zone.FateDiscard);
        var uniqueMine = Put(s, "training-unique-weapon", 0, Zone.FateDiscard);
        var theirs = Put(s, "training-weapon", 1, Zone.FateDiscard);                        // their pile: never yours
        Put(s, "training-follower", 0, Zone.FateDiscard);                                   // not a Weapon
        var e = Engine(s);
        var offers = Offers(e, 0, Ability).Where(o => o.Enabled).ToArray();
        Expect(offers.Length == 1 && offers[0].Command!.PerformerId == kensai.Id, "one performer: the Kensai");
        Expect(offers[0].TargetIds!.ToHashSet().SetEquals([mine.Id, uniqueMine.Id]), "Weapons in MY pile only");
        Reject(e, Offer(e, 0, Ability, armory.Id, mine.Id, kensai.Id) with { PerformerId = samurai.Id });
        Reject(e, Offer(e, 0, Ability, armory.Id, mine.Id, kensai.Id) with { TargetId = theirs.Id });
        // Open (the Action phase): the Weapon goes to the performer, not to the card whose ability it is.
        var command = Offer(e, 0, Ability, armory.Id, mine.Id, kensai.Id);
        Accept(e, command);
        Expect(e.State.Cards[mine.Id].AttachedTo == kensai.Id && e.State.Cards[mine.Id].Zone == Zone.Attached, "equipped to the performing Kensai");
        // The cap: a Kensai wields two Weapons (Two-Handed and the like aside), so a third is not offered.
        var second = Edit(e, st => { st.PriorityPlayerId = 0; st.AbilityUses(armory.Id).Clear(); });
        Accept(second, Offer(second, 0, Ability, armory.Id, uniqueMine.Id, kensai.Id));
        var extra = Edit(second, st => { Put(st, "training-weapon", 0, Zone.FateDiscard); st.PriorityPlayerId = 0; st.AbilityUses(armory.Id).Clear(); });
        Expect(Offers(extra, 0, Ability).All(o => !o.Enabled), "two Weapons is the Kensai's limit");
        // Battle (the other half of Battle/Open): the performer must be at the battlefield, and the Weapon joins his battlefield.
        var b = Table(Phase.Battle);
        var armory2 = Put(b, "training-discard-armory", 0, Zone.Home);
        var home = Put(b, "training-kensai", 0, Zone.Home);
        var there = Put(b, "training-kensai", 0, Zone.Battlefield, province: 0);
        Put(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var weapon = Put(b, "training-weapon", 0, Zone.FateDiscard);
        var be = Engine(b);
        Expect(Offers(be, 0, Ability).Where(o => o.Enabled).All(o => o.Command!.PerformerId == there.Id), "in a battle only the Kensai at the battlefield performs");
        Accept(be, Offer(be, 0, Ability, armory2.Id, weapon.Id, there.Id));
        Eq(be.State.Cards[weapon.Id].ProvinceIndex, 0, "the Weapon is at its wielder's battlefield");
        Expect(be.State.Cards[home.Id].Zone == Zone.Home, "the Kensai at home is untouched");
    }

    private static void KatagiDiscountOnlyFromHand()
    {
        var s = Table(Phase.Action);
        var armory = Put(s, "training-discard-armory", 0, Zone.Home);
        var katagi = Put(s, "oracle-l5r-5112", 0, Zone.Home);
        var inPile = Put(s, "training-costly-weapon", 0, Zone.FateDiscard);
        var inHand = Put(s, "training-costly-weapon", 0, Zone.Hand);
        var e = Engine(s);
        var pileQuote = Offers(e, 0, "training-attach-weapon-performer").Single(o => o.Enabled).TargetGold![inPile.Id];
        Eq(pileQuote.Cost, 3, "from a discard pile: the full printed cost, no Katagi discount");
        var handEquip = e.QueryActions(0).First(a => a.Type == "equip" && a.CardId == inHand.Id);
        Eq(handEquip.TargetGold![katagi.Id].Cost, 0, "from hand it is 4 less, so free");
    }

    private static void HiddenInformation()
    {
        var r = ActionPhase();
        var follower = Put(r.S, "training-follower", 0, Zone.FateDiscard);
        var secret = Put(r.S, "training-item", 0, Zone.Hand);
        Put(r.S, "training-lion-samurai", 1, Zone.Home);
        var e = Engine(r.S);
        Expect(e.QueryActions(1).All(a => a.Type is "wait" or "concede"), "the opponent is only told to wait");
        Accept(e, Offer(e, 0, JunNiAbility, r.JunNi.Id, follower.Id));
        var view = e.GetView(1);
        Eq(view.Players[0].Hand.Length, 0, "the hand stays private"); Eq(view.Players[0].HandCount, 1);
        HiddenIdsAbsent(view, [secret.Id]);
        Expect(e.QueryActions(1).Where(a => a.Type == "useAbility").All(a => a.CardId != r.JunNi.Id), "nothing of Jun-Ni's is offered across");
    }

    private static void OfferedJunNiCommandsValidate()
    {
        var r = ActionPhase();
        Put(r.S, "training-follower", 0, Zone.FateDiscard);
        Put(r.S, "training-follower", 1, Zone.FateDiscard);
        Put(r.S, "training-unique-follower", 1, Zone.FateDiscard);
        var e = Engine(r.S);
        OfferedCommandsValidate(e, () => Engine(r.S), alternateTargets: true);
    }
}

internal static class TestStateExtensions337
{
    // A card's once-per-turn stamps, so a test can let an ability be used again.
    public static Dictionary<string, int> AbilityUses(this MatchState state, string cardId) => state.Cards[cardId].AbilityUses;
}
