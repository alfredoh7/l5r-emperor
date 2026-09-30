namespace L5R.Core;

public sealed partial class MatchEngine
{
    /// <summary>Isolated real-engine review positions. Never injected into decks or online rooms.</summary>
    public static MatchEngine CreateDecisionShowcase(string kind)
    {
        if (kind is "preserving" or "rin" or "conqueror" or "straighten" or "discard") return CreateGenericDecisionShowcase(kind);
        if (kind is not ("yumaru" or "fukui" or "fukui-bowed")) throw new ArgumentException("Unknown decision showcase.", nameof(kind));
        var state = CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Battle; state.ActivePlayerId = 0; state.PriorityPlayerId = 0;
        state.CurrentBattleProvince = 0; state.UnresolvedBattlefields = [0]; state.PendingChoice = null;
        CardInstance Put(string definition, int owner, Zone zone, string? parent = null)
        {
            var card = new CardInstance { Id = "decision-review-" + state.Cards.Count, DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = true, ProvinceIndex = 0, AttachedTo = parent };
            state.Cards.Add(card.Id, card); return card;
        }
        var enemy = Put("oracle-l5r-3636", 1, Zone.Battlefield);
        var source = Put(kind == "yumaru" ? "oracle-l5r-5181" : "oracle-l5r-4375", 0, Zone.Battlefield);
        if (kind == "yumaru") Put("oracle-l5r-6481", 0, Zone.Attached, source.Id);
        else Put("oracle-l5r-9785", 0, Zone.Battlefield).Bowed = kind == "fukui-bowed";
        var engine = FromTestState(state);
        if (kind == "yumaru")
        {
            var action = engine.QueryActions(0).Single(a => a.Enabled && a.CardId == source.Id && a.Command?.AbilityId == "yumaru-move-home");
            var result = engine.Execute(action.Command! with { TargetId = enemy.Id });
            if (!result.Accepted || engine.State.PendingChoice?.Type != "effectChoice") throw new InvalidOperationException("Yumaru showcase failed to open its engine choice.");
        }
        return engine;
    }
    private static MatchEngine CreateGenericDecisionShowcase(string kind)
    {
        var state = CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Battle; state.ActivePlayerId = state.PriorityPlayerId = 0;
        state.CurrentBattleProvince = 0; state.UnresolvedBattlefields = [0]; state.PendingChoice = null;
        CardInstance Put(string definition, int owner, Zone zone)
        {
            var card = new CardInstance { Id = "generic-review-" + state.Cards.Count, DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = true, ProvinceIndex = zone == Zone.Battlefield ? 0 : null };
            state.Cards.Add(card.Id, card); if (zone == Zone.Hand) state.Players[owner].Hand.Add(card.Id); return card;
        }
        void Execute(MatchEngine e, GameCommand command)
        { if (!e.Execute(command).Accepted) throw new InvalidOperationException("Generic decision fixture trigger failed: " + kind); }
        if (kind == "straighten")
        {
            state.Phase = Phase.Dynasty;
            Put("training-choose-straighten-samurai", 1, Zone.Home).Bowed = true;
            var e = FromTestState(state); Execute(e, new("endDynasty", 0, e.Version)); return e;
        }
        if (kind == "discard")
        {
            state.Phase = Phase.Action; state.CurrentBattleProvince = null;
            state.PendingForcedDiscards.Add(new(0, "Daigotsu Bofana"));
            var e = FromTestState(state); e.OpenOverconfidentDrawWindow(1); return e;
        }
        if (kind == "conqueror")
        {
            foreach (var id in new[] { "oracle-l5r-5181", "oracle-l5r-2060" })
            { var c = Put(id, 0, Zone.Battlefield); c.GrantedKeywords.Add(new("Conqueror", "review fixture", "Turn")); }
            Put("training-lion-samurai", 1, Zone.Battlefield).Bowed = true;
            var e = FromTestState(state); Execute(e, new("pass", 0, e.Version)); Execute(e, new("pass", 1, e.Version)); return e;
        }
        var target = Put("oracle-l5r-3636", 1, kind == "rin" ? Zone.Home : Zone.Battlefield);
        var source = Put(kind == "rin" ? "oracle-l5r-7151" : "oracle-l5r-6060", 0, kind == "rin" ? Zone.Home : Zone.Hand);
        if (kind == "rin") { state.Phase = Phase.Action; state.CurrentBattleProvince = null; }
        else Put("oracle-l5r-2060", 0, Zone.Battlefield);
        var engine = FromTestState(state);
        Execute(engine, new("useAbility", 0, engine.Version, CardId: source.Id, TargetId: target.Id, AbilityId: kind == "rin" ? "rin-shame" : "preserving-honor")); return engine;
    }

}
