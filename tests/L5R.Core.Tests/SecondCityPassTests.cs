using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// The Second City unwired-card pass (issue #264), part two: cards whose old blockers named gaps that later work (#322 look, #336 action lock and stat penalties, #337 alternative
// costs, #387 created cards, #388 ability grants) closed, plus the small primitives they still needed. Every position is built explicitly and played through QueryActions and the
// commands it offers. Readings and assumptions: docs/decisions/2026-09-21-second-city-pass.md. The Spirit Strategies are in SecondCitySpiritTests.
internal static class SecondCityPassTests
{
    private const string Emoto = "oracle-l5r-9544", Interference = "oracle-l5r-3785", ControlYourDestiny = "oracle-l5r-1489", KatanaOfFire = "oracle-l5r-4291",
        TamingTheBeast = "oracle-l5r-7767", FlameKatana = "oracle-l5r-10022", Jurojin = "oracle-l5r-4070", ShindenAsahina = "oracle-l5r-6855";

    public static void Run(TestRunner runner) => runner.Run("second-city-pass", [
        ("Published shape: Emoto, Interference, Control Your Destiny, Katana of Fire, Taming the Beast, the Flame Katana and Jurojin's Blessing are the printed cards with the abilities named for them, ship their art, and their waves pin exactly them", PublishedShapes),
        ("Yoritomo Emoto: any unbowed Follower of his controller's at the battlefield -- not only one in his own unit -- is bowed as the cost and the target enemy card without attachments is destroyed", EmotoDestroys),
        ("Yoritomo Emoto's legality: a bowed Follower, one at home, an enemy Follower or a Personality cannot perform; a target with an attachment, an own card or one elsewhere is refused; a lone enemy Follower is destroyed as an attachment", EmotoLegality),
        ("Interference locks a Stronghold out of every action for the turn (either player's, the target's own controller's included) and bows it only on its user's own turn", InterferenceLocksAStronghold),
        ("Control Your Destiny: bows its Shugenja unless he is Void, looks at the top three of the Fate or the Dynasty deck, puts none, one or two on the bottom and the rest back in the chosen order -- and shows none of it to the opponent", ControlYourDestinyLooks),
        ("Control Your Destiny's cost is atomic and honest: a bowed Shugenja who is not Void cannot use it, a bowed Void Shugenja can (nothing is bowed), and it is never offered while its Shugenja cannot pay", ControlYourDestinyCost),
        ("Jurojin's Blessing's Limited action looks at the top Fate card and may discard it (or keep it), and only its controller sees it", JurojinLooks),
        ("Jurojin's Blessing's Open action discards the Celestial as its cost and puts a non-Unique Strategy from its controller's Fate discard pile into his hand -- not a Unique one, not the opponent's, not from a hand", JurojinReturns),
        ("Katana of Fire creates the Flame Katana once per game and attaches it to a chosen Personality; the Katana bows unless its wielder is Fire, and destroys an enemy card without attachments", KatanaOfFire_),
        ("Taming the Beast's Reaction: after another player's action that could destroy a defending Personality of its controller's targets him, and its Shugenja is Earth, it destroys itself and his destruction is negated -- only then", TamingTheBeastNegates),
        ("Taming the Beast's action straightens one of its controller's Nonhumans, bowing its Shugenja, even when the Shugenja's unit is not at the battle -- and only a Nonhuman of the acting player's", TamingTheBeastStraightens),
        ("Every enabled command offered for the cards of this pass validates and is accepted on a fresh engine", OfferedCommandsValidateFresh),
    ]);

