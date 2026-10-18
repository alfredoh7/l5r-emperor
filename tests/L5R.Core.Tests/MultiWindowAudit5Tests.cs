using static RegressionAssertions;
using static Batch336Support;
using L5R.Core;

// Multi-window audit, slice 5 (issue #830): abilities whose own preconditions the generic "kitchen sink" probe could not satisfy, so they were never seen
// enabled. Each test builds the card's real preconditions by hand and drives the ability through QueryActions / Execute in EVERY window its timing allows
// (offered enabled AND accepted, with the printed effect checked) and in the windows it does not (not offered, refused with the state untouched).
// Player 0 is always the actor ("me"); player 1 the opponent. Windows: Battle as attacker, Battle as defender, Action Phase as the active player, and the
// Action Phase holding the opportunity as the non-active player.
internal static partial class MultiWindowAudit5Tests
{
    private enum W { BattleAttacker, BattleDefender, ActionActive, ActionOther }
    private static readonly W[] Battle = [W.BattleAttacker, W.BattleDefender], Action = [W.ActionActive, W.ActionOther], Everywhere = [W.BattleAttacker, W.BattleDefender, W.ActionActive, W.ActionOther];

    public static void Run(TestRunner runner) => runner.Run("multi-window-audit-5", [
        ("Asahina Munefusa munefusa-straighten (Open): straightens my bowed Temple in the Action Phase as active and non-active player; not offered or accepted in a battle", Munefusa),
        ("Bonds of Coin bonds-of-coin-destroy (Political Open): destroys a 0-cost attachment in both Action windows, refused in a battle", BondsDestroy),
        ("Bonds of Coin bonds-of-coin-draw (Favor Limited): with the Imperial Favor and an unbowed Stronghold draws two for the active player only; refused as non-active player and in a battle", BondsDraw),
        ("Control the Board control-the-board-terrain (Battle): destroys the Terrain at the battlefield as attacker and as defender; refused in the Action Phase", ControlTheBoardTerrain),
        ("Duel of Serpents duel-serpents-honor (Open): a Nonhuman bows to gain 3 Honor when behind, in both Action windows; refused in a battle", DuelSerpentsHonor),
        ("Flanking Unit flanking-unit-move-adjacent (Battle): moves a Personality at the battlefield to the adjacent unresolved battlefield as attacker and as defender; refused in the Action Phase", FlankingAdjacent),
        ("Headbutt p5-headbutt-destroy-fresh (Battle): a Berserker who entered after the Combat Segment began destroys a weaker unarmed enemy card unbowed, as attacker and as defender; refused in the Action Phase", HeadbuttFresh),
        ("Isawa Hibana hibana-boost (Battle/Open): -3F on Hibana, +3F on a non-Water Personality in all four windows", HibanaBoost),
        ("Koan's Whisper w741-whisper-destroy (Void Limited): five Koan cards removed destroy a Province in the Action Phase as active player only; refused as non-active player and in a battle", WhisperDestroy),
        ("Meeting the Keepers keepers-draw (Open): bow my Ring, draw two, in both Action windows; refused in a battle", KeepersDraw),
        ("Mukudori's Wisdom wl-sg-mukudori-straighten (Battle/Open): straightens my bowed Ring in all four windows", MukudoriStraighten),
        ("Mukudori's Wisdom wl-sg-mukudori-bow (Battle/Open): bows the enemy Ring in all four windows", MukudoriBow),
        ("Provoked Violence wlsac-provoked-violence-yojimbo (Political Open): with a Yojimbo a Gold Cost 3 attachment is destroyed for free, in both Action windows; refused in a battle", ProvokedYojimbo),
        ("Ring of Fire ring-of-fire-recruit (Battle): after a battle that destroyed enemy cards, the Ring is put into play from hand, whether the winner attacked or defended; not offered before the grant or in the Action Phase", RingOfFireRecruit),
        ("Ring of Earth ring-of-earth-recruit (Battle): after a won battle against enemy units with no losses, the Ring is put into play from hand, attacking or defending; not offered before the grant or in the Action Phase", RingOfEarthRecruit),
        ("Serenity in Air serenity-air-straighten (Battle): straightens my Personality as attacker and as defender; refused in the Action Phase", SerenityBattle),
        ("Serenity in Air serenity-air-straighten-open (Open): straightens my Personality discarding a card in both Action windows; refused in a battle", SerenityOpen),
        ("Sinkhole pe-sinkhole-terrain (Battle): destroys the Terrain at the battlefield and bows the target, as attacker and as defender; refused in the Action Phase", SinkholeTerrain),
        ("Sinkhole pe-sinkhole-region (Battle): destroys the Region of the battlefield's province and moves the target home, as attacker and as defender; refused in the Action Phase", SinkholeRegion),
        ("Strong Defenses strong-defenses-boost (Battle): +5F and straighten for my defending Personality, as defender only; refused as attacker and in the Action Phase", StrongDefensesBoost),
        ("The Blade of the Balash pf-balash-attack-hand (Battle): a Dragon Clan player discards it from hand for a Melee 5 Attack, as attacker and as defender; refused in the Action Phase", BalashHand),
        ("The Honor of the Lion honor-lion-boost (Battle/Open): permanently raises an honorable Lion's Personal Honor to 4 and grants the bow ability in all four windows", HonorOfTheLion),
        ("The Sincerity of the Dragon sincerity-dragon-name (Battle/Open): a Dragon Clan player names a number (watch set, card removed from the game) in all four windows", SincerityOfTheDragon),
        ("Through the Breach pe-through-the-breach-remove (Limited): permanently removes a Castle Holding's ability in the Action Phase as active player only; refused as non-active player and in a battle", ThroughTheBreachRemove),
        ("Training token master training-move-token (Open): moves a token from target to performer in both Action windows; refused in a battle", TrainingMoveToken),
        ("Training token master training-destroy-token (Open): destroys a token on the target in both Action windows; refused in a battle", TrainingDestroyToken),
        ("Unsettling Doubts p5-doubts-remove (Open): permanently removes a borrowed Ring's abilities in both Action windows; refused in a battle", DoubtsRemove),
    ]);

