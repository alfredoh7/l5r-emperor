namespace L5R.Core;

// Attach a card to a Personality as an ability's effect (issue #337) -- the general shape behind
//
//   Moto Jun-Ni         "Limited: Target a Follower in any player's discard pile: Attach it to Jun-Ni, paying all costs."
//   Foothills Keep      "Battle/Open, [Gold]: Choose your performing Kensai and Equip a target Weapon in your discard pile to him."
//   Fading Shadows      "Ninja Battle: If this Follower is in your hand, target your performing Ninja and Equip it to him."
//
// and, unbuilt but now one AttachSpec away, Mirumoto Katagi - exp, Border Market, Inspirational Address, Frontier Farmer and the rest of
// the "Equip from a discard pile" family. Mendon's ruling for the discard-pile half: a general "attach a card from the discard pile to a
// Personality" with a cost mode -- the player pays the full cost, a discounted cost, or nothing -- the cost QUOTED and PAID through the
// Gold path (issue #303) BEFORE the card leaves the pile. Legality is the existing Equip legality, not a copy of it.
//
// What varies is data on the ability (AbilityDefinition.Attach, an AttachSpec):
//   Card     which card is attached: the ability's chosen TARGET (found in a discard pile) or the SOURCE card itself (in hand).
//   Pile     for a target: whose discard pile(s) it may be in -- your own, or any player's. (Followers, Items and Spells go to the Fate
//            discard pile; the Dynasty discard pile holds no attachment.)
//   Wielder  who receives it: the source (Jun-Ni attaches to herself) or the chosen performer (Foothills' "your performing Kensai").
//   Types / Keywords / ExcludedKeywords   what the card must be ("a Follower", "a Weapon", "a non-Unique Weapon").
// and the cost mode is the ability's own Cost rule: GoldCostRule.AttachCost() is "all costs" (the attachment's Equip cost -- printed cost
// less every discount that applies to this attachment, this wielder and this player, EquipGoldCost), AttachCost(modifier, floor) is a
// discount ("paying 1 less Gold"), and no Cost rule at all is "ignoring costs".
//
// The legality of the attach itself is what Equip already enforces, shared through RequireAttachable: the wielder is a Personality in play,
// the attachment's own "Will only/not attach to" and Honor Requirement (AttachmentLegalTarget), the Armor/Weapon caps (AttachmentAllowed)
// and Unique/Singular. The attached card is a NEW entry into play: whatever the discard pile held (tokens, modifiers, granted keywords,
// a stale once-per-turn stamp) is gone, and its "after this enters play" effects fire (OpenEntryReactionWindow), exactly like an Equip.
//
// Control follows the Personality (Mendon's ruling, issue #352): an attachment is controlled by the controller of the Personality it is attached to,
// and a card taken from an ENEMY discard or dead pile into play is controlled by the player who took it, but it is always returned to its OWNER's
// pile when it leaves play. The engine reads OwnerId as "controller" for every in-play card, so a taken card's OwnerId becomes its controller's and
// CardInstance.OriginalOwnerId remembers the real owner (TakeControl); the card goes back the moment it is no longer in play
// (ReturnCardsOutOfPlayToTheirOwners, run after every command, and directly where a card goes to a hand or deck). Everything the controller can do
// with it -- use its abilities, count it for Unique/Singular, unit membership, view -- then follows from OwnerId with no further change.

/// <summary>Where the attached card comes from.</summary>
public static class AttachSource
{
    /// <summary>The ability's chosen target, found in a discard pile.</summary>
    public const string Target = "target";
    /// <summary>The ability's own source card, which must be in the acting player's hand (Fading Shadows).</summary>
    public const string Self = "self";
}

/// <summary>Whose discard pile a target may be in.</summary>
public static class AttachPile
{
    public const string Own = "own-discard";
    public const string Any = "any-discard";
    /// <summary>Your own hand or your own discard pile (Harsh Taskmaster: "a Follower in your hand or discard pile").</summary>
    public const string OwnHandOrDiscard = "own-hand-or-discard";
    /// <summary>Your own hand only (Asako Ebara, Moto Hailung: "a Weapon/Follower in your hand").</summary>
    public const string OwnHand = "own-hand";
    /// <summary>A card under the ability's own source (P21, ZoneExtensions.cs: The Tachikaze's Followers): in Zone.Underneath with HostCardId the source, owned by the acting player.</summary>
    public const string UnderHost = "under-host";
}

