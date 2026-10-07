namespace L5R.Core;

// Primitive P24 (issue #690), costs. See EffectPrimitives.cs for the feature and docs/decisions/2026-10-06-690-p24-effect-primitives.md for the readings.
//
//   COST SUBSTITUTION  "You may bow this card instead of bowing a Ring to pay a cost" (Harmony's permanent trait). A card holding the trait (GrantBowInsteadOfRing, kept in
//                      MatchState.EffectPrimitives) may be bowed in the place of ONE Ring an action would bow as a declared cost: the Ring's own "bow this card", or another card's
//                      "bow your target Ring". It is the acting player's choice and rides on the command as GameCommand.CostOption = CostSubstitution.Option(holderId); the engine offers
//                      one more action per unbowed holder (CostSubstitutionOffers), validates it with everything else and bows the holder where the Ring would have been bowed. The
//                      holder is then the card that pays: a negation of ITS bowing blocks the cost, it cannot also be bowed for Gold, and the Ring stays as it was.
//   COST WAIVER        "He need not bow for his Spells with an odd Focus Value" (Koan's Jingasa; even: Koan's Robes). EngineHooks.CostWaivers is asked once per declared cost
//                      (CostExempt, the question "...unless he is Void" already answers): a callback that answers true means the cost is not owed, and its subject need not be able
//                      to pay it. SpellFocusParityWaives is the callback body the two Koan Items need.
//   COST CARDS         "Remove five Koan cards you control from the game", "remove a non-Ring card in any discard pile from the game", "destroy one of his Spells": the verbs exist
//                      (CostVerbs.RemoveSlotCards, CostVerbs.Sacrifice) and a cost-only target slot names the cards. What was missing is the slot categories; EffectPrimitiveTargets
//                      names them and EffectPrimitiveCosts builds the slot and the cost together.

/// <summary>The cost-substitution option a command names, and the kinds of substitution the store holds.</summary>
public static class CostSubstitution
{
    /// <summary>GameCommand.CostOption for "bow this card in the Ring's place": the prefix, then the id of the card bowed instead.</summary>
    public const string OptionPrefix = "p24-substitute:";
    public static string Option(string holderId) => OptionPrefix + holderId;
    /// <summary>"You may bow this card instead of bowing a Ring to pay a cost."</summary>
    public const string BowInsteadOfRing = "bow-instead-of-ring";
}

/// <summary>One card's standing permission to pay in another card's place (MatchState.EffectPrimitives.CostSubstitutions). Duration is GrantDuration.Permanent or GrantDuration.Turn
/// (good through <see cref="Turn"/>).</summary>
public sealed class CostSubstitutionGrant
{
    public string CardId { get; set; } = "";
    public string Kind { get; set; } = CostSubstitution.BowInsteadOfRing;
    public string Source { get; set; } = "";
    public string Duration { get; set; } = GrantDuration.Permanent;
    public int Turn { get; set; }
}

/// <summary>One declared cost asked of EngineHooks.CostWaivers: the cost, the ability's source card and the card the cost applies to (the wielder of the source for a Wielder cost).</summary>
internal sealed record CostWaiverContext(MatchEngine Engine, ActionCost Cost, CardInstance? Source, CardInstance? Subject);

internal static partial class EngineHooks
{
    /// <summary>"Need not" pay a declared cost (EffectPrimitivesCosts.cs). A predicate: the first callback that answers true waives the cost. Register with a unique id and a priority of 10000 or more.</summary>
    internal static readonly OrderedHooks<Func<CostWaiverContext, bool>> CostWaivers = new();
}

/// <summary>The target categories primitive P24 adds, for a slot that names the cards a cost spends. Each is a string the ability's slot carries as its Category.</summary>
public static class EffectPrimitiveTargets
{
    internal const string OwnInPlayWithKeywordPrefix = "p24-own-in-play-with-keyword:";
    internal const string PerformerAttachmentPrefix = "p24-performer-attachment:";
    internal const string AnyDiscardPrefix = "p24-any-discard";

    /// <summary>"A [keyword] card you control": any card in play the acting player controls with the keyword.</summary>
    public static string OwnCardInPlayWithKeyword(string keyword) => OwnInPlayWithKeywordPrefix + keyword;

    /// <summary>"One of his [type]s": a card of that type attached to the Personality performing the action (the chosen performer, else the source, else the source's wielder).</summary>
    public static string AttachmentOfPerformer(CardType type) => PerformerAttachmentPrefix + type;

    /// <summary>"A card in any discard pile" (a dead Personality's pile included), narrowed to a keyword, to every type but one, or to the acting player's own piles.</summary>
    public static string CardInAnyDiscard(string? keyword = null, CardType? exceptType = null, bool ownOnly = false) =>
        AnyDiscardPrefix + (keyword is null ? "" : $";keyword={keyword}") + (exceptType is { } type ? $";not-type={type}" : "") + (ownOnly ? ";own" : "");
}

