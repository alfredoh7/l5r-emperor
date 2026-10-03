using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using L5R.AI;
using L5R.Core;

return Nightly.Run(args);

internal static class Nightly
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private static string output = "artifacts/nightly";
    private static int matches, completed, capped, decisions, probes, cards;

    public static int Run(string[] args)
    {
        var seedCount = 32;
        var maxDecisions = 1600;
        var mode = "all";
        var inject = false;
        try
        {
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--seeds": seedCount = int.Parse(args[++i]); break;
                    case "--max-decisions": maxDecisions = int.Parse(args[++i]); break;
                    case "--mode": mode = args[++i]; break;
                    case "--output": output = args[++i]; break;
                    case "--inject-failure": inject = true; break;
                    default: throw new ArgumentException($"Unknown argument: {args[i]}");
                }
            }
            Require(seedCount is > 0 and <= 1024 && maxDecisions is > 0 and <= 10000, "Invalid nightly bounds.");
            Require(mode is "all" or "soak" or "catalogue", "Mode must be all, soak or catalogue.");
            Directory.CreateDirectory(output);
            if (inject) throw new InvalidOperationException("Deliberate nightly acceptance failure; no game defect is implied.");
            if (mode is "all" or "soak")
                for (var index = 0; index < seedCount; index++)
                {
                    var seed = 20260906UL + (ulong)index;
                    Play(MatchEngine.CreateStarterMatch(seed), seed, "starter", maxDecisions);
                    Play(MatchEngine.CreatePublishedMatch(seed), seed, "published-live", maxDecisions);
                }
            if (mode is "all" or "catalogue") CheckCatalogue();
            Write("summary.json", new { status = "passed", seedCount, maxDecisions, mode, matches, completed, capped, decisions, cards, probes });
            Console.WriteLine($"PASS nightly: {matches} matches ({completed} completed, {capped} capped), {decisions} decisions, {cards} published cards, {probes} offered-command probes.");
            return 0;
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(output);
            Write("summary.json", new { status = "failed", mode, matches, completed, capped, decisions, cards, probes, error = error.ToString() });
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void Play(MatchEngine engine, ulong seed, string profile, int limit)
    {
        var count = 0;
        try
        {
            while (engine.GetView(0).WinnerId is null && count < limit)
            {
                var player = engine.GetView(0).PriorityPlayerId;
                var command = TrainingOpponent.Choose(engine.GetView(player), engine.QueryActions(player)).Command;
                Require(command is not null, $"AI stalled at {profile}/{seed}, decision {count}.");
                var result = engine.Execute(command!);
                Require(result.Accepted, $"AI command rejected at {profile}/{seed}, decision {count}: {result.Error}");
                count++;
                decisions++;
                if (count % 100 == 0)
                {
                    CheckReplay(engine);
                    Console.WriteLine($"Replay verified {profile}/{seed}: {count} decisions.");
                }
            }
            CheckReplay(engine);
            var finished = engine.GetView(0).WinnerId is not null;
            if (finished) completed++;
            else
            {
                capped++;
                Write($"capped-{profile}-{seed}-replay.json", engine.ExportReplay());
            }
            matches++;
            Console.WriteLine($"PASS {profile}/{seed}: {count} decisions, {(finished ? engine.GetView(0).VictoryReason : "bounded soak; no winner")}; state {Hash(engine.State)}");
        }
        catch
        {
            Write("failed-replay.json", engine.ExportReplay());
            throw;
        }
    }

    private static void CheckReplay(MatchEngine engine)
    {
        var journal = JsonSerializer.Deserialize<ReplayRecord>(JsonSerializer.Serialize(engine.ExportReplay(), Json))!;
        var replay = MatchEngine.Replay(journal);
        Require(Hash(replay.State) == Hash(engine.State), "Replay full state differs (hidden state, RNG and pending choices included).");
        for (var viewer = 0; viewer < 2; viewer++)
            Require(Hash(replay.GetView(viewer)) == Hash(engine.GetView(viewer)), $"Replay differs for seat {viewer}.");
    }

    private static void CheckCatalogue()
    {
        var definitions = PublishedContent.Snapshot.Cards.Concat(PublishedContent.Waves.SelectMany(w => w.Cards))
            .DistinctBy(c => c.Id).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        Require(definitions.Length > 0, "Published-card discovery is empty.");
        var coverage = new List<object>();
        var failures = new List<object>();
        foreach (var definition in definitions)
        {
            var count = 0;
            foreach (var phase in new[] { Phase.Action, Phase.Battle, Phase.Dynasty })
                for (var owner = 0; owner < 2; owner++)
                {
                    var state = Fixture(definition, phase, owner);
                    var engine = MatchEngine.FromTestState(state);
                    for (var viewer = 0; viewer < 2; viewer++)
                        foreach (var option in engine.QueryActions(viewer).Where(a => a.Enabled && a.Command is not null))
                        {
                            // Each command executes against the exact same unmodified state.
                            // A prior probe must never consume an action, target or payment.
                            var probe = MatchEngine.FromTestState(state);
                            try
                            {
                                var result = probe.Execute(option.Command!);
                                Require(result.Accepted, result.Error ?? "Offered command was rejected.");
                            }
                            catch (Exception error)
                            {
                                failures.Add(new { definition.Id, phase, owner, viewer, option, error = error.ToString() });
                                if (failures.Count == 1) Write("failed-command.json", new { definition.Id, phase, owner, viewer, option, error = error.ToString(), state });
                            }
                            probes++;
                            var sourceId = option.CardId ?? option.Command!.CardId;
                            if (sourceId is not null && state.Cards.TryGetValue(sourceId, out var source) && source.DefinitionId == definition.Id) count++;
                        }
                }
            cards++;
            coverage.Add(new { definition.Id, definition.Type, sourceCommands = count });
            if (cards % 100 == 0) Console.WriteLine($"Catalogue: {cards}/{definitions.Length} cards, {probes} commands.");
        }
        Write("catalogue-coverage.json", coverage);
        Write("catalogue-failures.json", failures);
        Require(failures.Count == 0, $"{failures.Count} offered-command failures; inspect catalogue-failures.json and failed-command.json.");
    }

    private static MatchState Fixture(CardDefinition card, Phase phase, int owner)
    {
        var state = MatchEngine.CreateEconomyScenario().State;
        foreach (var player in state.Players)
        {
            foreach (var id in player.Hand.ToArray()) state.Cards.Remove(id);
            player.Hand.Clear();
            player.Honor = 20;
            player.DynastyDeck = player.DynastyDeck.Take(3).ToList();
            player.FateDeck = player.FateDeck.Take(3).ToList();
        }
        var retained = state.Players.SelectMany(p => p.DynastyDeck.Concat(p.FateDeck)
            .Concat(p.Provinces.Where(v => v.CardId is not null).Select(v => v.CardId!)).Append(p.StrongholdId)).ToHashSet();
        retained.UnionWith(state.Cards.Values.Where(c => c.Zone == Zone.Home).Select(c => c.Id));
        foreach (var id in state.Cards.Keys.Where(id => !retained.Contains(id)).ToArray()) state.Cards.Remove(id);
        for (var player = 0; player < 2; player++)
            for (var index = 0; index < 2; index++)
            {
                var support = new CardInstance
                {
                    Id = $"nightly-support-{player}-{index}",
                    DefinitionId = "training-crane-samurai",
                    OwnerId = player,
                    Zone = Zone.Home,
                    FaceUp = true,
                    Printed = CardCatalog.Get("training-crane-samurai") with { Keywords = ["Samurai", "Shugenja", "Kensai", "Commander", "Courtier"] }
                };
                state.Cards.Add(support.Id, support);
            }
        state.Phase = phase;
        state.ActivePlayerId = owner;
        state.PriorityPlayerId = owner;
        if (card.Clan != "Unaligned") state.Players[owner].Clan = card.Clan;
        if (phase == Phase.Battle)
        {
            state.CurrentBattleProvince = 0;
            state.UnresolvedBattlefields = [0];
            foreach (var unit in state.Cards.Values.Where(c => c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality))
            {
                unit.Zone = Zone.Battlefield;
                unit.ProvinceIndex = 0;
            }
        }
        var instance = new CardInstance { Id = "nightly-card", DefinitionId = card.Id, OwnerId = owner, FaceUp = true };
        switch (card.Type)
        {
            case CardType.Stronghold:
                state.Cards[state.Players[owner].StrongholdId].DefinitionId = card.Id;
                return state;
            case CardType.Strategy:
                instance.Zone = Zone.Hand;
                state.Players[owner].Hand.Add(instance.Id);
                break;
            case CardType.Item:
            case CardType.Follower:
            case CardType.Spell:
                instance.Zone = Zone.Attached;
                instance.AttachedTo = $"nightly-support-{owner}-0";
                break;
            case CardType.Region:
                instance.Zone = Zone.Region;
                instance.ProvinceIndex = 0;
                state.Players[owner].Provinces[0].RegionId = instance.Id;
                break;
            case CardType.Event:
                instance.Zone = Zone.EventInPlay;
                break;
            case CardType.Celestial:
                instance.Zone = Zone.Celestial;
                break;
            default:
                instance.Zone = phase == Phase.Dynasty && card.Type is CardType.Personality or CardType.Holding ? Zone.Province
                    : phase == Phase.Battle && card.Type == CardType.Personality ? Zone.Battlefield : Zone.Home;
                instance.ProvinceIndex = instance.Zone == Zone.Battlefield ? 0 : null;
                if (instance.Zone == Zone.Province)
                {
                    instance.ProvinceIndex = 0;
                    state.Cards.Remove(state.Players[owner].Provinces[0].CardId!);
                    state.Players[owner].Provinces[0].CardId = instance.Id;
                }
                break;
        }
        state.Cards.Add(instance.Id, instance);
        return state;
    }

    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
