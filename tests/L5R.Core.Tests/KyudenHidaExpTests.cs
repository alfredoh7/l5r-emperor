using static RegressionAssertions;
using L5R.Core;

// Kyuden Hida - exp (oracle-l5r-4638, stronghold-wave-8; issue #364): "Recon Reaction: After engaging: You have Reconnaissance at the current battlefield. Create and attach a
// 0F Scout Follower to each of one to three of your Personalities in your current army. Remove those Followers from the game after the battle ends." Not the Event
// (Rebuilding Kyuden Hida, 6200). Every position is played through the commands the engine offers.
internal static class KyudenHidaExpTests
{
    private const string KyudenHida = "oracle-l5r-4638", Ebizo = "oracle-l5r-1629", Scout = "oracle-l5r-9714";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: the Emperor Edition Stronghold (Province Strength 8, Gold 4, Starting Honor 3, Experienced), one Recon Reaction, art shipped, source and sha256 recorded, apart from the Event", PublishedShape),
            ("Engage Reaction: Reconnaissance at the battlefield and a 0F Scout Follower attached to each of one to three chosen Personalities of the current army, removed from the game when the battle ends", ScoutsAreCreatedAndRemoved),
            ("The choice is exactly one to three DIFFERENT Personalities of your own current army: none, four, a repeat, an enemy, a Personality at home are all rejected, and the offer lists the army", ChoiceRules),
            ("A Recon Reaction like any other: Ebizo gets his +1F, and a Defender's own Kyuden Hida works for the Defender's army (Reconnaissance is theirs)", ReconActionAndDefender),
            ("A Scout whose host is destroyed is still removed from the game (not left in a discard pile); the created card is Kyuden Hida Survivor, a published created-only scan (Mendon, issue #394)", RemovedEvenWhenHostDies),
            ("With no Personality in your army the Reaction is not offered and no Engage window opens for it", NeedsAnArmy),
        ];
        runner.Run("kyuden-hida-exp", tests);
    }

    private static void Check(bool ok, string? message = null) { if (!ok) throw new Exception(message ?? "Assertion failed"); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");
    private static int _added;
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"kh-{s.Cards.Count}-{++_added}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }
    // The economy scenario with published Strongholds, at Battle Selection, player 0 attacking province 0; `hidaOwner` holds Kyuden Hida - exp as its Stronghold.
    private static MatchState Selection(int hidaOwner)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218"; foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Cards[s.Players[hidaOwner].StrongholdId].DefinitionId = KyudenHida;
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        return s;
    }
    private static GameCommand Cmd(MatchEngine e, string type, int? player = null) => new(type, player ?? e.State.PriorityPlayerId, e.Version);
    private static void Pass(MatchEngine e) => Accept(e, Cmd(e, "pass"));
    private static void Decline(MatchEngine e) => Accept(e, Cmd(e, "declineReaction"));
    private static ActionOption? Offer(MatchEngine e, int player) => e.QueryActions(player).FirstOrDefault(o => o.Type == "useAbility" && o.Command?.AbilityId == "kyuden-hida-exp-scouts");
    private static void Engage(MatchEngine e, int owner)
    {
        Accept(e, e.QueryActions(0).First(o => o.Type == "selectBattle" && o.Enabled).Command!);
        Eq(e.State.PendingChoice?.Type, "engageReaction");
        while (e.State.PriorityPlayerId != owner) Decline(e);
    }
    private static void FinishBattle(MatchEngine e) { while (e.State.PendingChoice is not null) Decline(e); while (e.State.Phase == Phase.Battle && e.State.CurrentBattleProvince is not null) Pass(e); }
    private static string Why(MatchEngine e, int player, ActionOption? o) => o is null ? "not offered: " + string.Join("; ", e.QueryActions(player).Select(x => $"{x.Type}:{x.Label}[{x.Enabled}] {x.Reason}")) : o.Reason ?? "";

    private static void PublishedShape()
    {
        var c = CardCatalog.Get(KyudenHida);
        Eq(c.Type, CardType.Stronghold); Eq(c.Clan, "Crab"); Eq(c.ProvinceStrength, 8); Eq(c.GoldProduction, 4); Eq(c.StartingHonor, 3);
        Eq(string.Join(",", c.Keywords!), "Experienced"); Eq(string.Join(",", c.Abilities!), "kyuden-hida-exp-scouts"); Eq(c.Effect, "none"); Check(!c.ScenarioOnly);
        Check(c.Text.StartsWith("Recon Reaction: After engaging: You have Reconnaissance at the current battlefield.") && c.Text.EndsWith("after the battle ends."));
        var ability = AbilityCatalog.All["kyuden-hida-exp-scouts"];
        Eq(ability.Timing, "Reaction"); Eq(ability.Trigger, "after-engaging"); Check(ability.ActionKeywords!.Contains("Recon"));
        Check(File.Exists(Path.Combine(Support337.RepoRoot(), "src/L5R.Client/Content/Published", KyudenHida + ".jpg")), "art is shipped");
        Eq(PublishedContent.StrongholdWave8.Cards.Length, 1);
        var source = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Support337.RepoRoot(), "content/published/stronghold-wave-8-sources.json"))).RootElement[0];
        Eq(source.GetProperty("printing_set")[0].GetString(), "Emperor Edition"); Check(source.GetProperty("image_path").GetString()!.Contains("emperor-edition"));
        Check(source.GetProperty("image_sha256").GetString()!.Length == 64);
        // Not the Event: a different card, type and effect.
        var ev = CardCatalog.Get("oracle-l5r-6200");
        Eq(ev.Type, CardType.Event); Check(ev.Id != c.Id && ev.Effect == "rebuilding-kyuden-hida");
        var scout = CardCatalog.Get(Scout);
        Check(!scout.ScenarioOnly && scout.CreatedOnly && scout.Name == "Kyuden Hida Survivor" && scout.Type == CardType.Follower && scout.Force == 0 && scout.Keywords!.Contains("Scout"), "the created Follower: 0F, Scout, the created-only Survivor scan");
        Check(!CardCatalog.PlayableFromDecks.Any(x => x.Id == Scout) && Support337.ArtShipped(Scout), "never playable from a deck, art shipped");
        Check(!CardCatalog.Cards.ContainsKey("created-scout-follower"), "the synthetic scenario-only Scout is gone");
        Check(CreatedCards.Resolve(AbilityCatalog.All["kyuden-hida-exp-scouts"].Creates!).Id == Scout, "resolved by the spec: a 0F Scout Follower, naming the Survivor (the Kyuden Hida Scout scan prints the same card)");
    }

    private static void ScoutsAreCreatedAndRemoved()
    {
        var s = Selection(0);
        var units = new[] { "training-lion-samurai", "training-crane-samurai", "training-lion-veteran", "training-crane-veteran" }.Select(d => Add(s, d, 0, Zone.Battlefield, province: 0)).ToArray();
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Engage(e, 0);
        var offer = Offer(e, 0);
        Check(offer is { Enabled: true }, Why(e, 0, offer));
        Check(offer!.TargetIds!.OrderBy(x => x).SequenceEqual(units.Select(u => u.Id).OrderBy(x => x)), "the army is listed as the candidates");
        Check(offer.Command!.CardIds is null && offer.Command.TargetId is not null && units.Any(u => u.Id == offer.Command.TargetId), "the default command names one Personality as its target, as a click does");
        foreach (var o in e.QueryActions(0).Where(o => o.Enabled && o.Command is not null && o.Type != "concede")) Accept(MatchEngine.FromTestState(e.State), o.Command!);
        var chosen = new[] { units[0].Id, units[2].Id };
        Accept(e, offer.Command with { TargetId = null, CardIds = chosen });
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "You have Reconnaissance at the current battlefield (the Defender's province 0)");
        Check(!e.State.Players[1].Provinces[0].Reconnaissance[1], "and only you do");
        var scouts = e.State.Cards.Values.Where(c => c.DefinitionId == Scout).ToArray();
        Eq(scouts.Length, 2, "one Follower per chosen Personality");
        foreach (var f in scouts) { Eq(f.Zone, Zone.Attached); Check(chosen.Contains(f.AttachedTo!), "attached to a chosen Personality"); Eq(f.OwnerId, 0); Eq(f.ProvinceIndex, (int?)0); }
        Check(scouts.Select(f => f.AttachedTo).Distinct().Count() == 2, "each got its own");
        Check(e.State.Cards[units[1].Id].Zone == Zone.Battlefield && !e.State.Cards.Values.Any(c => c.DefinitionId == Scout && c.AttachedTo == units[1].Id), "an unchosen Personality got none");
        Eq(e.GetView(0).Players[0].Army.Single(c => c.Id == units[0].Id).UnitForce, e.GetView(0).Players[0].Army.Single(c => c.Id == units[0].Id).Force, "a 0F Follower adds nothing to the unit");
        Check(e.State.Events.Count(ev => ev.Type == "cardCreated") == 2);
        Eq(e.State.CreatedUntilBattleEndsIds.Count, 2);
        FinishBattle(e);
        foreach (var f in scouts) Eq(e.State.Cards[f.Id].Zone, Zone.RemovedFromGame, "removed from the game after the battle ends");
        Eq(e.State.CreatedUntilBattleEndsIds.Count, 0);
        Check(e.State.Cards.Values.All(c => c.DefinitionId != Scout || c.Zone == Zone.RemovedFromGame), "none remains anywhere");
    }

    private static void ChoiceRules()
    {
        var s = Selection(0);
        var a = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); var b = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var c = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0); var d = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var home = Add(s, "training-crane-samurai", 0, Zone.Home); var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Engage(e, 0);
        var template = Offer(e, 0)!.Command!;
        void Bad(string[]? ids, string? target = null) => Reject(e, template with { TargetId = target, CardIds = ids });
        Bad([]); Bad(null); Bad([a.Id, b.Id, c.Id, d.Id]); Bad([a.Id, a.Id]); Bad([enemy.Id]); Bad([a.Id, enemy.Id]); Bad([home.Id]); Bad(["no-such-card"]);
        Bad(null, enemy.Id); Bad(null, home.Id); Bad([a.Id], b.Id);
        Accept(MatchEngine.FromTestState(e.State), template with { TargetId = null, CardIds = [a.Id, b.Id, c.Id] });
        Accept(MatchEngine.FromTestState(e.State), template with { TargetId = null, CardIds = [d.Id] });
        Accept(MatchEngine.FromTestState(e.State), template with { TargetId = d.Id, CardIds = null });
    }

    private static void ReconActionAndDefender()
    {
        // Attacker's Kyuden Hida with Ebizo in the army: a Recon Reaction resolving gives Ebizo +1F (the funnel every Recon action passes).
        var s = Selection(0);
        var ebizo = Add(s, Ebizo, 0, Zone.Battlefield, province: 0); Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Engage(e, 0);
        var offer = Offer(e, 0);
        Accept(e, offer!.Command! with { TargetId = null, CardIds = [ebizo.Id] });
        Eq(e.State.Cards[ebizo.Id].Modifiers.Count(m => m.Effect == "force-after-own-recon-action"), 1, "a Recon Reaction: Ebizo's +1F");
        // The Defender's Stronghold: its own army, its own Reconnaissance.
        var s2 = Selection(1);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var defender = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Engage(e2, 1);
        var theirs = Offer(e2, 1);
        Check(theirs is { Enabled: true }, Why(e2, 1, theirs)); Check(theirs!.TargetIds!.SequenceEqual([defender.Id]), "the Defender's army, not the Attacker's");
        Check(Offer(e2, 0) is null or { Enabled: false }, "the other player's Stronghold is not Kyuden Hida");
        Accept(e2, theirs.Command! with { TargetId = null, CardIds = [defender.Id] });
        Check(e2.State.Players[1].Provinces[0].Reconnaissance[1] && !e2.State.Players[1].Provinces[0].Reconnaissance[0], "Reconnaissance belongs to the player who used it");
        Eq(e2.State.Cards.Values.Single(c => c.DefinitionId == Scout).OwnerId, 1);
    }

    private static void RemovedEvenWhenHostDies()
    {
        // The Attacker's lone unit (Force 3) loses to the Defender's (5): the host is destroyed and its Follower goes to the discard pile -- and is removed from the game.
        var s = Selection(0);
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Engage(e, 0);
        Accept(e, Offer(e, 0)!.Command! with { TargetId = null, CardIds = [host.Id] });
        var scout = e.State.Cards.Values.Single(c => c.DefinitionId == Scout);
        FinishBattle(e);
        Eq(e.State.Cards[host.Id].Zone, Zone.Dead, "the host lost");
        Eq(e.State.Cards[scout.Id].Zone, Zone.RemovedFromGame, "and the created Follower is removed from the game, not kept in a discard pile");
        Check(!e.State.Players[0].Hand.Contains(scout.Id));
    }

    private static void NeedsAnArmy()
    {
        var s = Selection(0);
        Add(s, "training-crane-samurai", 0, Zone.Home); Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(0).First(o => o.Type == "selectBattle" && o.Enabled).Command!);
        Check(e.State.PendingChoice is null, "no usable Reaction: no Engage window");
        Check(Offer(e, 0) is null or { Enabled: false });
    }
}
