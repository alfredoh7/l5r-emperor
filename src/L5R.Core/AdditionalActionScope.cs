namespace L5R.Core;

// Primitive P17 (issue #721, docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md): additional-action scope by ability.
//
//   ScopeAbilityIds     AdditionalActionGrant.ScopeAbilityIds (appended last, passed by name): the grant is spent only by a command that uses one of the named abilities ("you may take an additional action to
//                       use his second printed ability", Mirumoto Kalen - exp). It joins the other restricted grants: while a player holds nothing but restricted grants the only actions he may take are the ones
//                       they allow (RequireRestrictedBurstAllows; an Equip or a Terrain play in a battle: RequireNoRestrictedBurst), and an ordinary action forfeits them as always.
//   ACTION-PHASE EQUIP  Equip had no part in the Action Phase's additional actions: it neither spent a grant nor forfeited one. SpendEquipGrant makes it an action like any other there: it spends a grant that
//                       admits it (an unscoped one, or one scoped to the equipped card) and otherwise forfeits the held burst. A grant scoped to abilities never admits an Equip, and while only such grants are
//                       held an Equip is rejected, as in a battle.

public sealed partial class MatchEngine
{
    /// <summary>ConsumeAdditionalActionGrant's condition for a grant scoped to named abilities: the command uses one of them. A grant without the scope admits everything this term is asked about.</summary>
    private static bool AbilityScopeGrantMatches(AdditionalActionGrant grant, GameCommand? command) =>
        grant.ScopeAbilityIds is not { } ids || command?.AbilityId is { } abilityId && ids.Contains(abilityId);

    private static string ScopeAbilityMessage(AdditionalActionGrant grant) => "The additional action must use one of the abilities this effect names.";

    /// <summary>True when the player holds a burst and every grant in it is scoped to named abilities (so a command that names no ability, an Equip, can never spend one).</summary>
    private bool HoldsOnlyAbilityScopedBurst(int playerId)
    {
        var held = _state.AdditionalActions.Where(g => g.PlayerId == playerId && Forfeitable(g) && GrantUsableNow(g)).ToArray();
        return held.Length > 0 && held.All(g => g.ScopeAbilityIds is not null);
    }

    /// <summary>An Equip taken in the Action Phase: spends a grant of its player's that admits it, otherwise forfeits the held burst (as any ordinary action does). Returns true when a grant was spent and
    /// another is still held, so its holder keeps priority. A battle Equip keeps its own code in Equip.</summary>
    private bool SpendEquipGrant(GameCommand command, CardInstance card)
    {
        if (_state.Phase == Phase.Battle || !_state.AdditionalActions.Any(g => g.PlayerId == command.PlayerId && Forfeitable(g))) return false;
        var used = ConsumeAdditionalActionGrant(command.PlayerId, card.Id, command);
        if (!used) _state.AdditionalActions.RemoveAll(g => g.PlayerId == command.PlayerId && Forfeitable(g));
        return used && _state.AdditionalActions.Any(g => g.PlayerId == command.PlayerId && Forfeitable(g));
    }
}
