namespace L5R.Core;

/// <summary>Detached public facts, calculated by the same engine path used to resolve the current battle.</summary>
public sealed record BattleValuesView(int AttackerId, int DefenderId, int AttackerForce, int DefenderForce,
    int AttackerUnits, int DefenderUnits, int? ProvinceStrength);

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
        return new(attacker, defender, forces.Attacker, forces.Defender, a.Length, d.Length,
            province is int p ? EffectiveProvinceStrength(Player(defender).Provinces[p]) : null);
    }
}
