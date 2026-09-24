using static RegressionAssertions;
using System.Security.Cryptography;
using System.Text.Json;
using L5R.Core;

// The delayed "move home" battle effect (with its dependent second move) and the cards it and the delayed effects of issue #354 complete (issue #364;
// docs/decisions/2026-09-19-delayed-move-home-and-wave.md): The Wave Persists, Holding Cells, Power of the Night, Quick Recovery, Reckless Rush,
// Harsh Taskmaster, Steady Resolve and Akodo Dairuko. Every position is played through the commands the engine offers.
internal static class DelayedMoveHomeWaveTests
{
    private const string WavePersists = "oracle-l5r-9890", HoldingCells = "oracle-l5r-10733", PowerOfTheNight = "oracle-l5r-9920", QuickRecovery = "oracle-l5r-6132",
        RecklessRush = "oracle-l5r-6211", HarshTaskmaster = "oracle-l5r-10151", SteadyResolve = "oracle-l5r-9930", Dairuko = "oracle-l5r-200";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: the eight cards carry the printed cost box, Focus, keywords, abilities and text of their scans, ship their art, and record the source printing and its sha256", PublishedShape),
            ("The Wave Persists: after engaging, before the battle resolves, the target goes home and only then does the Shugenja move to the battlefield -- the swap decides the battle", WavePersistsSwap),
            ("The Wave Persists: only while the Shugenja is not at the current battlefield, only in the Engage window, and only your own Personalities at the battlefield are targets", WavePersistsOffers),
            ("Delayed move home: 'if he moved' -- a target already home, gone from play or a Shugenja gone from play moves nobody, and the effect lapses with its battle", DelayedMoveHomeEdges),
            ("The Wave Persists: 'While this Shugenja is Water, he has +2F' -- the bonus needs the Water keyword and reaches the wielder at home too", WaterStaticForce),
            ("Holding Cells: Kolat Battle for 4 Gold -- the acting player loses 1 Honor at once, the target (either side's) is bowed only when the battle is about to resolve", HoldingCellsBow),
            ("Power of the Night: an unbowed Ninja performs, bows unless Shadowlands; the target gets -3C before the battle resolves, and a Chi of 0 destroys him", PowerOfTheNightChi),
            ("Quick Recovery: the target is a bowed card in the performer's unit (him or his attachment), straightened before the battle resolves so it counts", QuickRecoveryStraighten),
            ("Reckless Rush: the performer bows, the target (an enemy card without attachments) bows, and is destroyed if it is an attachment or costs 0; the performer straightens before resolution", RecklessRushBow),
            ("Harsh Taskmaster: a Follower from your hand or your own discard pile attaches free to a Commander, is destroyed before the battle resolves, and grants an additional Battle action", HarshTaskmasterAttach),
            ("Steady Resolve and Akodo Dairuko (published): the printed Reactions join the before-resolution set, Dairuko's usable while bowed, and her Melee 8 Attack destroys what it reaches", SteadyResolveAndDairuko),
        ];
        runner.Run("delayed-move-home-wave", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok, string? message = null) { if (!ok) throw new Exception(message ?? "Assertion failed"); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");
    private static int _added;
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"dm-{s.Cards.Count}-{++_added}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        if (zone == Zone.FateDiscard) c.FaceUp = true;
        return c;
    }
    // The economy scenario with published Strongholds and empty hands, at the Battle Selection moment: player 0 attacks, the battlefield is province 0.
    private static MatchState Selection(params int[] battlefields)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218"; foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null;
        s.UnresolvedBattlefields = (battlefields.Length == 0 ? [0] : battlefields).ToList();
        return s;
    }
    // A battle already in its Combat Segment (the Defender to act).
    private static MatchState Combat()
    {
        var s = Selection(); s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.PriorityPlayerId = 1;
        return s;
    }
    private static GameCommand Cmd(MatchEngine e, string type, int? player = null) => new(type, player ?? e.State.PriorityPlayerId, e.Version);
    private static void Pass(MatchEngine e, int? player = null) => Accept(e, Cmd(e, "pass", player));
    private static void Decline(MatchEngine e, int? player = null) => Accept(e, Cmd(e, "declineReaction", player));
    private static ActionOption[] Offered(MatchEngine e, int player, string type) => e.QueryActions(player).Where(o => o.Type == type).ToArray();
    private static ActionOption? Offer(MatchEngine e, int player, string ability, string? card = null) =>
        e.QueryActions(player).FirstOrDefault(o => o.Type == "useAbility" && o.Command?.AbilityId == ability && (card is null || o.CardId == card));
    private static string Why(MatchEngine e, int player, ActionOption? offer) => offer is null
        ? "not offered: " + string.Join("; ", e.QueryActions(player).Where(o => o.Type == "useAbility").Select(o => $"{o.Label}[{o.Enabled}] {o.Reason}")) : offer.Reason ?? "";
    private static void Take(MatchEngine e, int player, string type, Func<ActionOption, bool>? pick = null)
    {
        var option = e.QueryActions(player).FirstOrDefault(o => o.Type == type && o.Enabled && o.Command is not null && (pick?.Invoke(o) ?? true));
        Check(option is not null, $"{type} was not offered to player {player}: " + string.Join("; ", e.QueryActions(player).Select(o => $"{o.Type}:{o.Label}[{o.Enabled}] {o.Reason}")));
        Accept(e, option!.Command!);
    }
    private static void Target(MatchEngine e, int player, string ability, string targetId, string? card = null, string? performer = null)
    {
        var option = e.QueryActions(player).FirstOrDefault(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == ability && (card is null || o.CardId == card)
            && (performer is null || o.Command!.PerformerId == performer) && (o.TargetIds?.Contains(targetId) ?? false));
        Check(option is not null, $"{ability} against {targetId} was not offered to player {player}: " + string.Join("; ", e.QueryActions(player).Where(o => o.Type == "useAbility").Select(o => $"{o.Label}[{o.Enabled}] {o.Reason}")));
        // The command the engine offers for THIS target and performer carries its own suggested payment; re-derive it for the chosen target.
        var offered = e.QueryActions(player).First(o => o.Type == "useAbility" && o.Command?.AbilityId == ability && (card is null || o.CardId == card) && (performer is null || o.Command!.PerformerId == performer));
        var command = offered.Command! with { TargetId = targetId };
        if (offered.TargetGold is { } byTarget && byTarget.TryGetValue(targetId, out var quote)) command = command with { SourceIds = quote.Payments.FirstOrDefault()?.SourceIds };
        Accept(e, command);
    }
    private static bool Fought(MatchEngine e, string message) => e.GetView(0).Events.Any(ev => ev.Type == "battleResolved" && ev.Message.Contains(message));
    private static int Force(MatchEngine e, CardInstance c)
    {
        var view = e.GetView(0);
        var card = view.Players.SelectMany(p => p.Home.Concat(p.Army)).Single(x => x.Id == c.Id);
        return card.Force;
    }
    private static void CloseWindows(MatchEngine e) { while (e.State.PendingChoice is not null && e.State.PendingChoice.Type is "engageReaction" or "beforeActionReaction" or "attackReaction") Decline(e); }
    // Both players pass in the Combat Segment (whoever holds priority first).
    private static void BothPass(MatchEngine e) { Pass(e); Pass(e); }

    // ---- published shape ---------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        var root = Support337.RepoRoot();
        // (id, name, type, GoldCost (the cost box), Focus, keywords, abilities, effect)
        var expected = new (string Id, string Name, CardType Type, int Cost, int Focus, string Keywords, string Abilities, string Effect)[]
        {
            (SteadyResolve, "Steady Resolve", CardType.Strategy, 0, 2, "", "steady-resolve-boost", "none"),
            (HoldingCells, "Holding Cells", CardType.Strategy, 0, 1, "", "holding-cells-bow", "none"),
            (PowerOfTheNight, "Power of the Night", CardType.Strategy, 0, 2, "Ninja", "power-of-the-night-chi", "none"),
            (QuickRecovery, "Quick Recovery", CardType.Strategy, 0, 2, "", "quick-recovery-straighten", "none"),
            (RecklessRush, "Reckless Rush", CardType.Strategy, 0, 3, "", "reckless-rush-bow", "none"),
            (HarshTaskmaster, "Harsh Taskmaster", CardType.Strategy, 0, 1, "", "harsh-taskmaster-attach", "none"),
            (WavePersists, "The Wave Persists", CardType.Spell, 2, 1, "Water", "wave-persists-move", "wave-persists-force"),
            (Dairuko, "Akodo Dairuko", CardType.Personality, 10, 0, "Clan Champion,Lion Clan,Loyal,Paragon,Samurai,Tactician,Unique", "dairuko-rally,dairuko-melee", "none"),
        };
        Eq(PublishedContent.StrategyWave27.Cards.Length, 6); Eq(PublishedContent.SpellWave9.Cards.Length, 1); Eq(PublishedContent.PersonalityWave38.Cards.Length, 1);
        foreach (var x in expected)
        {
            var c = CardCatalog.Get(x.Id);
            Eq(c.Name, x.Name); Eq(c.Type, x.Type); Eq(c.GoldCost, x.Cost, x.Name + " cost"); Eq(c.Focus, x.Focus, x.Name + " focus"); Check(!c.ScenarioOnly);
            Eq(string.Join(",", c.Keywords!.OrderBy(k => k, StringComparer.Ordinal)), x.Keywords, x.Name + " keywords");
            Eq(string.Join(",", c.Abilities!), x.Abilities, x.Name + " abilities"); Eq(c.Effect, x.Effect, x.Name + " effect");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", x.Id + ".jpg")), x.Name + " art is shipped");
        }
        var dairuko = CardCatalog.Get(Dairuko);
        Eq(dairuko.Force, 6); Eq(dairuko.Chi, 5); Eq(dairuko.PersonalHonor, 5); Eq(dairuko.HonorRequirement, (int?)10); Eq(dairuko.Clan, "Lion");
        Check(dairuko.Text.StartsWith("Reaction: Even if Dairuko is bowed, before the resolution of a battle at her battlefield") && dairuko.Text.EndsWith("Battle: Melee 8 Attack."), "the Emperor Edition scan's own words");
        Eq(CardCatalog.Get(HoldingCells).Text, "Kolat Battle, [4]: Target a Personality. Before this battle resolves, bow him. Lose 1 Honor.");
        // The wave sources record the printing and the art's sha256 (the WebP working copy the receipt names), and the receipt agrees.
        foreach (var (profile, ids) in new[] { ("strategy-wave-27", new[] { SteadyResolve, HoldingCells, PowerOfTheNight, QuickRecovery, RecklessRush, HarshTaskmaster }), ("spell-wave-9", new[] { WavePersists }), ("personality-wave-38", new[] { Dairuko }) })
        {
            var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", profile + "-sources.json"))).RootElement.EnumerateArray().ToArray();
            Eq(sources.Length, ids.Length);
            foreach (var id in ids)
            {
                var source = sources.Single(s => s.GetProperty("card_id").GetString() == id);
                Check(source.GetProperty("source_url").GetString()!.Contains("oracleofthevoid.com"), id + " source url");
                var receiptDir = Path.Combine(root, "assets/reference/oracle");
                var receipt = Directory.EnumerateFiles(receiptDir, id + "-printing-*-image-1.json", SearchOption.AllDirectories)
                    .Select(f => JsonDocument.Parse(File.ReadAllText(f)).RootElement).Single(r => r.GetProperty("working_copy").GetProperty("path").GetString() == source.GetProperty("image_path").GetString());
                Eq(source.GetProperty("image_sha256").GetString(), receipt.GetProperty("working_copy").GetProperty("sha256").GetString(), id + " art sha256");
            }
        }
    }

    // ---- The Wave Persists ---------------------------------------------------------------------------------------------
    private sealed record WaveRig(MatchEngine E, CardInstance A, CardInstance S, CardInstance Spell, CardInstance Y);

    // Attacker (player 0): A (Scout samurai, Force 2) at the battlefield; a Water Shugenja S (Force 2, +2F from the Spell) at home with The Wave Persists attached.
    // Defender: Y (Crane samurai, Force 3). Baseline: A loses 2 v 3; with the swap S wins 4 v 3.
    private static WaveRig WaveTable(Action<MatchState, CardInstance, CardInstance, CardInstance>? arrange = null)
    {
        var s = Selection();
        var a = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
        var shugenja = Add(s, "training-water-shugenja", 0, Zone.Home);
        var spell = Add(s, WavePersists, 0, Zone.Attached, parent: shugenja.Id);
        var y = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        arrange?.Invoke(s, a, shugenja, y);
        return new(MatchEngine.FromTestState(s), a, shugenja, spell, y);
    }
    private static void EngageThenUse(MatchEngine e, WaveRig r)
    {
        Take(e, 0, "selectBattle");
        Eq(e.State.PendingChoice?.Type, "engageReaction");
        while (e.State.PriorityPlayerId != 0) Decline(e);
        Target(e, 0, "wave-persists-move", r.A.Id, r.Spell.Id);
    }

    private static void WavePersistsSwap()
    {
        var r = WaveTable(); var e = r.E;
        // Without the Reaction: A (2) loses to Y (3).
        {
            var baseline = MatchEngine.FromTestState(e.State);
            Take(baseline, 0, "selectBattle"); CloseWindows(baseline); BothPass(baseline);
            Check(Fought(baseline, "attacking Force 2, defending Force 3"), "baseline: the weak unit loses");
            Eq(baseline.State.Cards[r.A.Id].Zone, Zone.Dead);
        }
        EngageThenUse(e, r);
        Check(e.State.DelayedBattleEffects.Count == 1 && e.State.DelayedBattleEffects[0].Kind == "moveHome" && e.State.DelayedBattleEffects[0].ThenMoveId == r.S.Id, "registered, not applied");
        Eq(e.State.Cards[r.A.Id].Zone, Zone.Battlefield, "he is still there while the players may act");
        Eq(e.State.Cards[r.S.Id].Zone, Zone.Home);
        CloseWindows(e); BothPass(e);
        var log = e.GetView(0).Events.Select(ev => ev.Type).ToList();
        Check(log.IndexOf("unitMovedHome") >= 0 && log.IndexOf("unitMovedToBattlefield") > log.IndexOf("unitMovedHome"), "the target goes home first, and only then does the Shugenja move");
        Check(log.LastIndexOf("unitMovedToBattlefield") < log.IndexOf("battleResolved"), "both moves precede the Force comparison");
        Check(Fought(e, "attacking Force 4, defending Force 3"), "the Shugenja (2 +2F Water) fights in the moved unit's place and wins");
        Eq(e.State.Cards[r.Y.Id].Zone, Zone.Dead, "the Defender's army was destroyed");
        Eq(e.State.Cards[r.A.Id].Zone, Zone.Home, "the moved Personality is safe at home"); Check(!e.State.Cards[r.A.Id].Bowed, "and unbowed: nothing bowed him");
        Eq(e.State.Cards[r.Spell.Id].AttachedTo, r.S.Id, "the Spell went with its Shugenja");
        Eq(e.State.DelayedBattleEffects.Count, 0);
        Check(e.State.PendingMoveReactionCardId is null, "a move the set applies queues no after-action window");
    }

    private static void WavePersistsOffers()
    {
        // Offered, enabled, with only Personalities of yours at the battlefield as targets, in the Engage window.
        var r = WaveTable((s, a, shugenja, y) => { Add(s, "training-crane-veteran", 0, Zone.Home); Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); });
        var e = r.E;
        Take(e, 0, "selectBattle");
        while (e.State.PriorityPlayerId != 0) Decline(e);
        var offer = Offer(e, 0, "wave-persists-move", r.Spell.Id);
        Check(offer is { Enabled: true }, Why(e, 0, offer));
        var atBattle = e.State.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality).Select(c => c.Id).OrderBy(x => x).ToArray();
        Check(offer!.TargetIds!.OrderBy(x => x).SequenceEqual(atBattle), "exactly your Personalities at the battlefield: " + string.Join(",", offer.TargetIds!));
        Check(!offer.TargetIds!.Contains(r.Y.Id), "an enemy is no target"); Check(!offer.TargetIds.Contains(r.S.Id), "nor is the Shugenja at home");
        // Not once the Engage window is over: it is a Reaction to engaging, not a Battle action.
        CloseWindows(e);
        Check(Offer(e, 0, "wave-persists-move") is null or { Enabled: false }, "not offered in the Combat Segment");
        // A Shugenja already at the battlefield: "if this Shugenja is not at the current battlefield".
        var there = WaveTable((s, a, shugenja, y) => { shugenja.Zone = Zone.Battlefield; shugenja.ProvinceIndex = 0; });
        Take(there.E, 0, "selectBattle");
        Check(there.E.State.PendingChoice is null, "nothing to react with: no Engage window opens");
        var blocked = Offer(there.E, 0, "wave-persists-move", there.Spell.Id);
        Check(blocked is null or { Enabled: false }, "the Shugenja is at the battlefield: no Reaction");
        Reject(there.E, new("useAbility", 0, there.E.Version, CardId: there.Spell.Id, AbilityId: "wave-persists-move", TargetId: there.A.Id));
        // The Defender has no Wave Persists of its own; a Spell attached to a Shugenja at ANOTHER battlefield qualifies (he is not at the current one).
        var other = WaveTable((s, a, shugenja, y) => { shugenja.Zone = Zone.Battlefield; shugenja.ProvinceIndex = 1; s.UnresolvedBattlefields = [0, 1]; });
        Take(other.E, 0, "selectBattle");
        while (other.E.State.PriorityPlayerId != 0) Decline(other.E);
        Check(Offer(other.E, 0, "wave-persists-move", other.Spell.Id) is { Enabled: true }, "at another battlefield he is not at the current one either");
    }

    private static void DelayedMoveHomeEdges()
    {
        // (a) the target is already home when the effect's turn comes: he did not move, so the Shugenja does not
        var a = WaveTable(); EngageThenUse(a.E, a);
        CloseWindows(a.E);
        var st = a.E.State; st.Cards[a.A.Id].Zone = Zone.Home; st.Cards[a.A.Id].ProvinceIndex = null;
        var e1 = MatchEngine.FromTestState(st); BothPass(e1);
        Eq(e1.State.Cards[a.S.Id].Zone, Zone.Home, "he had not moved, so she stays");
        Check(!e1.GetView(0).Events.Any(ev => ev.Type == "unitMovedToBattlefield"));
        Check(Fought(e1, "attacking Force 0, defending Force 3"));
        // (b) the target is gone from play: skipped without a throw, nobody moves
        var b = WaveTable(); EngageThenUse(b.E, b);
        CloseWindows(b.E);
        var st2 = b.E.State; st2.Cards[b.A.Id].Zone = Zone.Dead; st2.Cards[b.A.Id].ProvinceIndex = null;
        var e2 = MatchEngine.FromTestState(st2); BothPass(e2);
        Eq(e2.State.Cards[b.S.Id].Zone, Zone.Home, "the effect was skipped");
        Eq(e2.State.DelayedBattleEffects.Count, 0, "and is gone");
        // (c) the Shugenja is gone from play by then: he still goes home, nothing follows and nothing throws
        var c = WaveTable(); EngageThenUse(c.E, c);
        CloseWindows(c.E);
        var st3 = c.E.State; st3.Cards[c.S.Id].Zone = Zone.Dead; st3.Cards[c.S.Id].ProvinceIndex = null;
        var e3 = MatchEngine.FromTestState(st3); BothPass(e3);
        Eq(e3.State.Cards[c.A.Id].Zone, Zone.Home, "the target still moved home");
        Eq(e3.State.Cards[c.S.Id].Zone, Zone.Dead);
        // (d) it lapses with its battle: a registered effect the set never gathered does not carry into the next battle
        var d = WaveTable(); EngageThenUse(d.E, d);
        Eq(d.E.State.DelayedBattleEffects.Count, 1);
        CloseWindows(d.E); BothPass(d.E);
        Eq(d.E.State.DelayedBattleEffects.Count, 0);
    }

    private static void WaterStaticForce()
    {
        // Water: +2F for the wielder, home or not.
        var r = WaveTable();
        Eq(Force(r.E, r.S), 4, "a Water Shugenja (2) with The Wave Persists: +2F at home too");
        // Not Water: the bonus does not apply.
        var s = Selection();
        var plain = Add(s, "training-shugenja", 0, Zone.Home); Add(s, WavePersists, 0, Zone.Attached, parent: plain.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(Force(e, plain), 1, "a Shugenja without Water gains nothing");
        // Two copies stack like any Force bonus.
        var s2 = Selection();
        var host = Add(s2, "training-water-shugenja", 0, Zone.Home); Add(s2, WavePersists, 0, Zone.Attached, parent: host.Id); Add(s2, WavePersists, 0, Zone.Attached, parent: host.Id);
        Eq(Force(MatchEngine.FromTestState(s2), host), 6);
    }

    // ---- Holding Cells ------------------------------------------------------------------------------------------------
    private static void HoldingCellsBow()
    {
        // Attacker (player 0): X (Lion samurai, Force 4) and Holding Cells; Defender: Y (Crane veteran, Force 5) and Z (Crane samurai, Force 3). Y bowed before the
        // resolution leaves Z (3) against X (4): the Attacker wins.
        var s = Combat();
        var x = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var y = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var cells = Add(s, HoldingCells, 0, Zone.Hand);
        var honor = s.Players[0].Honor;
        var e = MatchEngine.FromTestState(s);
        Pass(e, 1);
        var offer = Offer(e, 0, "holding-cells-bow", cells.Id);
        Check(offer is { Enabled: true }, Why(e, 0, offer));
        Eq(offer!.Gold?.Cost, (int?)4, "the 4 Gold is the action's own cost");
        Check(offer.TargetIds!.Contains(y.Id) && offer.TargetIds.Contains(x.Id), "either side's Personality at the battlefield");
        Check(offer.Command!.SourceIds is { Length: > 0 }, "the offered command carries its payment");
        AllOffersAccepted(e, 0);
        Target(e, 0, "holding-cells-bow", y.Id, cells.Id);
        Eq(e.State.Players[0].Honor, honor - 1, "Lose 1 Honor: the acting player's, at once");
        Check(!e.State.Cards[y.Id].Bowed, "the bow waits for the resolution");
        Eq(e.State.Cards[cells.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        Check(e.GetView(0).Events.Any(ev => ev.Type == "payment" && ev.Message.Contains("paid 4 Gold")), "4 Gold was paid");
        Pass(e, 1); Pass(e, 0);
        Check(Fought(e, "attacking Force 4, defending Force 3"), "the bowed Personality contributes nothing");
        Eq(e.State.Cards[y.Id].Zone, Zone.Dead, "the Defender's army lost, bowed or not");
        // Without the Gold it is offered disabled.
        var poor = Combat(); Add(poor, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(poor, "training-crane-veteran", 1, Zone.Battlefield, province: 0); Add(poor, HoldingCells, 0, Zone.Hand);
        foreach (var c in poor.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
        var ep = MatchEngine.FromTestState(poor); Pass(ep, 1);
        Check(Offer(ep, 0, "holding-cells-bow") is null or { Enabled: false }, "no Gold, no action");
        // It is a Kolat action: the classification carries the keyword.
        Check(AbilityCatalog.All["holding-cells-bow"].ActionKeywords!.Contains("Kolat"));
    }

    // ---- Power of the Night -----------------------------------------------------------------------------------------------
    private static void PowerOfTheNightChi()
    {
        // Non-Shadowlands Ninja performer bows; a Chi 4 target keeps living at -3C; a Chi 3 target goes to 0 and is destroyed.
        foreach (var (targetDef, dies) in new[] { ("training-crane-veteran", false), ("training-crane-samurai", true) })
        {
            var s = Combat();
            var ninja = Add(s, "training-ninja-source", 0, Zone.Battlefield, province: 0);
            var target = Add(s, targetDef, 1, Zone.Battlefield, province: 0);
            var card = Add(s, PowerOfTheNight, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s); Pass(e, 1);
            Target(e, 0, "power-of-the-night-chi", target.Id, card.Id, ninja.Id);
            Check(e.State.Cards[ninja.Id].Bowed, "the performer bows: he is not Shadowlands");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard);
            Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "nothing happens to the target yet");
            Pass(e, 1); Pass(e, 0);
            if (dies) Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "Chi 0 destroys him before the resolution");
            else Check(e.State.Cards[target.Id].Modifiers.Any(m => m.Stat == "Chi" && m.Amount == -3), "the -3C stands: " + string.Join("; ", e.State.Cards[target.Id].Modifiers.Select(m => $"{m.Stat}{m.Amount}")) + " | " + string.Join("; ", e.GetView(0).Events.TakeLast(6).Select(x => x.Message)));
        }
        // A Shadowlands Ninja does not bow.
        var sh = Combat();
        var shadow = Add(sh, "training-ninja-source", 0, Zone.Battlefield, province: 0); shadow.DefinitionId = "training-ninja-source";
        var shadowlands = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly && c.Keywords?.Contains("Ninja") == true && c.Keywords.Contains("Shadowlands"));
        shadow.DefinitionId = shadowlands.Id;
        var t = Add(sh, "training-crane-veteran", 1, Zone.Battlefield, province: 0); var cardS = Add(sh, PowerOfTheNight, 0, Zone.Hand);
        var es = MatchEngine.FromTestState(sh); Pass(es, 1);
        Target(es, 0, "power-of-the-night-chi", t.Id, cardS.Id, shadow.Id);
        Check(!es.State.Cards[shadow.Id].Bowed, "a Shadowlands performer is not bowed");
        // A bowed Ninja, a non-Ninja and a Ninja at home are no performers.
        var no = Combat();
        Add(no, "training-ninja-source", 0, Zone.Battlefield, bowed: true, province: 0); Add(no, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(no, "training-ninja-source", 0, Zone.Home);
        Add(no, "training-crane-veteran", 1, Zone.Battlefield, province: 0); Add(no, PowerOfTheNight, 0, Zone.Hand);
        var en = MatchEngine.FromTestState(no); Pass(en, 1);
        Check(en.QueryActions(0).Where(o => o.Type == "useAbility" && o.Command?.AbilityId == "power-of-the-night-chi").All(o => !o.Enabled), "no unbowed Ninja at the battlefield: not playable");
    }

    // ---- Quick Recovery -------------------------------------------------------------------------------------------------------
    private static void QuickRecoveryStraighten()
    {
        // Attacker: P (Lion samurai, Force 4) with an attached ashigaru (Force 2, bowed: it adds nothing while bowed) and Q (Crane samurai, Force 3, bowed, another unit);
        // Defender: Y (Force 5). Straightening the ashigaru before the resolution makes the unit 6 and wins; without it 4 loses.
        var s = Combat();
        var p = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: p.Id);
        var q = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var card = Add(s, QuickRecovery, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s); Pass(e, 1);
        var byPerformer = e.QueryActions(0).Where(o => o.Type == "useAbility" && o.Command?.AbilityId == "quick-recovery-straighten").ToArray();
        var forP = byPerformer.First(o => o.Command!.PerformerId == p.Id);
        Check(forP.TargetIds!.SequenceEqual(new[] { follower.Id }), "the one bowed card in his unit: " + string.Join(",", forP.TargetIds!));
        Check(!forP.TargetIds!.Contains(q.Id), "another unit's card is not in his unit");
        AllOffersAccepted(e, 0);
        Target(e, 0, "quick-recovery-straighten", follower.Id, card.Id, p.Id);
        Check(e.State.Cards[follower.Id].Bowed, "not yet: before the resolution");
        Pass(e, 1); Pass(e, 0);
        Check(Fought(e, "attacking Force 6, defending Force 5"), "the ashigaru was straightened before the Force was read: 4 + 2");
        Eq(e.State.Cards[q.Id].Bowed || e.State.Cards[q.Id].Zone != Zone.Battlefield, true, "the other unit's bowed card was left alone");
        // Straightening HIM: an army of one unbowed Personality fights.
        var s2 = Combat();
        var p2 = Add(s2, "training-lion-samurai", 0, Zone.Battlefield, bowed: true, province: 0); Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        Add(s2, QuickRecovery, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2); Pass(e2, 1);
        Target(e2, 0, "quick-recovery-straighten", p2.Id);
        Pass(e2, 1); Pass(e2, 0);
        Check(Fought(e2, "attacking Force 4, defending Force 5"), "he was straightened before the Force was read: he counts");
        // Nothing bowed in the unit: not offered.
        var s3 = Combat(); Add(s3, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(s3, "training-crane-veteran", 1, Zone.Battlefield, province: 0); Add(s3, QuickRecovery, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3); Pass(e3, 1);
        Check(Offer(e3, 0, "quick-recovery-straighten") is null or { Enabled: false }, "no bowed card in his unit");
    }

    // ---- Reckless Rush ------------------------------------------------------------------------------------------------------------
    private static void RecklessRushBow()
    {
        // Attacker: R (Lion samurai, Force 4). Defender: a Crane samurai (cost 5), a base-cost-0 Personality, and a Follower on another Personality.
        var zeroCost = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly && c.GoldCost == 0 && c.Force > 0);
        foreach (var kind in new[] { "costly", "free", "follower" })
        {
            var s = Combat();
            var rusher = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            CardInstance target;
            if (kind == "costly") target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            else if (kind == "free") target = Add(s, zeroCost.Id, 1, Zone.Battlefield, province: 0);
            else { var host = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); target = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id); }
            Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
            var card = Add(s, RecklessRush, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s); Pass(e, 1);
            var offer = e.QueryActions(0).First(o => o.Type == "useAbility" && o.Command?.AbilityId == "reckless-rush-bow");
            Check(offer.TargetIds!.Contains(target.Id), kind + " target offered");
            Target(e, 0, "reckless-rush-bow", target.Id, card.Id, rusher.Id);
            Check(e.State.Cards[rusher.Id].Bowed, "the performer bowed as the cost");
            if (kind == "costly") { Check(e.State.Cards[target.Id].Bowed && e.State.Cards[target.Id].Zone == Zone.Battlefield, "bowed, not destroyed: it costs " + CardCatalog.Get("training-crane-samurai").GoldCost); }
            else Check(e.State.Cards[target.Id].Zone != Zone.Battlefield && e.State.Cards[target.Id].Zone != Zone.Attached, kind + ": destroyed (an attachment, or a base Gold Cost of 0)");
            Pass(e, 1); Pass(e, 0);
            Check(Fought(e, "attacking Force 4"), kind + ": straightened before the resolution, so he fought (a bowed performer would have counted 0)");
            Check(e.GetView(0).Events.Any(ev => ev.Type == "beforeBattleEffect" && ev.Message.Contains("straighten")), kind + ": the straightening was a member of the set");
        }
        // Not offered against a Personality with attachments; a bowed performer is no performer.
        var s2 = Combat();
        Add(s2, "training-lion-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var loaded = Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0); Add(s2, "training-follower", 1, Zone.Attached, province: 0, parent: loaded.Id);
        Add(s2, RecklessRush, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2); Pass(e2, 1);
        var none = Offer(e2, 0, "reckless-rush-bow");
        Check(none is null or { Enabled: false } || !none.TargetIds!.Contains(loaded.Id), "no target with attachments and no unbowed performer");
    }

    // ---- Harsh Taskmaster ----------------------------------------------------------------------------------------------------------
    private static void HarshTaskmasterAttach()
    {
        var commanderDef = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly && c.Keywords?.Contains("Commander") == true && c.PersonalHonor >= 1);
        foreach (var from in new[] { Zone.Hand, Zone.FateDiscard })
        {
            var s = Combat();
            var commander = Add(s, commanderDef.Id, 0, Zone.Battlefield, province: 0);
            var follower = Add(s, "training-follower", 0, from);
            Add(s, "training-follower", 1, Zone.FateDiscard); // the opponent's discard pile is not "your discard pile"
            var enemyHand = Add(s, "training-follower", 1, Zone.Hand);
            Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
            var card = Add(s, HarshTaskmaster, 0, Zone.Hand);
            s.Players[0].Honor = 20;
            var e = MatchEngine.FromTestState(s); Pass(e, 1);
            var offer = Offer(e, 0, "harsh-taskmaster-attach", card.Id);
            Check(offer is { Enabled: true }, Why(e, 0, offer));
            Check(offer!.TargetIds!.Contains(follower.Id) && !offer.TargetIds!.Contains(enemyHand.Id) && offer.TargetIds!.All(id => e.State.Cards[id].OwnerId == 0), "only Followers of yours in your hand or your discard pile");
            AllOffersAccepted(e, 0);
            Target(e, 0, "harsh-taskmaster-attach", follower.Id, card.Id, commander.Id);
            var attached = e.State.Cards[follower.Id];
            Eq(attached.Zone, Zone.Attached); Eq(attached.AttachedTo, commander.Id, "attached to the performing Commander");
            Check(!e.GetView(0).Events.Any(ev => ev.Type == "payment"), "ignoring costs: nothing was paid");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was played");
            Eq(e.State.AdditionalActions.Count(g => g.PlayerId == 0), 1, "you may take an additional Battle action");
            Eq(e.State.PriorityPlayerId, 0, "and keep priority for it");
            Check(e.State.DelayedBattleEffects.Any(d => d.Kind == "destroy" && d.TargetId == follower.Id), "the destruction is registered for the resolution");
            Pass(e, 0); // forfeits the additional action
            Pass(e, 1);
            Check(e.State.Cards[follower.Id].Zone == Zone.FateDiscard, "destroyed before the battle resolved (a destroyed Follower goes to its owner's discard pile)");
        }
        // A non-Commander is no performer.
        var s2 = Combat(); Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(s2, "training-follower", 0, Zone.Hand); Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0); Add(s2, HarshTaskmaster, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2); Pass(e2, 1);
        Check(Offer(e2, 0, "harsh-taskmaster-attach") is null or { Enabled: false }, "the performer must be a Commander");
    }

    // ---- Steady Resolve and Akodo Dairuko -----------------------------------------------------------------------------------------
    private static void SteadyResolveAndDairuko()
    {
        // Steady Resolve (published): an opposed Samurai of yours at the battlefield performs; +2F before the totals.
        var s = Combat();
        var x = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var steady = Add(s, SteadyResolve, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        BothPass(e);
        Eq(e.State.PendingChoice?.Type, "beforeBattleReaction"); Eq(e.State.PriorityPlayerId, 0);
        Take(e, 0, "useAbility", o => o.Command!.AbilityId == "steady-resolve-boost" && o.Command.PerformerId == x.Id);
        Check(Fought(e, "attacking Force 4, defending Force 5"), "+2F before the Force was read");
        Eq(e.State.Cards[steady.Id].Zone, Zone.FateDiscard);
        // Akodo Dairuko (published): Tireless -- usable while bowed; the army is not bowed by the resolution.
        var s2 = Combat(); s2.UnresolvedBattlefields = [0, 1];
        var dairuko = Add(s2, Dairuko, 0, Zone.Battlefield, bowed: true, province: 0); var ally = Add(s2, "training-lion-veteran", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2); BothPass(e2);
        Eq(e2.State.PendingChoice?.Type, "beforeBattleReaction"); Eq(e2.State.PendingChoice?.CardId, dairuko.Id, "even bowed");
        Take(e2, 0, "useAbility", o => o.Command!.AbilityId == "dairuko-rally");
        Check(!e2.State.Cards[ally.Id].Bowed && e2.State.Cards[ally.Id].Zone == Zone.Home, "the resolution did not bow her army: " + e2.State.Cards[ally.Id].Zone + " " + e2.State.Cards[ally.Id].Bowed + " | " + string.Join("; ", e2.GetView(0).Events.TakeLast(6).Select(x => x.Message)));
        // Melee 8 Attack: a Personality of Force 8 or less is destroyed (8 exactly included), one of Force 9 is still a legal target but survives; she must be unbowed and at the battlefield like any
        // Battle action.
        foreach (var (bonus, dies) in new[] { (0, true), (2, true), (3, false) })
        {
            var s3 = Combat();
            var attacker = Add(s3, Dairuko, 0, Zone.Battlefield, province: 0);
            var victim = Add(s3, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            if (bonus > 0) s3.Cards[victim.Id].Modifiers.Add(new AppliedModifier("test", "test", "Force", bonus));
            var e3 = MatchEngine.FromTestState(s3); Pass(e3, 1);
            var offer = Offer(e3, 0, "dairuko-melee", attacker.Id);
            Check(offer is { Enabled: true }, Why(e3, 0, offer));
            // A target above the strength is still a legal target (core rules); the Attack just fails to destroy it.
            Check(offer!.TargetIds!.Contains(victim.Id), $"Force {6 + bonus} is a legal target of strength 8");
            Target(e3, 0, "dairuko-melee", victim.Id, attacker.Id);
            while (e3.State.PendingChoice?.Type == "attackReaction") Decline(e3);
            Eq(e3.State.Cards[victim.Id].Zone, dies ? Zone.Dead : Zone.Battlefield, $"Force {6 + bonus} against strength 8");
        }
        // Bowed, she cannot attack (only the Reaction is Tireless).
        var s4 = Combat(); Add(s4, Dairuko, 0, Zone.Battlefield, bowed: true, province: 0); Add(s4, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4); Pass(e4, 1);
        Check(Offer(e4, 0, "dairuko-melee") is null or { Enabled: false }, "a bowed Dairuko has no Battle action");
    }

    private static void AllOffersAccepted(MatchEngine e, int player)
    {
        foreach (var option in e.QueryActions(player).Where(o => o.Enabled && o.Command is not null && o.Type is not "concede"))
            Accept(MatchEngine.FromTestState(e.State), option.Command!);
    }
}
