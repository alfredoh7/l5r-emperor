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
}

/// <summary>Who receives the attached card.</summary>
public static class AttachWielder
{
    /// <summary>The ability's own source card (which must be a Personality in play).</summary>
    public const string Source = "source";
    /// <summary>The performer the acting player chose (AbilityDefinition.Performer names which Personalities are eligible).</summary>
    public const string Performer = "performer";
}

/// <summary>An "attach a card" effect. See the file comment; Types is required for a Target attach (the kinds of card the ability names).</summary>
public sealed record AttachSpec(string Card = AttachSource.Target, string Pile = AttachPile.Any, string Wielder = AttachWielder.Source,
    CardType[]? Types = null, string[]? Keywords = null, string[]? ExcludedKeywords = null);

public sealed partial class MatchEngine
{
    // Why this discarded card may not be the target of an attach, or null when it may (the pile, its owner, its kind and keywords).
    private string? AttachTargetError(AttachSpec spec, int playerId, CardInstance card)
    {
        var def = Definition(card);
        if (card.Zone != Zone.FateDiscard) return "Choose a card in a discard pile.";
        if (spec.Pile == AttachPile.Own && card.OwnerId != playerId) return "Choose a card in your own discard pile.";
        if (spec.Types is { } types && !types.Contains(def.Type)) return $"Choose {string.Join(" or ", types.Select(t => "a " + t))} in a discard pile.";
        if (spec.Keywords is { } required && !required.All(k => Keyword(card, k))) return $"Choose a card with {string.Join(", ", required)}.";
        if (spec.ExcludedKeywords is { } excluded && excluded.Any(k => Keyword(card, k))) return $"Choose a card without {string.Join(" or ", excluded)}.";
        return null;
    }

    private IEnumerable<CardInstance> AttachCandidatesInDiscard(AttachSpec spec, int playerId) =>
        _state.Cards.Values.Where(c => AttachTargetError(spec, playerId, c) is null);

    // The wielder an attach command names: the source itself, or the chosen performer.
    private CardInstance? WielderOfAttach(AttachSpec spec, GameCommand command, CardInstance? source) =>
        spec.Wielder == AttachWielder.Performer
            ? (command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var performer) ? performer : null)
            : source;

    // What Equip enforces on ANY attach, in one place (the Equip command, Equip (Battle), Low Market and Singh Remnants each still carry their
    // own copy of these four Requires; this is the one a new attach path calls instead of writing a fifth).
    private void RequireAttachable(CardInstance attachment, CardInstance wielder, int playerId)
    {
        var def = Definition(attachment);
        var wielderName = Definition(wielder).Name;
        Require(InPlay(wielder) && Definition(wielder).Type == CardType.Personality, $"{wielderName} is not a Personality in play.");
        Require(AttachmentLegalTarget(def, wielder), $"{def.Name} will not attach to {wielderName}.");
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
            Require(source is not null && source.Zone == Zone.Hand && player.Hand.Contains(source.Id), "This card must be in your hand to be attached.");
            Require(command.TargetId is null, "This action has no target.");
            attachment = source!;
        }
        else
        {
            attachment = Card(command.TargetId);
            var error = AttachTargetError(spec, player.Id, attachment);
            Require(error is null, error ?? "");
        }
        var wielder = WielderOfAttach(spec, command, source) ?? throw new RuleViolation("Choose a Personality to attach it to.");
        RequireAttachable(attachment, wielder, player.Id);
    }

    // ExecuteAbilityEffects' step: the Gold has already been paid (PayForAbility), so the card leaves its place and enters play attached.
    // Returns true when an entering-play reaction window opened (the caller returns without finishing, as Low Market does).
    private bool ExecuteAttach(GameCommand command, AttachSpec spec, PlayerState player, CardInstance? source, CardInstance? target)
    {
        var attachment = spec.Card == AttachSource.Self ? source! : target!;
        var wielder = WielderOfAttach(spec, command, source)!;
        var def = Definition(attachment);
        var fromDiscard = attachment.Zone == Zone.FateDiscard;
        Player(attachment.OwnerId).Hand.Remove(attachment.Id);
        if (fromDiscard)
        {
            // A new entry into play: nothing the card carried while it was last in play (or since) follows it back.
            attachment.Tokens.Clear(); attachment.Modifiers.Clear(); attachment.GrantedKeywords.Clear(); attachment.RemovedKeywords.Clear();
            attachment.Protections.Clear(); attachment.AbilityUses.Clear(); attachment.Bowed = false; attachment.Dishonorable = false;
        }
        attachment.Zone = Zone.Attached; attachment.AttachedTo = wielder.Id; attachment.FaceUp = true; attachment.ProvinceIndex = wielder.ProvinceIndex;
        if (def.AttachGrantsKeyword is string granted) GrantKeyword(wielder, granted, attachment.Id, "Attachment");
        Emit("equipped", $"{def.Name} attached to {Definition(wielder).Name}{(fromDiscard ? " from a discard pile" : "")}.", player.Id, attachment.Id);
        return OpenEntryReactionWindow(player.Id, attachment, 1 - player.Id);
    }
}
