using static RegressionAssertions;
using static Support337;
using L5R.Core;

// Fading Shadows (oracle-l5r-2437, issue #337): "You may include up to 10 copies of this card in your deck. Will only attach to a Ninja. After this Follower
// enters play, lose 1 Honor. Ninja Battle: If this Follower is in your hand, target your performing Ninja and Equip it to him." The Battle action is the attach
// primitive with the SOURCE card as the attachment (AttachSource.Self) and the performing Ninja as the wielder; the copy limit is CardDefinition.DeckCopyLimit,
// read by the deck builder. Every reading is in docs/decisions/2026-09-19-alternative-costs-attach-from-discard.md. Player 0 acts.
internal static class FadingShadowsTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Fading Shadows is the printed card (Force 1, Chi 0, Honor Requirement 0, Cost 0, Ninja), whole -- Will only attach to a Ninja, a mandatory 1 Honor loss on entry, the Ninja Battle attach from hand and a copy limit of 10 -- and it does NOT carry the Reserve keyword", PublishedShape),
            ("The Ninja Battle action is offered once per performing Ninja at the battlefield (bowed or not), for a Fading Shadows in hand only, in the Battle phase only; a non-Ninja, a Ninja at home and a card anywhere but your hand are never offered", OfferedToNinjasFromHandInBattle),
            ("Equip flow: the card leaves the hand and attaches to the performing Ninja at his battlefield (so it counts in his unit), the mandatory Honor loss applies, no Gold is needed, priority passes and no additional action is granted", EquipsFromHand),
            ("Will only attach to a Ninja: every non-Ninja performer is refused, however the command is built; a Personality that HAS Ninja (granted) is a Ninja", OnlyNinjasReceiveIt),
            ("Two copies in hand are two separate offers per Ninja, each with its own once-per-turn use", TwoCopiesAreTwoOffers),
            ("Hidden information: the opponent is offered nothing, the card's identity is not in their view while it is in hand, and it is public once attached", HiddenInformation),
            ("Deck builder: 10 copies raise no note, 11 raise one that names the card's own limit of 10; every other card keeps the usual 3 (1 if Unique), and Fading Shadows' limit does not leak onto them", DeckCopyLimit),
            ("Deck copy limit round trip: a deck file with 10 Fading Shadows loads and reviews clean; the same file with 11 is kept (never hard-blocked) and flagged", DeckFileRoundTrip),
            ("Every offered Fading Shadows command validates on a fresh engine", OfferedFadingShadowsCommandsValidate),
        ];
        runner.Run("fading-shadows", tests);
    }

    private const string Ability = "fading-shadows-equip";

    private sealed record Rig(MatchState S, CardInstance Ninja, CardInstance Card);

    private static Rig Battle()
    {
        var s = Table(Phase.Battle);
        var ninja = Put(s, "training-ninja-source", 0, Zone.Battlefield, province: 0);
        Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var card = Put(s, Support337.FadingShadows, 0, Zone.Hand);
        return new(s, ninja, card);
    }

    private static void PublishedShape()
    {
        var d = CardCatalog.Get(Support337.FadingShadows);
        Expect(!d.ScenarioOnly && d.Type == CardType.Follower && d.Name == "Fading Shadows" && d.Clan == "Unaligned");
        // Verified against the scan: Force 1, Chi 0, Honor Requirement 0, Gold Cost 0, Focus 0.
        Eq(d.Force, 1); Eq(d.Chi, 0); Eq(d.GoldCost, 0); Eq(d.HonorRequirement, 0); Eq(d.Focus, 0);
        Expect(d.Keywords!.SequenceEqual(["Ninja"]), "the Ninja keyword, and no Reserve keyword");
        Eq(d.Text, "You may include up to 10 copies of this card in your deck.\nWill only attach to a Ninja.\nAfter this Follower enters play, lose 1 Honor.\nNinja Battle: If this Follower is in your hand, target your performing Ninja and Equip it to him.");
        Eq(d.AttachRequiresKeyword, "Ninja"); Eq(d.EntryHonorLoss, 1); Eq(d.DeckCopyLimit, 10);
        Expect(d.Abilities!.SequenceEqual([Ability]), "one ability"); Eq(d.Effect, "none");
        var a = AbilityCatalog.All[Ability];
        Eq(a.Timing, "Battle"); Eq(a.Target, "none"); Eq(a.Performer, "own-personality-with-keyword-at-battle"); Expect(a.RequiredKeywords!.SequenceEqual(["Ninja"]));
        Eq(a.Attach!.Card, AttachSource.Self); Eq(a.Attach.Wielder, AttachWielder.Performer); Eq(a.Cost!.Basis, GoldCostBasis.AttachCost);
        Expect(ArtShipped(Support337.FadingShadows), "art is shipped"); Eq(d.ArtworkKey, $"res://Content/Published/{Support337.FadingShadows}.jpg");
    }

    private static void OfferedToNinjasFromHandInBattle()
    {
        var r = Battle();
        var bowedNinja = Put(r.S, "training-ninja-source", 0, Zone.Battlefield, bowed: true, province: 0);
        var samurai = Put(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);                 // not a Ninja
        var homeNinja = Put(r.S, "training-ninja-source", 0, Zone.Home);                                     // a Ninja, but not at the battlefield
        var discarded = Put(r.S, Support337.FadingShadows, 0, Zone.FateDiscard);                             // not in hand
        var e = Engine(r.S);
        var offers = Offers(e, 0, Ability).Where(o => o.Enabled).ToArray();
        Expect(offers.All(o => o.CardId == r.Card.Id), "only the copy in hand is a source");
        Expect(offers.Select(o => o.Command!.PerformerId).OrderBy(x => x).SequenceEqual(new[] { r.Ninja.Id, bowedNinja.Id }.OrderBy(x => x)), "a performer per Ninja at the battlefield, bowed or not");
        Expect(offers.All(o => o.Command!.TargetId is null && o.TargetIds!.Length == 0), "the performing Ninja is the wielder: there is no separate target");
        foreach (var bad in new[] { samurai.Id, homeNinja.Id })
            Reject(e, Offer(e, 0, Ability, r.Card.Id, performerId: r.Ninja.Id) with { PerformerId = bad });
        Reject(e, Offer(e, 0, Ability, r.Card.Id, performerId: r.Ninja.Id) with { CardId = discarded.Id });
        // Battle timing: nothing in the Action phase.
        var action = Table(Phase.Action);
        Put(action, "training-ninja-source", 0, Zone.Home); Put(action, Support337.FadingShadows, 0, Zone.Hand);
        var ae = Engine(action);
        Expect(Offers(ae, 0, Ability).All(o => !o.Enabled), "a Battle action: not in the Action phase");
    }

    private static void EquipsFromHand()
    {
        var r = Battle();
        var e = Engine(r.S);
        var honor = e.State.Players[0].Honor; var hand = e.State.Players[0].Hand.Count;
        var unitBefore = e.GetView(0).Players[0].Army.Single(c => c.Id == r.Ninja.Id).UnitForce;
        var command = Offer(e, 0, Ability, r.Card.Id, performerId: r.Ninja.Id);
        Expect(command.SourceIds is null or { Length: 0 }, "it costs nothing, so no sources are named");
        Accept(e, command);
        var s = e.State; var card = s.Cards[r.Card.Id];
        Expect(card.Zone == Zone.Attached && card.AttachedTo == r.Ninja.Id && card.FaceUp, "attached to the performing Ninja");
        Eq(card.ProvinceIndex, 0, "at his battlefield, so it is in his unit there");
        Eq(s.Players[0].Hand.Count, hand - 1, "left the hand"); Expect(!s.Players[0].Hand.Contains(card.Id));
        Eq(s.Players[0].Honor, honor - 1, "After this Follower enters play, lose 1 Honor");
        var view = e.GetView(1);
        Eq(view.Players[0].Army.Single(c => c.Id == r.Ninja.Id).UnitForce, unitBefore + 1, "its Force 1 joins the unit");
        Eq(s.PriorityPlayerId, 1, "priority passed"); Expect(s.AdditionalActions.Count == 0, "no additional action is granted (unlike Equip (Battle)'s Spell)");
        Expect(s.Events.Any(x => x.Type == "equipped" && x.CardId == card.Id), "it is in the public log");
        Expect(Offers(e, 0, Ability).All(o => !o.Enabled) || e.QueryActions(0).All(a => a.Type is "wait" or "concede"), "spent");
    }

    private static void OnlyNinjasReceiveIt()
    {
        var r = Battle();
        var samurai = Put(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = Engine(r.S);
        var command = Offer(e, 0, Ability, r.Card.Id, performerId: r.Ninja.Id);
        Reject(e, command with { PerformerId = samurai.Id });
        Reject(e, command with { PerformerId = null });
        Reject(e, command with { TargetId = samurai.Id });                       // a target is not part of this action
        var granted = Edit(e, st => st.Cards[samurai.Id].GrantedKeywords.Add(new("Ninja", "test", "Permanent")));
        Accept(granted, command with { PerformerId = samurai.Id, ExpectedVersion = granted.Version });
        Eq(granted.State.Cards[r.Card.Id].AttachedTo, samurai.Id);
    }

    private static void TwoCopiesAreTwoOffers()
    {
        var r = Battle();
        var second = Put(r.S, Support337.FadingShadows, 0, Zone.Hand);
        var e = Engine(r.S);
        Eq(Offers(e, 0, Ability).Count(o => o.Enabled), 2, "one offer per copy");
        Accept(e, Offer(e, 0, Ability, r.Card.Id, performerId: r.Ninja.Id));
        var again = Edit(e, st => st.PriorityPlayerId = 0);
        var rest = Offers(again, 0, Ability).Where(o => o.Enabled).ToArray();
        Expect(rest.Length == 1 && rest[0].CardId == second.Id, "the other copy still has its own use this turn");
        Accept(again, Offer(again, 0, Ability, second.Id, performerId: r.Ninja.Id));
        Eq(again.State.Cards.Values.Count(c => c.AttachedTo == r.Ninja.Id), 2);
    }

    private static void HiddenInformation()
    {
        var r = Battle();
        var e = Engine(r.S);
        Expect(e.QueryActions(1).All(a => a.Type is "wait" or "concede"), "the opponent is only told to wait");
        HiddenIdsAbsent(e.GetView(1), [r.Card.Id]);
        Eq(e.GetView(1).Players[0].HandCount, 1); Eq(e.GetView(1).Players[0].Hand.Length, 0);
        Accept(e, Offer(e, 0, Ability, r.Card.Id, performerId: r.Ninja.Id));
        Expect(e.GetView(1).Players[0].Army.Any(c => c.Id == r.Card.Id && c.Definition!.Name == "Fading Shadows"), "public once attached");
    }

    private static DeckCardInfo Info(string id)
    {
        var d = CardCatalog.Get(id);
        return new(d.Id, d.Name, [d.Type.ToString()], [d.Type is CardType.Personality or CardType.Holding ? "Dynasty" : "Fate"], d.Keywords ?? [], true, d.CreatedOnly, d.DeckCopyLimit);
    }

    private static DeckCardInfo[] Pool() => [Info(Support337.FadingShadows),
        new("plain", "Plain Follower", ["Follower"], ["Fate"], [], true), new("unique", "Unique Samurai", ["Personality"], ["Dynasty"], ["Unique"], true)];

    private static void DeckCopyLimit()
    {
        foreach (var (copies, flagged) in new[] { (1, false), (3, false), (4, false), (10, false), (11, true), (40, true) })
        {
            var d = new DeckWorkshop(Pool()); d.Adjust("Fading Shadows", copies);
            var review = d.Review();
            Eq(review.OverLimit.Contains("Fading Shadows"), flagged, $"{copies} copies of Fading Shadows");
            if (flagged) Expect(review.Warnings.Any(w => w.Contains("Fading Shadows") && w.Contains("limit of 10")), "the note names the card's own limit");
            else Expect(!review.Warnings.Any(w => w.Contains("Fading Shadows")), $"{copies} copies raise no note about Fading Shadows");
        }
        foreach (var (name, copies, flagged) in new[] { ("Plain Follower", 3, false), ("Plain Follower", 4, true), ("Plain Follower", 10, true), ("Unique Samurai", 1, false), ("Unique Samurai", 2, true) })
        {
            var d = new DeckWorkshop(Pool()); d.Adjust("Fading Shadows", 10); d.Adjust(name, copies);
            Eq(d.Review().OverLimit.Contains(name), flagged, $"{copies} copies of {name} next to ten Fading Shadows");
            Expect(!d.Review().OverLimit.Contains("Fading Shadows"), "ten Fading Shadows are never flagged");
        }
        // A caller that never threads the limit through (an older DeckCardInfo) keeps the usual rule for the same card.
        var legacy = new DeckWorkshop([new("x", "Fading Shadows", ["Follower"], ["Fate"], ["Ninja"], true)]); legacy.Adjust("Fading Shadows", 4);
        Expect(legacy.Review().OverLimit.Contains("Fading Shadows"), "without the limit, the usual 3 applies");
    }

    private static void DeckFileRoundTrip()
    {
        foreach (var (copies, flagged) in new[] { (10, false), (11, true) })
        {
            var d = new DeckWorkshop(Pool()); d.Load($"Legality: Emperor\n{copies} Fading Shadows\n");
            Eq(d.Count("Fading Shadows"), copies, "the file's copies are kept, never hard-blocked");
            Eq(d.Review().OverLimit.Contains("Fading Shadows"), flagged);
            var again = new DeckWorkshop(Pool()); again.Load(DeckFormat.Write(d.ToDeck()));
            Eq(again.Count("Fading Shadows"), copies); Eq(again.Review().OverLimit.Contains("Fading Shadows"), flagged);
        }
        // DeckFormat itself: the per-card limit replaces the usual one for that name only.
        var deck = new Deck("Emperor", [new("Fading Shadows", 10), new("Other", 4)]);
        var legality = DeckFormat.CheckLegality(deck, _ => [], name => name == "Fading Shadows" ? 10 : null);
        Expect(!legality.IsLegal && legality.Violations.Length == 1 && legality.Violations[0].CardName == "Other" && legality.Violations[0].Limit == 3, "only the unlimited card is over");
    }

    private static void OfferedFadingShadowsCommandsValidate()
    {
        var r = Battle();
        Put(r.S, "training-ninja-source", 0, Zone.Battlefield, bowed: true, province: 0);
        Put(r.S, Support337.FadingShadows, 0, Zone.Hand);
        var e = Engine(r.S);
        OfferedCommandsValidate(e, () => Engine(r.S), alternateTargets: true);
    }
}
