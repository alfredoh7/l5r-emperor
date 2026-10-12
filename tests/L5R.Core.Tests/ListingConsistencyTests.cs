using L5R.Core;

// Issue #842 (stability): QueryActions and ValidateAbility each carry their own switch over performer and target categories, and the #830/#833 audits found about ten places where they drifted apart.
// The invariant, checked for every Battle / Open / Limited ability of every card in four windows: whatever the validator would accept -- a (source, performer, target) combination -- the listing must
// offer as an enabled option. Forged commands carry no payment, so a Gold-costing action the validator refuses for want of a payment is simply not examined (no false alarm, some missed cases).
internal static partial class ListingConsistencyTests
{
    public record Gap(string Card, string Ability, string Scenario, string Performer, string Target, string Why = "");

    // Not a disagreement about the target: the option carries the payment chosen for its first target (another target can cost more), or the action asks the player for a further selection.
    private static bool IsPaymentOrPrompt(string why) => why.StartsWith("This payment needs", StringComparison.Ordinal) || why.StartsWith("No Gold sources", StringComparison.Ordinal)
        || why.Contains("cannot also be bowed for its Gold", StringComparison.Ordinal) || why.Contains("Select province cards", StringComparison.Ordinal) || why.Contains("face-down province", StringComparison.Ordinal);

    public static int Examined;

    public static List<Gap> Find(IEnumerable<CardDefinition> cards)
    {
        var gaps = new List<Gap>(); Examined = 0;
        foreach (var card in cards)
            foreach (var scenario in MultiWindowAuditTests.Scenarios)
                for (var variant = 0; variant < (card.Type is CardType.Spell or CardType.Ring ? 2 : 1); variant++)
                {
                    MatchEngine e; CardInstance source; int me;
                    try { (e, source, me) = MultiWindowAuditTests.Build(card, scenario, variant); } catch { continue; }
                    var state = e.State;
                    var inPlay = state.Cards.Values.Where(c => c.Zone is not (Zone.DynastyDeck or Zone.FateDeck or Zone.Stronghold)).ToList();
                    // One representative per distinct (definition, owner, zone, bowed): the many identical Gold sources add no coverage.
                    var distinct = inPlay.GroupBy(c => (c.DefinitionId, c.OwnerId, c.Zone, c.Bowed, c.ProvinceIndex is not null, c.AttachedTo is not null)).Select(g => g.First()).ToList();
                    var personalities = distinct.Where(c => c.OwnerId == me && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality).ToList();
                    var options = e.QueryActions(me).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId is not null).ToList();
                    // The reverse: every target an enabled option names must itself be accepted by the validator for that option's own command.
                    foreach (var option in options.Where(o => o.CardId == source.Id && o.TargetIds is { Length: > 0 }))
                        foreach (var targetId in option.TargetIds!)
                            if (e.AbilityRejectionForTest(option.Command! with { TargetId = targetId }) is { } why && !IsPaymentOrPrompt(why))
                                gaps.Add(new(card.Name, option.Command!.AbilityId!, scenario, option.Command.PerformerId ?? "", state.Cards.TryGetValue(targetId, out var t) ? CardCatalog.Get(t.DefinitionId).Name : targetId, "offered target refused: " + why));
                    foreach (var id in card.Abilities ?? [])
                    {
                        if (!AbilityCatalog.All.TryGetValue(id, out var ability) || ability.Timing is not ("Battle" or "Open" or "Limited" or "BattleOrOpen") || ability.Target is "slots") continue;
                        var performers = ability.Performer is null ? new List<CardInstance?> { null } : personalities.Cast<CardInstance?>().ToList();
                        var targets = ability.Target is "none" ? new List<CardInstance?> { null } : distinct.Cast<CardInstance?>().ToList();
                        foreach (var performer in performers)
                            foreach (var target in targets)
                            {
                                var command = new GameCommand("useAbility", me, e.Version, CardId: source.Id, AbilityId: id, PerformerId: performer?.Id, TargetId: target?.Id);
                                if (e.AbilityRejectionForTest(command) is not null) continue;
                                Examined++;
                                var offered = options.Any(o => o.CardId == source.Id && o.Command!.AbilityId == id
                                    && (performer is null || o.Command.PerformerId == performer.Id || o.Label.Contains($"({performer.Id})"))
                                    && (target is null || o.Command.TargetId == target.Id || o.TargetIds?.Contains(target.Id) == true));
                                if (!offered) gaps.Add(new(card.Name, id, scenario, performer is null ? "" : CardCatalog.Get(performer.DefinitionId).Name, target is null ? "" : CardCatalog.Get(target.DefinitionId).Name));
                            }
                    }
                }
        return gaps;
    }

    private static void ValidatedMeansListed()
    {
        var gaps = Find(CardCatalog.Cards.Values.Where(c => !c.CreatedOnly).OrderBy(c => c.Id, StringComparer.Ordinal));
        Console.WriteLine($"listing-consistency: {Examined} validator-accepted combinations examined, {gaps.Count} not offered");
        var path = Environment.GetEnvironmentVariable("L5R_CONSISTENCY_OUT");
        if (path is not null) File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(gaps));
        var byAbility = gaps.GroupBy(g => (g.Card, g.Ability)).Select(g => $"{g.Key.Card} {g.Key.Ability} ({g.Count()}: e.g. {g.First().Scenario}, performer '{g.First().Performer}', target '{g.First().Target}' {g.First().Why})").ToList();
        Batch336Support.Check(gaps.Count == 0, $"{byAbility.Count} abilities disagree between the validator and QueryActions (accepted-but-not-offered, or offered target refused): " + string.Join("; ", byAbility.Take(40)));
    }

    public static void Run(TestRunner runner) => runner.Run("listing-consistency", [
        ("Whatever the validator accepts for a Battle / Open / Limited action -- source, performer, target -- QueryActions offers as an enabled option, in every window (issue #842)", ValidatedMeansListed),
    ]);
}
