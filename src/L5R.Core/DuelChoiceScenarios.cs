namespace L5R.Core;

public sealed partial class MatchEngine
{
    // Deterministic review positions only; no changes to decks or live legality.
    private static void ConfigureDuelChoiceScenario(MatchState state, string id, Func<int, string, Zone, string?, CardInstance> add)
    {
        var focus = id switch
        {
            "choice-attach" => "oracle-l5r-6702",
            "choice-straighten" => "oracle-l5r-10139",
            "choice-destroy" => "oracle-l5r-8002",
            "choice-effect" => "oracle-l5r-10123",
            _ => "oracle-l5r-2031"
        };
        foreach (var card in state.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Hand && c.DefinitionId != "oracle-l5r-7483").ToArray())
        { state.Players[0].Hand.Remove(card.Id); state.Cards.Remove(card.Id); }
        add(0, focus, Zone.Hand, null);
        if (id == "choice-multiple") add(0, focus, Zone.Hand, null);
        add(0, "oracle-l5r-1092", Zone.FateDiscard, null);
        add(0, "oracle-l5r-3779", Zone.FateDiscard, null);
        if (id == "choice-crowded") for (var i = 0; i < 8; i++) add(0, i % 2 == 0 ? "oracle-l5r-1092" : "oracle-l5r-3779", Zone.FateDiscard, null);
        var home = add(0, "oracle-l5r-2166", Zone.Home, null);
        if (id == "choice-straighten") home.Bowed = true;
    }
}
