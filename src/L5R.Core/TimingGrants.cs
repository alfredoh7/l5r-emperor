namespace L5R.Core;

// Player-level timing grants (issue #352). A card in play can give its CONTROLLER an additional timing for an existing class of action, without
// touching the action itself. Foothills Keep is the first:
//
//   "Equipping Weapons is a Battle/Open action for you."
//
// Mendon's ruling: this ADDS a Battle/Open timing for equipping Weapons; it does not replace the player's Equip (Limited) action, which is unchanged
// (Action phase, the active player's own segment). The action class is "equipping a Weapon" (ActionClass.EquipWeapon); the grant names the timing
// the class gains. Nothing else about the action changes: the same legality (Personal Honor, "will only attach to", Armor/Weapon caps,
// Unique/Singular), the same cost through the Gold path, the same entry effects.
//
//   Open   the action may be taken in the Action phase whenever its controller holds priority -- in ANY player's action segment, not only their own
//          (that is what makes Open different from Limited). The wielder is a Personality at Home, exactly as for Equip (Limited).
//   Battle the action may be taken as a Battle action: in the Battle phase, with a unit at the current battlefield, on a wielder that is AT the current
//          battlefield. Mendon: "During Battle, the Personality must be at the current battlefield and the Foothills Keep unbowed" -- a grant whose
//          BattleRequiresUnbowedSource is set only works in a battle while the card that grants it is unbowed (out of battle it is a trait, so it is
//          not asked).
//
// The set of grants is data (EffectTimingGrants, keyed by the granting card's Effect), and the action classes are a name each equip-shaped action
// reports (EquipActionClass), so another card that changes when some action may be taken is one table row -- see the decision document for the
// unwired cards this unblocks.

/// <summary>The classes of action a timing grant can name.</summary>
public static class ActionClass
{
    /// <summary>Equipping a Weapon from your hand (the ordinary Equip command with a Weapon-keyword Item).</summary>
    public const string EquipWeapon = "equip-weapon";
}

/// <summary>An additional timing (<paramref name="Timing"/>: "Battle", "Open" or "BattleOrOpen") a card gives its controller for one class of action.</summary>
public sealed record TimingGrant(string ActionClass, string Timing, bool BattleRequiresUnbowedSource = false)
{
    public bool GrantsOpen => Timing is "Open" or "BattleOrOpen";
    public bool GrantsBattle => Timing is "Battle" or "BattleOrOpen";
}

public sealed partial class MatchEngine
{
    // Foothills Keep (oracle-l5r-2626): "Equipping Weapons is a Battle/Open action for you." Keyed by the granting card's Effect.
    private static readonly IReadOnlyDictionary<string, TimingGrant[]> EffectTimingGrants = new Dictionary<string, TimingGrant[]>(StringComparer.Ordinal)
    {
        ["equip-weapons-battle-open"] = [new(ActionClass.EquipWeapon, "BattleOrOpen", BattleRequiresUnbowedSource: true)],
    };

    // The class of action equipping this card is, when a timing grant can name it.
    private string? EquipActionClass(CardInstance attachment) => Keyword(attachment, "Weapon") ? ActionClass.EquipWeapon : null;

    // Every grant of this class that the player's cards in play give them, with the card that gives it.
    private IEnumerable<(CardInstance Source, TimingGrant Grant)> TimingGrantsOf(int playerId, string actionClass) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && EffectTimingGrants.ContainsKey(Definition(c).Effect))
            .SelectMany(c => EffectTimingGrants[Definition(c).Effect].Where(g => g.ActionClass == actionClass).Select(g => (c, g)))
            .OrderBy(x => x.Item1.Id, StringComparer.Ordinal);

    // Why this equip may not be taken right now (null when it may): the ordinary Equip (Limited) timing, or a granted Open or Battle timing.
    private string? EquipTimingError(int playerId, CardInstance attachment)
    {
        var actionClass = EquipActionClass(attachment);
        var grants = actionClass is null ? [] : TimingGrantsOf(playerId, actionClass).ToArray();
        if (_state.Phase == Phase.Action)
            return playerId == _state.ActivePlayerId || grants.Any(x => x.Grant.GrantsOpen) ? null : "Equip is a Limited action on your turn.";
        if (_state.Phase == Phase.Battle)
        {
            if (grants.Length == 0) return "Equip is not a Battle action.";
            if (_state.CurrentBattleProvince is not int province || !Army(playerId, province).Any()) return "You need a unit at the current battlefield to take a Battle action.";
            return grants.Any(x => x.Grant.GrantsBattle && (!x.Grant.BattleRequiresUnbowedSource || !x.Source.Bowed)) ? null
                : $"{Definition(grants[0].Source).Name} must be unbowed to equip during a battle.";
        }
        return "Equip is a Limited action on your turn.";
    }

    // The equip options QueryActions offers this player now, in the exact shape the Action phase always listed them (one quote per candidate wielder,
    // an unpayable one disabled with its reason), now for the Action phase's Open timing and for a Battle-phase grant as well.
    private IEnumerable<ActionOption> EquipActions(int playerId)
    {
        var battle = _state.Phase == Phase.Battle;
        if (_state.Phase != Phase.Action && !battle) yield break;
        var wielders = battle
            ? (_state.CurrentBattleProvince is int province ? Army(playerId, province).ToArray() : [])
            : _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Home && Definition(c).Type == CardType.Personality).ToArray();
        foreach (var card in Player(playerId).Hand.Select(Card).Where(c => Definition(c).Type is CardType.Item or CardType.Follower))
        {
            if (EquipTimingError(playerId, card) is not null) continue;
            var def = Definition(card);
            var legalTargets = wielders.Where(t => AttachmentAllowed(card, t) && AttachmentLegalTarget(def, t)).Select(t => t.Id).ToArray();
            // One Gold quote per candidate wielder (issue #303): Katagi's discount depends on the wielder, so the price can differ per target.
            var quotes = legalTargets.ToDictionary(id => id, id => QuoteEquipGold(playerId, card, Card(id)));
            var affordable = legalTargets.Where(id => quotes[id].Payments.Length > 0).ToArray();
            var reason = legalTargets.Length == 0 ? "No Personality meets the attachment requirements." : affordable.Length == 0 ? "Insufficient unbowed Gold production." : null;
            var first = affordable.FirstOrDefault();
            var firstQuote = first is null ? null : quotes[first];
            yield return new("equip", $"Equip {def.Name} · {firstQuote?.Cost ?? def.GoldCost} Gold{(battle ? " · Battle" : _state.ActivePlayerId == playerId ? "" : " · Open")}",
                reason is null, reason,
                reason is null ? new GameCommand("equip", playerId, _state.Version, card.Id, firstQuote!.Payments[0].SourceIds, TargetId: first) : null,
                card.Id, reason is null ? affordable : legalTargets, Gold: firstQuote,
                TargetGold: affordable.Any(id => quotes[id].Kind == GoldCostKind.Derived) ? affordable.ToDictionary(id => id, id => quotes[id]) : null);
        }
    }
}
