namespace L5R.Core;

// Emperor Edition Gempukku pass (issue #461): execution/validation helpers for the set's Personalities. Each is documented at its own use in CardAbilities.cs.
public sealed partial class MatchEngine
{
    private const string NegishiShieldEffect = "negishi-destruction-shield";

    // The Personality performing the action now in progress: the running command's own performer, or a pending Attack's source.
    private CardInstance? CurrentActionPerformer() =>
        _runningCommand is { } running ? ActionPerformingPersonality(running)
        : _state.PendingAttack?.SourceId is { } attackSource && _state.Cards.TryGetValue(attackSource, out var attacker) ? ActingPersonality(attacker) : null;

    // Daigotsu Negishi - exp (oracle-l5r-10227): "Negate Negishi's destruction from effects of actions performed by any Personality whose unit's total Gold Cost was lower than Negishi's
    // unit's (when they performed it)." Read where an action's effect destroys a card (DestroyUnits with an acting player); a battle's own resolution is not an action's effect. The totals are
    // read as the destruction resolves, which for one action's effects is the moment it was performed.
    private bool NegishiNegatesDestruction(CardInstance card)
    {
        if (Definition(card).Effect != NegishiShieldEffect || CurrentActionPerformer() is not { } performer) return false;
        if (UnitTotalGoldCost(performer, out _) >= UnitTotalGoldCost(card, out _)) return false;
        Emit("destructionNegated", $"{Definition(card).Name}'s destruction was negated: the performing unit's total Gold Cost was lower than his own.", card.OwnerId, card.Id);
        return true;
    }

    // Jade's Roar - exp: Daidoji Yuki's Recon grant and first-opportunity claim, the claim carrying the "must be a Terrain action" limit on the claimant's first action.
    private void JadesRoarRecon(int playerId, CardInstance source)
    {
        GrantReconnaissanceAtCurrentBattlefield(playerId);
        ClaimFirstOpportunity(playerId, "Engage", source);
        _state.PendingEngage!.FirstActionKeyword = "Terrain";
        _state.PendingEngage.FirstActionKeywordPlayerId = playerId;
    }

    // Shosuro Hawado - exp 2: "Target a card in a province: Discard it. Refill the province face-up." The card leaves its province face-up into its owner's Dynasty discard pile, then the
    // top of that owner's Dynasty deck refills the province face-up (a Destroyed province, or an empty deck, refills nothing).
    private void DiscardProvinceCardAndRefillFaceUp(CardInstance target, CardInstance source)
    {
        var owner = Player(target.OwnerId);
        var province = owner.Provinces.Single(p => p.CardId == target.Id);
        target.Zone = Zone.DynastyDiscard; target.ProvinceIndex = null; target.FaceUp = true; province.CardId = null;
        Emit("provinceDiscard", $"{Definition(target).Name} was discarded from {owner.Name}'s province {province.Index + 1} ({Definition(source).Name}).", owner.Id, target.Id);
        if (province.Destroyed || owner.DynastyDeck.Count == 0) return;
        var refillId = owner.DynastyDeck[0]; owner.DynastyDeck.RemoveAt(0);
        var refill = Card(refillId);
        province.CardId = refillId; refill.Zone = Zone.Province; refill.ProvinceIndex = province.Index; refill.FaceUp = true;
        Emit("provinceRefilled", $"{owner.Name} refilled province {province.Index + 1} face up with {Definition(refill).Name}.", owner.Id, refillId);
    }
}
