using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #830, slice 3: hand-built scenarios for multi-window abilities the generic MultiWindowAuditTests harness could not satisfy (Imperial Favor,
// cards in a discard pile, Rings, Terrain, cards under a Holding, ...). Each test builds the card's real preconditions in all four windows --
// battle-attacker, battle-defender, action-active, action-other -- proves the action is offered and accepted in every window its printed timing allows,
// that its effect happens, and that the same command is refused in every other window.
internal static partial class MultiWindowAudit3Tests
{
    private static readonly string[] AllWindows = ["battle-attacker", "battle-defender", "action-active", "action-other"];

    /// <summary>One window's fixture: the state, the player probed (Me, who holds priority) and the ids the command and the effect check use.</summary>
    private sealed class Fx
    {
        public MatchState S = null!; public int Me; public bool Battle; public string Window = "";
        public string Src = ""; public string? Tgt, Perf; public SlotSelection[]? Sels; public Dictionary<string, string> Ids = new();
        public Func<MatchEngine, MatchEngine>? Prep; // a step taken in the window before the probe (an earlier action this phase)
        public int Foe => 1 - Me;
    }

    // battle-attacker: player 1 attacks and holds priority; battle-defender: player 0 defends; action-active: player 0's turn; action-other: player 1 acts in player 0's turn.
    private static Fx Win(string window)
    {
        var battle = window.StartsWith("battle");
        var me = window is "battle-defender" or "action-active" ? 0 : 1;
        var s = Table(battle ? Phase.Battle : Phase.Action, window == "battle-defender" ? 1 : window == "action-other" ? 0 : me);
        s.PriorityPlayerId = me;
        return new Fx { S = s, Me = me, Battle = battle, Window = window };
    }

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    // A card in play at the window's own location: the current battlefield in a battle, home otherwise.
    private static CardInstance Unit(Fx f, string def, int owner, bool bowed = false) =>
        Add(f.S, def, owner, f.Battle ? Zone.Battlefield : Zone.Home, bowed, f.Battle ? 0 : null);
    private static CardInstance Gear(Fx f, string def, CardInstance host) => Add(f.S, def, host.OwnerId, Zone.Attached, false, host.ProvinceIndex, host.Id);
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "afterActionResolvedReaction" or "unimpeachableNameReaction" or "honorChangeReaction" or "afterEngagingReaction" }; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }
    private static string Def(string ability, string name) =>
        CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && c.Name == name && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();

    private static GameCommand Cmd(MatchEngine e, Fx f, string ability) => Use(e, f.Me, ability, f.Src, f.Tgt, f.Perf, f.Sels ?? []);

    // The command the engine itself offers (retargeted, with its quoted payment), or a failure naming what WAS offered.
    private static GameCommand Offered(MatchEngine e, Fx f, string ability)
    {
        var options = Options(e, f.Me, ability, f.Src, f.Perf);
        var option = options.FirstOrDefault(o => o.Enabled && (f.Tgt is null || o.TargetIds is null || o.TargetIds.Contains(f.Tgt)));
        Check(option is not null, $"{ability} was not offered in {f.Window}: " + (options.Length == 0 ? "not listed" : string.Join("; ", options.Select(o => $"[{o.Enabled}] {o.Reason}"))));
        var quote = (f.Tgt is not null ? option!.TargetGold?.GetValueOrDefault(f.Tgt) : null) ?? option!.Gold;
        var command = option!.Command! with { TargetId = f.Tgt ?? option.Command!.TargetId, PerformerId = f.Perf ?? option.Command.PerformerId };
        if (f.Sels is not null) command = command with { Selections = f.Sels };
        if (command.SourceIds is null && quote?.Payments.FirstOrDefault() is { } pay) command = command with { SourceIds = pay.SourceIds };
        return command;
    }

    /// <summary>Probes one ability in all four windows: offered and accepted in each window its timing allows (then <paramref name="effect"/> checks the result
    /// against the position before), listed-but-disabled or absent and refused in every other.</summary>
    private static void Windows(string ability, string timing, Func<string, Fx> build, Action<MatchEngine, MatchEngine, Fx>? effect = null, string[]? accept = null)
    {
        var allowed = accept ?? MultiWindowAuditTests.Expected(timing); // `accept` narrows the windows for an ability whose own card-specific condition holds in fewer windows than its timing allows
        foreach (var window in AllWindows)
        {
            var f = build(window);
            var e = MatchEngine.FromTestState(f.S);
            if (f.Prep is not null) e = f.Prep(e);
            if (allowed.Contains(window))
            {
                var command = Offered(e, f, ability);
                var before = e.Clone();
                Accept(e, command);
                effect?.Invoke(before, e, f);
            }
            else
            {
                Check(!CanUse(e, f.Me, ability, f.Src, f.Perf), $"{ability} is offered in {window}, outside its {timing} window");
                Reject(e, Cmd(e, f, ability));
            }
        }
    }

    // ---- Akodo Tezuka -------------------------------------------------------------------------------------------------------------------------------------
    private static void TezukaGrant()
    {
        const string tezuka = "oracle-l5r-298";
        Windows("tezuka-grant", "BattleOrOpen", w =>
        {
            var f = Win(w);
            var t = Unit(f, tezuka, f.Me); Unit(f, "training-crane-samurai", f.Foe);
            var bv = Add(f.S, "training-bushido-virtue", f.Me, Zone.FateDiscard); var hand = Add(f.S, "training-weapon", f.Me, Zone.Hand);
            f.Src = t.Id; f.Sels = [Pick("target", bv.Id), Pick("discard", hand.Id)]; f.Ids["bv"] = bv.Id; f.Ids["hand"] = hand.Id;
            return f;
        }, (before, e, f) =>
        {
            var bv = f.Ids["bv"];
            Eq(e.State.Cards[f.Ids["hand"]].Zone, Zone.FateDiscard, "the cost card was discarded");
            Check(e.State.AdditionalActions.Count == 1 && e.State.AdditionalActions[0] is { PlayerId: var p, FromDiscard: true } g && p == f.Me && g.ScopeCardIds!.SequenceEqual([bv]), "an additional action scoped to the chosen virtue");
            // The grant is playable in this window: its Battle action in a battle; its Open action (and Limited for the active player) in the Action phase.
            var offers = e.QueryActions(f.Me).Where(a => a.CardId == bv && a.Type == "useAbility" && a.Enabled).Select(a => a.Command!.AbilityId).OrderBy(x => x).ToArray();
            string[] expected = f.Battle ? ["training-bow-at-battle"] : f.Window == "action-active" ? ["training-bow-any", "training-limited-draw"] : ["training-bow-any"];
            Check(offers.SequenceEqual(expected), $"{f.Window}: granted actions {string.Join(",", offers)}");
            var play = f.Battle ? "training-bow-at-battle" : "training-bow-any";
            var victim = e.State.Cards.Values.First(c => c.OwnerId == f.Foe && c.Zone is Zone.Battlefield or Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality);
            Accept(e, Offer(e, f.Me, play, bv, victim.Id));
            Check(e.State.Cards[victim.Id].Bowed, "the granted action resolved");
            Eq(e.State.Cards[bv].Zone, Zone.RemovedFromGame, "removed from the game after its action");
        });
    }

    // ---- Manipulate Energies --------------------------------------------------------------------------------------------------------------------------------
    private static void ManipulateEnergies()
    {
        foreach (var keyword in new[] { "Cavalry", "Naval", "Tactician" })
            Windows($"manipulate-energies-give-{keyword.ToLowerInvariant()}", "Open", w =>
            {
                var f = Win(w);
                var henshin = Unit(f, "training-henshin-personality", f.Me); var target = Unit(f, "training-lion-samurai", f.Foe);
                f.Src = Add(f.S, Def("manipulate-energies-give-cavalry", "Manipulate Energies"), f.Me, Zone.Hand).Id; f.Perf = henshin.Id; f.Tgt = target.Id;
                return f;
            }, (before, e, f) =>
            {
                Check(!before.HasKeyword(f.Tgt!, keyword) && e.HasKeyword(f.Tgt!, keyword), $"the target was permanently given {keyword}");
                Eq(e.State.Cards[f.Src].Zone, Zone.FateDiscard, "the Strategy was spent");
            });
    }

    // ---- Testimony ----------------------------------------------------------------------------------------------------------------------------------------------
    private static void Testimony()
    {
        foreach (var (ability, dishonor) in new[] { ("testimony-dishonor", true), ("testimony-rehonor", false) })
            Windows(ability, "Limited", w =>
            {
                var f = Win(w);
                var performer = Unit(f, "training-crane-veteran", f.Me); var target = Unit(f, "training-lion-samurai", f.Foe); target.Dishonorable = !dishonor;
                f.Src = Add(f.S, Def("testimony-dishonor", "Testimony"), f.Me, Zone.Hand).Id; f.Perf = performer.Id; f.Tgt = target.Id;
                return f;
            }, (before, e, f) => Eq(e.State.Cards[f.Tgt!].Dishonorable, dishonor, dishonor ? "dishonored" : "rehonored"));
    }

    // ---- The World Disappears -----------------------------------------------------------------------------------------------------------------------------------
    private static void WorldDisappears()
    {
        foreach (var (ability, voidMonk) in new[] { ("world-disappears-void", true), ("world-disappears-bow", false) })
            Windows(ability, "Open", w =>
            {
                var f = Win(w);
                var monk = Kw(Unit(f, "training-crane-samurai", f.Me, bowed: voidMonk), "Monk"); if (voidMonk) Kw(monk, "Void");
                var kiho = Kw(Add(f.S, "training-focus-strategy", f.Me, Zone.FateDiscard), "Kiho");
                f.Src = Add(f.S, Def("world-disappears-void", "The World Disappears"), f.Me, Zone.Hand).Id; f.Perf = monk.Id; f.Tgt = kiho.Id;
                return f;
            }, (before, e, f) =>
            {
                Eq(e.State.Cards[f.Tgt!].Zone, Zone.Hand, "the Kiho Strategy went to the hand");
                Eq(e.State.Cards[f.Perf!].Bowed, !before.State.Cards[f.Perf!].Bowed || voidMonk, voidMonk ? "the Void Monk bowed or not as he was" : "the Monk bowed as the cost");
            });
    }

    // ---- Shiba's Guidance -----------------------------------------------------------------------------------------------------------------------------------------
    private static void ShibasGuidance()
    {
        Windows("shibas-guidance-return", "BattleOrOpen", w =>
        {
            var f = Win(w); f.S.Players[f.Me].Clan = "Phoenix";
            Unit(f, "training-crane-samurai", f.Me);
            var spell = Add(f.S, "oracle-l5r-8473", f.Me, Zone.FateDiscard); // Thunder's Favor, a non-Unique Spell
            f.Src = Add(f.S, "oracle-l5r-6836", f.Me, Zone.Celestial).Id; f.Tgt = spell.Id;
            return f;
        }, (before, e, f) =>
        {
            Eq(e.State.Cards[f.Tgt!].Zone, Zone.Hand, "the Spell is back in the hand");
            Eq(e.State.Cards[f.Src].Zone, Zone.DynastyDiscard, "the Celestial was discarded");
        });
    }

    // ---- Two Stings (from the discard pile) --------------------------------------------------------------------------------------------------------------------
    private static void TwoStings()
    {
        Windows("two-stings-attack-from-discard", "Battle", w =>
        {
            var f = Win(w);
            var mine = Unit(f, "training-crane-samurai", f.Me); var enemy = Unit(f, "training-lion-veteran", f.Foe);
            var follower = Add(f.S, "training-follower", f.Foe, Zone.Attached, false, f.Battle ? 0 : null, enemy.Id);
            f.Src = Add(f.S, Def("two-stings-attack-from-discard", "Two Stings"), f.Me, Zone.FateDiscard).Id; f.Perf = mine.Id; f.Tgt = follower.Id;
            return f;
        }, (before, e, f) =>
        {
            Settle(e);
            Check(e.State.Cards[f.Perf!].Bowed, "the performer bowed as the cost");
            Check(e.State.Cards[f.Tgt!].Zone != Zone.Attached, "the Ranged 4 Attack destroyed the Follower");
            Eq(e.State.Cards[f.Src].Zone, Zone.RemovedFromGame, "removed from the game");
        });
    }

    // ---- Wyrmbone Katana ----------------------------------------------------------------------------------------------------------------------------------------
    private static void WyrmboneMove()
    {
        Windows("pf-wyrmbone-move", "Battle", w =>
        {
            var f = Win(w);
            // The wielder was assigned to the current battlefield earlier this phase and has since been sent home.
            var wielder = Add(f.S, "training-lion-samurai", f.Me, Zone.Home);
            var sword = Add(f.S, "oracle-l5r-9414", f.Me, Zone.Attached, false, null, wielder.Id);
            Add(f.S, "training-crane-samurai", f.Foe, f.Battle ? Zone.Battlefield : Zone.Home, false, f.Battle ? 0 : null);
            f.S.IvoryMovement = new() { Key = $"{f.S.TurnNumber}|{f.S.ActivePlayerId}", Assigned = new() { [wielder.Id] = [0] } };
            f.Src = sword.Id; f.Ids["wielder"] = wielder.Id;
            return f;
        }, (before, e, f) =>
        {
            Check(e.State.Cards[f.Ids["wielder"]] is { Zone: Zone.Battlefield, ProvinceIndex: 0 }, "the wielder moved to the current battlefield");
        });
    }

    // ---- Bayushi Irezu ------------------------------------------------------------------------------------------------------------------------------------------
    private static void IrezuMove()
    {
        Windows("irezu-move", "Battle", w =>
        {
            var f = Win(w);
            var irezu = Add(f.S, Def("irezu-move", "Bayushi Irezu"), f.Me, Zone.Home); // he must be at home
            var scorpion = Unit(f, "training-scorpion-veteran", f.Me); Unit(f, "training-crane-samurai", f.Foe);
            f.Src = irezu.Id; f.Tgt = scorpion.Id;
            return f;
        }, (before, e, f) =>
        {
            Check(e.State.Cards[f.Src] is { Zone: Zone.Battlefield, ProvinceIndex: 0 }, "Irezu moved to the current battlefield");
            Eq(e.State.Cards[f.Tgt!].Zone, Zone.Home, "the other Scorpion Personality was moved home");
        });
    }

    // ---- Celestial Sword of the Phoenix ------------------------------------------------------------------------------------------------------------------------------
    private static void CelestialSwordPhoenix()
    {
        Windows("celestial-sword-phoenix-return", "BattleOrOpen", w =>
        {
            var f = Win(w);
            var wielder = Unit(f, "training-shugenja", f.Me); Unit(f, "training-crane-samurai", f.Foe);
            var sword = Gear(f, Def("celestial-sword-phoenix-return", "Celestial Sword of the Phoenix"), wielder);
            var dead = Add(f.S, "training-shugenja", f.Me, Zone.Dead);
            f.Src = sword.Id; f.Tgt = dead.Id;
            return f;
        }, (before, e, f) =>
        {
            var back = e.State.Cards[f.Tgt!];
            Eq(back.Zone, f.Battle ? Zone.Battlefield : Zone.Home, "in play at the sword's location");
            Eq(back.ProvinceIndex, f.Battle ? 0 : (int?)null, "at the current battlefield only in a battle");
        });
    }

    // ---- Daigotsu Yutaka ------------------------------------------------------------------------------------------------------------------------------------------
    private static void YutakaReturn()
    {
        foreach (var dark in new[] { false, true })
            Windows(dark ? "p5-yutaka-return-dark" : "p5-yutaka-return", "Battle", w =>
            {
                var f = Win(w);
                for (var i = 0; i < 8; i++) Add(f.S, "training-marketplace", f.Me, Zone.Home);
                var yutaka = Add(f.S, Def("p5-yutaka-return", "Daigotsu Yutaka"), f.Me, Zone.Dead);
                var enemy = Unit(f, "training-lion-samurai", f.Foe); // he would be opposed
                f.Src = yutaka.Id;
                if (dark && f.Battle)
                {
                    // a Dark Virtue action taken earlier this phase makes his return 3 Gold cheaper
                    var bower = Kw(Unit(f, "training-battle-bower", f.Me), "Dark Virtue");
                    f.Prep = e =>
                    {
                        Accept(e, Use(e, f.Me, "training-bow-at-battle", bower.Id, enemy.Id));
                        for (var guard = 0; guard < 4 && e.State.PriorityPlayerId != f.Me; guard++) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
                        return e;
                    };
                }
                return f;
            }, (before, e, f) =>
            {
                Settle(e);
                Check(e.State.Cards[f.Src] is { Zone: Zone.Battlefield, ProvinceIndex: 0 }, "Yutaka is back at the current battlefield");
                Eq(e.State.Players[f.Me].Honor, before.State.Players[f.Me].Honor - 1, "his entry cost 1 Honor");
            }, dark ? ["battle-attacker", "battle-defender"] : null);
    }

    // ---- Hiruma Tensin ------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx TensinFx(string window, bool recon)
    {
        var f = Win(window);
        var tensin = Unit(f, Def("hiruma-sniper-attack", "Hiruma Tensin"), f.Me);
        var follower = Gear(f, "training-follower", tensin);
        var target = Unit(f, "training-lion-samurai", f.Foe);
        if (recon && f.Battle) f.S.Players[1 - f.S.ActivePlayerId].Provinces[0].Reconnaissance[f.Me] = true;
        f.Src = tensin.Id; f.Tgt = target.Id; f.Ids["follower"] = follower.Id;
        return f;
    }

    private static void TensinAttacks()
    {
        Windows("hiruma-sniper-attack", "Battle", w => TensinFx(w, recon: true), (before, e, f) =>
        {
            Settle(e);
            Check(e.State.Cards[f.Tgt!].Zone != Zone.Battlefield, "the Ranged 4 Attack destroyed the Force 4 target");
            Check(!e.State.Cards[f.Src].Bowed, "no cost");
        });
        Windows("tensin-follower-attack", "Battle", w => { var f = TensinFx(w, recon: false); f.Perf = f.Ids["follower"]; return f; }, (before, e, f) =>
        {
            Settle(e);
            Check(e.State.Cards[f.Perf!].Bowed && !e.State.Cards[f.Src].Bowed, "the performing Follower bowed as the cost");
            Check(e.State.Cards[f.Tgt!].Zone != Zone.Battlefield, "the Ranged 4 Attack destroyed the Force 4 target");
        });
        // Without Reconnaissance the first Attack is refused in both battle windows.
        foreach (var w in new[] { "battle-attacker", "battle-defender" })
        {
            var f = TensinFx(w, recon: false); var e = MatchEngine.FromTestState(f.S);
            Check(!CanUse(e, f.Me, "hiruma-sniper-attack", f.Src), $"{w}: no Reconnaissance, no Attack");
            Reject(e, Cmd(e, f, "hiruma-sniper-attack"));
        }
    }

    // ---- Kitsuki Jakuei -----------------------------------------------------------------------------------------------------------------------------------------
    private static void JakueiBowFavor()
    {
        Windows("jakuei-bow-favor", "Battle", w =>
        {
            var f = Win(w); f.S.FavorPlayerId = f.Me;
            var jakuei = Unit(f, Def("jakuei-bow-favor", "Kitsuki Jakuei"), f.Me); var target = Unit(f, "training-lion-samurai", f.Foe);
            f.Src = jakuei.Id; f.Tgt = target.Id;
            return f;
        }, (before, e, f) =>
        {
            Check(e.State.Cards[f.Tgt!].Bowed && e.State.Cards[f.Tgt!].Zone == Zone.Home, "the target was bowed and moved home");
            Eq(e.State.FavorPlayerId, null, "the Imperial Favor was discarded");
        });
        // Without the Favor the action is refused in the battle windows too.
        foreach (var w in new[] { "battle-attacker", "battle-defender" })
        {
            var f = Win(w); var jakuei = Unit(f, Def("jakuei-bow-favor", "Kitsuki Jakuei"), f.Me); var target = Unit(f, "training-lion-samurai", f.Foe);
            f.Src = jakuei.Id; f.Tgt = target.Id; var e = MatchEngine.FromTestState(f.S);
            Check(!CanUse(e, f.Me, "jakuei-bow-favor", f.Src), $"{w}: not offered without the Favor");
            Reject(e, Cmd(e, f, "jakuei-bow-favor"));
        }
    }

    // ---- Rings put into play after a granted condition --------------------------------------------------------------------------------------------------------
    private static Fx RingFx(string window, string def, bool grant)
    {
        var f = Win(window); f.S.BattleNumber = 1;
        Unit(f, "training-crane-samurai", f.Me); Unit(f, "training-lion-samurai", f.Foe);
        var ring = Add(f.S, def, f.Me, Zone.Hand);
        if (grant) f.S.AdditionalActions.Add(new AdditionalActionGrant(f.Me, [ring.Id], Battle: 1));
        f.Src = ring.Id;
        return f;
    }

    private static void RingsRecruit()
    {
        var air = Def("ring-of-air-recruit", "Ring of Air"); var voidRing = Def("wl-false-ring-of-void-recruit", "False Ring of the Void");
        Windows("ring-of-air-recruit", "Battle", w => RingFx(w, air, grant: true), (before, e, f) => Eq(e.State.Cards[f.Src].Zone, Zone.Home, "the Ring is in play"));
        Windows("wl-false-ring-of-void-recruit", "Battle", w => RingFx(w, voidRing, grant: true), (before, e, f) =>
        {
            Eq(e.State.Cards[f.Src].Zone, Zone.Home, "the Ring is in play");
            Eq(e.State.Players[f.Me].Honor, before.State.Players[f.Me].Honor - 2, "2 Honor lost");
        });
        // Without the granted condition neither Ring may be put into play, in any battle window.
        foreach (var (ability, def) in new[] { ("ring-of-air-recruit", air), ("wl-false-ring-of-void-recruit", voidRing) })
            foreach (var w in new[] { "battle-attacker", "battle-defender" })
            {
                var f = RingFx(w, def, grant: false); var e = MatchEngine.FromTestState(f.S);
                Check(!CanUse(e, f.Me, ability, f.Src), $"{ability} offered in {w} without its grant");
                Reject(e, Cmd(e, f, ability));
            }
        // A False Ring of Water is a standing condition: Province Strength 11 or more at the current battlefield.
        Windows("false-ring-of-water-recruit", "Battle", w =>
        {
            var f = RingFx(w, "training-false-ring-of-water", grant: false);
            if (f.Battle) f.S.Players[1 - f.S.ActivePlayerId].Provinces.Single(p => p.Index == 0).Strength = 11;
            return f;
        }, (before, e, f) =>
        {
            Eq(e.State.Cards[f.Src].Zone, Zone.Home, "the Ring is in play");
            Eq(e.State.Players[f.Me].Honor, before.State.Players[f.Me].Honor - 2, "2 Honor lost");
        });
        foreach (var w in new[] { "battle-attacker", "battle-defender" })
        {
            var f = RingFx(w, "training-false-ring-of-water", grant: false);
            f.S.Players[1 - f.S.ActivePlayerId].Provinces.Single(p => p.Index == 0).Strength = 10;
            var e = MatchEngine.FromTestState(f.S);
            Check(!CanUse(e, f.Me, "false-ring-of-water-recruit", f.Src), $"{w}: Province Strength 10 is not enough");
            Reject(e, Cmd(e, f, "false-ring-of-water-recruit"));
        }
    }

    // ---- Gozu -----------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void GozuDestroy()
    {
        const string floodedPass = "oracle-l5r-2613", extendedManeuvers = "oracle-l5r-2419";
        foreach (var discard in new[] { false, true })
            Windows(discard ? "sp-gozu-destroy-discard" : "sp-gozu-destroy", "Battle", w =>
            {
                var f = Win(w);
                var gozu = Unit(f, Def("sp-gozu-destroy", "Gozu"), f.Me); Unit(f, "training-lion-samurai", f.Foe);
                var terrain = Add(f.S, floodedPass, f.Foe, f.Battle ? Zone.Battlefield : Zone.Home, false, f.Battle ? 0 : null);
                Add(f.S, "training-focus-strategy", f.Me, Zone.Hand);
                foreach (var id in f.S.Players[f.Me].FateDeck.ToArray()) f.S.Cards.Remove(id);
                f.S.Players[f.Me].FateDeck.Clear();
                var found = Add(f.S, extendedManeuvers, f.Me, Zone.FateDeck); found.FaceUp = false; f.S.Players[f.Me].FateDeck.Add(found.Id);
                f.Src = gozu.Id; f.Tgt = terrain.Id;
                return f;
            }, (before, e, f) =>
            {
                Eq(e.State.Cards[f.Tgt!].Zone, Zone.FateDiscard, "the Terrain was destroyed");
                if (discard) Eq(e.State.PendingChoice?.Type, "searchSelect", "discarding a card opened the Fate-deck search for a Terrain");
            });
    }

    // ---- Moto Hailung ---------------------------------------------------------------------------------------------------------------------------------------------
    private static void HailungAttach()
    {
        Fx Hailung(string w, string clan)
        {
            var f = Win(w); f.S.Players[f.Me].Clan = clan;
            var hailung = Unit(f, Def("hailung-attach", "Moto Hailung"), f.Me); Unit(f, "training-lion-samurai", f.Foe);
            var follower = Add(f.S, "training-follower", f.Me, Zone.Hand);
            f.Src = hailung.Id; f.Tgt = follower.Id;
            return f;
        }
        void Attached(MatchEngine before, MatchEngine e, Fx f) => Eq(e.State.Cards[f.Tgt!].AttachedTo, f.Src, "the Follower is attached to Hailung");
        // A non-Crab attacker pays full price; defending, or as a Crab Clan player, he pays 4 less. Each price is offered in its own windows only.
        Windows("hailung-attach", "Battle", w => Hailung(w, "Lion"), Attached, ["battle-attacker"]);
        Windows("hailung-attach-discount", "Battle", w => Hailung(w, "Lion"), Attached, ["battle-defender"]);
        Windows("hailung-attach", "Battle", w => Hailung(w, "Crab"), null, []);
        Windows("hailung-attach-discount", "Battle", w => Hailung(w, "Crab"), Attached, ["battle-attacker", "battle-defender"]);
    }

    // ---- P'an Ku ------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void PanKuTake()
    {
        Windows("w827-panku-take", "Limited", w =>
        {
            var f = Win(w);
            var panKu = Unit(f, Def("w827-panku-take", "P'an Ku"), f.Me); var target = Unit(f, "training-lion-samurai", f.Foe);
            for (var i = 0; i <= CardCatalog.Get("training-lion-samurai").PersonalHonor; i++) target.Tokens.Add(new($"{target.Id}-t{i}", "Madness", "test"));
            f.Src = panKu.Id; f.Tgt = target.Id;
            return f;
        }, (before, e, f) =>
        {
            Eq(e.State.Cards[f.Tgt!].OwnerId, f.Me, "control of the Personality was taken");
            Eq(e.State.Cards[f.Tgt!].Tokens.Count(t => t.Type == "Madness"), 0, "its Madness tokens were removed");
            Check(e.State.Cards[f.Src].Bowed, "P'an Ku bowed");
        });
    }

    // ---- Seek the Stain -------------------------------------------------------------------------------------------------------------------------------------------
    private static void SeekTheStain()
    {
        const string seek = "oracle-l5r-6559";
        Windows("p5-seek-stain-equip-limited", "Limited", w =>
        {
            var f = Win(w);
            var shugenja = Add(f.S, "training-seeds5-void-shugenja", f.Me, Zone.Home); // the Limited Equip is taken from home
            Unit(f, "training-lion-samurai", f.Foe);
            f.Src = Add(f.S, seek, f.Me, Zone.FateDiscard).Id; f.Perf = shugenja.Id;
            return f;
        }, (before, e, f) => { Settle(e); Eq(e.State.Cards[f.Src].AttachedTo, f.Perf, "attached from the discard pile to the Void Shugenja"); });
        Windows("p5-seek-stain-equip-battle", "Battle", w =>
        {
            var f = Win(w);
            var shugenja = Unit(f, "training-seeds5-void-shugenja", f.Me); Unit(f, "training-lion-samurai", f.Foe); // opposed at the battle
            f.Src = Add(f.S, seek, f.Me, Zone.FateDiscard).Id; f.Perf = shugenja.Id;
            return f;
        }, (before, e, f) => { Settle(e); Eq(e.State.Cards[f.Src].AttachedTo, f.Perf, "attached from the discard pile to the opposed Void Shugenja"); });
    }

    // ---- Stolen Merchandise ----------------------------------------------------------------------------------------------------------------------------------------
    private static void StolenMerchandise()
    {
        const string market = "oracle-l5r-10214"; // a Market (base Gold Cost 3) with a base ability
        Windows("p5-stolen-ability", "Open", w =>
        {
            var f = Win(w); f.S.Players[f.Me].Clan = "Crane";
            var stolen = Add(f.S, Def("p5-stolen-ability", "Stolen Merchandise"), f.Me, Zone.Home);
            var theirs = Add(f.S, market, f.Foe, Zone.Home);
            f.Src = stolen.Id; f.Tgt = theirs.Id;
            return f;
        }, (before, e, f) => Check(e.AbilityIdsForTest(f.Src).Contains("market-draw-crane"), "it copied the Market's base ability"));
    }

    // ---- The New Foundries ----------------------------------------------------------------------------------------------------------------------------------------
    private static Fx FoundriesFx(string window, string spellDef, bool shugenjaBowed = false)
    {
        var f = Win(window);
        f.S.Cards[f.S.Players[f.Me].StrongholdId].DefinitionId = "oracle-l5r-9947";
        var spell = Add(f.S, spellDef, f.Me, Zone.Underneath);
        var shugenja = Unit(f, "training-shugenja", f.Me, bowed: shugenjaBowed); var enemy = Unit(f, "training-lion-samurai", f.Foe);
        f.Src = f.S.Players[f.Me].StrongholdId; f.Ids["spell"] = spell.Id; f.Ids["shugenja"] = shugenja.Id; f.Ids["enemy"] = enemy.Id;
        return f;
    }

    private static void FoundriesBattleActions()
    {
        const string airSpell = "oracle-l5r-10131", earthSpell = "oracle-l5r-6141", voidSpell = "oracle-l5r-1489";
        Windows("p5-foundries-straighten", "Battle", w =>
        {
            var f = FoundriesFx(w, airSpell, shugenjaBowed: true);
            f.Sels = [Pick("spell", f.Ids["spell"]), Pick("shugenja", f.Ids["shugenja"])];
            return f;
        }, (before, e, f) =>
        {
            Check(!e.State.Cards[f.Ids["shugenja"]].Bowed, "the Shugenja at the battle was straightened");
            Eq(e.State.Cards[f.Ids["spell"]].Zone, Zone.FateDiscard, "the Air Spell was discarded from under the Stronghold");
        });
        Windows("p5-foundries-attack", "Battle", w => { var f = FoundriesFx(w, earthSpell); f.Tgt = f.Ids["enemy"]; return f; }, (before, e, f) =>
        {
            Settle(e);
            Eq(e.State.Cards[f.Ids["spell"]].Zone, Zone.FateDiscard, "the Earth Spell was discarded from under the Stronghold");
            Eq(e.State.Cards[f.Ids["enemy"]].Zone, Zone.Dead, "the Ranged 4 Attack destroyed the 4 Force enemy");
        });
        Windows("p5-foundries-draw", "Battle", w => FoundriesFx(w, voidSpell), (before, e, f) =>
        {
            Eq(e.State.Cards[f.Ids["spell"]].Zone, Zone.FateDiscard, "the Void Spell was discarded from under the Stronghold");
            Eq(e.State.Players[f.Me].Hand.Count, before.State.Players[f.Me].Hand.Count + 1, "a card was drawn");
        });
    }

    // ---- Training holding maker (a fixture: an Open action that needs the current battlefield) -------------------------------------------------------------
    private static void TrainingTerrainOpenIsNeverLegal()
    {
        // "Open" is an Action-phase window and a Terrain is created at the current battlefield, which only exists in a battle: no window can satisfy both,
        // so the fixture ability must be refused everywhere (it exists to prove exactly that).
        foreach (var w in AllWindows)
        {
            var f = Win(w); f.Src = Unit(f, "training-holding-maker", f.Me).Id; Unit(f, "training-lion-samurai", f.Foe);
            var e = MatchEngine.FromTestState(f.S);
            Check(!CanUse(e, f.Me, "training-create-terrain-open", f.Src), $"training-create-terrain-open was offered in {w}");
            Reject(e, Cmd(e, f, "training-create-terrain-open"));
        }
    }

    // ---- The Heir's Wrath --------------------------------------------------------------------------------------------------------------------------------------------
    private static void HeirsWrath()
    {
        Fx Wrath(string w, bool favor)
        {
            var f = Win(w); f.S.Players[f.Me].Honor = 10;
            f.Src = Add(f.S, Def("heirs-wrath-favor", "The Heir's Wrath"), f.Me, Zone.Hand).Id;
            var target = Unit(f, "training-shugenja", f.Foe);
            if (favor) f.S.FavorPlayerId = f.Me;
            else { var big = Unit(f, "training-lion-samurai", f.Me); Force(big, 1); f.Perf = big.Id; } // 5 Force: may be bowed to pay
            f.Tgt = target.Id;
            return f;
        }
        Windows("heirs-wrath-favor", "Limited", w => Wrath(w, favor: true), (before, e, f) =>
        {
            Settle(e);
            Eq(e.State.Cards[f.Tgt!].Zone, Zone.Dead, "the target was destroyed");
            Eq(e.State.Players[f.Me].Honor, 8, "2 Honor lost");
            Eq(e.State.FavorPlayerId, null, "the Imperial Favor was discarded");
        });
        Windows("heirs-wrath-bow", "Limited", w => Wrath(w, favor: false), (before, e, f) =>
        {
            Settle(e);
            Eq(e.State.Cards[f.Tgt!].Zone, Zone.Dead, "the target was destroyed");
            Eq(e.State.Players[f.Me].Honor, 8, "2 Honor lost");
            Check(e.State.Cards[f.Perf!].Bowed, "the 5 Force Personality was bowed");
        });
    }

    public static void Run(TestRunner runner) => runner.Run("multi-window-audit-3", [
        ("Akodo Tezuka (tezuka-grant): offered and accepted in all four windows, grants the Bushido Virtue's action of that phase and removes it from the game afterwards", TezukaGrant),
        ("Manipulate Energies (give Cavalry / Naval / Tactician): an Open Kiho action offered and accepted in both Action-phase windows, refused in both battles; the keyword is permanently given", ManipulateEnergies),
        ("Testimony (dishonor / rehonor): a Limited Political action offered only to the active player in the Action phase, refused for the other player and in battle; the target is dishonored / rehonored", Testimony),
        ("The World Disappears (Void Monk meditates / Monk bows): an Open action offered in both Action-phase windows, refused in battle; the Kiho Strategy returns to the hand", WorldDisappears),
        ("Shiba's Guidance (return a Spell or Kiho): a Phoenix player discards the Celestial in all four windows; the Spell returns to the hand", ShibasGuidance),
        ("Two Stings (attack from the discard pile): a Battle action for attacker and defender, refused in the Action phase; the Ranged Attack resolves and the card is removed from the game", TwoStings),
        ("Bayushi Irezu (irezu-move): a Home Battle action for attacker and defender; Irezu moves to the battlefield and the other Scorpion goes home; refused in the Action phase", IrezuMove),
        ("Celestial Sword of the Phoenix (return): Battle/Open in all four windows; the honorably dead Phoenix Personality enters at the sword's location", CelestialSwordPhoenix),
        ("Daigotsu Yutaka (return from the dead, full price and 3 less after a Dark Virtue action): a Battle action for attacker and defender, refused in the Action phase", YutakaReturn),
        ("Hiruma Tensin (Reconnaissance Attack and Follower Attack): Battle actions for attacker and defender, refused in the Action phase and without Reconnaissance", TensinAttacks),
        ("Kitsuki Jakuei (bow and send home by discarding the Imperial Favor): a Battle action for attacker and defender that needs the Favor, refused in the Action phase", JakueiBowFavor),
        ("Ring of Air / False Ring of the Void / Training False Ring of Water (put into play): Battle actions for attacker and defender when their condition holds, refused without it and in the Action phase", RingsRecruit),
        ("Gozu (destroy a Terrain, with and without discarding for the Fate-deck search): Battle actions for attacker and defender, refused in the Action phase", GozuDestroy),
        ("Moto Hailung (attach a Follower from the hand): full price for a non-Crab attacker, 4 less defending or as a Crab; each offered only in its own battle windows, never in the Action phase", HailungAttach),
        ("P'an Ku (take control of a Madness-ridden Personality): Limited, offered to the active player only in the Action phase", PanKuTake),
        ("Seek the Stain (equip from the discard pile): Limited from home for the active player, Battle for an opposed Void Shugenja in both battle windows", SeekTheStain),
        ("Stolen Merchandise (copy another player's Holding ability): an Open action in both Action-phase windows, refused in battle", StolenMerchandise),
        ("The New Foundries (straighten / attack / draw by discarding a Spell from under it): Battle actions for attacker and defender, refused in the Action phase", FoundriesBattleActions),
        ("Training holding maker (training-create-terrain-open): the Open action that needs a current battlefield is refused in every window", TrainingTerrainOpenIsNeverLegal),
        ("The Heir's Wrath (Favor way and bow way): a Limited Political action for the active player only, refused for the other player and in battle", HeirsWrath),
        ("Wyrmbone Katana (move back to the battlefield): a Battle action for attacker and defender once its wielder was assigned there and has left; refused in the Action phase", WyrmboneMove),
    ]);
}