/// <summary>A cost-only slot and the cost that spends the cards chosen for it, built together so they cannot disagree. Put the slot in AbilityDefinition.Slots and the cost in Costs.</summary>
public static class EffectPrimitiveCosts
{
    /// <summary>"Remove <paramref name="count"/> [keyword] cards you control from the game" (Koan's Whisper: five Koan cards).</summary>
    public static (TargetSlot Slot, ActionCost Cost) RemoveOwnCardsWithKeyword(string keyword, int count, string slotId = "p24-remove") =>
        (new TargetSlot(slotId, EffectPrimitiveTargets.OwnCardInPlayWithKeyword(keyword), $"{count} {keyword} card{(count == 1 ? "" : "s")} you control, to remove from the game (a cost)", count, count) { CostOnly = true },
            new ActionCost(CostVerbs.RemoveSlotCards, CostSubject.Slot(slotId)));

    /// <summary>"Remove [count] card(s) in any discard pile from the game" (Scorching Lash: a non-Ring card).</summary>
    public static (TargetSlot Slot, ActionCost Cost) RemoveFromAnyDiscard(int count = 1, string? keyword = null, CardType? exceptType = null, bool ownOnly = false, string slotId = "p24-discard") =>
        (new TargetSlot(slotId, EffectPrimitiveTargets.CardInAnyDiscard(keyword, exceptType, ownOnly), $"{count} card{(count == 1 ? "" : "s")} in a discard pile, to remove from the game (a cost)", count, count) { CostOnly = true },
            new ActionCost(CostVerbs.RemoveSlotCards, CostSubject.Slot(slotId)));

    /// <summary>"Destroy one of his [type]s" (Tamori Tsuchiya: one of his Spells): an attachment of that type on the performing Personality, destroyed as the cost.</summary>
    public static (TargetSlot Slot, ActionCost Cost) DestroyOwnAttachment(CardType type, string slotId = "p24-attachment") =>
        (new TargetSlot(slotId, EffectPrimitiveTargets.AttachmentOfPerformer(type), $"One of his {type}s, to destroy (a cost)") { CostOnly = true },
            new ActionCost(CostVerbs.Sacrifice, CostSubject.Slot(slotId)));
}

public sealed partial class MatchEngine
{
    // ---- the substitution store -------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Gives <paramref name="holder"/> the trait "You may bow this card instead of bowing a Ring to pay a cost" (Harmony: permanently).</summary>
    internal void GrantBowInsteadOfRing(CardInstance holder, string source, string duration = GrantDuration.Permanent)
    {
        EffectPrimitives.CostSubstitutions.RemoveAll(g => g.Duration == GrantDuration.Turn && g.Turn != _state.TurnNumber);
        if (EffectPrimitives.CostSubstitutions.Any(g => g.CardId == holder.Id && g.Kind == CostSubstitution.BowInsteadOfRing && g.Duration == duration && (duration != GrantDuration.Turn || g.Turn == _state.TurnNumber))) return;
        EffectPrimitives.CostSubstitutions.Add(new CostSubstitutionGrant { CardId = holder.Id, Kind = CostSubstitution.BowInsteadOfRing, Source = source, Duration = duration, Turn = _state.TurnNumber });
        Emit("costSubstitutionGranted", $"{Definition(holder).Name} may be bowed instead of a Ring to pay a cost{(duration == GrantDuration.Permanent ? "" : " this turn")} ({source}).", holder.OwnerId, holder.Id);
    }

    /// <summary>Whether the card holds the trait now.</summary>
    internal bool MayBowInsteadOfRing(CardInstance card) =>
        EffectPrimitives.CostSubstitutions.Any(g => g.CardId == card.Id && g.Kind == CostSubstitution.BowInsteadOfRing && (g.Duration != GrantDuration.Turn || g.Turn == _state.TurnNumber));

    private static bool IsCostSubstitutionOption(string? costOption) => costOption is not null && costOption.StartsWith(CostSubstitution.OptionPrefix, StringComparison.Ordinal);

    // The card the command bows in a Ring's place; null when the command names none (or names a card that does not exist).
    private CardInstance? CostSubstituteNamed(GameCommand command) =>
        IsCostSubstitutionOption(command.CostOption) && _state.Cards.TryGetValue(command.CostOption![CostSubstitution.OptionPrefix.Length..], out var holder) ? holder : null;

    private static bool IsRingInPlay(CardInstance? card) => card is not null && InPlay(card) && Definition(card).Type == CardType.Ring;

