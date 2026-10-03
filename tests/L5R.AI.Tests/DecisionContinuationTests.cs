using L5R.Core;
using L5R.AI;
using System.Reflection;
using System.Text.Json;

internal static class DecisionContinuationTests
{
    public static void Run()
    {
        foreach (var kind in new[] { "brash", "bow", "before" })
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            s.Phase = kind == "brash" ? Phase.InfantryDeclaration : Phase.Battle; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
            s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
            CardInstance Add(string def, int owner, Zone zone)
            {
                var c = new CardInstance { Id = "ai-continuation-" + s.Cards.Count, DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = true, ProvinceIndex = zone == Zone.Battlefield ? 0 : null }; s.Cards[c.Id] = c; return c;
            }
            GameCommand trigger;
            if (kind == "brash")
            {
                var card = Add("training-brash-personality", 0, Zone.Home);
                trigger = new("attack", 0, s.Version, Assignments: new() { [card.Id] = 0 });
            }
            else
            {
                var target = Add("training-lion-samurai", 1, Zone.Battlefield);
                Add(kind == "bow" ? "oracle-l5r-8521" : "training-rokai", 1, Zone.Battlefield);
                var source = Add(kind == "bow" ? "training-battle-bower" : "training-no-attachments-destroyer", 0, Zone.Battlefield);
                trigger = new("useAbility", 0, s.Version, CardId: source.Id, TargetId: target.Id, AbilityId: kind == "bow" ? "training-bow-at-battle" : "training-destroy-no-attachments");
            }
            var e = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [s])!;
            if (!e.Execute(trigger).Accepted) throw new Exception("Fixture trigger rejected");
            for (var n = 0; e.State.PendingChoice is not null && n < 8; n++)
            {
                var seat = e.State.PriorityPlayerId;
                var view = JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(e.GetView(seat)))!;
                var actions = JsonSerializer.Deserialize<ActionOption[]>(JsonSerializer.Serialize(e.QueryActions(seat)))!;
                var command = TrainingOpponent.Choose(view, actions).Command ?? throw new Exception(kind + ": AI has no continuation");
                if (!actions.Any(a => a.Enabled && a.Command is { } c && c.Type == command.Type && c.AbilityId == command.AbilityId && c.CardId == command.CardId)) throw new Exception("AI invented a continuation");
                var result = e.Execute(command); if (!result.Accepted) throw new Exception(kind + ": " + result.Error);
            }
            if (e.State.PendingChoice is not null) throw new Exception(kind + ": AI left a stuck decision");
        }
    }
}
