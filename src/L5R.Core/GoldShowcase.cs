namespace L5R.Core;

public sealed partial class MatchEngine
{
    /// <summary>Fixture matches for verifying the generalized Gold payment panel (issue #303) in the real
    /// native client, one per cost kind. They are ordinary engine states (nothing is faked in the client), built
    /// only for the scripted capture/smoke harness: no menu, room or deck can reach them.
    /// "fixed": Kharmic's fixed 2 Gold ability. "derived": Equip (Battle) with three Spells priced 0, 1 and 2
    /// (the cost follows the target). "variable": a player-chosen amount ("pay zero or more Gold").</summary>
    public static MatchEngine CreateGoldPaymentShowcase(string kind)
    {
        var s = CreateEconomyScenario().State;
        s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        CardInstance Add(string definition, int owner, Zone zone, int? province = null)
        {
            var card = new CardInstance { Id = $"showcase-{s.Cards.Count}", DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, ProvinceIndex = province };
            s.Cards.Add(card.Id, card);
            if (zone == Zone.Hand) s.Players[owner].Hand.Add(card.Id);
            return card;
        }
        // A published Stronghold turns on the player abilities (Kharmic, Equip (Battle)); a few Marketplaces give
        // the payment panel several sources and alternative payments to choose between.
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        for (var i = 0; i < 4; i++) Add("training-marketplace", 0, Zone.Home);
        switch (kind)
        {
            case "fixed":
            {
                var province = s.Cards[s.Players[0].Provinces[0].CardId!];
                province.DefinitionId = "training-kharmic-dynasty"; province.FaceUp = true;
                Add("training-kharmic-fate", 0, Zone.Hand);
                break;
            }
            case "derived":
            {
                s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
                Add("training-shugenja", 0, Zone.Battlefield, 0);
                Add("training-lion-veteran", 1, Zone.Battlefield, 0);
                Add("oracle-l5r-10133", 0, Zone.Hand); // Suitengu's Path, cost 0
                Add("oracle-l5r-9889", 0, Zone.Hand);  // The Earth's Hunger, cost 1
                Add("oracle-l5r-6502", 0, Zone.Hand);  // Scouring Flood, cost 2
                break;
            }
            case "variable":
                Add("training-cost-fixture", 0, Zone.Home);
                break;
            default:
                throw new ArgumentException("Unknown Gold payment showcase.", nameof(kind));
        }
        return FromTestState(s);
    }
}