    // The top-level statements of Program.cs declare local Check/Add, which shadow the imported statics: forward to the shared helpers explicitly.
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));

    private static CardView View(MatchEngine e, string id, int viewer = 0) => e.GetView(viewer).Players
        .SelectMany(p => p.Home.Concat(p.Army).Concat(p.Discard).Concat(p.Hand).Append(p.Stronghold)).Single(c => c.Id == id);

    private static GameCommand ReactionOffer(MatchEngine e, int player, string ability, string? cardId = null)
    {
        var offers = e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability && (cardId is null || a.CardId == cardId)).ToArray();
        Check(offers.Length == 1, $"{ability}: expected one enabled offer for player {player}, got {offers.Length}; " + string.Join("; ", e.QueryActions(player).Select(a => $"{a.Type}/{a.Label} [{a.Enabled}] {a.Reason}")));
        return offers[0].Command!;
    }

    private static bool ReactionOffered(MatchEngine e, int player, string ability) =>
        e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability);

    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.PendingChoice is not { Type: "beforeActionReaction" or "attackReaction" }, "the window closed");
    }

    private static GameCommand ChooseDeck(MatchEngine e, string deck, int owner = 0) => new("chooseLookDeck", 0, e.Version, Choice: deck, TargetPlayerId: owner);
    private static GameCommand Arrange(MatchEngine e, params string[] tokens) => new("arrangeTopCards", 0, e.Version, CardIds: tokens);

    // ---- published shapes ----------------------------------------------------------------------------------------------------------------------------
    private static void PublishedShapes()
    {
        var root = RepoRoot();
        void Shipped(string id) { Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped"); Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg"); }
        var emoto = CardCatalog.Get(Emoto);
        Check(!emoto.ScenarioOnly && emoto.Type == CardType.Personality && emoto.Name == "Yoritomo Emoto" && emoto.Clan == "Mantis", "Emoto");
        Eq(emoto.Force, 6); Eq(emoto.Chi, 3); Eq(emoto.GoldCost, 10); Eq(emoto.PersonalHonor, 1); Check(emoto.HonorRequirement is null, "no Honor Requirement");
        foreach (var k in new[] { "Naval", "Captain of the Third Kama", "Commander", "Mantis Clan", "Samurai" }) Check(emoto.Keywords!.Contains(k), k);
        Eq(emoto.Text, "Battle: Bow your performing Follower and target an enemy card without attachments: Destroy it.");
        Check(emoto.Abilities!.SequenceEqual(new[] { "emoto-follower-destroy" }), "Emoto's ability");
        var interference = CardCatalog.Get(Interference);
        Check(interference.Type == CardType.Strategy && interference.Name == "Interference" && interference.Focus == 4 && interference.GoldCost == 0, "Interference");
        Eq(interference.Text, "Political Open: A target Stronghold's abilities may not be used. If it is your turn, bow it.");
        Check(interference.Abilities!.SequenceEqual(new[] { "interference-lock" }), "Interference's ability");
        var control = CardCatalog.Get(ControlYourDestiny);
        Check(control.Type == CardType.Spell && control.Name == "Control Your Destiny" && control.Focus == 2 && control.GoldCost == 0 && control.Keywords!.Contains("Void"), "Control Your Destiny");
        Check(control.Text.StartsWith("Limited: Bow this Shugenja unless he is Void: Look at the top three cards of your Fate or Dynasty deck."), "its text");
        var katana = CardCatalog.Get(KatanaOfFire);
        Check(katana.Type == CardType.Spell && katana.Name == "Katana of Fire" && katana.GoldCost == 4 && katana.Focus == 2 && katana.Keywords!.Contains("Fire") && katana.Keywords.Contains("Experienced"), "Katana of Fire");
        var taming = CardCatalog.Get(TamingTheBeast);
        Check(taming.Type == CardType.Spell && taming.Name == "Taming the Beast" && taming.Focus == 1 && taming.GoldCost == 0 && taming.Keywords!.Contains("Earth"), "Taming the Beast");
        Check(taming.Abilities!.SequenceEqual(new[] { "taming-beast-negate", "taming-beast-straighten" }), "both of its abilities");
        var flame = CardCatalog.Get(FlameKatana);
        Check(flame.Type == CardType.Item && flame.CreatedOnly && flame.Name == "Flame Katana" && flame.Force == 3 && flame.Chi == 1 && flame.Keywords!.Contains("Weapon") && flame.Keywords.Contains("Fire"), "the Flame Katana is a created-only +3F/+1C Fire Weapon");
        Check(flame.Text.Contains("Battle: Bow this card unless this Personality is Fire, and target an enemy card without attachments: Destroy it."), "its text");
        var jurojin = CardCatalog.Get(Jurojin);
        Check(jurojin.Type == CardType.Celestial && jurojin.Name == "Jurojin's Blessing" && jurojin.Keywords!.Contains("Unique") && jurojin.Keywords.Contains("Fortune"), "Jurojin's Blessing");
        Check(jurojin.Abilities!.SequenceEqual(new[] { "jurojin-look", "jurojin-return" }), "both of its abilities");
        foreach (var id in new[] { Emoto, Interference, ControlYourDestiny, KatanaOfFire, TamingTheBeast, FlameKatana, Jurojin }) { Shipped(id); Check(!CardCatalog.Get(id).ScenarioOnly, id); }
        Eq(PublishedContent.PersonalityWave42.Cards.Length, 2); Eq(PublishedContent.StrategyWave34.Cards.Length, 1); Eq(PublishedContent.SpellWave12.Cards.Length, 3);
        Eq(PublishedContent.ItemWave7.Cards.Length, 1); Eq(PublishedContent.CelestialWave3.Cards.Length, 1);
        // The declared data: costs are declared, so a negated action still pays them.
        Check(AbilityCatalog.All["control-your-destiny-look"].Costs!.Single() is { Verb: CostVerb.Bow, Subject: CostSubject.Wielder, UnlessKeyword: "Void" }, "bow the wielder unless Void");
        Check(AbilityCatalog.All["flame-katana-destroy"].Costs!.Single() is { Verb: CostVerb.Bow, Subject: CostSubject.Source, UnlessKeyword: "Fire" }, "bow the Katana unless its wielder is Fire");
        Check(AbilityCatalog.All["taming-beast-straighten"].SourceUnitMayBeAway && AbilityCatalog.All["taming-beast-straighten"].Timing == "BattleOrOpen", "the unit may be away");
        foreach (var source in new[] { "source", "sources" }) { _ = source; }
        Check(File.ReadAllText(Path.Combine(root, "content/published/spell-wave-12-sources.json")).Contains("image_sha256"), "receipts are recorded");
    }

    // ---- Yoritomo Emoto -------------------------------------------------------------------------------------------------------------------------------
    // Player 0 attacks (the acting player) with Emoto, a Follower in his own unit, another Personality with a Follower, and an enemy Personality (Force 4) to destroy.
    private sealed record EmotoRig(MatchState S, CardInstance Emoto, CardInstance OwnFollower, CardInstance Other, CardInstance OtherFollower, CardInstance Enemy);
    private static EmotoRig EmotoTable()
    {
        var s = Table(Phase.Battle, 0);
        var emoto = Add(s, Emoto, 0, Zone.Battlefield, province: 0);
        var own = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: emoto.Id);
        var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var otherFollower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: other.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return new(s, emoto, own, other, otherFollower, enemy);
    }

    private static void EmotoDestroys()
    {
        foreach (var performer in new Func<EmotoRig, CardInstance>[] { r => r.OwnFollower, r => r.OtherFollower })
        {
            var r = EmotoTable(); var chosen = performer(r);
            var e = MatchEngine.FromTestState(r.S);
            var option = Enabled(e, 0, "emoto-follower-destroy", r.Emoto.Id, chosen.Id);
            Check(option.TargetIds!.Contains(r.Enemy.Id), "the enemy Personality is a legal target");
            Accept(e, Offer(e, 0, "emoto-follower-destroy", r.Emoto.Id, r.Enemy.Id, chosen.Id));
            Eq(e.State.Cards[r.Enemy.Id].Zone, Zone.Dead, "the target is destroyed");
            Check(e.State.Cards[chosen.Id].Bowed, "the performing Follower is bowed as the cost");
            Check(!e.State.Cards[r.Emoto.Id].Bowed, "Emoto is not bowed");
        }
    }

    private static void EmotoLegality()
    {
        var r = EmotoTable();
        var bowed = Add(r.S, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: r.Other.Id);
        var atHome = Add(r.S, "training-crane-samurai", 0, Zone.Home);
        var homeFollower = Add(r.S, "training-follower", 0, Zone.Attached, parent: atHome.Id);
        var enemyFollower = Add(r.S, "training-follower", 1, Zone.Attached, province: 0, parent: r.Enemy.Id);
        var armed = Add(r.S, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var armour = Add(r.S, "training-item", 1, Zone.Attached, province: 0, parent: armed.Id);
        var loneEnemy = Add(r.S, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var followerOnly = Add(r.S, "training-follower", 1, Zone.Attached, province: 0, parent: loneEnemy.Id);
        _ = followerOnly;
        var e = MatchEngine.FromTestState(r.S);
        Check(!CanUse(e, 0, "emoto-follower-destroy", r.Emoto.Id, bowed.Id), "a bowed Follower is not offered as the performer");
        Check(!CanUse(e, 0, "emoto-follower-destroy", r.Emoto.Id, homeFollower.Id), "a Follower whose unit is at home is not at the battle");
        Check(!CanUse(e, 0, "emoto-follower-destroy", r.Emoto.Id, enemyFollower.Id), "an enemy's Follower is not 'your' Follower");
        Check(!CanUse(e, 0, "emoto-follower-destroy", r.Emoto.Id, r.Other.Id), "a Personality is not a Follower");
        var ok = Enabled(e, 0, "emoto-follower-destroy", r.Emoto.Id, r.OwnFollower.Id);
        var targets = ok.TargetIds!.ToHashSet();
        Check(!targets.Contains(r.Other.Id) && !targets.Contains(r.OwnFollower.Id), "an own card is not an enemy card");
        Check(!targets.Contains(armed.Id), "a Personality with an attachment is refused (it must have none)");
        Check(targets.Contains(r.Enemy.Id) == false || CardHasNoAttachment(e, r.Enemy.Id), "");
        Reject(e, ok.Command! with { TargetId = armed.Id });
        Reject(e, ok.Command! with { TargetId = r.Other.Id });
        // A lone enemy Follower (no attachments of its own) is a legal target and, an attachment, is destroyed as one: it goes to the Fate discard pile, its host lives.
        var s2 = Table(Phase.Battle, 0);
        var emoto = Add(s2, Emoto, 0, Zone.Battlefield, province: 0);
        var mine = Add(s2, "training-follower", 0, Zone.Attached, province: 0, parent: emoto.Id);
        var host = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var theirs = Add(s2, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "emoto-follower-destroy", emoto.Id, theirs.Id, mine.Id));
        Eq(e2.State.Cards[theirs.Id].Zone, Zone.FateDiscard, "destroyed as an attachment");
        Eq(e2.State.Cards[host.Id].Zone, Zone.Battlefield, "its host lives");
        Check(e2.State.Cards[theirs.Id].AttachedTo is null, "and it is detached");
    }

    private static bool CardHasNoAttachment(MatchEngine e, string id) => !e.State.Cards.Values.Any(c => c.Zone == Zone.Attached && c.AttachedTo == id);

    // ---- Interference --------------------------------------------------------------------------------------------------------------------------------
    private static void InterferenceLocksAStronghold()
    {
        // Player 0's turn: the enemy Stronghold (Shinden Asahina, whose Open action needs a Shugenja and an unbowed enemy Personality) is locked AND bowed.
        MatchState Build(int active)
        {
            var st = Table(Phase.Action, active);
            st.Cards[st.Players[1].StrongholdId].DefinitionId = ShindenAsahina;
            Add(st, "training-shugenja", 1, Zone.Home); Add(st, "training-crane-samurai", 0, Zone.Home);
            Add(st, Interference, 0, Zone.Hand);
            return st;
        }
        var s = Build(0);
        var spell = s.Cards.Values.Single(c => c.DefinitionId == Interference);
        var e = MatchEngine.FromTestState(s);
        var enemyStronghold = s.Players[1].StrongholdId; var ownStronghold = s.Players[0].StrongholdId;
        var option = Enabled(e, 0, "interference-lock", spell.Id);
        Check(option.TargetIds!.Contains(enemyStronghold) && option.TargetIds.Contains(ownStronghold), "either player's Stronghold is a legal target");
        Check(option.TargetIds!.All(t => CardCatalog.Get(e.State.Cards[t].DefinitionId).Type == CardType.Stronghold), "and nothing else is");
        Check(!View(e, enemyStronghold, 1).ActionLocked, "not locked yet");
        // Control: on player 1's own turn, before any lock, the Shinden action is on offer.
        var control = Edit(MatchEngine.FromTestState(Build(0)), st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 1; });
        Check(CanUse(control, 1, "shinden-choice"), "unlocked, the Stronghold's Open action is offered");
        Accept(e, Offer(e, 0, "interference-lock", spell.Id, enemyStronghold));
        Check(e.State.Cards[enemyStronghold].Bowed, "bowed: it is Interference's user's own turn");
        Check(View(e, enemyStronghold, 0).ActionLocked && View(e, enemyStronghold, 1).ActionLocked, "and locked, visibly to both players");
        Eq(e.State.Cards[spell.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Later, on player 1's own turn, the locked Stronghold still cannot act.
        var later = Edit(e, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 1; st.Cards[enemyStronghold].Bowed = false; });
        var shinden = Options(later, 1, "shinden-choice");
        Check(shinden.Length > 0 && shinden.All(o => !o.Enabled) && shinden.Any(o => o.Reason!.Contains("abilities may not be used")), "the locked Stronghold's action is refused: " + string.Join("; ", shinden.Select(o => o.Reason)) + " || all: " + string.Join("; ", later.QueryActions(1).Select(a => $"{a.Type}/{a.Label} [{a.Enabled}] {a.Reason}")));

        // Not the user's turn: the target is locked but NOT bowed.
        var s2 = Table(Phase.Action, 1);
        s2.PriorityPlayerId = 0;
        var spell2 = Add(s2, Interference, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        var target = s2.Players[1].StrongholdId;
        Accept(e2, Offer(e2, 0, "interference-lock", spell2.Id, target));
        Check(!e2.State.Cards[target].Bowed && View(e2, target, 1).ActionLocked, "locked, not bowed, when it is not the user's turn");
        // The lock ends with the turn.
        var t = e2.State.TurnNumber;
        RunUntil(e2, () => e2.State.TurnNumber > t);
        Check(!View(e2, target, 1).ActionLocked, "the lock is gone once the turn has ended");
    }

    // ---- Control Your Destiny -------------------------------------------------------------------------------------------------------------------------
    private sealed record LookRig(MatchState S, CardInstance Shugenja, CardInstance Spell);
    private static LookRig LookTable(bool voidShugenja = false, bool bowed = false)
    {
        var s = Table(Phase.Action, 0);
        var sh = Add(s, "training-shugenja", 0, Zone.Home, bowed: bowed);
        if (voidShugenja) GiveKeyword(sh, "Void");
        var spell = Add(s, ControlYourDestiny, 0, Zone.Attached, parent: sh.Id);
        return new(s, sh, spell);
    }

    private static void ControlYourDestinyLooks()
    {
        var r = LookTable();
        var fateTop = r.S.Players[0].FateDeck.Take(3).ToArray(); var dynastyTop = r.S.Players[0].DynastyDeck.Take(3).ToArray();
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Enabled(e, 0, "control-your-destiny-look", r.Spell.Id).Command!);
        Check(e.State.Cards[r.Shugenja.Id].Bowed, "the Shugenja is bowed as the cost (he is not Void)");
        Eq(e.State.PendingChoice?.Type, "lookDeck", "the player chooses which deck");
        var choices = e.GetView(0).PendingLook!.DeckChoices!;
        Check(choices.Length == 2 && choices.All(c => c.OwnDeck) && choices.Select(c => c.Deck).OrderBy(x => x).SequenceEqual(new[] { "Dynasty", "Fate" }), "the Fate or the Dynasty deck of his own");
        Check(e.GetView(1).PendingLook is null, "the opponent sees no look");
        Accept(e, ChooseDeck(e, "Fate"));
        var view = e.GetView(0).PendingLook!;
        Eq(view.Cards!.Length, 3, "three cards are shown"); Eq(view.Requested, 3);
        Check(view.Rules!.Single() is { Disposition: LookDisposition.Bottom, Min: 0, Max: 2 } && view.CanReorder && view.Remainder == LookDisposition.Top, "up to two may go to the bottom, the rest back on top in any order");
        Check(view.Cards.Select(c => c.DefinitionId).SequenceEqual(fateTop.Select(id => e.State.Cards[id].DefinitionId)), "the top three of the Fate deck, top first");
        Check(e.GetView(1).PendingLook is null, "still nothing for the opponent");
        // Too many to the bottom is refused (a third), and the state is untouched.
        Reject(e, Arrange(e, "0:bottom", "1:bottom", "2:bottom"));
        // One to the bottom, the other two back in a new order.
        Accept(e, Arrange(e, "0:bottom", "2:top", "1:top"));
        var deck = e.State.Players[0].FateDeck;
        Check(deck[0] == fateTop[2] && deck[1] == fateTop[1] && deck[^1] == fateTop[0], "the third card is on top, then the second; the first is on the bottom");
        Check(e.State.PendingChoice is null && e.State.Players[0].DynastyDeck.Take(3).SequenceEqual(dynastyTop), "the Dynasty deck is untouched");

        // The Dynasty deck, nothing moved to the bottom.
        var r2 = LookTable(voidShugenja: true);
        var top2 = r2.S.Players[0].DynastyDeck.Take(3).ToArray();
        var e2 = MatchEngine.FromTestState(r2.S);
        Accept(e2, Enabled(e2, 0, "control-your-destiny-look", r2.Spell.Id).Command!);
        Check(!e2.State.Cards[r2.Shugenja.Id].Bowed, "a Void Shugenja is not bowed");
        Accept(e2, ChooseDeck(e2, "Dynasty"));
        Accept(e2, Arrange(e2, "1:top", "0:top", "2:top"));
        Check(e2.State.Players[0].DynastyDeck.Take(3).SequenceEqual(new[] { top2[1], top2[0], top2[2] }), "reordered on top");
    }

    private static void ControlYourDestinyCost()
    {
        var notVoid = LookTable(bowed: true);
        var e = MatchEngine.FromTestState(notVoid.S);
        var options = Options(e, 0, "control-your-destiny-look");
        Check(options.Length > 0 && options.All(o => !o.Enabled) && options.Any(o => o.Reason!.Contains("already bowed")), "a bowed Shugenja who is not Void cannot pay: " + string.Join("; ", options.Select(o => o.Reason)));
        var voidBowed = LookTable(voidShugenja: true, bowed: true);
        var e2 = MatchEngine.FromTestState(voidBowed.S);
        Accept(e2, Enabled(e2, 0, "control-your-destiny-look", voidBowed.Spell.Id).Command!);
        Check(e2.State.Cards[voidBowed.Shugenja.Id].Bowed, "nothing was bowed or straightened: he is still bowed");
        Check(e2.State.PendingChoice?.Type == "lookDeck", "and the look began");
        // The Spell must be attached: in a hand it is not offered at all.
        var s3 = Table(Phase.Action, 0); Add(s3, ControlYourDestiny, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e3, 0, "control-your-destiny-look"), "a Spell in a hand is not offered");
        // It is a Limited action: only on its controller's own turn.
        var other = Edit(MatchEngine.FromTestState(LookTable().S), st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 0; });
        Check(!CanUse(other, 0, "control-your-destiny-look"), "not on another player's turn");
    }

    // ---- Jurojin's Blessing ---------------------------------------------------------------------------------------------------------------------------
    private static void JurojinLooks()
    {
        foreach (var discard in new[] { true, false })
        {
            var s = Table(Phase.Action, 0);
            var celestial = Add(s, Jurojin, 0, Zone.Celestial);
            var top = s.Players[0].FateDeck[0]; var second = s.Players[0].FateDeck[1];
            var e = MatchEngine.FromTestState(s);
            Accept(e, Enabled(e, 0, "jurojin-look", celestial.Id).Command!);
            var view = e.GetView(0).PendingLook!;
            Eq(view.Cards!.Length, 1, "one card is shown"); Check(view.Rules!.Single() is { Disposition: LookDisposition.Discard, Min: 0, Max: 1 }, "it may be discarded");
            Check(e.GetView(1).PendingLook is null, "the opponent sees nothing of it");
            HiddenIdsAbsent(e.GetView(1), [top]);
            Accept(e, Arrange(e, discard ? "0:discard" : "0:top"));
            if (discard) Check(e.State.Cards[top].Zone == Zone.FateDiscard && e.State.Players[0].FateDeck[0] == second, "discarded: the next card is on top");
            else Check(e.State.Players[0].FateDeck[0] == top, "kept on top");
            Check(e.State.Cards[celestial.Id].Zone == Zone.Celestial, "the Celestial stays in play");
        }
    }

    private static void JurojinReturns()
    {
        var s = Table(Phase.Action, 0);
        var celestial = Add(s, Jurojin, 0, Zone.Celestial);
        var strategy = Add(s, "training-rally", 0, Zone.FateDiscard);
        var unique = Add(s, "training-rally", 0, Zone.FateDiscard); GiveKeyword(unique, "Unique");
        var theirs = Add(s, "training-rally", 1, Zone.FateDiscard);
        var followerInDiscard = Add(s, "training-follower", 0, Zone.FateDiscard);
        var inHand = Add(s, "training-rally", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "jurojin-return", celestial.Id);
        Check(option.TargetIds!.SequenceEqual(new[] { strategy.Id }), "only my own non-Unique Strategy in my Fate discard pile is a target: " + string.Join(",", option.TargetIds!));
        foreach (var bad in new[] { unique.Id, theirs.Id, followerInDiscard.Id, inHand.Id }) Reject(e, option.Command! with { TargetId = bad });
        Accept(e, option.Command! with { TargetId = strategy.Id });
        Check(e.State.Players[0].Hand.Contains(strategy.Id) && e.State.Cards[strategy.Id].Zone == Zone.Hand, "the Strategy is in my hand");
        Eq(e.State.Cards[celestial.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
        Check(!e.GetView(1).Players.SelectMany(p => p.Hand).Any(c => c.Id == strategy.Id && c.Definition is not null), "and the opponent does not see it in my hand");
        // With no Strategy to return, the Celestial is not spent: the action is not offered.
        var s2 = Table(Phase.Action, 0); Add(s2, Jurojin, 0, Zone.Celestial);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "jurojin-return"), "nothing to return, nothing offered");
    }

    // ---- Katana of Fire and the Flame Katana -----------------------------------------------------------------------------------------------------------
    private static void KatanaOfFire_()
    {
        var s = Table(Phase.Action, 0);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Home);
        var spell = Add(s, KatanaOfFire, 0, Zone.Attached, parent: shugenja.Id);
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "katana-of-fire-create", spell.Id, samurai.Id));
        var katana = e.State.Cards.Values.Single(c => c.DefinitionId == FlameKatana);
        Check(katana.Zone == Zone.Attached && katana.AttachedTo == samurai.Id && katana.OwnerId == 0, "the Flame Katana is attached to the chosen Personality");
        Check(CardCatalog.Get(FlameKatana).CreatedOnly, "a created card");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        var used = Options(e, 0, "katana-of-fire-create").Single();
        Check(!used.Enabled && used.Reason!.Contains("already been used"), "once per game: " + used.Reason);

        // The Flame Katana's action: an enemy card without attachments is destroyed; the Katana bows unless its wielder is Fire.
        foreach (var fire in new[] { false, true })
        {
            var b = Table(Phase.Battle, 0);
            var wielder = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var blade = Add(b, FlameKatana, 0, Zone.Attached, province: 0, parent: wielder.Id);
            if (fire) GiveKeyword(wielder, "Fire");
            var enemy = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var armed = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(b, "training-item", 1, Zone.Attached, province: 0, parent: armed.Id);
            var eb = MatchEngine.FromTestState(b);
            var option = Enabled(eb, 0, "flame-katana-destroy", blade.Id);
            Check(option.TargetIds!.Contains(enemy.Id) && !option.TargetIds.Contains(armed.Id), "an enemy card without attachments only");
            Accept(eb, Offer(eb, 0, "flame-katana-destroy", blade.Id, enemy.Id));
            Eq(eb.State.Cards[enemy.Id].Zone, Zone.Dead, "destroyed");
            Eq(eb.State.Cards[blade.Id].Bowed, !fire, fire ? "a Fire wielder does not bow it" : "otherwise the Katana bows");
        }
        // A bowed Katana cannot be used, Fire wielder or not (abilities on a bowed card may not be used).
        var c = Table(Phase.Battle, 0);
        var w = Add(c, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(w, "Fire");
        Add(c, FlameKatana, 0, Zone.Attached, bowed: true, province: 0, parent: w.Id);
        Add(c, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(c), 0, "flame-katana-destroy"), "a bowed Katana is not usable");
    }

    // ---- Taming the Beast ------------------------------------------------------------------------------------------------------------------------------
    // Player 1 attacks; player 0 defends with a Shugenja (Earth) carrying the Spell and a defending Personality at province 0.
    private sealed record TamingRig(MatchState S, CardInstance Shugenja, CardInstance Spell, CardInstance Defender, CardInstance Destroyer);
    private static TamingRig TamingTable(bool earth = true, string actor = "training-no-attachments-destroyer")
    {
        var s = Table();
        var sh = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        if (earth) GiveKeyword(sh, "Earth");
        var spell = Add(s, TamingTheBeast, 0, Zone.Attached, province: 0, parent: sh.Id);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var destroyer = Add(s, actor, 1, Zone.Battlefield, province: 0);
        return new(s, sh, spell, defender, destroyer);
    }

    private static void TamingTheBeastNegates()
    {
        var r = TamingTable();
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", r.Destroyer.Id, r.Defender.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for the defending player");
        Check(e.State.Cards[r.Defender.Id].Zone == Zone.Battlefield, "nothing has happened yet");
        Accept(e, ReactionOffer(e, 0, "taming-beast-negate", r.Spell.Id));
        ClosePassing(e);
        Eq(e.State.Cards[r.Spell.Id].Zone, Zone.FateDiscard, "the Spell destroyed itself as the cost");
        Check(e.State.Cards[r.Defender.Id].Zone is Zone.Battlefield or Zone.Home, "the Personality's destruction was negated");
        Check(e.State.Events.Any(x => x.Type == "destructionNegated"), "logged");
        Check(e.State.Cards[r.Shugenja.Id].Zone != Zone.FateDiscard, "the Shugenja is untouched");

        // Declined: he dies.
        var d = TamingTable();
        var ed = MatchEngine.FromTestState(d.S);
        Accept(ed, Offer(ed, 1, "training-destroy-no-attachments", d.Destroyer.Id, d.Defender.Id));
        ClosePassing(ed);
        Eq(ed.State.Cards[d.Defender.Id].Zone, Zone.Dead, "without the reaction he is destroyed");
        Eq(ed.State.Cards[d.Spell.Id].Zone, Zone.Attached, "and the Spell is untouched");

        // Not offered: the Shugenja is not Earth; the action cannot destroy (a bow); the target is not a defender (he attacks); the actor is his own controller.
        var notEarth = TamingTable(earth: false);
        var en = MatchEngine.FromTestState(notEarth.S);
        Accept(en, Offer(en, 1, "training-destroy-no-attachments", notEarth.Destroyer.Id, notEarth.Defender.Id));
        Check(!ReactionOffered(en, 0, "taming-beast-negate"), "not while its Shugenja is not Earth");
        ClosePassing(en);
        var bow = TamingTable(actor: "training-battle-bower");
        var eb = MatchEngine.FromTestState(bow.S);
        Accept(eb, Offer(eb, 1, "training-bow-at-battle", bow.Destroyer.Id, bow.Defender.Id));
        Check(!ReactionOffered(eb, 0, "taming-beast-negate"), "not against an action that cannot destroy him");
        ClosePassing(eb);
        var attacker = Table(Phase.Battle, 0); attacker.PriorityPlayerId = 1; // player 0 is the Attacker; the enemy acts on his opportunity
        var sh2 = Add(attacker, "training-shugenja", 0, Zone.Battlefield, province: 0); GiveKeyword(sh2, "Earth");
        Add(attacker, TamingTheBeast, 0, Zone.Attached, province: 0, parent: sh2.Id);
        var attacking = Add(attacker, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(attacker, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var ea = MatchEngine.FromTestState(attacker);
        Accept(ea, Offer(ea, 1, "training-destroy-no-attachments", enemy.Id, attacking.Id));
        Check(!ReactionOffered(ea, 0, "taming-beast-negate"), "not for an attacking Personality: it says defending");
    }

    private static void TamingTheBeastStraightens()
    {
        // In Battle: the Shugenja's unit is at home, the Nonhuman (bowed) is at the battlefield -- still legal, the Shugenja bows.
        var s = Table(Phase.Battle, 0);
        var sh = Add(s, "training-shugenja", 0, Zone.Home);
        var spell = Add(s, TamingTheBeast, 0, Zone.Attached, parent: sh.Id);
        var beast = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0); GiveKeyword(beast, "Nonhuman");
        var human = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var enemyBeast = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0); GiveKeyword(enemyBeast, "Nonhuman");
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "taming-beast-straighten", spell.Id);
        Check(option.TargetIds!.SequenceEqual(new[] { beast.Id }), "only my own Nonhuman: " + string.Join(",", option.TargetIds!));
        Reject(e, option.Command! with { TargetId = human.Id }); Reject(e, option.Command! with { TargetId = enemyBeast.Id });
        Accept(e, option.Command! with { TargetId = beast.Id });
        Check(!e.State.Cards[beast.Id].Bowed && e.State.Cards[sh.Id].Bowed, "the Nonhuman is straightened and the Shugenja bowed");

        // A bowed Shugenja cannot pay.
        var b = Table(Phase.Battle, 0);
        var sh2 = Add(b, "training-shugenja", 0, Zone.Home, bowed: true);
        Add(b, TamingTheBeast, 0, Zone.Attached, parent: sh2.Id);
        var beast2 = Add(b, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0); GiveKeyword(beast2, "Nonhuman");
        Check(!CanUse(MatchEngine.FromTestState(b), 0, "taming-beast-straighten"), "a bowed Shugenja cannot be bowed again");

        // As an Open action (the Action phase), no battle at all.
        var o = Table(Phase.Action, 0);
        var sh3 = Add(o, "training-shugenja", 0, Zone.Home);
        var spell3 = Add(o, TamingTheBeast, 0, Zone.Attached, parent: sh3.Id);
        var beast3 = Add(o, "training-crane-samurai", 0, Zone.Home, bowed: true); GiveKeyword(beast3, "Nonhuman");
        var eo = MatchEngine.FromTestState(o);
        Accept(eo, Offer(eo, 0, "taming-beast-straighten", spell3.Id, beast3.Id));
        Check(!eo.State.Cards[beast3.Id].Bowed && eo.State.Cards[sh3.Id].Bowed, "Open: straightened, Shugenja bowed");
    }

    private static void OfferedCommandsValidateFresh()
    {
        MatchState Build()
        {
            var s = Table(Phase.Action, 0);
            var sh = Add(s, "training-shugenja", 0, Zone.Home);
            Add(s, ControlYourDestiny, 0, Zone.Attached, parent: sh.Id);
            var sh2 = Add(s, "training-shugenja", 0, Zone.Home);
            var spell = Add(s, TamingTheBeast, 0, Zone.Attached, parent: sh2.Id);
            var beast = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true); GiveKeyword(beast, "Nonhuman");
            Add(s, Interference, 0, Zone.Hand);
            Add(s, Jurojin, 0, Zone.Celestial); Add(s, "training-rally", 0, Zone.FateDiscard);
            var sh3 = Add(s, "training-shugenja", 0, Zone.Home);
            Add(s, KatanaOfFire, 0, Zone.Attached, parent: sh3.Id);
            _ = spell;
            return s;
        }
        var e = MatchEngine.FromTestState(Build());
        var offered = e.QueryActions(0).Where(a => a.Enabled && a.Command?.AbilityId is { } id && id is "control-your-destiny-look" or "taming-beast-straighten" or "interference-lock" or "jurojin-look" or "jurojin-return" or "katana-of-fire-create").ToArray();
        Check(offered.Select(a => a.Command!.AbilityId).Distinct().Count() == 6, "all six are offered: " + string.Join(",", offered.Select(a => a.Command!.AbilityId)));
        OfferedCommandsValidate(e, () => MatchEngine.FromTestState(Build()), alternateTargets: true);
    }
}
