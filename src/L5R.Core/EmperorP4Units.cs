namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, GROUP P4 (continued): ASSIGNMENT AFTER ENGAGING AND AN ATTACH TO A TARGETED PERSONALITY FROM THE HAND.
//
//   * AN ASSIGNMENT OUTSIDE THE MANEUVERS SEGMENTS (Shinjo Hayan: "After engaging: Once per turn, you may assign your (unbowed) Unicorn Clan Personality in your home to the current battlefield"):
//     the rulings pass R1's assignment to a created battlefield (AssignToCreatedBattlefield) -- an ASSIGNMENT, not a movement (core rules), with every assignment restriction and the same "after
//     he is assigned" Reactions and watches as the Maneuvers Segments -- used on the current battlefield, on its player's side (attacking when he is the Attacker).
//   * AN ATTACH FROM THE HAND TO A TARGETED PERSONALITY (Ikoma Shinohara): Daigotsu Gyoken's two-slot attach (AttachWielder.PersonalitySlot) with the hand pile (AttachPile.OwnHand, Moto
//     Hailung's), paying all costs through the Gold path (GoldCostRule.AttachCost, priced for that Follower and that Personality).
// ======================================================================================================================================================================================
public static class EmperorP4UnitAbilities
{
    public const string HayanAssign = "hayan-assign";
    public const string ShinoharaAttach = "shinohara-attach";
    public const string UnicornAtHome = "p4-own-unbowed-unicorn-personality-at-home";

    public static Dictionary<string, AbilityDefinition> AddEmperorP4Units(this Dictionary<string, AbilityDefinition> all)
    {
        // Shinjo Hayan (oracle-l5r-6883): "After Hayan enters play: Discard all your other Sensei Personalities." (the Sensei entry trait, Effect "discard-other-sensei") and
        // "After engaging: Once per turn, you may assign your (unbowed) Unicorn Clan Personality in your home to the current battlefield." No designator: a triggered trait (TraitReactions), modelled
        // with Reaction timing in the Engage window; "you may" leaves the assignment itself optional, its printed "Once per turn" is its use limit, and it is usable while Hayan is bowed.
        all[HayanAssign] = new("Reaction", "After engaging: Once per turn, you may assign your (unbowed) Unicorn Clan Personality in your home to the current battlefield", UnicornAtHome, Trigger: "after-engaging");
        // Ikoma Shinohara (oracle-l5r-3639): "Battle: Target a Follower in your hand and one of your Personalities: Attach the Follower to the Personality, paying all costs."
        all[ShinoharaAttach] = new("Battle", "Target a Follower in your hand and one of your Personalities: Attach the Follower to the Personality, paying all costs", "slots",
            Slots: [new("attachment", "attachment-in-discard", "A Follower in your hand"), new("personality", "own-personality", "One of your Personalities")],
            Cost: GoldCostRule.AttachCost(), Attach: new(AttachSource.Target, AttachPile.OwnHand, AttachWielder.PersonalitySlot, [CardType.Follower]));
        return all;
    }
}

public sealed partial class MatchEngine
{
    private bool P4UnitTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target) => category switch
    {
        // Hayan: "your (unbowed) Unicorn Clan Personality in your home" -- one who may be assigned now.
        EmperorP4UnitAbilities.UnicornAtHome => target.OwnerId == player.Id && Definition(target).Type == CardType.Personality && target.Zone == Zone.Home && !target.Bowed
            && HasClanAffiliation(target, "Unicorn") && _state.CurrentBattleProvince is not null && MayBeAssignedToCreatedBattlefield(target, player.Id == _state.ActivePlayerId, mayBeBowed: false),
        _ => P4MoreTargetAllows(category, command, player, source, target)
    };

    private bool? RunEmperorP4Units(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case EmperorP4UnitAbilities.HayanAssign:
                if (target is null || _state.CurrentBattleProvince is not int battlefield) return false;
                if (!AssignToCreatedBattlefield(target, battlefield, attacking: player.Id == _state.ActivePlayerId, mayBeBowed: false))
                    Emit("assignFailed", $"{Definition(source!).Name}: {Definition(target).Name} may not be assigned now.", player.Id, target.Id);
                return false;
            case EmperorP4UnitAbilities.ShinoharaAttach:
                return ExecuteAttach(command, ability.Attach!, player, source, target);
            default:
                return null;
        }
    }
}