    // The Ring this action would bow as a declared cost -- the one a substitute is bowed in place of: its single way to pay (CostOptions), else the first declared Bow cost whose subject
    // is a Ring in play. A cost that is not owed (the Ring is played from the hand, an exemption) and the slot-wide bows name none.
    private CardInstance? RingBowedAsCost(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        if (ability.CostOptions is [{ Verb: CostVerb.Bow } only] && CostSubjectCard(only, ability, command, source) is { } optionSubject && IsRingInPlay(optionSubject)) return optionSubject;
        foreach (var cost in ability.Costs ?? [])
        {
            if (cost.Verb != CostVerb.Bow || cost.Subject == EmperorRemainingCAbilities.AllPerformingSlots || cost.UnlessInHand && source is { Zone: Zone.Hand }) continue;
            var subject = CostSubjectCard(new CostOption(cost.Verb, cost.Verb, cost.Verb, cost.Subject), ability, command, source);
            if (IsRingInPlay(subject) && !CostExempt(cost, source, subject)) return subject;
        }
        return null;
    }

    // ValidateCostOption's step for a command that names a substitute.
    private void ValidateCostSubstitution(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        var holder = CostSubstituteNamed(command) ?? throw new RuleViolation("Choose the card to bow in the Ring's place.");
        var name = Definition(holder).Name;
        Require(ability.CostOptions is null or { Length: 1 }, "Choose how to pay this action's cost; a substitute cannot be named as well.");
        Require(InPlay(holder) && holder.OwnerId == command.PlayerId && MayBowInsteadOfRing(holder), $"{name} may not be bowed in a Ring's place.");
        Require(!holder.Bowed, $"{name} is already bowed, so it cannot be bowed in the Ring's place.");
        var ring = RingBowedAsCost(ability, command, source);
        Require(ring is not null, "This action bows no Ring to pay a cost.");
        // The substitute cannot pay twice: it is not also a card the action bows as a cost of its own.
        Require(!PlannedCosts(ability, command, source).Any(c => c.Cost.Verb is CostVerb.Bow or CostVerbs.BowUnit && c.Subject?.Id == holder.Id), $"{name} is already bowed to pay this action's cost.");
    }

