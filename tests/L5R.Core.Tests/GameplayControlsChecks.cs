using L5R.Core;
using System.Text.Json;

internal static class GameplayControlsChecks
{
    public static void SourcesAndPrivacy()
    {
        foreach (var kind in new[] { "reaction", "destruction", "discount", "entry", "event", "honor", "honor-reaction", "killing", "infiltrator", "greasing", "favor", "additional" })
        {
            var engine = MatchEngine.CreateGameplayControlsShowcase(kind);
            var seat = engine.State.PriorityPlayerId;
            var options = engine.QueryActions(seat);
            if (!options.Any(a => a.Enabled && a.Type != "concede")) throw new Exception("No offered choice: " + kind);
            foreach (var option in options.Where(a => a.Gold is not null))
            {
                var restored = JsonSerializer.Deserialize<GoldQuote>(JsonSerializer.Serialize(option.Gold))!;
                if ((restored.Sources ?? []).Any(m => m.Produced < 0) ||
                    restored.Sources?.Select(m => m.Id).Distinct().Count() != restored.Sources?.Length)
                    throw new Exception("Invalid or duplicate quote metadata");
                if (kind == "reaction" && restored.Sources?.Any(m => m.Kind == "reaction") == true)
                {
                    var methods = restored.Sources!;
                    var reaction = methods.Single(m => m.Kind == "reaction");
                    var bow = methods.Single(m => m.CardId == reaction.CardId && m.Kind == "bow");
                    if (bow.Id == reaction.Id || bow.Produced != 2 || reaction.Produced != 1 || reaction.ConflictingSourceIds!.Contains(bow.Id))
                        throw new Exception("Bow and Reaction were conflated");
                }
                if (kind == "destruction" && restored.Sources?.All(m => m.Kind != "destroy") == true) throw new Exception("Missing destruction methods");
            }
            var expected = kind switch { "entry" => "entryCostDecision", "honor" => "honorLossDecision", "honor-reaction" => "honorChangeReaction", "event" => "eventChoice", "killing" => "killingGroundsSelect", "infiltrator" => "infiltratorStraighten", _ => null };
            if (expected is not null && engine.State.PendingChoice?.Type != expected) throw new Exception(kind + " did not open " + expected);
            if (kind == "entry")
            {
                var choices = options.Where(a => a.Type == "entryCostChoice").ToArray();
                if (!choices.Any(a => a.DecisionCandidateIds?.Length == 1) || engine.QueryActions(1 - seat).Any(a => a.DecisionCandidateIds?.Length > 0))
                    throw new Exception("Entry candidates missing or disclosed");
                var answer = choices.First(a => a.Command!.Choice!.StartsWith("remove:", StringComparison.Ordinal));
                var id = answer.DecisionCandidateIds!.Single();
                if (engine.GetView(1 - seat).Players[seat].Hand.Any(c => c.Id == id)) throw new Exception("Private candidate face leaked");
                var result = engine.Execute(answer.Command!);
                if (!result.Accepted || engine.State.Cards[id].Zone != Zone.RemovedFromGame) throw new Exception("Entry answer did not remove the selected card");
                if (engine.Execute(answer.Command!).Accepted) throw new Exception("Stale entry answer accepted");
            }
        }
    }
}
