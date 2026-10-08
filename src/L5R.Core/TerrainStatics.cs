namespace L5R.Core;

/// <summary>A province strength bonus that lasts while its source stays in play (Hasty Fortifications: "Give this province +1 strength until this card leaves play"). It is part of the
/// province's CURRENT strength for as long as the source is in play and is simply not counted after, so nothing has to undo it.</summary>
public sealed record ProvinceBonus(string SourceId, int Amount);

// Terrain statics and province strength limits (issue #418, Mendon's rulings of 2026-09-21).
//
// A province has a CURRENT strength: the printed strength as raised and lowered by everything that has acted on it (Ring of Earth, a duel win, an Attack, Improved Defenses, a Region,
// a Terrain's bonus). A limit -- a MAXIMUM here -- does not stop the current strength from being raised: the raise is applied and the limit is enforced on what anything READS. So
// while the current strength is above the maximum every read is the maximum; below it, every read is the current strength; and when the limit ends (its Terrain leaves play, its Event
// ends) the province is back at its current strength, raises included. EffectiveProvinceStrength is that read. Times of Strife ("all Provinces have a maximum strength equal to
// their base strength") is the first limit and Ancient Feud the second, for the province at its battlefield until the battle is resolved.
//
// A Terrain is an ordinary card in play at a battlefield with a passive, non-optional trait, and so it is read live from where it stands: Shiotome Plains' Force adjustment is a
// function of the card and the Terrain at its battlefield, and Hasty Fortifications' province bonuses belong to it and are not counted once it is gone.
public sealed partial class MatchEngine
{
    // The Terrain in play at the battlefield at this province index, if any (a battlefield holds one Terrain unless a card says otherwise).
    private IEnumerable<CardInstance> TerrainsAtBattlefield(int provinceIndex) =>
        _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == provinceIndex && Keyword(c, "Terrain"));

    // The battlefield's province belongs to the Defender: this province is "at" a battlefield of the battle in progress.
    private bool ProvinceIsBattlefieldOfCurrentBattle(ProvinceState province) =>
        _state.Phase is Phase.Battle or Phase.BattleSelection && _state.Players[1 - _state.ActivePlayerId].Provinces.Contains(province);

    /// <summary>The highest strength anything may read the province as, or null when nothing limits it.</summary>
    private int? ProvinceStrengthMax(ProvinceState province)
    {
        int? max = null;
        void Cap(int value) => max = max is int current ? Math.Min(current, value) : value;
        // Times of Strife (issue #259): global, and a ceiling only.
        if (ControlsEventEffect(0, "times-of-strife") || ControlsEventEffect(1, "times-of-strife")) Cap(province.PrintedStrength);
        // Ancient Feud (issue #418): "This battlefield's province has a maximum strength equal to its base strength" -- while the Terrain is in play there.
        if (ProvinceIsBattlefieldOfCurrentBattle(province) && TerrainsAtBattlefield(province.Index).Any(t => Definition(t).Effect == "ancient-feud")) Cap(province.PrintedStrength);
        if (Wave734ProvinceCapped(province)) Cap(province.PrintedStrength); // Mumoku no Oni, Shosuro Tanihara (issue #734): "While attacking at a battlefield, its Province has a maximum strength equal to its printed strength."
        if (P4ProvinceCapped(province)) Cap(province.PrintedStrength); // The Snow Has Teeth (issue #503): "a maximum strength equal to its base strength until the end of the battle".
        return max;
    }

    // What the province's bonuses from cards still in play add to its current strength.
    private int ProvinceSourceBonuses(ProvinceState province)
    {
        var ownerId = _state.Players.First(p => p.Provinces.Contains(province)).Id;
        var ignoresOthers = ProvinceIgnoresOthersCards(ownerId); // P12 (ImmunityLedger.cs): other players' cards do not affect these provinces
        return province.SourceBonuses.Where(b => _state.Cards.TryGetValue(b.SourceId, out var source) && InPlay(source) && !(ignoresOthers && source.OwnerId != ownerId)).Sum(b => b.Amount);
    }

    // Test seam (L5R.Core.Tests has InternalsVisibleTo): a card's current Force, Personality or Follower.
    internal int CardForceForTest(string cardId) => CardForce(Card(cardId));

    // Shiotome Plains: "While at this battlefield, Cavalry cards have +1F and Infantry cards have -1F" -- applied at the card level to the Personalities and Followers at the battlefield
    // (Infantry being every card without the Cavalry keyword), lost the moment the Terrain leaves play. A Follower is at its host's battlefield.
    private int TerrainForceAdjustment(CardInstance card)
    {
        var battlefield = card.Zone == Zone.Battlefield ? card.ProvinceIndex : card.Zone == Zone.Attached && card.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) && host.Zone == Zone.Battlefield ? host.ProvinceIndex : null;
        if (battlefield is not int index) return 0;
        var attackerPenalty = HoldTheWallsForceAdjustment(card, index); // Hold the Walls (issue #622, TfStrategiesA.cs): "Attacking Personalities and Followers have -1F."
        if (!TerrainsAtBattlefield(index).Any(t => Definition(t).Effect == "shiotome-plains")) return attackerPenalty;
        return attackerPenalty + (Keyword(card, "Cavalry") ? 1 : -1);
    }

    // Hasty Fortifications: "After each resolution of one of your Battle actions (including this card's): Give this province +1 strength until this card leaves play." Each resolution adds
    // one, so they stack; they are the Terrain's, and go with it -- destroyed, or discarded when the battle is resolved.
    private void FireAfterOwnBattleActionTerrainTraits(AbilityDefinition ability, int actingPlayerId)
    {
        if (ability.Timing != "Battle" && !(ability.Timing == "BattleOrOpen" && _state.Phase == Phase.Battle)) return;
        foreach (var hasty in _state.Cards.Values.Where(c => c.OwnerId == actingPlayerId && c.Zone == Zone.Battlefield && Definition(c).Effect == "hasty-fortifications").ToArray())
            AddHastyBonus(hasty);
    }

    private void AddHastyBonus(CardInstance hasty)
    {
        if (hasty.ProvinceIndex is not int index) return;
        var province = BattlefieldProvince(index); // a battlefield without a province has none to strengthen (issue #492, rulings pass R1)
        if (province is null) return;
        province.SourceBonuses.Add(new ProvinceBonus(hasty.Id, 1));
        Emit("provinceStrengthRaised", $"{Definition(hasty).Name}: province {index + 1} has +1 strength until it leaves play.", hasty.OwnerId, hasty.Id);
    }
}