    // ---- rigs and the window runner ------------------------------------------------------------------------------------------------------------------------------

    // Program.cs top-level helpers of the same names shadow the static imports in this file, so the shared ones are named explicitly (as the other suites do).
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    private static bool InBattle(W w) => w is W.BattleAttacker or W.BattleDefender;

    // Player 0 holds the opportunity in every window. Battle: province 0 is the battlefield, player 0 attacks (BattleAttacker) or defends (BattleDefender).
    private static MatchState Rig(W w)
    {
        var s = w switch
        {
            W.BattleAttacker => Table(Phase.Battle, 0),
            W.BattleDefender => Table(Phase.Battle, 1),
            W.ActionActive => Table(Phase.Action, 0),
            _ => Table(Phase.Action, 1),
        };
        s.PriorityPlayerId = 0;
        return s;
    }

    // A unit: at the battlefield in a battle window, at home in the Action Phase.
    private static CardInstance Unit(MatchState s, W w, string def, int owner, bool bowed = false) => InBattle(w) ? Add(s, def, owner, Zone.Battlefield, bowed, province: 0) : Add(s, def, owner, Zone.Home, bowed);

    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }

    // Both sides have a unit at the battlefield in a battle window (the presence a Battle action needs); nothing in the Action Phase.
    private static void Armies(MatchState s, W w) { if (InBattle(w)) { Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); } }

    private sealed record Case(MatchEngine E, string Ability, string CardId, GameCommand Cmd, Action<MatchEngine>? Verify = null, string? Performer = null);

    // Runs the build in every window: where the timing allows, the ability is offered enabled and the command is accepted and its effect checked; elsewhere it is
    // not offered and the command is refused with the position unchanged.
    private static void Windows(string what, W[] allowed, Func<W, Case> build)
    {
        foreach (var w in Enum.GetValues<W>())
        {
            var c = build(w);
            var options = Options(c.E, 0, c.Ability, c.CardId, c.Performer);
            if (allowed.Contains(w))
            {
                Check(options.Any(o => o.Enabled), $"{what}: not offered enabled in {w}: " + string.Join("; ", options.Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
                Accept(c.E, c.Cmd);
                c.Verify?.Invoke(c.E);
            }
            else
            {
                Check(!options.Any(o => o.Enabled), $"{what}: offered enabled in {w}, where its timing forbids it");
                Reject(c.E, c.Cmd);
            }
        }
    }

    private static GameCommand Choose(MatchEngine e, string choice) =>
        e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Enabled && a.Command!.Choice == choice).Command!;

    // ---- Open ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Munefusa() => Windows("Munefusa", Action, w =>
    {
        var s = Rig(w);
        var mune = Unit(s, w, "oracle-l5r-505", 0);
        var temple = Add(s, "training-temple-holding", 0, Zone.Home, bowed: true);
        Armies(s, w);
        var e = MatchEngine.FromTestState(s);
        return new(e, "munefusa-straighten", mune.Id, Use(e, 0, "munefusa-straighten", mune.Id, temple.Id),
            e2 => Check(!e2.State.Cards[temple.Id].Bowed, "the Temple is straightened"));
    });

    private static void BondsDestroy() => Windows("Bonds of Coin destroy", Action, w =>
    {
        var s = Rig(w);
        var host = Unit(s, w, "training-lion-samurai", 1);
        var free = Add(s, "training-zero-cost-item", 1, Zone.Attached, parent: host.Id);
        var priced = Add(s, "training-item", 1, Zone.Attached, parent: host.Id);
        var card = Add(s, "oracle-l5r-1092", 0, Zone.Hand);
        Armies(s, w);
        var e = MatchEngine.FromTestState(s);
        return new(e, "bonds-of-coin-destroy", card.Id, Use(e, 0, "bonds-of-coin-destroy", card.Id, selections: Pick("attachments", free.Id)), e2 =>
        {
            Eq(e2.State.Cards[free.Id].Zone, Zone.FateDiscard, "the 0-cost attachment is destroyed");
            Eq(e2.State.Cards[priced.Id].Zone, Zone.Attached, "the priced one is untouched");
            Eq(e2.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        });
    });

    private static void BondsDraw() => Windows("Bonds of Coin draw", [W.ActionActive], w =>
    {
        var s = Rig(w);
        s.FavorPlayerId = 0;
        var card = Add(s, "oracle-l5r-1092", 0, Zone.Hand);
        Armies(s, w);
        var e = MatchEngine.FromTestState(s);
        var hand = e.State.Players[0].Hand.Count; var deck = e.State.Players[0].FateDeck.Count;
        return new(e, "bonds-of-coin-draw", card.Id, Use(e, 0, "bonds-of-coin-draw", card.Id), e2 =>
        {
            Check(e2.State.Cards[e2.State.Players[0].StrongholdId].Bowed, "the Stronghold is bowed as the cost");
            Check(e2.State.FavorPlayerId is null, "the Imperial Favor is discarded");
            Eq(e2.State.Players[0].Hand.Count, hand - 1 + 2, "the Strategy left the hand and two cards were drawn"); Eq(e2.State.Players[0].FateDeck.Count, deck - 2);
        });
    });

    private static void DuelSerpentsHonor() => Windows("Duel of Serpents honor", Action, w =>
    {
        var s = Rig(w);
        s.Players[0].Honor = 2; s.Players[1].Honor = 5;
        var card = Add(s, "oracle-l5r-2246", 0, Zone.Hand);
        var nonhuman = Kw(Unit(s, w, "training-crane-samurai", 0), "Nonhuman");
        Armies(s, w);
        var e = MatchEngine.FromTestState(s);
        return new(e, "duel-serpents-honor", card.Id, Use(e, 0, "duel-serpents-honor", card.Id, performerId: nonhuman.Id), e2 =>
        {
            Eq(Honor(e2, 0), 5, "gain 3 Honor");
            Check(e2.State.Cards[nonhuman.Id].Bowed, "the performer bowed as the cost");
        }, nonhuman.Id);
    });

    private static void KeepersDraw() => Windows("Meeting the Keepers draw", Action, w =>
    {
        var s = Rig(w);
        var ring = Add(s, "training-ring-of-air", 0, Zone.Home);
        var card = Add(s, "oracle-l5r-5018", 0, Zone.Hand);
        Armies(s, w);
        var e = MatchEngine.FromTestState(s);
        var hand = e.State.Players[0].Hand.Count; var deck = e.State.Players[0].FateDeck.Count;
        return new(e, "keepers-draw", card.Id, Use(e, 0, "keepers-draw", card.Id, ring.Id), e2 =>
        {
            Check(e2.State.Cards[ring.Id].Bowed, "the Ring was bowed as the cost");
            Eq(e2.State.Players[0].FateDeck.Count, deck - 2, "two cards were drawn"); Eq(e2.State.Players[0].Hand.Count, hand - 1 + 2);
        });
    });

    private static void ProvokedYojimbo() => Windows("Provoked Violence (Yojimbo)", Action, w =>
    {
        var s = Rig(w);
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var courtier = Kw(Unit(s, w, "training-crane-courtier", 0), "Courtier");
        Kw(Unit(s, w, "training-crane-samurai", 0), "Yojimbo");
        var host = Unit(s, w, "training-lion-samurai", 1);
        var dear = Add(s, "training-costly-weapon", 1, Zone.Attached, parent: host.Id); // Gold Cost 3: free with a Yojimbo
        var card = Add(s, "oracle-l5r-10951", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        return new(e, "wlsac-provoked-violence-yojimbo", card.Id, Use(e, 0, "wlsac-provoked-violence-yojimbo", card.Id, dear.Id, null, Pick("attachment", dear.Id), Pick("courtier", courtier.Id)), e2 =>
        {
            Check(e2.State.Cards[courtier.Id].Bowed, "my Courtier is bowed");
            Eq(e2.State.Cards[dear.Id].Zone, Zone.FateDiscard, "the Gold Cost 3 attachment is destroyed with a Yojimbo");
        });
    });

    private static void TrainingMoveToken() => Windows("Training token master move", Action, w =>
    {
        var s = Rig(w);
        var master = Unit(s, w, "training-token-master", 0);
        master.Tokens = [new("test-token", "Training", "test-fixture", "Force", 1)];
        var other = Unit(s, w, "training-crane-samurai", 0);
        Armies(s, w);
        var e = MatchEngine.FromTestState(s);
        return new(e, "training-move-token", master.Id, Use(e, 0, "training-move-token", master.Id, master.Id, other.Id), e2 =>
        {
            Eq(e2.State.Cards[master.Id].Tokens.Count, 0, "the token left the target"); Eq(e2.State.Cards[other.Id].Tokens.Count, 1, "and arrived on the performer");
            Eq(e2.State.Cards[other.Id].Tokens[0].Id, "test-token");
        }, other.Id);
    });

    private static void TrainingDestroyToken() => Windows("Training token master destroy", Action, w =>
    {
        var s = Rig(w);
        var master = Unit(s, w, "training-token-master", 0);
        master.Tokens = [new("test-token", "Training", "test-fixture", "Force", 1)];
        Armies(s, w);
        var e = MatchEngine.FromTestState(s);
        return new(e, "training-destroy-token", master.Id, Use(e, 0, "training-destroy-token", master.Id, master.Id),
            e2 => Eq(e2.State.Cards[master.Id].Tokens.Count, 0, "the token is destroyed"));
    });

    private static void DoubtsRemove() => Windows("Unsettling Doubts", Action, w =>
    {
        var s = Rig(w);
        var doubts = Add(s, "oracle-l5r-10178", 0, Zone.Hand);
        var borrowed = Add(s, "training-ring-of-air", 1, Zone.Home); borrowed.AbilityUses["ee-e:no-enlightenment"] = 1; // entered play by another card's text
        Armies(s, w);
        var e = MatchEngine.FromTestState(s);
        Check(e.AbilityIdsForTest(borrowed.Id).Length > 0, "the Ring has abilities before");
        return new(e, "p5-doubts-remove", doubts.Id, Use(e, 0, "p5-doubts-remove", doubts.Id, borrowed.Id), e2 =>
        {
            Eq(e2.AbilityIdsForTest(borrowed.Id).Length, 0, "the Ring permanently has no abilities");
            Eq(e2.State.Cards[doubts.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        });
    });

    // ---- Limited ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ThroughTheBreachRemove() => Windows("Through the Breach", [W.ActionActive], w =>
    {
        var s = Rig(w);
        var breach = Add(s, "oracle-l5r-9992", 0, Zone.Hand);
        var castle = Add(s, "oracle-l5r-2658", 1, Zone.Home); // a Castle Holding with one non-Gold ability
        Armies(s, w);
        var e = MatchEngine.FromTestState(s);
        Check(e.AbilityIdsForTest(castle.Id).Length > 0, "the Castle has an ability before");
        return new(e, "pe-through-the-breach-remove", breach.Id, Use(e, 0, "pe-through-the-breach-remove", breach.Id, castle.Id), e2 =>
        {
            Check(e2.State.PendingChoice is null && e2.AbilityIdsForTest(castle.Id).Length == 0, "its one non-Gold ability is removed");
            Eq(e2.State.Cards[castle.Id].Printed!.GoldProduction, 5, "the Gold production is untouched");
        });
    });

    // Koan's Whisper: a Void Shugenja with the Spell and five Koan cards; the opponent has four Provinces.
    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition> tweak, string? parent = null, int? province = null)
    {
        var card = Add(s, baseId, owner, zone, false, province, parent);
        card.Printed = tweak(CardCatalog.Get(baseId) with { Id = id });
        return card;
    }

    private static void WhisperDestroy() => Windows("Koan's Whisper", [W.ActionActive], w =>
    {
        var s = Rig(w);
        var shugenja = Fx(s, "training-crane-samurai", "w741-void", 0, Zone.Home, d => d with { Force = 2, Chi = 3, PersonalHonor = 2, Keywords = ["Shugenja", "Void"], Abilities = [], Effect = "none", GoldCost = 3 });
        var spell = Add(s, "oracle-l5r-10540", 0, Zone.Attached, parent: shugenja.Id);
        var koans = Enumerable.Range(0, 5).Select(i => Fx(s, "training-item", $"w741-koan-{i}", 0, Zone.Attached, d => d with { Keywords = ["Koan"] }, parent: shugenja.Id)).ToArray();
        Armies(s, w);
        var e = MatchEngine.FromTestState(s);
        return new(e, "w741-whisper-destroy", spell.Id, Use(e, 0, "w741-whisper-destroy", spell.Id, null, null, new SlotSelection("province", ProvinceOwner: 1, Province: 0), Pick("p24-remove", koans.Select(k => k.Id).ToArray())), e2 =>
        {
            Check(e2.State.Players[1].Provinces[0].Destroyed, "the Province is destroyed");
            Check(koans.All(k => e2.State.Cards[k.Id].Zone == Zone.RemovedFromGame), "the five Koan cards are removed from the game");
            Check(e2.State.Cards[spell.Id].Bowed, "the Spell is bowed");
        });
    });

    // ---- Battle ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ControlTheBoardTerrain() => Windows("Control the Board", Battle, w =>
    {
        var s = Rig(w);
        Armies(s, w);
        var terrain = Add(s, "training-before-resolution-boost-terrain", 1, Zone.Battlefield, province: 0);
        var card = Add(s, "oracle-l5r-1486", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        return new(e, "control-the-board-terrain", card.Id, Use(e, 0, "control-the-board-terrain", card.Id, terrain.Id),
            e2 => Check(e2.State.Cards[terrain.Id].Zone != Zone.Battlefield, "the Terrain is destroyed"));
    });

    private static void FlankingAdjacent() => Windows("Flanking Unit", Battle, w =>
    {
        var s = Rig(w);
        s.UnresolvedBattlefields = [0, 1];
        var host = Unit(s, w, "training-crane-samurai", 0);
        var flanking = Add(s, "oracle-l5r-2598", 0, Zone.Attached, province: InBattle(w) ? 0 : null, parent: host.Id);
        var target = Unit(s, w, "training-lion-samurai", 1);
        if (InBattle(w)) Check(MatchEngine.FromTestState(s).AdjacentUnresolvedBattlefields(0).SequenceEqual([1]), "province 1 is the adjacent unresolved battlefield");
        var e = MatchEngine.FromTestState(s);
        return new(e, "flanking-unit-move-adjacent", flanking.Id, Use(e, 0, "flanking-unit-move-adjacent", flanking.Id, target.Id) with { Province = 1 }, e2 =>
            { Eq(e2.State.Cards[target.Id].Zone, Zone.Battlefield); Eq(e2.State.Cards[target.Id].ProvinceIndex, 1, "moved to the adjacent battlefield"); });
    });

    private static void HibanaBoost() => Windows("Isawa Hibana", Everywhere, w =>
    {
        var s = Rig(w);
        var hibana = Unit(s, w, "oracle-l5r-10305", 0);
        var ally = Unit(s, w, "training-crane-samurai", 0);
        if (InBattle(w)) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var hf = e.CardForceForTest(hibana.Id); var af = e.CardForceForTest(ally.Id);
        return new(e, "hibana-boost", hibana.Id, Use(e, 0, "hibana-boost", hibana.Id, ally.Id), e2 =>
        { Eq(e2.CardForceForTest(hibana.Id), hf - 3, "Hibana paid 3F"); Eq(e2.CardForceForTest(ally.Id), af + 3, "the Personality got +3F"); });
    });

    private static void MukudoriStraighten() => Windows("Mukudori's Wisdom (straighten)", Everywhere, w =>
    {
        var s = Rig(w);
        var mine = Add(s, "training-ring-of-air", 0, Zone.Home, bowed: true); Add(s, "training-ring-of-water", 1, Zone.Home);
        var card = Add(s, "oracle-l5r-10728", 0, Zone.Hand);
        Armies(s, w);
        var e = MatchEngine.FromTestState(s);
        return new(e, "wl-sg-mukudori-straighten", card.Id, Use(e, 0, "wl-sg-mukudori-straighten", card.Id, mine.Id), e2 =>
        { Check(!e2.State.Cards[mine.Id].Bowed, "my bowed Ring is straightened"); Eq(e2.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent"); });
    });

    private static void MukudoriBow() => Windows("Mukudori's Wisdom (bow)", Everywhere, w =>
    {
        var s = Rig(w);
        var mine = Add(s, "training-ring-of-air", 0, Zone.Home); var theirs = Add(s, "training-ring-of-water", 1, Zone.Home);
        var card = Add(s, "oracle-l5r-10728", 0, Zone.Hand);
        Armies(s, w);
        var e = MatchEngine.FromTestState(s);
        return new(e, "wl-sg-mukudori-bow", card.Id, Use(e, 0, "wl-sg-mukudori-bow", card.Id, theirs.Id), e2 =>
        { Check(e2.State.Cards[theirs.Id].Bowed && !e2.State.Cards[mine.Id].Bowed, "the enemy Ring is bowed, mine is not"); });
    });

    private static void SerenityBattle() => Windows("Serenity in Air (Battle)", Battle, w =>
    {
        var s = Rig(w);
        var kata = Add(s, "oracle-l5r-9926", 0, Zone.Home);
        var bowed = Unit(s, w, "training-crane-samurai", 0, bowed: true);
        if (InBattle(w)) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var spare = Add(s, "training-focus-strategy", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        return new(e, "serenity-air-straighten", kata.Id, Use(e, 0, "serenity-air-straighten", kata.Id, bowed.Id), e2 =>
        {
            Check(!e2.State.Cards[bowed.Id].Bowed, "straightened");
            Check(e2.State.Players[0].Hand.Contains(spare.Id), "no card is discarded in a battle");
            Eq(e2.State.Cards[kata.Id].Zone, Zone.Home, "the Kata stays in play");
        });
    });

    private static void SerenityOpen() => Windows("Serenity in Air (Open)", Action, w =>
    {
        var s = Rig(w);
        var kata = Add(s, "oracle-l5r-9926", 0, Zone.Home);
        var tired = Unit(s, w, "training-crane-samurai", 0, bowed: true);
        var discard = Add(s, "training-focus-strategy", 0, Zone.Hand);
        if (InBattle(w)) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        return new(e, "serenity-air-straighten-open", kata.Id, Use(e, 0, "serenity-air-straighten-open", kata.Id, null, null, Pick("target", tired.Id), Pick("discard", discard.Id)), e2 =>
        {
            Check(!e2.State.Cards[tired.Id].Bowed, "straightened");
            Eq(e2.State.Cards[discard.Id].Zone, Zone.FateDiscard, "the card was discarded as the cost");
        });
    });

    private static void SinkholeTerrain() => Windows("Sinkhole (Terrain)", Battle, w =>
    {
        var s = Rig(w);
        Add(s, "training-crane-samurai", 0, InBattle(w) ? Zone.Battlefield : Zone.Home, province: InBattle(w) ? 0 : null);
        var enemy = Unit(s, w, "training-lion-samurai", 1);
        var terrain = Add(s, "oracle-l5r-2613", 1, Zone.Battlefield, province: 0);
        var card = Add(s, "oracle-l5r-9986", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        return new(e, "pe-sinkhole-terrain", card.Id, Use(e, 0, "pe-sinkhole-terrain", card.Id, enemy.Id), e2 =>
        {
            Eq(e2.State.Cards[terrain.Id].Zone, Zone.FateDiscard, "the Terrain was destroyed");
            Eq(e2.State.PendingChoice?.Type, "effectChoice", "bow or move home is the acting player's choice");
            Accept(e2, Choose(e2, "bow"));
            Check(e2.State.Cards[enemy.Id].Bowed && e2.State.Cards[enemy.Id].Zone == Zone.Battlefield, "his path is blocked: he is bowed");
        });
    });

    private static void SinkholeRegion() => Windows("Sinkhole (Region)", Battle, w =>
    {
        var s = Rig(w);
        Add(s, "training-crane-samurai", 0, InBattle(w) ? Zone.Battlefield : Zone.Home, province: InBattle(w) ? 0 : null);
        var enemy = Unit(s, w, "training-lion-samurai", 1);
        // The Region sits in the province under attack: the Defender's province 0 (player 1 when I attack, me when I defend).
        var owner = w == W.BattleDefender ? 0 : 1;
        var region = Add(s, "oracle-l5r-6123", owner, Zone.Region, province: 0); s.Players[owner].Provinces[0].RegionId = region.Id;
        var card = Add(s, "oracle-l5r-9986", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        return new(e, "pe-sinkhole-region", card.Id, Use(e, 0, "pe-sinkhole-region", card.Id, enemy.Id), e2 =>
        {
            Eq(e2.State.Cards[region.Id].Zone, Zone.DynastyDiscard, "the Region was destroyed");
            Eq(e2.State.PendingChoice?.Type, "effectChoice", "bow or move home is the acting player's choice");
            Accept(e2, Choose(e2, "home"));
            Eq(e2.State.Cards[enemy.Id].Zone, Zone.Home, "his path is blocked: he moved home");
        });
    });

    private static void StrongDefensesBoost() => Windows("Strong Defenses", [W.BattleDefender], w =>
    {
        var s = Rig(w);
        var defender = Unit(s, w, "training-crane-samurai", 0, bowed: true);
        if (InBattle(w)) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var card = Add(s, "oracle-l5r-9931", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var force = e.CardForceForTest(defender.Id);
        return new(e, "strong-defenses-boost", card.Id, Use(e, 0, "strong-defenses-boost", card.Id, null, defender.Id), e2 =>
        {
            Eq(e2.CardForceForTest(defender.Id), force + 5, "+5F");
            Check(!e2.State.Cards[defender.Id].Bowed, "straightened before the battle resolves");
        }, defender.Id);
    });

    private static void BalashHand() => Windows("The Blade of the Balash (hand)", Battle, w =>
    {
        var s = Rig(w);
        s.Players[0].Clan = "Dragon";
        Unit(s, w, "training-crane-samurai", 0);
        var card = Add(s, "oracle-l5r-9975", 0, Zone.Hand);
        var target = Unit(s, w, "training-lion-samurai", 1); Force(target, 1); // Force 5: within the Melee 5
        var e = MatchEngine.FromTestState(s);
        return new(e, "pf-balash-attack-hand", card.Id, Use(e, 0, "pf-balash-attack-hand", card.Id, target.Id), e2 =>
        {
            Eq(e2.State.Cards[target.Id].Zone, Zone.Dead, "the Melee 5 Attack destroyed its target");
            Eq(e2.State.Cards[card.Id].Zone, Zone.FateDiscard, "discarded from the hand as the cost");
        });
    });

    private static void HonorOfTheLion() => Windows("The Honor of the Lion", Everywhere, w =>
    {
        var s = Rig(w);
        var lion = Unit(s, w, "training-lion-samurai", 0); // Personal Honor 2
        if (InBattle(w)) Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var card = Add(s, "oracle-l5r-8138", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        return new(e, "honor-lion-boost", card.Id, Use(e, 0, "honor-lion-boost", card.Id, null, lion.Id), e2 =>
        {
            Eq(e2.State.Cards[lion.Id].Modifiers.Where(m => m.Stat == "PersonalHonor").Sum(m => m.Amount), 2, "a bonus raising his Personal Honor from 2 to 4");
            Check(e2.State.Cards[lion.Id].GrantedAbilities.Any(g => g.AbilityId == "honor-lion-bow" && g.Duration == GrantDuration.Permanent), "the Battle ability is granted permanently");
        }, lion.Id);
    });

    private static void SincerityOfTheDragon() => Windows("The Sincerity of the Dragon", Everywhere, w =>
    {
        var s = Rig(w);
        s.Players[0].Clan = "Dragon"; // "If you are a Dragon Clan player"
        var dragonId = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly && c.Clan == "Dragon").Id;
        var dragon = Unit(s, w, dragonId, 0);
        if (InBattle(w)) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var card = Add(s, "oracle-l5r-8338", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        return new(e, "sincerity-dragon-name", card.Id, Use(e, 0, "sincerity-dragon-name", card.Id, null, dragon.Id), e2 =>
        {
            Eq(e2.State.PendingChoice?.Type, "effectChoice", "the number is a choice");
            Accept(e2, Choose(e2, "name-2"));
            Eq(e2.State.SincerityWatches.Single().Number, 2, "the number is recorded");
            Eq(e2.State.Cards[card.Id].Zone, Zone.RemovedFromGame, "the card is removed from the game");
        }, dragon.Id);
    });

    // ---- Ring recruitment (Battle, in the after-battle window) -----------------------------------------------------------------------------------------------------

    // I hold the Ring; my army (overwhelmingly strong) fights a lone enemy Personality at province 0, attacking (BattleAttacker) or defending. The recruit is a grant
    // that exists only once the battle resolves, so it is neither offered nor accepted before then, nor in the Action Phase.
    private static void RingRecruit(string ringDef, string ability)
    {
        foreach (var w in Battle)
        {
            var s = Rig(w);
            var ring = Add(s, ringDef, 0, Zone.Hand);
            var mine = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0); Force(mine, 20);
            Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, 0, ability, ring.Id), $"{w}: the recruit is not offered before the battle resolves");
            Reject(e, Use(e, 0, ability, ring.Id));
            Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version)); Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            Eq(e.State.PendingChoice?.Type, "battleRecruit", $"{w}: the won battle opens the after-battle window");
            Check(CanUse(e, 0, ability, ring.Id), $"{w}: the recruit is offered enabled in the window: " + string.Join("; ", Options(e, 0, ability, ring.Id).Select(o => o.Reason)));
            Accept(e, Enabled(e, 0, ability, ring.Id).Command!);
            Eq(e.State.Cards[ring.Id].Zone, Zone.Home, $"{w}: the Ring is put into play from the hand");
            Check(e.State.AdditionalActions.All(g => g.ScopeCardIds?.Contains(ring.Id) != true), $"{w}: the grant is spent");
        }
        foreach (var w in Action)
        {
            var s = Rig(w);
            var ring = Add(s, ringDef, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, 0, ability, ring.Id), $"{w}: not offered in the Action Phase");
            Reject(e, Use(e, 0, ability, ring.Id));
        }
    }

    private static void RingOfFireRecruit() => RingRecruit("oracle-l5r-6335", "ring-of-fire-recruit");
    private static void RingOfEarthRecruit() => RingRecruit("oracle-l5r-6334", "ring-of-earth-recruit");

    // ---- Headbutt (needs a real Combat Segment) ---------------------------------------------------------------------------------------------------------------------

    private static void HeadbuttFresh()
    {
        foreach (var w in Battle)
        {
            var attacker = w == W.BattleAttacker ? 0 : 1;
            var s = MatchEngine.CreateEconomyScenario().State;
            foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
            s.Phase = Phase.BattleSelection; s.ActivePlayerId = attacker; s.PriorityPlayerId = attacker; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var weak = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0).Id);
            var headbutt = Add(s, "oracle-l5r-3010", 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("selectBattle", attacker, e.Version, Province: 0));
            while (e.State.PendingChoice?.Type == "engageReaction") Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            string berserker = "";
            e = Edit(e, st => { berserker = Add(st, "training-berserker", 0, Zone.Battlefield, province: 0).Id; st.PriorityPlayerId = 0; });
            Check(e.NotInPlayWhenCombatSegmentBegan(e.State.Cards[berserker]), $"{w}: the Berserker entered after the Combat Segment began");
            Check(CanUse(e, 0, "p5-headbutt-destroy-fresh", headbutt.Id, berserker), $"{w}: the unbowed way is offered: " + string.Join("; ", Options(e, 0, "p5-headbutt-destroy-fresh", headbutt.Id, berserker).Select(o => o.Reason)));
            Check(!CanUse(e, 0, "p5-headbutt-destroy", headbutt.Id, berserker), $"{w}: the bowing way is not his");
            Accept(e, Use(e, 0, "p5-headbutt-destroy-fresh", headbutt.Id, weak.Id, berserker));
            Check(!e.State.Cards[berserker].Bowed, $"{w}: he was not bowed");
            Check(e.State.Cards[weak.Id].Zone != Zone.Attached, $"{w}: the Follower was destroyed");
        }
        foreach (var w in Action)
        {
            var s = Rig(w);
            var headbutt = Add(s, "oracle-l5r-3010", 0, Zone.Hand);
            var berserker = Add(s, "training-berserker", 0, Zone.Home);
            var foe = Add(s, "training-follower", 1, Zone.Attached, parent: Add(s, "training-lion-samurai", 1, Zone.Home).Id);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, 0, "p5-headbutt-destroy-fresh", headbutt.Id, berserker.Id), $"{w}: not offered in the Action Phase");
            Reject(e, Use(e, 0, "p5-headbutt-destroy-fresh", headbutt.Id, foe.Id, berserker.Id));
        }
    }
}
