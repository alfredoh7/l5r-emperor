namespace L5R.Core;

/// <summary>Detached public facts, calculated by the same engine path used to resolve the current battle.</summary>
public sealed record BattleValuesView(int AttackerId, int DefenderId, int AttackerForce, int DefenderForce,
    int AttackerUnits, int DefenderUnits, int? ProvinceStrength, bool ProvinceFalls = false);

public sealed partial class MatchEngine
{
    private BattleValuesView CurrentBattleValues(int battlefield)
    {
        var attacker = _state.ActivePlayerId;
        var defender = 1 - attacker;
        var a = Army(attacker, battlefield).ToArray();
        var d = Army(defender, battlefield).ToArray();
        var forces = ArmyForces(a, d, attacker, defender);
        var province = BattlefieldProvinceIndex(battlefield);
        int? strength = province is int p ? EffectiveProvinceStrength(Player(defender).Provinces[p]) : null;
        // Resolution's own test (MatchEngine battle resolution): the attacker's Force must exceed the defender's by MORE than the
        // Province Strength, and none of the bars to destruction applies. Evaluated as though both players pass from here.
        var falls = strength is int s && forces.Attacker > forces.Defender + s && !AramasuResolutionActive(attacker)
            && !PerfectMomentBarsDestruction(attacker) && !RendBarsProvinceDestruction(attacker) && !RendTheSoulTiesBattle(a, d, true, false);
        return new(attacker, defender, forces.Attacker, forces.Defender, a.Length, d.Length, strength, falls);
    }
}
