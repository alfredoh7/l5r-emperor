using System.Text.Json;
using System.Text.RegularExpressions;
using L5R.Core;
using static RegressionAssertions;

// Mendon's R4 rulings (issue #492), part 3: Cherry Blossom Festival, Luxurious Silk, The Killing Grounds, Bad Kharma and Footsteps of Madness, Deception Revealed and the ordered searches.
internal static partial class EmperorRulingR4Tests
{
    // ---- 2. Cherry Blossom Festival: Reactions, not traits -------------------------------------------------------------------------------------------------------------------------

    // A player 0 who purchases `definition` from his first province (with Cherry Blossom Festival, player 1's, in play or not).
    private static MatchEngine Purchased(string definition, bool cherry, int honor = 20)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = honor;
        if (cherry) Add(s, CherryBlossom, 1, Zone.EventInPlay);
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = definition; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(definition).GoldCost).First().SourceIds));
        return e;
    }

    private static void CherryBlossomBarsReactionsOnly()
    {
        // Proclaim is a Reaction: barred; offered without the Event.
        var barredProclaim = Purchased("training-crane-samurai", true).QueryActions(0).FirstOrDefault(o => o.Type == "proclaim");
        Check(barredProclaim is { Enabled: false } && (barredProclaim.Reason ?? "").Contains("Cherry Blossom"), "Proclaim is a Reaction and is refused");
        Check(Purchased("training-crane-samurai", false).QueryActions(0).Any(o => o.Type == "proclaim" && o.Enabled), "and offered without the Event");

        // Overconfident is not a Reaction: the draw is still offered (and taken) while the Event is in play.
        var overconfident = Purchased("training-overconfident-personality", true);
        Eq(overconfident.State.PendingChoice?.Type, "overconfidentDraw", "the Overconfident draw is offered under Cherry Blossom Festival");
        var hand = overconfident.State.Players[1].Hand.Count;
        Accept(overconfident, new GameCommand("overconfidentDraw", 1, overconfident.Version, Choice: "draw"));
        Eq(overconfident.State.Players[1].Hand.Count, hand + 1, "and the card was drawn");

        // A trait modelled as a Reaction (Ikoma Ayumi's "After Ayumi enters play, lose 2 Honor.") is not barred; a printed Reaction (Kitsuki Horume's) is.
        var ayumi = Purchased(Ayumi, true);
        var ayumiCard = ayumi.State.Cards.Values.Single(c => c.DefinitionId == Ayumi);
        var lost = Honor(ayumi, 0);
        Accept(ayumi, new GameCommand("useAbility", 0, ayumi.Version, CardId: ayumiCard.Id, AbilityId: "ayumi-entry"));
        Eq(Honor(ayumi, 0), lost - 2, "the trait applied under Cherry Blossom Festival");
        var horume = Purchased(Horume, true);
        var horumeCard = horume.State.Cards.Values.Single(c => c.DefinitionId == Horume);
        Check(Rejection(horume, new GameCommand("useAbility", 0, horume.Version, CardId: horumeCard.Id, AbilityId: "horume-gain", TargetPlayerId: 0)).Contains("Cherry Blossom"), "Horume's printed Reaction is refused");
        var free = Purchased(Horume, false);
        Accept(free, new GameCommand("useAbility", 0, free.Version, CardId: free.State.Cards.Values.Single(c => c.DefinitionId == Horume).Id, AbilityId: "horume-gain", TargetPlayerId: 0));

        // Gold-source Reactions are Reactions: Yoritomo Sakuma's, Yasuki Dokansuto's (a printed "Kolat Reaction") and Luxurious Silk's produce nothing while the Event is in play.
        MatchEngine Producers(bool cherry, out CardInstance sakuma, out CardInstance dokansuto, out CardInstance silk, out CardInstance spell, out CardInstance holding)
        {
            var s = Table(Phase.Action, 0);
            sakuma = Add(s, Sakuma, 0, Zone.Home); dokansuto = Add(s, Dokansuto, 0, Zone.Home); silk = Add(s, LuxuriousSilk, 0, Zone.Home, bowed: true);
            spell = Add(s, "training-spell", 0, Zone.Hand); holding = Add(s, "training-marketplace", 0, Zone.Hand);
            var silkId = silk.Id; var sakumaId = sakuma.Id;
            foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != silkId && c.Id != sakumaId && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
            if (cherry) Add(s, CherryBlossom, 1, Zone.EventInPlay);
            return MatchEngine.FromTestState(s);
        }
        var open = Producers(false, out var sakuma1, out var dokansuto1, out var silk1, out var spell1, out var holding1);
        Eq(open.GoldProduced(sakuma1.Id, 0, new PaymentSubject(spell1, PaymentKind.Play)), 2, "Sakuma produces 2 Gold for a payment");
        Eq(open.GoldProduced(dokansuto1.Id, 0, new PaymentSubject(holding1, PaymentKind.Recruit)), 2, "Dokansuto produces 2 Gold for a Holding");
        // Issue #501: a Reaction's Gold is a payment token of its own ("<card>#reaction"); the card's own id is its bow.
        Check(open.PaymentOptions(0, 2, new PaymentSubject(spell1, PaymentKind.Equip)).Any(p => p.SourceIds.SequenceEqual([silk1.Id + "#reaction"])), "the bowed Silk pays 2 Gold for an action");
        var barred = Producers(true, out var sakuma2, out var dokansuto2, out var silk2, out var spell2, out var holding2);
        Eq(barred.GoldProduced(sakuma2.Id, 0, new PaymentSubject(spell2, PaymentKind.Play)), 0, "Cherry Blossom Festival: Sakuma's Reaction produces nothing");
        Eq(barred.GoldProduced(dokansuto2.Id, 0, new PaymentSubject(holding2, PaymentKind.Recruit)), 0, "and Dokansuto's neither");
        Check(!barred.PaymentOptions(0, 2, new PaymentSubject(spell2, PaymentKind.Equip)).Any(p => p.SourceIds.Any(id => id.StartsWith(silk2.Id, StringComparison.Ordinal))), "and the bowed Silk cannot pay");

        // A hand Reaction is barred at its window (Solid Defense on Weak Poison's loss): none opens.
        var s3 = Table(Phase.Action, 0);
        var poison = Add(s3, WeakPoison, 0, Zone.Hand); var victim = Add(s3, "training-lion-samurai", 1, Zone.Home);
        Add(s3, SolidDefense, 1, Zone.Hand); var wall = Add(s3, "training-lion-samurai", 1, Zone.Home); Force(wall, 5 - CardCatalog.Get(wall.DefinitionId).Force);
        Add(s3, CherryBlossom, 1, Zone.EventInPlay);
        var e3 = MatchEngine.FromTestState(s3);
        var h0 = Honor(e3, 0);
        Accept(e3, Offer(e3, 0, "weak-poison-force-zero", poison.Id, victim.Id));
        Check(e3.State.PendingChoice is null, "no Reaction may be taken: no window");
        Eq(Honor(e3, 0), h0 - 2, "the loss stands");
    }

    private static readonly string[] ExpectedTraitReactions =
    [
        "ancestral-sword-hantei-honor", "aya-entry-dark-virtue", "ayumi-entry", "castle-of-order-redirect", "celestial-sword-crab-honor", "celestial-sword-crane-honor",
        "celestial-sword-lion-honor", "celestial-sword-mantis-honor", "celestial-sword-phoenix-honor", "celestial-sword-scorpion-lose", "celestial-sword-unicorn-honor", "clay-kshatriya-invest",
        "goju-asuka-give", // issue #501: Goju Asuka's "After Asuka enters play: You may give control of her to another player." is a trait
        "hayan-assign", // issue #503: Shinjo Hayan's "After engaging: Once per turn, you may assign ..." prints no designator, so it is a trait
        "kae-favor-negate", // issue #501 (Mendon's item 16): Kakita Kae's Favor discard triggers like a Reaction but is a trait
        "makubesu-create", "never-safe-return", "nojima-entry", "noritada-invest", "otomo-favor-token", "rika-bushido-virtue-bonus", "shihiro-boost", "small-time-bully-dishonor",
        "spiders-web-attach", "spiders-web-attach-kensai", "tf-band-brothers-create", "thick-marsh-entry", "tsudoken-exp2-entry", // issue #622: Band of Brothers' entering-play trait
        "w821-governor-create", // issue #821: The Governor's Estate's "Twice per turn, after your Dynasty Phase begins: You may pay 2 Gold; ..." prints no designator, so it is a trait
        "wl2-invest-two-tokens", // issue #674: Shosuro Nigawa and Utaku Sayaka's Invest (the trait shape of Clay Kshatriya)
        "words-of-consecration-honor", // issue #622: Words of Consecration's "After this card enters play, if this Shugenja is Air: Choose a player, who gains or loses 1 Honor." is a trait
    ];

    private static void TraitReactionAudit()
    {
        Eq(string.Join(",", TraitReactions.All.OrderBy(x => x, StringComparer.Ordinal)), string.Join(",", ExpectedTraitReactions), "the trait-modelled Reaction-timed abilities (pinned: a change is a ruling, Mendon's R4 Cherry Blossom Festival)");
        foreach (var id in TraitReactions.All)
        {
            Check(AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing == "Reaction", $"{id} is a Reaction-timed ability");
            Check(!Regex.IsMatch(ability!.Label, @"^\s*(\w+ )*Reaction\b"), $"{id}'s label does not print a Reaction designator: {ability.Label}");
        }
        // Against the printed texts (an "Interrupt:" is a Reaction to "before an action's effects resolve" under the Emperor Edition rules: Absolution, issue #622): a card whose text never says Reaction has no printed-Reaction ability; a card printing more Reaction lines than it has Reaction-timed non-trait abilities has one
        // mislabelled as a trait. (Split variants of one printed line -- Horume's gain and lose -- are several abilities for a line, so "at least as many abilities as lines".)
        var failures = new List<string>();
        var audited = 0;
        foreach (var card in CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && !c.Id.StartsWith("training-", StringComparison.Ordinal) && c.Abilities is { Length: > 0 }))
        {
            var reactions = card.Abilities!.Where(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing == "Reaction").ToArray();
            if (reactions.Length == 0) continue;
            var text = card.Text ?? "";
            // (Some published texts repeat their lines; a line is counted once.)
            var distinct = text.Split('\n').Select(l => l.Trim()).Distinct().ToArray();
            var lines = distinct.Count(l => Regex.IsMatch(l, @"\b(Reaction|Interrupt|Engage)\b") /* Emperor Edition: Engage and Interrupt replace the Reaction designator (issue #622) */ && !distinct.Any(o => o != l && o.StartsWith(l, StringComparison.Ordinal))); // (a line broken in two by the scan counts once)
            var nonTrait = reactions.Count(a => !TraitReactions.Is(a));
            audited++;
            if (nonTrait > 0 && lines == 0) failures.Add($"{card.Name}: {nonTrait} Reaction ability(ies) but no printed Reaction");
            if (lines > nonTrait) failures.Add($"{card.Name}: {lines} printed Reaction line(s) but only {nonTrait} non-trait Reaction ability(ies)");
        }
        Check(failures.Count == 0, "Reaction-timed abilities against the printed texts: " + string.Join("; ", failures));
        Check(audited > 150, $"the audit read the printed Reaction cards ({audited})");
    }

    // ---- 3. Luxurious Silk ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void LuxuriousSilkRecruits()
    {
        // The predicate: an Equip, a Strategy played from a hand and an ability's own payment are payments for an action; a recruit is one only by an ability whose cost prices the Personality and which
        // does not bring him in "paying all costs".
        var s = Table(Phase.Action, 0);
        var card = Add(s, "training-crane-samurai", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        bool ForAction(PaymentSubject subject) => MatchEngine.PaymentIsForAnAction(subject);
        Check(ForAction(new(card, PaymentKind.Equip)) && ForAction(new(card, PaymentKind.Play)) && ForAction(new(card, PaymentKind.Action)), "an Equip, a Strategy play and an ability are payments for an action");
        Check(!ForAction(new(card, PaymentKind.Recruit)), "a plain recruit (the Dynasty purchase, a search that recruits) is not");
        Check(ForAction(new(card, PaymentKind.Recruit, AbilityCatalog.All["yojimbos-duty-recruit"])), "a recruit by an ability whose cost is the Personality's Gold Cost is: Yojimbo's Duty (paying less Gold)");
        Check(ForAction(new(card, PaymentKind.Recruit, AbilityCatalog.All["hitaka-recruit-oni"])), "Chuda Hitaka's Oni (paying 9 less Gold)");
        foreach (var payingAllCosts in new[] { "at-any-cost-recruit", "your-clan-needs-you", "goju-plot-recruit", "essence-of-undeath-recruit", "black-pearl-recruit", "kunji-exp-return-paid", "requesting-reinforcements-recruit", "shinden-shorai-recruit", "dark-naga-rise-recruit" })
            Check(!ForAction(new(card, PaymentKind.Recruit, AbilityCatalog.All[payingAllCosts])) && AbilityCatalog.All[payingAllCosts].Label.Contains("paying all costs", StringComparison.OrdinalIgnoreCase),
                $"{payingAllCosts} requires the player to pay all costs normally: Silk does not pay for it");
        Check(e.State.Cards[card.Id].Zone == Zone.Hand, "fixture");

        // Through the payment: every other producer bowed and Silk bowed; 2 Gold is Silk alone.
        MatchEngine Rig(out CardInstance silk, out CardInstance recruit)
        {
            var t = Table(Phase.Action, 0);
            silk = Add(t, LuxuriousSilk, 0, Zone.Home, bowed: true);
            recruit = Add(t, "training-lion-samurai", 0, Zone.Hand);
            var silkId = silk.Id;
            foreach (var c in t.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != silkId && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
            return MatchEngine.FromTestState(t);
        }
        var rig = Rig(out var silk1, out var recruit1);
        bool SilkAlone(PaymentSubject subject) => rig.PaymentOptions(0, 2, subject).Any(p => p.SourceIds.SequenceEqual([silk1.Id + "#reaction"])); // issue #501: the Reaction's token
        Check(SilkAlone(new(recruit1, PaymentKind.Recruit, AbilityCatalog.All["yojimbos-duty-recruit"])), "Silk pays for the ability's recruit");
        Check(!SilkAlone(new(recruit1, PaymentKind.Recruit)), "not for the ordinary recruit of the Dynasty phase");
        Check(!SilkAlone(new(recruit1, PaymentKind.Recruit, AbilityCatalog.All["your-clan-needs-you"])), "not for a recruit that pays all costs");
        Check(SilkAlone(new(recruit1, PaymentKind.Equip)), "an Equip still can");

        // End to end: Yojimbo's Duty ("Recruit him, paying less Gold equal to your Yojimbo's printed Gold Cost plus 1") for a Courtier whose price is then 2: the bowed Silk pays it.
        var u = Table(Phase.Action, 0);
        var duty = Add(u, "oracle-l5r-101", 0, Zone.Hand);
        var yojimbo = Kw(Add(u, "training-shugenja", 0, Zone.Home), "Yojimbo");
        var courtier = Kw(Add(u, "training-lion-courtier", 0, Zone.DynastyDiscard), "Courtier");
        var silk2 = Add(u, LuxuriousSilk, 0, Zone.Home, bowed: true);
        var silk2Id = silk2.Id;
        foreach (var c in u.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != silk2Id && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
        u.Players[0].Honor = 10;
        var ue = MatchEngine.FromTestState(u);
        var option = Enabled(ue, 0, "yojimbos-duty-recruit", duty.Id, yojimbo.Id);
        var quote = option.TargetGold![courtier.Id];
        Eq(quote.Cost, 2, "the Courtier costs 2 after the discount");
        Check(quote.Payments.Length > 0 && quote.Payments.Any(p => p.SourceIds.Contains(silk2.Id + "#reaction")), "and the bowed Silk is among the ways to pay it"); // issue #501: the Reaction's token
        Accept(ue, Offer(ue, 0, "yojimbos-duty-recruit", duty.Id, courtier.Id, yojimbo.Id));
        Eq(ue.State.Cards[courtier.Id].Zone, Zone.Home, "he entered play");
        Check(ue.State.Cards[silk2.Id].Bowed, "and Silk stayed bowed");
    }

    // ---- 4. The Killing Grounds ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KillingGroundsEachPlayer()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 1;
        var grounds = Add(s, KillingGrounds, 0, Zone.Battlefield, province: 0);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var a = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var b = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        int Fired() => e.State.Events.Count(ev => ev.Type == "killingGrounds");
        Accept(e, new("pass", 1, e.Version));
        Eq(Fired(), 1, "player 1's first pass triggers it");
        Accept(e, e.QueryActions(1).First(o => o.Type == "killingGroundsSelect").Command!);
        Eq(new[] { a, b }.Count(c => e.State.Cards[c.Id].Zone == Zone.Dead), 1, "and he destroyed one of his own");
        Eq(e.State.PriorityPlayerId, 0, "the pass went on: player 0 has the next opportunity");
        // Player 0 takes an action instead of passing: it is not a pass, so nothing is spent for him.
        Accept(e, Offer(e, 0, "training-bow-at-battle", bower.Id, e.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Force > 0).Id));
        Eq(Fired(), 1, "an action is not a pass");
        // Player 1 passes again: that is his second time, and nothing happens.
        Check(e.State.PriorityPlayerId == 1 && e.State.PendingChoice is null, "player 1 has the opportunity again");
        Accept(e, new("pass", 1, e.Version));
        Eq(Fired(), 1, "the first time for player 1 has been used: his second pass does not trigger it");
        // Player 0's FIRST pass triggers it for him, though player 1 already used his.
        Accept(e, new("pass", 0, e.Version));
        Eq(Fired(), 2, "player 0's first pass triggers it too: the first time for each player");
        Check(e.State.Cards[grounds.Id].AbilityUses.Count == 2, "one spent time for each player, on the Terrain");
        Check(mine.Id.Length > 0, "fixture");
    }

    // ---- 5. Bad Kharma, Footsteps of Madness: on top of the other Reactions -----------------------------------------------------------------------------------------------------------

    // Player 1 (Family Honor -3) plays a Strategy's Battle action against Akodo Kakihara (player 0's: "After another player's action targets Kakihara: Give him a Force bonus" is a Reaction, so the
    // action has a before-action window of its own); player 0 holds Bad Kharma.
    private static MatchEngine KharmaRig(int honor, out CardInstance strategy, out CardInstance ancestor, out CardInstance kakihara, out CardInstance kharma)
    {
        var s = Table(Phase.Battle, 1); s.Players[1].Honor = honor;
        strategy = Add(s, Forefather, 1, Zone.Hand);
        ancestor = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Ancestor");
        kakihara = Add(s, Kakihara, 0, Zone.Battlefield, province: 0);
        kharma = Add(s, BadKharma, 0, Zone.Hand);
        return MatchEngine.FromTestState(s);
    }

    private static void BadKharmaAndFootstepsOnTop()
    {
        // Bad Kharma after another Reaction of the same action: the window of Kakihara's Reaction opens first; when it has closed the hand Reaction may still be played and cancels the action.
        var e = KharmaRig(-3, out var strategy, out var ancestor, out var kakihara, out var kharma);
        Accept(e, Offer(e, 1, "forefather-destroy", strategy.Id, kakihara.Id, ancestor.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the action has a before-action window (Kakihara's Reaction)");
        Accept(e, Enabled(e, 0, "kakihara-force-bonus", kakihara.Id).Command!); // the modifier played first
        for (var guard = 0; guard < 8 && e.State.PendingChoice?.Type == "beforeActionReaction"; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, "unimpeachableNameReaction", "the window is no longer skipped: it follows the before-action window");
        Eq(e.State.PendingChoice?.PlayerId, 0, "for the player who holds Bad Kharma");
        Accept(e, Enabled(e, 0, "bad-kharma-negate", kharma.Id).Command!);
        PassWindow(e);
        Eq(e.State.Cards[kakihara.Id].Zone, Zone.Battlefield, "on top of the modifier, Bad Kharma cancelled the destruction");
        Check(e.State.Cards[kakihara.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount > 0), "the modifier played before it stands");
        Eq(e.State.Cards[ancestor.Id].Zone, Zone.Battlefield, "and he ignored the action's costs");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the action was still taken");

        // Declining Kakihara's Reaction: Bad Kharma is still offered afterwards.
        var d = KharmaRig(-3, out var strategyD, out var ancestorD, out var kakiharaD, out var kharmaD);
        Accept(d, Offer(d, 1, "forefather-destroy", strategyD.Id, kakiharaD.Id, ancestorD.Id));
        for (var guard = 0; guard < 8 && d.State.PendingChoice?.Type == "beforeActionReaction"; guard++) Accept(d, new("declineReaction", d.State.PriorityPlayerId, d.Version));
        Eq(d.State.PendingChoice?.Type, "unimpeachableNameReaction", "offered after the before-action window closed unused");
        PassWindow(d);
        Eq(d.State.Cards[kakiharaD.Id].Zone, Zone.Dead, "passed: the action resolved");
        Eq(d.State.Cards[kharmaD.Id].Zone, Zone.Hand, "and Bad Kharma stayed in the hand");

        // A Family Honor above -1: nothing to offer, no second window.
        var n = KharmaRig(0, out var strategyN, out var ancestorN, out var kakiharaN, out _);
        Accept(n, Offer(n, 1, "forefather-destroy", strategyN.Id, kakiharaN.Id, ancestorN.Id));
        for (var guard = 0; guard < 8 && n.State.PendingChoice is not null; guard++) { Check(n.State.PendingChoice.Type == "beforeActionReaction", "only the before-action window"); Accept(n, new("declineReaction", n.State.PriorityPlayerId, n.Version)); }
        Eq(n.State.Cards[kakiharaN.Id].Zone, Zone.Dead, "it resolved");

        // An action the before-action window negated needs no further Reaction: nothing is left to cancel.
        var t = Table(Phase.Battle, 1); t.Players[1].Honor = -3;
        var strategyT = Add(t, Forefather, 1, Zone.Hand); var ancestorT = Kw(Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Ancestor");
        var targetT = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var negator = Add(t, "training-untargeted-negator", 0, Zone.Home); var kharmaT = Add(t, BadKharma, 0, Zone.Hand);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 1, "forefather-destroy", strategyT.Id, targetT.Id, ancestorT.Id));
        Accept(te, Enabled(te, 0, "training-negate-untargeted-action", negator.Id).Command!);
        PassWindow(te);
        Check(te.State.PendingChoice is null, "no second window for an action already negated");
        Eq(te.State.Cards[kharmaT.Id].Zone, Zone.Hand, "Bad Kharma was not needed");
        Eq(te.State.Cards[targetT.Id].Zone, Zone.Battlefield, "the negation held");

        // Footsteps of Madness, on an action from a Stronghold that has a before-action window: Shamate Keep moves Kakihara home (Personal Honor 4, equal or lower than the performer's).
        var f = Table(Phase.Battle, 1);
        var keep = StrongholdAs(f, 1, ShamateKeep);
        var performer = Add(f, "training-lion-dignitary", 1, Zone.Battlefield, province: 0);
        var kak = Add(f, Kakihara, 0, Zone.Battlefield, province: 0);
        var footsteps = Add(f, Footsteps, 0, Zone.Hand);
        for (var i = 0; i < 4; i++) Add(f, "training-border-keep", 0, Zone.Home);
        var fe = MatchEngine.FromTestState(f);
        var honor = Honor(fe, 1);
        Accept(fe, Offer(fe, 1, "shamate-move", keep.Id, kak.Id, performer.Id));
        Eq(fe.State.PendingChoice?.Type, "beforeActionReaction", "the Stronghold action has a before-action window too");
        for (var guard = 0; guard < 8 && fe.State.PendingChoice?.Type == "beforeActionReaction"; guard++) Accept(fe, new("declineReaction", fe.State.PriorityPlayerId, fe.Version));
        Eq(fe.State.PendingChoice?.Type, "unimpeachableNameReaction", "Footsteps of Madness is offered after it");
        Accept(fe, Enabled(fe, 0, "footsteps-madness-negate", footsteps.Id).Command!);
        PassWindow(fe);
        Eq(fe.State.Cards[kak.Id].Zone, Zone.Battlefield, "the move was negated");
        Eq(Honor(fe, 1), honor, "and so was the Honor it would have gained");
    }

    private static void BadKharmaAdditionalAction()
    {
        MatchEngine Rig(bool kharma, out CardInstance strategy, out CardInstance ancestor, out CardInstance target)
        {
            var s = Table(Phase.Battle, 1); s.Players[1].Honor = -3;
            s.AdditionalActions.Add(new(1, null, false));
            strategy = Add(s, Forefather, 1, Zone.Hand);
            ancestor = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Ancestor");
            target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (kharma) Add(s, BadKharma, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var strategy, out var ancestor, out var target);
        Accept(e, Offer(e, 1, "forefather-destroy", strategy.Id, target.Id, ancestor.Id));
        Eq(e.State.PendingChoice?.Type, "unimpeachableNameReaction", "an additional Battle action on a Strategy is an announced action: the window opens");
        var kharma = e.State.Cards.Values.Single(c => c.DefinitionId == BadKharma);
        Accept(e, Enabled(e, 0, "bad-kharma-negate", kharma.Id).Command!);
        PassWindow(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "cancelled");
        Eq(e.State.Cards[ancestor.Id].Zone, Zone.Battlefield, "costs ignored");
        Eq(e.State.AdditionalActions.Count, 0, "the additional action was consumed");
        var plain = Rig(false, out var strategy2, out var ancestor2, out var target2);
        Accept(plain, Offer(plain, 1, "forefather-destroy", strategy2.Id, target2.Id, ancestor2.Id));
        Eq(plain.State.Cards[target2.Id].Zone, Zone.Dead, "without Bad Kharma the additional action resolves at once");
    }

    // ---- Deception Revealed: Rings bowed by an action -----------------------------------------------------------------------------------------------------------------------------

    private static void DeceptionRevealedRings()
    {
        // As a cost: Asako Chukage's "Bow your target Ring".
        {
            var s = Table(Phase.Action, 0);
            var chukage = Add(s, AsakoChukage, 0, Zone.Home); var ring = Add(s, "training-ring-of-air", 0, Zone.Home); var card = Add(s, DeceptionRevealed, 0, Zone.Hand);
            Deck(s, 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "chukage-draw", chukage.Id, ring.Id));
            Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "a Ring bowed as an action's cost");
            Accept(e, Offer(e, 0, "deception-revealed-straighten", card.Id, ring.Id));
            Check(!e.State.Cards[ring.Id].Bowed, "it was straightened");
        }
        // As an effect: an Open action bows an enemy card, a Ring of the enemy's.
        {
            var s = Table(Phase.Action, 0);
            var bower = Add(s, "training-card-bower", 0, Zone.Home); var ring = Add(s, "training-ring-of-air", 1, Zone.Home); var card = Add(s, DeceptionRevealed, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "training-bow-enemy-card-open", bower.Id, ring.Id));
            Check(e.State.Cards[ring.Id].Bowed, "the action bowed the Ring as an effect");
            Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "a Ring bowed as an action's effect");
            Accept(e, Offer(e, 0, "deception-revealed-straighten", card.Id, ring.Id));
            Check(!e.State.Cards[ring.Id].Bowed, "it was straightened");
        }
        // Through an Attack held open for Reactions (the action resolves when the window closes): the Ring bowed as the Attack's cost.
        {
            var s = Table(Phase.Battle, 0);
            var ring = Add(s, "oracle-l5r-10535", 0, Zone.Home); var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Force(target, -1);
            Add(s, "training-attack-reactor", 1, Zone.Home); Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var card = Add(s, DeceptionRevealed, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Batch336Support.Use(e, 0, "false-ring-of-water-attack", ring.Id, target.Id));
            Eq(e.State.PendingChoice?.Type, "attackReaction", "the Attack waits for Reactions");
            PassUntilWindowIs(e, "afterActionResolvedReaction");
            Check(e.State.Cards[ring.Id].Bowed, "the Ring is bowed");
            Accept(e, Offer(e, 0, "deception-revealed-straighten", card.Id, ring.Id));
            Check(!e.State.Cards[ring.Id].Bowed, "the deferred Attack's action bowed it: straightened");
        }
        // A Focus Effect is not an action: a Ring it bows is not one an action bowed, though the duel it belongs to is.
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); var theirs = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand); var focus = Add(s, "training-focus-ring-bower", 0, Zone.Hand);
            var ring = Add(s, "training-ring-of-air", 0, Zone.Home); var card = Add(s, DeceptionRevealed, 0, Zone.Hand);
            var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
            Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: focus.Id));
            Accept(e, new GameCommand("strike", 1, e.Version));
            Check(e.State.Cards[ring.Id].Bowed, "the Focus Effect bowed the Ring");
            Check(e.State.PendingChoice is null && !e.QueryActions(0).Any(a => a.Command?.AbilityId == "deception-revealed-straighten" && a.Enabled), "but no action did, so Deception Revealed has nothing to straighten");
            Check(card.Id.Length > 0, "fixture");
        }
        // No Ring bowed by anything: nothing to react to (an action that bowed no Ring).
        {
            var s = Table(Phase.Action, 0);
            var bower = Add(s, "training-card-bower", 0, Zone.Home); var other = Add(s, "training-lion-samurai", 1, Zone.Home); Add(s, "training-ring-of-air", 1, Zone.Home, bowed: true); Add(s, DeceptionRevealed, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "training-bow-enemy-card-open", bower.Id, other.Id));
            Check(e.State.PendingChoice is null, "a Ring that was bowed before the action, and stays so, is not one it bowed");
        }
    }

    private static void PassUntilWindowIs(MatchEngine e, string type)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice?.Type != type; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, type, "the window is open");
    }

    // ---- Ordered searches -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine RingSearch(bool inPile, bool inDeck, out CardInstance pileRing, out CardInstance deckRing, out CardInstance togashi)
    {
        var s = Table(Phase.Action, 0); s.Players[0].Clan = "Dragon";
        togashi = Add(s, TogashisGuidance, 0, Zone.Celestial);
        pileRing = inPile ? Add(s, "oracle-l5r-6339", 0, Zone.FateDiscard) : togashi;
        deckRing = togashi;
        if (inDeck) { deckRing = Add(s, "oracle-l5r-6334", 0, Zone.FateDeck); deckRing.FaceUp = false; s.Players[0].FateDeck.Insert(0, deckRing.Id); }
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "togashis-guidance-ring-search").Command!);
        return e;
    }

    private static void OrderedSearches()
    {
        // A Ring qualifies in the discard pile (the first place): he must take it from there; the deck is not searched, shown, shuffled, or even hinted at.
        var e = RingSearch(true, true, out var pileRing, out var deckRing, out _);
        Eq(e.State.PendingSearch!.Deck, SearchDeck.FateDiscard, "the discard pile is searched first");
        var deckBefore = e.State.Players[0].FateDeck.ToArray();
        var view = JsonSerializer.Serialize(e.GetView(0).PendingSearch);
        Check(view.Contains(pileRing.DefinitionId) && !view.Contains(deckRing.DefinitionId), "the searcher's view lists the pile's Ring only, never the deck's");
        Check(!e.QueryActions(0).Any(a => a.Type == "passSearch"), "no pass is offered");
        Reject(e, new GameCommand("passSearch", 0, e.Version));
        Reject(e, new GameCommand("passSearch", 0, e.Version, Choice: MatchEngine.SearchStopChoice));
        Reject(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: deckRing.DefinitionId));
        Accept(e, e.QueryActions(0).Single(a => a.Type == "selectSearchCard").Command!);
        Eq(e.State.Cards[pileRing.Id].Zone, Zone.Hand, "the Ring came from the pile");
        Check(e.State.Players[0].FateDeck.SequenceEqual(deckBefore) && e.State.Events.All(ev => ev.Type != "deckShuffled"), "the deck was neither searched nor shuffled");
        Check(e.State.Events.All(ev => ev.Type != "cardShown" || !ev.Message.Contains("Fate deck")), "and nothing of it was shown");

        // Nothing in the pile: he may go on to the deck, or stop; going on searches the deck (shown now), and taking the Ring shuffles it.
        var g = RingSearch(false, true, out _, out var deckRing2, out _);
        Eq(g.QueryActions(0).Count(a => a.Type == "passSearch"), 2, "search the deck next, or stop");
        Check(!JsonSerializer.Serialize(g.GetView(0).PendingSearch).Contains(deckRing2.DefinitionId), "the deck is not shown while the pile is searched");
        Accept(g, g.QueryActions(0).First(a => a.Type == "passSearch" && a.Command!.Choice is null).Command!);
        Eq(g.State.PendingSearch!.Deck, SearchDeck.Fate, "the Fate deck is now searched");
        Check(g.State.Events.All(ev => ev.Type != "deckShuffled"), "a discard pile is not shuffled");
        Accept(g, g.QueryActions(0).Single(a => a.Type == "selectSearchCard").Command!);
        Eq(g.State.Cards[deckRing2.Id].Zone, Zone.Hand, "the Ring came from the deck");
        Eq(g.State.Events.Count(ev => ev.Type == "deckShuffled"), 1, "and the searched deck was shuffled");

        // Stopping: the deck is never searched, so it is not shuffled.
        var h = RingSearch(false, true, out _, out var deckRing3, out _);
        Accept(h, h.QueryActions(0).First(a => a.Type == "passSearch" && a.Command!.Choice == MatchEngine.SearchStopChoice).Command!);
        Check(h.State.PendingSearch is null && h.State.Cards[deckRing3.Id].Zone == Zone.FateDeck && h.State.Events.All(ev => ev.Type != "deckShuffled"), "he stopped: the deck was not touched");

        // Nothing anywhere: the deck was searched (and found empty of Rings), so it is reshuffled all the same.
        var n = RingSearch(false, false, out _, out _, out _);
        Accept(n, n.QueryActions(0).First(a => a.Type == "passSearch" && a.Command!.Choice is null).Command!);
        Accept(n, n.QueryActions(0).Single(a => a.Type == "passSearch").Command!);
        Check(n.State.PendingSearch is null, "the search ended");
        Eq(n.State.Events.Count(ev => ev.Type == "deckShuffled"), 1, "the deck that was searched is reshuffled whether or not a card was found");
    }

    // "A Focus Effect is not an action": Bad Kharma ("After another player with Family Honor of -1 or lower announces an action on a Strategy") is for the duel Strategy's own action, announced at the
    // start; the Focus Effect of a card focused in that duel is not an action, so no window is opened for it (and Footsteps of Madness, Solid Defense and Rhetoric are equally silent).
    private static void FocusEffectIsNotAnAction()
    {
        var s = Table(Phase.Battle, 1); s.Players[1].Honor = -3; s.PriorityPlayerId = 1;
        var mine = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); var theirs = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var duel = Add(s, "training-plain-duel-strategy", 1, Zone.Hand); var conspiracy = Add(s, Conspiracy, 1, Zone.Hand);
        var top = Add(s, "training-focus-strategy", 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, top.Id);
        var kharma = Add(s, BadKharma, 0, Zone.Hand); var sd = Add(s, SolidDefense, 0, Zone.Hand); var wall = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0); Force(wall, 5 - CardCatalog.Get(wall.DefinitionId).Force);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 1);
        Accept(e, Offer(e, 1, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        Eq(e.State.PendingChoice?.Type, "unimpeachableNameReaction", "the duel Strategy's action is announced: Bad Kharma's window opens");
        PassWindow(e);
        var windows = new List<string>();
        void Step(GameCommand command) { Accept(e, command); if (e.State.PendingChoice is { } open) windows.Add(open.Type); }
        Step(new GameCommand("focus", 0, e.Version, Choice: "deck"));
        Step(new GameCommand("focus", 1, e.Version, CardId: conspiracy.Id));
        Step(new GameCommand("strike", 0, e.Version));
        Check(windows.All(w => w is not ("unimpeachableNameReaction" or "beforeActionReaction" or "honorChangeReaction")), "the Focus Effect opened no action window: " + string.Join(",", windows));
        Eq(e.State.Cards[kharma.Id].Zone, Zone.Hand, "Bad Kharma was not played");
        Eq(e.State.Cards[sd.Id].Zone, Zone.Hand, "nor Solid Defense");
        Check(Honor(e, 1) == honor + 2 || e.State.Cards[mine.Id].Zone == Zone.Dead, "the Focus Effect's gain stands (if his Personality won) -- nothing negated it");
    }
}
