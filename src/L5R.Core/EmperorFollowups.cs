namespace L5R.Core;

// ======================================================================================================================================================================================
// EMPEROR EDITION FOLLOW-UPS (issue #498): Mendon's rulings after PR #497.
//
//   * "A CARD WITHOUT ATTACHMENTS IN HIS UNIT" (Makito's reading, applied to every card printing that phrase): the unit is the Personality and everything attached to him (Followers, Items, Spells...);
//     the acting player chooses ANY card of the unit that has no attachments -- the Personality himself only when he has none. UnitCardsWithoutAttachments lists them for a Personality chosen as the target
//     (Makito, Asako Kaitoko, Daidoji Sosuke); IsUnitCardWithoutAttachments answers it for a card targeted directly (Volcano Tattoo, Shiba Iaimiko's second target, Bayushi Nitoshi).
// ======================================================================================================================================================================================
public sealed partial class MatchEngine
{
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
