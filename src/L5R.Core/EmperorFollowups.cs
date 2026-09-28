namespace L5R.Core;

// ======================================================================================================================================================================================
// EMPEROR EDITION FOLLOW-UPS (issue #498): Mendon's rulings after PR #497.
//
//   * "A CARD WITHOUT ATTACHMENTS IN HIS UNIT" (Makito's reading, applied to every card printing that phrase): the unit is the Personality and everything attached to him (Followers, Items, Spells...);
//     the acting player chooses ANY card of the unit that has no attachments -- the Personality himself only when he has none. UnitCardsWithoutAttachments lists them for a Personality chosen as the target
//     (Makito, Asako Kaitoko, Daidoji Sosuke); IsUnitCardWithoutAttachments answers it for a card targeted directly (Volcano Tattoo, Shiba Iaimiko's second target, Bayushi Nitoshi).
// ======================================================================================================================================================================================
public static class EmperorFollowupsAbilities
{
    public const string ChukageFollow = "chukage-follow";
    public const string RendFocus = "rend-the-soul-focus";
    public const string RendBattle = "rend-the-soul-battle";
    public const string NoHidingPlace = "no-hiding-place";
    /// <summary>No Hiding Place's trigger: "After you announce an action with a Ranged Attack" -- a Strategy in the announcing player's hand, in the announce window (EmperorFollowupsAttack.cs).</summary>
    public const string NoHidingPlaceTrigger = "own-ranged-attack-action-announced";

    public static Dictionary<string, AbilityDefinition> AddEmperorFollowups(this Dictionary<string, AbilityDefinition> all)
    {
        // Asako Chukage (oracle-l5r-533, the Forgotten Legacy scan): "Reaction: Even if Chukage is bowed, after the resolution of an action that moved your Personality: Move Chukage to the Personality's
        // location. Straighten Chukage's unit if he moved." -- the after-action-resolved window; the moved Personality is chosen as the Reaction is used (EmperorFollowupsSearch.cs).
        all[ChukageFollow] = new("Reaction", "Even if Chukage is bowed, after the resolution of an action that moved your Personality: Move Chukage to the Personality's location. Straighten Chukage's unit if he moved",
            MatchEngine.ChukageFollowTarget, Trigger: AfterActionReactionTriggers.AfterActionResolved);
        // Rend the Soul (oracle-l5r-6260): "As a Focus Effect: Raise this card's Focus Value to 5. If your Personality wins this duel, he loses instead (the other Personality still also loses)."
        all[RendFocus] = new("FocusEffect", "As a Focus Effect: Raise this card's Focus Value to 5. If your Personality wins this duel, he loses instead (the other Personality still also loses)", "none");
        // "Battle: Choose your performing opposed Personality: Give him +5F. If the army he is in during this battle's resolution wins, this battle is tied instead. You will not destroy the province in
        // this battle's resolution." (Pure Intent's performer category.)
        all[RendBattle] = new("Battle", "Choose your performing opposed Personality: Give him +5F. If the army he is in during this battle's resolution wins, this battle is tied instead. You will not destroy the province in this battle's resolution",
            "none", Performer: "own-personality-opposed");
        // No Hiding Place (oracle-l5r-5599): "Reaction: After you announce an action with a Ranged Attack: Give one Ranged Attack from the action -1 strength. That Ranged Attack may target a card in the
        // Defender's home (if otherwise legal)."
        all[NoHidingPlace] = new("Reaction", "After you announce an action with a Ranged Attack: Give one Ranged Attack from the action -1 strength. That Ranged Attack may target a card in the Defender's home (if otherwise legal)",
            "none", Trigger: NoHidingPlaceTrigger);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- hooks into the shared switches (CardAbilities.cs) ----

    private bool FollowupsTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target) => category switch
    {
        ChukageFollowTarget => ChukageFollowTargetAllows(command, player, source, target),
        _ => false
    };

    // "Even if Chukage is bowed" (the bowed-source exemption list's hook).
    private static bool FollowupsEvenIfBowed(string id) => id == EmperorFollowupsAbilities.ChukageFollow;

    // The after-action-resolved Reactions' conditions (AfterActionResolvedApplies' hook); null when the id is not this pass's.
    private bool? FollowupsAfterActionApplies(CardInstance source, string abilityId, int playerId) => abilityId switch
    {
        EmperorFollowupsAbilities.ChukageFollow => ChukageFollowApplies(source, playerId),
        _ => null
    };

    // The execution switch's hook: null when the id is not this pass's; false when its effects are done; true when a decision opened.
    private bool? RunEmperorFollowups(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case EmperorFollowupsAbilities.ChukageFollow:
                ChukageFollow(source!, target!);
                return false;
            case EmperorFollowupsAbilities.RendBattle:
                RendTheSoulBattle(command, player, source!);
                return false;
            case EmperorFollowupsAbilities.NoHidingPlace:
                NoHidingPlaceReaction(player, source!);
                return false;
            default:
                return null;
        }
    }

    // ---- "a card without attachments in his unit" ----

    /// <summary>The cards of <paramref name="personality"/>'s unit that have no attachments: himself when he has none, otherwise each of his attachments (any type) without attachments of its own.</summary>
    private CardInstance[] UnitCardsWithoutAttachments(CardInstance personality) =>
        !HasAttachments(personality) ? [personality]
            : Attachments(personality.Id).Where(a => !HasAttachments(a)).OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();

    /// <summary>True when <paramref name="card"/> is a card of a unit (an in-play Personality, or a card attached to one) and has no attachments.</summary>
    private bool IsUnitCardWithoutAttachments(CardInstance card)
    {
        if (HasAttachments(card)) return false;
        if (Definition(card).Type == CardType.Personality) return InPlay(card);
        return card.Zone == Zone.Attached && card.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) && Definition(host).Type == CardType.Personality;
    }

    // ---- Kurohito's Perfection (Mendon's ruling, issue #498): "delay its effects until YOUR End Phase begins" ----

    // A delayed effect waiting for "your End Phase" belongs to one player: it is due only as THAT player's End Phase begins. A Favor action taken on another player's turn (through Plans Within Plans'
    // Limited-instead-of-Open) draws as its taker's next End Phase begins, not as the current turn's does. An effect recorded without a player is due at the first End Phase.
    private bool EndPhaseIsOwners(DelayedEffect effect) => effect.PlayerId is not int owner || owner == _state.ActivePlayerId;
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorFollowups_Rung4b1()
    {
        EngineHooks.Abilities.Register("AddEmperorFollowups", 1800, static abilities => abilities.AddEmperorFollowups());
    }
}