    // The payment's question, asked where a cost bows `subject`: the card that is bowed -- the substitute, for the one Ring it stands in for; otherwise the subject itself.
    private CardInstance CostBowPayer(GameCommand command, CardInstance subject)
    {
        if (CostSubstituteNamed(command) is not { } holder || !IsRingInPlay(subject) || !InPlay(holder) || holder.Bowed) return subject;
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return subject;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var card) ? card : null;
        if (RingBowedAsCost(ability, command, source)?.Id != subject.Id) return subject;
        Emit("costSubstituted", $"{Definition(holder).Name} was bowed instead of {Definition(subject).Name} to pay the cost.", holder.OwnerId, holder.Id);
        return holder;
    }

    // PlannedCostsOf's step: the costs as they will be paid -- the substitute, not the Ring, is the card bowed (so a negation of the substitute's bowing is the one that blocks the
    // cost, and the substitute cannot also be bowed for Gold).
    private List<(ActionCost Cost, CardInstance? Subject)> CostsAfterSubstitution(GameCommand command, List<(ActionCost Cost, CardInstance? Subject)> costs)
    {
        if (CostSubstituteNamed(command) is not { } holder || command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return costs;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var card) ? card : null;
        if (RingBowedAsCost(ability, command, source) is not { } ring) return costs;
        var index = costs.FindIndex(c => c.Cost.Verb == CostVerb.Bow && c.Subject?.Id == ring.Id);
        if (index >= 0) costs[index] = (costs[index].Cost, holder);
        return costs;
    }

    // The offered actions' step (the QueryActions ability loop): one more way to pay per card the acting player could bow in a Ring's place for this ability. Only an unbowed holder is
    // offered. An ability is a candidate when its source is a Ring in play with a Bow cost of its own, or when the category that names the card it bows names a Ring.
    private IEnumerable<CostOption?> CostSubstitutionOffers(AbilityDefinition ability, int playerId, CardInstance? source)
    {
        if (EffectPrimitives.CostSubstitutions.Count == 0 || ability.CostOptions is { Length: > 1 } || !MayBowARingAsCost(ability, source)) yield break;
        foreach (var holderId in EffectPrimitives.CostSubstitutions.Select(g => g.CardId).Distinct().Order(StringComparer.Ordinal).ToArray())
            if (_state.Cards.TryGetValue(holderId, out var holder) && InPlay(holder) && holder.OwnerId == playerId && !holder.Bowed && MayBowInsteadOfRing(holder))
                yield return new CostOption(CostSubstitution.Option(holder.Id), $"Bow {Definition(holder).Name} instead of the Ring", CostVerb.Bow);
    }

    private static bool NamesARing(string? category) => category is not null && category.Contains("ring", StringComparison.OrdinalIgnoreCase);

    private static bool MayBowARingAsCost(AbilityDefinition ability, CardInstance? source)
    {
        bool Names(string subject) => subject switch
        {
            CostSubject.Source => IsRingInPlay(source),
            CostSubject.Performer => ability.Performer is null ? IsRingInPlay(source) : NamesARing(ability.Performer),
            CostSubject.Target => NamesARing(ability.Target),
            _ when subject.StartsWith(CostSubject.SlotPrefix, StringComparison.Ordinal) => NamesARing(ability.Slots?.FirstOrDefault(s => s.Id == subject[CostSubject.SlotPrefix.Length..])?.Category),
            _ => false
        };
        return ability.CostOptions is [{ Verb: CostVerb.Bow } only] && Names(only.Subject) || (ability.Costs ?? []).Any(c => c.Verb == CostVerb.Bow && Names(c.Subject));
    }

    // ---- waivers ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // CostExempt's first question: does a registered rule say this cost need not be paid?
    private bool CostWaivedByRule(ActionCost cost, CardInstance? source, CardInstance? subject)
    {
        var hooks = EngineHooks.CostWaivers.Items;
        if (hooks.Length == 0) return false;
        var context = new CostWaiverContext(this, cost, source, subject ?? (cost.Subject == CostSubject.Wielder && source is not null ? WielderOf(source) : null));
        foreach (var hook in hooks)
            if (hook(context)) return true;
        return false;
    }

    /// <summary>"This Personality need not bow for his Spells with an odd Focus Value (0 is even)" (Koan's Jingasa; <paramref name="odd"/> false: Koan's Robes' even). True when the cost is
    /// "Bow this Shugenja" of a Spell whose current Focus Value has that parity, and its wielder carries an attachment with the Effect <paramref name="carrierEffect"/>.</summary>
    internal bool SpellFocusParityWaives(CostWaiverContext context, string carrierEffect, bool odd)
    {
        if (context.Cost.Verb != CostVerb.Bow || context.Cost.Subject != CostSubject.Wielder || context.Source is not { } spell || Definition(spell).Type != CardType.Spell) return false;
        if (WielderOf(spell) is not { } wielder || context.Subject is { } subject && subject.Id != wielder.Id) return false;
        var focus = Math.Max(0, Definition(spell).Focus + StatModifier(spell, "Focus"));
        return (focus % 2 != 0) == odd && Attachments(wielder.Id).Any(a => Definition(a).Effect == carrierEffect);
    }

    // ---- the cost-card categories -----------------------------------------------------------------------------------------------------------------------------------------------------------

    // TargetAllows' hook: the two categories whose cards are in play (a slot's category is judged the same way).
    private bool EffectPrimitiveTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        if (category.StartsWith(EffectPrimitiveTargets.OwnInPlayWithKeywordPrefix, StringComparison.Ordinal))
            return InPlay(target) && target.OwnerId == player.Id && Keyword(target, category[EffectPrimitiveTargets.OwnInPlayWithKeywordPrefix.Length..]);
        if (category.StartsWith(EffectPrimitiveTargets.PerformerAttachmentPrefix, StringComparison.Ordinal))
        {
            var performer = command.PerformerId is { } performerId && _state.Cards.TryGetValue(performerId, out var chosen) ? chosen : source is null ? null : ActingPersonality(source);
            return performer is not null && target.Zone == Zone.Attached && target.AttachedTo == performer.Id && target.Id != source?.Id
                && Definition(target).Type.ToString() == category[EffectPrimitiveTargets.PerformerAttachmentPrefix.Length..];
        }
        return false;
    }

    private static bool IsEffectPrimitiveOutOfPlayTarget(string category) => category.StartsWith(EffectPrimitiveTargets.AnyDiscardPrefix, StringComparison.Ordinal);

    // ValidateAbility's arm for a card in a discard pile (it is not in play, so it never reaches the in-play categories).
    private void ValidateEffectPrimitiveOutOfPlayTarget(string category, PlayerState player, CardInstance target)
    {
        Require(target.Zone is Zone.FateDiscard or Zone.DynastyDiscard or Zone.Dead, "Choose a card in a discard pile.");
        foreach (var term in category.Split(';').Skip(1))
        {
            if (term == "own") Require(target.OwnerId == player.Id, "Choose a card in one of your own discard piles.");
            else if (term.StartsWith("keyword=", StringComparison.Ordinal)) Require(Keyword(target, term["keyword=".Length..]), $"Choose a {term["keyword=".Length..]} card in a discard pile.");
            else if (term.StartsWith("not-type=", StringComparison.Ordinal)) Require(Definition(target).Type.ToString() != term["not-type=".Length..], $"Choose a card in a discard pile that is not a {term["not-type=".Length..]}.");
            else throw new InvalidOperationException($"Unknown discard-pile category term '{term}'.");
        }
    }
}