/// <summary>Who receives the attached card.</summary>
public static class AttachWielder
{
    /// <summary>The ability's own source card (which must be a Personality in play).</summary>
    public const string Source = "source";
    /// <summary>The performer the acting player chose (AbilityDefinition.Performer names which Personalities are eligible).</summary>
    public const string Performer = "performer";
    /// <summary>The Personality chosen for the ability's "personality" target slot (Forgotten Legacy pass, issue #467, batch 1: Daigotsu Gyoken's "Target your Personality
    /// and a non-Unique attachment in any player's discard pile: Attach it to the Personality") -- a TARGET, not a performer, so the source still performs.</summary>
    public const string PersonalitySlot = "personality-slot";
    /// <summary>The Personality whose entering play opened the window this Reaction is used in (Emperor Edition Stronghold pass, issue #486 batch 3, The Spider's Web: "After your
    /// Personality enters play: ... attach a Weapon to him").</summary>
    public const string Entered = "entered";
}

/// <summary>An "attach a card" effect. See the file comment; Types is required for a Target attach (the kinds of card the ability names).
/// IgnoreHonorRequirement: "ignoring Honor Requirements" (Daigotsu Gyoken) -- a Follower's Honor Requirement is not checked against the wielder.</summary>
public sealed record AttachSpec(string Card = AttachSource.Target, string Pile = AttachPile.Any, string Wielder = AttachWielder.Source,
    CardType[]? Types = null, string[]? Keywords = null, string[]? ExcludedKeywords = null, bool IgnoreHonorRequirement = false);

public sealed partial class MatchEngine
{
    // Why this discarded card may not be the target of an attach, or null when it may (the pile, its owner, its kind and keywords).
    private string? AttachTargetError(AttachSpec spec, int playerId, CardInstance card, string? sourceId = null)
    {
        var def = Definition(card);
        if (spec.Pile == AttachPile.OwnHand)
        {
            if (card.OwnerId != playerId || card.Zone != Zone.Hand || !Player(playerId).Hand.Contains(card.Id)) return "Choose a card in your hand.";
        }
        else if (spec.Pile == AttachPile.UnderHost)
        {
            if (card.Zone != Zone.Underneath || card.OwnerId != playerId || card.HostCardId is null || sourceId is not null && card.HostCardId != sourceId) return "Choose a card under this card.";
        }
        else if (spec.Pile == AttachPile.OwnHandOrDiscard)
        {
            if (card.OwnerId != playerId || card.Zone != Zone.FateDiscard && !(card.Zone == Zone.Hand && Player(playerId).Hand.Contains(card.Id))) return "Choose a card in your hand or your own discard pile.";
        }
        else
        {
            if (card.Zone != Zone.FateDiscard) return "Choose a card in a discard pile.";
            if (spec.Pile == AttachPile.Own && card.OwnerId != playerId) return "Choose a card in your own discard pile.";
        }
        if (spec.Types is { } types && !types.Contains(def.Type)) return $"Choose {string.Join(" or ", types.Select(t => "a " + t))} in {(spec.Pile == AttachPile.OwnHand ? "your hand" : spec.Pile == AttachPile.OwnHandOrDiscard ? "your hand or a discard pile" : "a discard pile")}.";
        if (spec.Keywords is { } required && !required.All(k => Keyword(card, k))) return $"Choose a card with {string.Join(", ", required)}.";
        if (spec.ExcludedKeywords is { } excluded && excluded.Any(k => Keyword(card, k))) return $"Choose a card without {string.Join(" or ", excluded)}.";
        return null;
    }

    private IEnumerable<CardInstance> AttachCandidatesInDiscard(AttachSpec spec, int playerId, string? sourceId = null) =>
        _state.Cards.Values.Where(c => AttachTargetError(spec, playerId, c, sourceId) is null);

