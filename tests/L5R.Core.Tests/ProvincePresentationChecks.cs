using System.Text.Json;
using L5R.Client;
using L5R.Core;

public static class ProvincePresentationChecks
{
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void SlotsAndSerialization()
    {
        var view = MatchEngine.CreatePublishedMatch(91).GetView(0);
        var original = view.Players[0].Provinces[0];
        Require(ProvincePresentation.Slots(original) is [{ Index: 0 }] && ProvincePresentation.Slots(original)[0].Card == original.Card,
            "Legacy payload lost its original card/slot.");
        var hidden = new CardView("hidden-p0-province0-slot1", null, 0, Zone.Province, false, false, 0, null, 0);
        var multi = original with { DynastySlots = [new(0, null), new(1, hidden)] };
        var roundTrip = JsonSerializer.Deserialize<ProvinceView>(JsonSerializer.Serialize(multi))!;
        Require(ProvincePresentation.Slots(roundTrip) is [{ Index: 0, Card: null }, { Index: 1, Card.Definition: null }], "Empty/hidden slots did not survive transport.");
        Require(ProvincePresentation.Cards(roundTrip).Count() == 1 && ProvincePresentation.Cards(roundTrip).Single().Id == hidden.Id,
            "Legacy Card was duplicated after explicit slot payload replaced it.");
        Require(ProvincePresentation.Slots(original with { HoldsNoDynastyCards = true }).Length == 0, "No-Dynasty province gained an inferred slot.");
        Require(ProvincePresentation.Cards(original with { DynastySlots = [new(0, hidden)] }).Single().Id == hidden.Id,
            "Explicit single hidden slot inherited a legacy face.");
        Require(!ProvincePresentation.Cards(original with { DynastySlots = [new(0, null)] }).Any(),
            "Explicit single empty slot inherited a legacy face.");
        var oldJson = "{\"Index\":0,\"Strength\":5,\"Destroyed\":false,\"Card\":null,\"Region\":null}";
        Require(ProvincePresentation.Slots(JsonSerializer.Deserialize<ProvinceView>(oldJson)!).Length == 1, "Older wire payload rejected.");
    }

    public static void BattleReceipts()
    {
        var view = MatchEngine.CreatePublishedMatch(91).GetView(0);
        var definition = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly);
        var a = new CardView("receipt-a", definition, 0, Zone.Province, true, false, 1, null, definition.Force);
        var b = a with { Id = "receipt-b" };
        var before = view with { Phase = Phase.Battle, Events = [], Players = view.Players.Select(p => p.Id != 0 ? p : p with
        { Provinces = p.Provinces.Select(province => province.Index != 1 ? province : province with
            { Card = null, DynastySlots = [new(0,a),new(1,b)] }).ToArray() }).ToArray() };
        var after = before with { Version = before.Version + 1, Events = [new(1,"battleResolved","Battle resolved")], Players = before.Players.Select(p => p.Id != 0 ? p : p with
        { Provinces = p.Provinces.Select(province => province.Index != 1 ? province : province with { Destroyed = true, DynastySlots = [] }).ToArray(),
            Discard = [..p.Discard, a with { Zone = Zone.DynastyDiscard }, b with { Zone = Zone.DynastyDiscard }] }).ToArray() };
        var journal = new BattleJournal(); journal.Observe(before,null); journal.Observe(after,before);
        Require(journal.Aftermath.Count(r => r.CardId is "receipt-a" or "receipt-b") == 2, "Battle history omitted a province slot.");
    }

    public static void DecisionFreshness()
    {
        var view = MatchEngine.CreatePublishedMatch(91).GetView(0);
        var command = new GameCommand("engineOrder", 0, view.Version, Choice: "engine-opaque-choice", Province: 1);
        var option = new ProvinceOrderOption("b-a", "B then A", [1,0], command);
        view = view with { PendingProvinceOrder = new("decision-1", 0, 0, 1, "Choose the order", [option]) };
        var copy = JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(view))!;
        Require(ProvincePresentation.CurrentOption(copy, "decision-1", view.Version, "b-a")?.Command == command,
            "Decision did not retain the exact engine command.");
        Require(ProvincePresentation.CurrentOption(copy with { ViewerId = 1 }, "decision-1", view.Version, "b-a") is null, "Other seat may confirm.");
        Require(ProvincePresentation.CurrentOption(copy, "old-decision", view.Version, "b-a") is null, "Stale decision accepted.");
        Require(ProvincePresentation.CurrentOption(copy with { Version = view.Version + 1 }, "decision-1", view.Version, "b-a") is null, "Stale version accepted.");
        Require(ProvincePresentation.CurrentOption(copy with { PendingProvinceOrder = null }, "decision-1", view.Version, "b-a") is null, "Completed decision accepted twice.");
        Require(ProvincePresentation.CurrentOption(copy, "decision-1", view.Version, "invented") is null, "Unoffered order accepted.");
        foreach (var invalid in new[] { option with { Enabled = false }, option with { Command = null }, option with { Command = command with { PlayerId = 1 } }, option with { Command = command with { ExpectedVersion = view.Version - 1 } } })
            Require(ProvincePresentation.CurrentOption(copy with { PendingProvinceOrder = copy.PendingProvinceOrder! with { Options = [invalid] } }, "decision-1", view.Version, "b-a") is null,
                "Disabled, missing, wrong-seat or stale command accepted.");
    }
}