    // The wielder an attach command names: the source itself, or the chosen performer.
    private CardInstance? WielderOfAttach(AttachSpec spec, GameCommand command, CardInstance? source) =>
        spec.Wielder == AttachWielder.Performer
            ? (command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var performer) ? performer : null)
            : spec.Wielder == AttachWielder.Entered
                ? (_state.PendingChoice?.CardId is { } enteredId && _state.Cards.TryGetValue(enteredId, out var entered) ? entered : null)
            : spec.Wielder == AttachWielder.PersonalitySlot
                ? (command.Selections?.FirstOrDefault(s => s.Slot == "personality")?.CardIds is [var slotId, ..] && _state.Cards.TryGetValue(slotId, out var targeted) ? targeted : null)
                : source;

    // What Equip enforces on ANY attach, in one place (the Equip command, Equip (Battle), Low Market and Singh Remnants each still carry their
    // own copy of these four Requires; this is the one a new attach path calls instead of writing a fifth).
    private void RequireAttachable(CardInstance attachment, CardInstance wielder, int playerId, bool ignoreHonorRequirement = false)
    {
        var def = Definition(attachment);
        var wielderName = Definition(wielder).Name;
        Require(InPlay(wielder) && Definition(wielder).Type == CardType.Personality, $"{wielderName} is not a Personality in play.");
        Require(AttachmentLegalTarget(ignoreHonorRequirement ? def with { HonorRequirement = null } : def, wielder), $"{def.Name} will not attach to {wielderName}.");
        Require(AttachmentAllowed(attachment, wielder), "A Personality may attach only one Armor and, without Kensai or a Kama, only one Weapon.");
        Require(!(Keyword(attachment, "Unique") || Keyword(attachment, "Singular")) || !ControlsCopyOf(playerId, def.Name),
            $"You already control this {(Keyword(attachment, "Unique") ? "Unique" : "Singular")} card.");
    }

    // ValidateAbility's step for an ability with an Attach spec. The target category check (a card in a discard pile) has already run for
    // a Target attach; this adds what is not about the target alone.
    private void ValidateAttach(GameCommand command, AttachSpec spec, PlayerState player, CardInstance? source)
    {
        CardInstance attachment;
        if (spec.Card == AttachSource.Self)
        {
            Require(source is not null && (source.Zone == Zone.Hand && player.Hand.Contains(source.Id) || source.Zone == Zone.FateDiscard && source.OwnerId == player.Id && spec.Pile == AttachPile.Own), "This card must be in your hand (or, for a card that names its own discard pile, in it) to be attached.");
            Require(command.TargetId is null, "This action has no target.");
            attachment = source!;
        }
        else
        {
            attachment = Card(command.TargetId);
            var error = AttachTargetError(spec, player.Id, attachment, source?.Id);
            Require(error is null, error ?? "");
        }
        var wielder = WielderOfAttach(spec, command, source) ?? throw new RuleViolation("Choose a Personality to attach it to.");
        RequireAttachable(attachment, wielder, player.Id, spec.IgnoreHonorRequirement);
    }

    // Control (issue #352): `controllerId` now controls `card`. The real owner is remembered the first time a card changes hands and restored when it
    // leaves play; a card already controlled by its owner is untouched.
    private void TakeControl(CardInstance card, int controllerId)
    {
        if (card.OwnerId == controllerId) return;
        card.OriginalOwnerId ??= card.OwnerId;
        card.OwnerId = controllerId;
        if (card.OriginalOwnerId == controllerId) card.OriginalOwnerId = null; // issue #501: control given back to the owner -- he controls his own card again
        Emit("controlTaken", $"{Player(controllerId).Name} took control of {Definition(card).Name}.", controllerId, card.Id);
    }

    // The card is no longer controlled by anyone: it is back with its owner (whose discard pile, hand or deck it is in or is entering).
    private void RestoreOwner(CardInstance card)
    {
        if (card.OriginalOwnerId is not int owner) return;
        card.OwnerId = owner; card.OriginalOwnerId = null;
    }

    // The funnel that returns every card that left play by ANY path (destroyed, discarded, removed from the game, returned to hand) to its owner's
    // pile: run once after each command, so no exit path has to remember it. Effects of the exit itself (an Expendable draw, the destruction penalty)
    // have already been resolved for the controller, as they should be.
    private void ReturnCardsOutOfPlayToTheirOwners()
    {
        // A gift in another player's hand (Naoharu's Gift, issue #492 group F) is his to play until it leaves the hand.
        foreach (var card in _state.Cards.Values.Where(c => c.OriginalOwnerId is not null && !InPlay(c) && c.Zone != Zone.Hand && !P5Borrowed(c))) RestoreOwner(card); // issue #503: Learning's borrowed Strategy
        SweepP5Borrowed();
        ApplyControlReturnsAfterCommand();
        SweepRemovedAbilities(); // issue #667 (AbilityRemoval.cs): a card whose abilities were removed has them again once it has left play.
        SweepP5Records(); // issue #503 (EmperorP5Zones.cs): a Ring whose abilities were removed is a new object once it has left play. // issue #501 (EmperorMechB.cs): a return owed to a card that left play (Sachina), and returns blocked by Unique/Singular tried again.
    }

    // ExecuteAbilityEffects' step: the Gold has already been paid (PayForAbility), so the card leaves its place and enters play attached.
    // Returns true when an entering-play reaction window opened (the caller returns without finishing, as Low Market does).
    private bool ExecuteAttach(GameCommand command, AttachSpec spec, PlayerState player, CardInstance? source, CardInstance? target)
    {
        var attachment = spec.Card == AttachSource.Self ? source! : target!;
        var wielder = WielderOfAttach(spec, command, source)!;
        var fromDiscard = attachment.Zone is Zone.FateDiscard or Zone.Underneath;
        PlaceAttachment(attachment, wielder, player);
        // Clearing Crew (issue #476 batch 6): an attach from a discard pile is not an entry "from your hand".
        _attachingFromPileId = fromDiscard ? attachment.Id : null;
        try { return OpenEntryReactionWindow(player.Id, attachment, 1 - player.Id); }
        finally { _attachingFromPileId = null; }
    }

    // Everything an attach does short of the entering-play window: the card leaves its hand or discard pile, enters play attached to the wielder under his
    // controller, and is logged. Split from ExecuteAttach (Regret attaches several cards, then opens one window each: RegretEmbracingVirtue.cs).
    private void PlaceAttachment(CardInstance attachment, CardInstance wielder, PlayerState player)
    {
        var def = Definition(attachment);
        var fromDiscard = attachment.Zone is Zone.FateDiscard or Zone.Underneath; // P21: a card from under a host is a new entry into play like one from a discard pile
        if (!fromDiscard && attachment.Zone == Zone.Hand) { Player(attachment.OwnerId).AttachedFromHandThisTurn = true; NoteAttachedFollowerFromHand(attachment, wielder); NoteAttachedFromHandForGlory(attachment.OwnerId, attachment); }
        Player(attachment.OwnerId).Hand.Remove(attachment.Id);
        if (fromDiscard)
        {
            // A new entry into play: nothing the card carried while it was last in play (or since) follows it back.
            ResetCardState(attachment);
        }
        TakeControl(attachment, wielder.OwnerId);
        attachment.HostCardId = null;
        attachment.Zone = Zone.Attached; attachment.AttachedTo = wielder.Id; attachment.FaceUp = true; attachment.ProvinceIndex = wielder.ProvinceIndex;
        PromoPaymentAttached(attachment); // Coin Pouch (issue #667): "This card enters play bowed."
        if (def.AttachGrantsKeyword is string granted) GrantKeyword(wielder, granted, attachment.Id, "Attachment");
        if (fromDiscard) NoteAttached(player.Id, attachment, "discard"); // Issue #501 (EmperorMechD.cs): the attach log (a hand attach is logged above).
        Emit("equipped", $"{def.Name} attached to {Definition(wielder).Name}{(fromDiscard ? " from a discard pile" : "")}.", player.Id, attachment.Id);
    }
}
