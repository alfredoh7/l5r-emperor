using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace L5R.Core;

// P22 (issue #690): created cards, copies and grants. Engine only -- no card is wired here; the abilities in this file are "training-p22-" fixtures the tests drive, and each is a
// one-line example of the call a card's own feature file makes. The declarative half lives on CreatedCardSpec (CreatedCards.cs, CreatedCardEntry.cs); this file holds what executes it
// and the primitives that are not about creation:
//
//   * REMOVAL OF A CREATED CARD (CreatedCardSpec.RemoveWhen). "When the turn ends" and "after your next turn begins" are the existing timed removals (ScheduleTimedRemoval,
//     EmperorMechD.cs), scheduled as the cards are made. "After the next time this game this Spell straightens or leaves play" is a watch kept here (P22.RemovalWatches): the
//     watch is marked due where a bowed card straightens (Straighten's one call), and after every command a due watch, or one whose card is no longer in play or is found
//     unbowed where it was last seen bowed, removes its created card.
//   * CURRENT VALUES (CreatedValue.Current, CreatedSubject.Slot, CreatedStat.Focus): CurrentCreatedValue is the read DerivePrinted uses.
//   * EXCHANGE, COPY AND SET OF A STAT (core rules Glossary). Set: "give a stat a bonus/penalty sufficient to reach the stated value". Copy: "sets it to the current value of the
//     copied card's stat". Exchange: "note both stats' current values, then simultaneously set each to the other's noted value". All three are modifiers (ApplyModifier for the
//     turn, AddPermanentModifier for "permanently"), so they end, are negated and destroy a Personality at 0 Chi exactly as any other bonus or penalty does.
//   * OVERLAY ONTO AN ARBITRARY PERSONALITY (OverlayOnto): the Experienced overlay's own state transfer (TransferOverlayState), for a card that is not a higher level of the
//     target ("Some card effects overlay onto a Personality that isn't a less-experienced version, following these same mechanics").
//   * FILTER-SCOPED GRANTS, read live through AbilityIdsOf: a rule a card prints ("Your Nonhuman Spirits have the ability ...": CreatedCopies690Catalog.RegisterFilterGrant), or
//     a grant an action makes for a while (GrantAbilityByFilter, P22.FilterGrants). Either reaches a card that enters play later and stops the moment its source is gone.
//   * TRAITS AS CATALOGUE ENTRIES. A trait is an AbilityDefinition with Timing "Trait" (GrantedTraits.Ability). It is not an ability: AbilityIdsOf leaves it out, so it is never
//     offered, played, copied or removed as one; TraitIdsOf is where a card's traits are read. Given with GrantTrait (a GrantedAbility record, so it follows an overlay, ends with
//     its duration and shows in the card's granted abilities with its Trait timing) or printed on a created card (CreatedCardSpec.Abilities). A continuous trait is read with
//     HasTrait by the hook that applies it; a triggered trait has a rule (CreatedCopies690Catalog.RegisterTrait) that the engine runs when its trigger comes -- mandatory, bowed
//     or not, every time, and before any Reaction to the same trigger (core rules, Traits and effects).
//
// Copying an ability onto an attachment for the turn is BeginCopy (CardCopy.cs) with the attachment as the receiver and GrantDuration.Turn; a transfer that changes control is
// TransferAttachment(..., allowControlChange: true) (AttachmentTransfers.cs). Readings: docs/decisions/2026-10-06-690-p22-created-copies-grants.md.

/// <summary>A created card that leaves the game after the next time its watched card straightens or leaves play. WatchedBowed is the watched card's bowed state as last seen;
/// Due is set the moment the watched card straightens, and the removal is carried out when the command in progress ends.</summary>
public sealed class CreatedRemovalWatch
{
    public string CreatedCardId { get; set; } = "";
    public string WatchedCardId { get; set; } = "";
    public bool WatchedBowed { get; set; }
    public bool Due { get; set; }
    public int PlayerId { get; set; }
    public string Source { get; set; } = "";
}

/// <summary>Which of a player's cards in play a filter-scoped grant reaches: of a type (null: any), with every keyword of Keywords and at least one of AnyKeywords.
/// ExcludeSource leaves out the granting card itself ("your other ...").</summary>
public sealed record GrantFilter(CardType? Type = CardType.Personality, string[]? Keywords = null, string[]? AnyKeywords = null, bool ExcludeSource = false);

/// <summary>A grant an action made to every card of PlayerId that fits Filter, for Duration (GrantDuration.Turn or Permanent) and, when SourceCardId is set, only while
/// that card is in play.</summary>
public sealed class FilterGrant
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string AbilityId { get; set; } = "";
    public GrantFilter Filter { get; set; } = new();
    public string Duration { get; set; } = GrantDuration.Turn;
    public int Turn { get; set; }
    public string? SourceCardId { get; set; }
    public string Source { get; set; } = "";
}

public sealed class CreatedCopies690State
{
    public List<CreatedRemovalWatch> RemovalWatches { get; set; } = [];
    public List<FilterGrant> FilterGrants { get; set; } = [];
    public int FilterGrantSequence { get; set; }
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(22200)]
    public CreatedCopies690State P22 { get; set; } = new();
}

/// <summary>A filter-scoped grant a card prints: while a card whose definition GrantorIs accepts is in play, each of its controller's cards that fits Filter has AbilityId.</summary>
internal sealed record FilterGrantRule(string Id, Func<CardDefinition, bool> GrantorIs, string AbilityId, GrantFilter Filter)
{
    /// <summary>The rule for the cards whose catalogue Effect is `effect` (how a card names its own traits).</summary>
    public static Func<CardDefinition, bool> OnEffect(string effect) => definition => definition.Effect == effect;
}

/// <summary>What a triggered trait is told when its trigger comes: the action that has just resolved, its player and its source card.</summary>
internal readonly record struct TraitFiring(AbilityDefinition Ability, int ActingPlayerId, CardInstance? Source);

/// <summary>The rule of a triggered trait (an ability with Timing "Trait"): when Trigger comes, for each card in play that has the trait and passes Condition, Run is carried out.</summary>
internal sealed record GrantedTraitRule(string AbilityId, string Trigger, Action<MatchEngine, TraitFiring, CardInstance> Run, Func<MatchEngine, TraitFiring, CardInstance, bool>? Condition = null);

public static class GrantedTraits
{
    /// <summary>The Timing of an ability that is a trait: never an action, never offered, never copied as an ability.</summary>
    public const string Timing = "Trait";
    /// <summary>"After the resolution of an action ...": any player's action, of any kind; the rule's Condition narrows it.</summary>
    public const string AfterActionResolved = "after-action-resolved";

    /// <summary>The catalogue entry of a trait: its printed text and no target. Its use limit is never read: a trait is not used, it applies.</summary>
    public static AbilityDefinition Ability(string label) => new(Timing, label, "none");

    private static readonly Lazy<HashSet<string>> Ids = new(() => AbilityCatalog.All.Where(p => p.Value.Timing == Timing).Select(p => p.Key).ToHashSet(StringComparer.Ordinal));

    public static bool Is(string abilityId) => Ids.Value.Contains(abilityId);

    /// <summary>AbilityIdsOf's filter: a trait is not one of the abilities a card has.</summary>
    internal static bool IsNot(string abilityId) => !Ids.Value.Contains(abilityId);
}

/// <summary>The rules cards register for P22: filter-scoped grants and triggered traits. A card's feature file registers in its module initializer; a test registers a fixture rule
/// and clears it when it is done.</summary>
internal static class CreatedCopies690Catalog
{
    private static readonly ConcurrentDictionary<string, FilterGrantRule> FilterRules = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, GrantedTraitRule> TraitRules = new(StringComparer.Ordinal);
    private static volatile FilterGrantRule[] _filterRules = [];
    private static volatile GrantedTraitRule[] _traitRules = [];

    public static void RegisterFilterGrant(FilterGrantRule rule)
    {
        if (!FilterRules.TryAdd(rule.Id, rule)) throw new InvalidOperationException($"Filter grant rule {rule.Id} is already registered.");
        _filterRules = FilterRules.Values.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
    }

    public static void RegisterTrait(GrantedTraitRule rule)
    {
        if (!TraitRules.TryAdd(rule.AbilityId, rule)) throw new InvalidOperationException($"Trait {rule.AbilityId} already has a rule.");
        _traitRules = TraitRules.Values.OrderBy(r => r.AbilityId, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Removes the filter grant rule with this id and the trait rule of this ability id.</summary>
    public static void Clear(string id)
    {
        if (FilterRules.TryRemove(id, out _)) _filterRules = FilterRules.Values.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
        if (TraitRules.TryRemove(id, out _)) _traitRules = TraitRules.Values.OrderBy(r => r.AbilityId, StringComparer.Ordinal).ToArray();
    }

    internal static FilterGrantRule[] FilterGrantRules => _filterRules;
    internal static GrantedTraitRule[] Traits => _traitRules;
}

/// <summary>The fixture abilities of P22 (ids "training-p22-"): never on a published card. Each shows the one call a wired card makes.</summary>
public static class CreatedCopies690Abilities
{
    public const string CreateForTurn = "training-p22-create-for-turn", CreateUntilNextTurn = "training-p22-create-until-next-turn", CreateWhileBowed = "training-p22-create-while-bowed",
        CreateCopyUntilNextTurn = "training-p22-create-copy-until-next-turn", CreateValued = "training-p22-create-valued", CreateFocusWeapon = "training-p22-create-focus-weapon",
        CreateArmed = "training-p22-create-armed", ExchangeForceChi = "training-p22-exchange-force-chi", ExchangePermanent = "training-p22-exchange-permanent",
        ExchangeTwoCards = "training-p22-exchange-two-cards", CopyForce = "training-p22-copy-force", CopyChiForTurn = "training-p22-copy-chi-for-turn", Overlay = "training-p22-overlay",
        CopyToAttachment = "training-p22-copy-to-attachment", TransferAnywhere = "training-p22-transfer-anywhere", GrantSpirits = "training-p22-grant-spirits",
        GrantSpiritsWhileHere = "training-p22-grant-spirits-while-here", SelfBoost = "training-p22-self-boost", GiveTrait = "training-p22-give-trait",
        StraightenTrait = "training-p22-trait-straighten", ShieldTrait = "training-p22-trait-shield";

    /// <summary>The target category of CopyToAttachment: another attachment of the Personality the source is attached to.</summary>
    public const string OtherAttachmentOfWielder = "p22-other-attachment-of-wielder";

    public static readonly GrantFilter NonhumanSpirits = new(CardType.Personality, ["Nonhuman", "Spirit"]);

    // The created cards of the fixtures: scans that exist, each still held to its printed type, stats and keywords (CreatedCards.Resolve).
    private static readonly CreatedCardSpec Bat = new(CardType.Personality, Force: 0, Chi: 2, PersonalHonor: 0, Keywords: ["Bat", "Nonhuman", "Cavalry"], ProxyId: "oracle-l5r-10809");
    private static readonly CreatedCardSpec HiredSamurai = new(CardType.Personality, Force: 3, Chi: 3, PersonalHonor: 2, Keywords: ["Samurai"], ProxyId: "oracle-l5r-10811");
    private static readonly CreatedCardSpec Weapon = new(CardType.Item, Chi: 0, Keywords: ["Weapon"], AbilityText: "This Item's Force Modifier is equal to the discarded card's Focus Value",
        ProxyId: "oracle-l5r-11348")
    { HostSlot = "target" };
    /// <summary>A Bat made with an ability and a trait of its own. Bat prints neither, so this spec is not an ability's Creates (CreatedCards.Verify would refuse it, as it must): the
    /// fixture hands it to ExecuteCreate itself, to show what CreatedCardSpec.Abilities and Trait write on the created card.</summary>
    public static readonly CreatedCardSpec ArmedBat = Bat with { Abilities = [SelfBoost, ShieldTrait], Trait = "training-p22-armed-bat" };

    public static Dictionary<string, AbilityDefinition> AddCreatedCopies690(this Dictionary<string, AbilityDefinition> all)
    {
        all[CreateForTurn] = new("Open", "Training: create a Bat; remove it from the game when the turn ends", "none", Creates: Bat with { RemoveWhen = CreatedRemoval.TurnEnds });
        all[CreateUntilNextTurn] = new("Open", "Training: create a 3F/3C/2PH Samurai; remove it from the game after your next turn begins", "none",
            Creates: HiredSamurai with { RemoveWhen = CreatedRemoval.NextTurnBegins });
        all[CreateWhileBowed] = new("Open", "Training: bow this card: create a Bat; after the next time this game this card straightens or leaves play, remove the Bat from the game", "none",
            Costs: ActionCost.BowSource, Creates: Bat with { RemoveWhen = CreatedRemoval.WhenSourceStraightensOrLeaves });
        all[CreateCopyUntilNextTurn] = new("Open", "Training: create a copy of a target Personality; remove the copy from the game after your next turn begins", "personality",
            Creates: new CreatedCardSpec(CardType.Personality, CopyOfTarget: true) { RemoveWhen = CreatedRemoval.NextTurnBegins });
        all[CreateValued] = new("Open", "Training: create a Samurai with Force equal to this card's Force plus 1 (plus 2 more if it is a Commander), and the target's Chi and Personal Honor", "personality",
            Creates: HiredSamurai with
            {
                Values = [new(CreatedStat.Force, CreatedSubject.Source, Plus: 1, Keyword: "Commander", PlusWithKeyword: 2) { Current = true },
                    new(CreatedStat.Chi, CreatedSubject.Target) { Current = true }, new(CreatedStat.PersonalHonor, CreatedSubject.Target) { Current = true }]
            });
        all[CreateFocusWeapon] = new("Open", "Training: discard a card to create a Weapon with a Force Modifier equal to the discarded card's Focus Value plus 1 and attach it to your target Personality",
            "slots", Slots: [new("discard", "hand", "A card from your hand, to discard") { CostOnly = true }, new("target", "own-personality", "Your Personality, to carry the Weapon")],
            Costs: [new(CostVerbs.Discard, CostSubject.Slot("discard"))],
            Creates: Weapon with { Values = [new(CreatedStat.Force, CreatedSubject.Slot, Read: CreatedStat.Focus, Plus: 1) { Slot = "discard" }] });
        all[CreateArmed] = new("Open", "Training: create a Bat with an ability and a trait", "none");
        all[ExchangeForceChi] = new("Open", "Training: switch a target Personality's Force and Chi", "personality");
        all[ExchangePermanent] = new("Open", "Training: permanently switch a target Personality's Force and Chi", "personality");
        all[ExchangeTwoCards] = new("Open", "Training: exchange this card's Force with a target Personality's Chi", "personality");
        all[CopyForce] = new("Open", "Training: this card permanently copies a target Personality's Force", "personality");
        all[CopyChiForTurn] = new("Open", "Training: this card copies a target Personality's Chi", "personality");
        all[Overlay] = new("Open", "Training: if the top card of the target Personality's controller's Dynasty deck is a Personality, overlay him on the target", "personality");
        all[CopyToAttachment] = new("Open", "Training: target another attachment of this Personality: copy an ability on it that does not itself copy abilities to this card (until the turn ends)",
            OtherAttachmentOfWielder, CopiesAbilities: true);
        all[TransferAnywhere] = new("Open", "Training: transfer a target attachment to any player's target Personality (this may change control of the attachment)", "slots",
            Slots: [new("attachment", "attachment", "An attachment"), new("to", "personality", "Any player's Personality")]);
        all[GrantSpirits] = new("Open", "Training: until the turn ends, your Nonhuman Spirits have the ability, 'Give this card +1F'", "none");
        all[GrantSpiritsWhileHere] = new("Open", "Training: while this card is in play, your Nonhuman Spirits have the ability, 'Give this card +1F'", "none");
        all[SelfBoost] = new("Open", "Training: give this card +1F", "none");
        all[GiveTrait] = new("Open", "Training: permanently give a target Personality the trait, 'After the resolution of an action from an Item, straighten this Personality'", "personality");
        all[StraightenTrait] = GrantedTraits.Ability("After the resolution of an action from an Item, straighten this Personality");
        all[ShieldTrait] = GrantedTraits.Ability("Training: a continuous trait of a created card");
        return all;
    }
}

public sealed partial class MatchEngine
{
    private CreatedCopies690State P22 => _state.P22;

    // ---- removal of a created card (CreatedCardSpec.RemoveWhen) --------------------------------------------------------------------------------------------------------------------------

    // ExecuteCreate's step: the rest of the creation (beforeWindow) is run after the removal has been arranged for the cards this creation minted. They are the cards of the given
    // definition whose "created-" numbers follow the number the sequence stood at, so a card another effect creates meanwhile is not taken for one of them.
    private Action? WithCreatedRemoval(CreatedCardSpec spec, GameCommand command, PlayerState player, string definitionId, Action? beforeWindow)
    {
        if (spec.RemoveWhen is not { } removal) return beforeWindow;
        var first = _state.CreatedCardSequence;
        var sourceName = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var source) ? Definition(source).Name : "A created card";
        return () =>
        {
            for (var number = first + 1; number <= _state.CreatedCardSequence; number++)
                if (_state.Cards.TryGetValue($"created-{number:D4}", out var created) && created.DefinitionId == definitionId && InPlay(created))
                    ArrangeCreatedRemoval(created, removal, command, player, sourceName);
            beforeWindow?.Invoke();
        };
    }

    private void ArrangeCreatedRemoval(CardInstance created, CreatedRemoval removal, GameCommand command, PlayerState player, string sourceName)
    {
        if (!removal.IsWatch)
        {
            ScheduleTimedRemoval(created, removal.When, player.Id, sourceName);
            if (IsCopyCard(created)) AddCopyCondition(created, $"Removed from the game {DescribeRemovalMoment(removal.When, player.Id)} ({sourceName}).");
            return;
        }
        // No card to watch (it left play as a cost of this very action): it has already "left play", so the created card goes at the end of this command.
        var watched = ValueSubject(CreatedSubject.Source, command);
        P22.RemovalWatches.Add(new CreatedRemovalWatch { CreatedCardId = created.Id, WatchedCardId = watched?.Id ?? "", WatchedBowed = watched?.Bowed ?? false, PlayerId = player.Id, Source = sourceName });
        var watchedName = watched is null ? sourceName : Definition(watched).Name;
        Emit("createdRemovalWatched", $"{sourceName}: {Definition(created).Name} will be removed from the game after the next time {watchedName} straightens or leaves play.", player.Id, created.Id);
        if (IsCopyCard(created)) AddCopyCondition(created, $"Removed from the game after the next time {watchedName} straightens or leaves play.");
    }

    // After every command (RemoveCreatedCardsLeftInPiles' call): the watches whose card straightened or left play, and the filter grants that have ended.
    private void SweepCreatedCopies690()
    {
        if (P22.FilterGrants.Count > 0) P22.FilterGrants.RemoveAll(g => !FilterGrantLive(g));
        if (P22.RemovalWatches.Count == 0) return;
        var removedAny = false;
        foreach (var watch in P22.RemovalWatches.ToArray())
        {
            if (!_state.Cards.TryGetValue(watch.CreatedCardId, out var created) || !InPlay(created)) { P22.RemovalWatches.Remove(watch); continue; }
            var watched = _state.Cards.TryGetValue(watch.WatchedCardId, out var found) ? found : null;
            var left = watched is null || !InPlay(watched);
            var straightened = !left && (watch.Due || watch.WatchedBowed && !watched!.Bowed);
            if (!left && !straightened) { watch.WatchedBowed = watched!.Bowed; continue; }
            P22.RemovalWatches.Remove(watch);
            var name = Definition(created).Name;
            if (Definition(created).Type == CardType.Personality) RemoveUnitFromGame(created, watch.Source);
            else RemoveFromGame(created);
            Emit("createdRemoval", $"{watch.Source}: {name} is removed from the game ({(left ? "its card left play" : "its card straightened")}).", watch.PlayerId, created.Id);
            removedAny = true;
        }
        if (removedAny) ReturnCardsOutOfPlayToTheirOwners(); // an attachment of the removed unit that another player owned goes back to its owner now, as after any command
    }

    // Straighten's call, made as a card is about to go from bowed to unbowed by the Straighten Phase or an effect: the watches on it are due. The removal itself waits for the end
    // of the command (SweepCreatedCopies690), so it never happens in the middle of the effect that straightened the card.
    private void NoteCreatedCopies690Straightens(CardInstance card)
    {
        if (!card.Bowed || P22.RemovalWatches.Count == 0) return;
        foreach (var watch in P22.RemovalWatches) if (watch.WatchedCardId == card.Id) watch.Due = true;
    }

    // ---- current values --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private int CurrentCreatedValue(CardInstance subject, CreatedStat read) => read switch
    {
        CreatedStat.GoldCost => CardGoldCost(subject),
        CreatedStat.Force => CurrentStat(subject, "Force"),
        CreatedStat.Chi => CurrentStat(subject, "Chi"),
        CreatedStat.PersonalHonor => CurrentStat(subject, "PersonalHonor"),
        _ => Definition(subject).Focus
    };

    /// <summary>A card's stat as it stands: "Force", "Chi" or "PersonalHonor", with everything that modifies it (a Personality's Chi includes his Items').</summary>
    internal int CurrentStat(CardInstance card, string stat) => stat switch
    {
        "Force" => CardForce(card),
        "Chi" => Definition(card).Type == CardType.Personality ? PersonalityChi(card) : Definition(card).Chi + StatModifier(card, "Chi"),
        "PersonalHonor" => CardPersonalHonor(card),
        _ => throw new InvalidOperationException($"{stat} is not a stat that is read as a current value.")
    };

    /// <summary>A stat before any floor at 0 and any cap: Force may be below 0 here, and a Force or Personal Honor cap is not applied. A Set measures from this.</summary>
    private int RawStat(CardInstance card, string stat) => stat switch
    {
        "Force" => Definition(card).Type switch
        {
            CardType.Personality => PersonalityForceRaw(card),
            CardType.Follower => FollowerForceRaw(card),
            _ => Definition(card).Force + StatModifier(card, "Force")
        },
        "PersonalHonor" => CardPersonalHonorUncapped(card),
        _ => CurrentStat(card, stat)
    };

    // ---- Set, Copy, Exchange ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool StatCanBeSet(CardDefinition definition, string stat) => stat switch
    {
        "Force" => definition.Type is CardType.Personality or CardType.Follower,
        "Chi" or "PersonalHonor" => definition.Type == CardType.Personality,
        _ => false
    };

    /// <summary>Set (core rules Glossary): "give a stat a bonus/penalty sufficient to reach the stated value". For the turn, or permanently. The bonus or penalty is an ordinary
    /// one: an effect that negates it leaves the stat where it was, and a Personality whose Chi is set to 0 is destroyed. Returns whether a modifier was applied.</summary>
    internal bool SetStat(CardInstance card, string stat, int value, string sourceId, string effect, bool permanent = false)
    {
        Require(StatCanBeSet(Definition(card), stat), $"{Definition(card).Name} has no {stat} to set.");
        return InPlay(card) && GiveSufficient(card, stat, value - RawStat(card, stat), sourceId, effect, permanent);
    }

    internal void ApplyModifierForTest(string cardId, int force) => ApplyModifier(Card(cardId), "test-setter", "modifier-test", "Force", force);
    internal bool SetStatForTest(string cardId, string stat, int value, bool permanent = false) => SetStat(Card(cardId), stat, value, "test-setter", "set-test", permanent);

    // The bonus or penalty of a Set, worked out by the caller (an Exchange works both out from the noted values before either is given). False when nothing was applied.
    // The delta is measured from the stat's raw value (RawStat: no floor, no cap), so a Force floored at 0 gets exactly enough to reach the stated value, and a capped Force is set underneath its cap.
    private bool GiveSufficient(CardInstance card, string stat, int delta, string sourceId, string effect, bool permanent)
    {
        if (delta == 0 || !InPlay(card)) return false;
        var modifiers = card.Modifiers.Count;
        if (permanent) AddPermanentModifier(card, sourceId, effect, stat, delta); else ApplyModifier(card, sourceId, effect, stat, delta);
        if (!InPlay(card)) return true; // a Chi penalty that destroyed him
        if (card.Modifiers.Count != modifiers + 1) return false; // negated or delayed
        // "A Personality reduced to 0 Chi is destroyed": ApplyModifier sees to it for the turn's penalty; a permanent one is judged here.
        if (permanent && stat == "Chi" && Definition(card).Type == CardType.Personality && PersonalityChi(card) <= 0) { DestroyUnits([card], true); return true; }
        return true;
    }

    /// <summary>Copy (core rules Glossary): "copying a stat sets it to the current value of the copied card's stat". The value is read once, now.</summary>
    internal bool CopyStat(CardInstance from, CardInstance to, string stat, string sourceId, string effect, bool permanent = false)
    {
        Require(from.Id != to.Id, "A card never copies its own stats.");
        var value = CurrentStat(from, stat);
        var applied = SetStat(to, stat, value, sourceId, effect, permanent);
        Emit("statCopied", $"{Definition(to).Name} {(permanent ? "permanently " : "")}copies the {StatName(stat)} of {Definition(from).Name} ({value}).", to.OwnerId, to.Id, value);
        return applied;
    }

    /// <summary>Exchange (core rules Glossary): "note both stats' current values, then simultaneously set each to the other's noted value". The two stats may be two stats of one
    /// card ("Switch the target's Force and Chi") or one stat of each of two cards. Both values are noted before either is set.</summary>
    internal void ExchangeStats(CardInstance a, string statA, CardInstance b, string statB, string sourceId, string effect, bool permanent = false)
    {
        Require(a.Id != b.Id || statA != statB, "A stat is not exchanged with itself.");
        Require(StatCanBeSet(Definition(a), statA) && StatCanBeSet(Definition(b), statB), "Those stats cannot be exchanged.");
        var (notedA, notedB) = (CurrentStat(a, statA), CurrentStat(b, statB));
        Emit("statsExchanged", a.Id == b.Id
            ? $"{Definition(a).Name}'s {StatName(statA)} ({notedA}) and {StatName(statB)} ({notedB}) are {(permanent ? "permanently " : "")}exchanged."
            : $"{Definition(a).Name}'s {StatName(statA)} ({notedA}) and {Definition(b).Name}'s {StatName(statB)} ({notedB}) are {(permanent ? "permanently " : "")}exchanged.", a.OwnerId, a.Id);
        // Simultaneous: both bonuses or penalties are worked out from the noted values before either is given.
        var (deltaA, deltaB) = (notedB - RawStat(a, statA), notedA - RawStat(b, statB));
        GiveSufficient(a, statA, deltaA, sourceId, effect, permanent);
        GiveSufficient(b, statB, deltaB, sourceId, effect, permanent);
    }

    private static string StatName(string stat) => stat == "PersonalHonor" ? "Personal Honor" : stat;

    // ---- overlay onto an arbitrary Personality ---------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Why a card may not be overlaid onto a Personality in play, or null. The Experienced overlay's rules without its title and level tests: "overlaying skips Honor
    /// Requirement and entering-play costs, but other requirements/restrictions (e.g. Loyal) still apply" (core rules, Experienced).</summary>
    internal string? OverlayOntoError(CardInstance target, CardInstance newCard)
    {
        if (!InPlay(target) || Definition(target).Type != CardType.Personality) return "Only a Personality in play is overlaid.";
        if (newCard.Id == target.Id || InPlay(newCard) || Definition(newCard).Type != CardType.Personality) return "Only a Personality that is not in play overlays another.";
        var controller = target.OwnerId;
        if (!LoyaltyAllowsControl(newCard, Player(controller).Clan, controller)) return $"{Definition(newCard).Name} is Loyal and cannot be controlled by {Player(controller).Name}.";
        if ((Keyword(newCard, "Unique") || Keyword(newCard, "Singular")) && _state.Cards.Values.Any(c => c.Id != target.Id && c.OwnerId == controller && InPlay(c)
                && string.Equals(Experienced.BaseTitle(Definition(c).Name), Experienced.BaseTitle(Definition(newCard).Name), StringComparison.OrdinalIgnoreCase)))
            return $"{Player(controller).Name} already controls this {(Keyword(newCard, "Unique") ? "Unique" : "Singular")} card.";
        return null;
    }

    /// <summary>Overlays <paramref name="newCard"/> (a Personality in a deck, a hand, a province or a pile) onto <paramref name="target"/>, a Personality in play: the new card takes
    /// the target's place under the target's controller with its state, ongoing effects, attachments and tokens, and the target's card is removed from the game. The new card does
    /// not enter play (no entering-play window, no entering-play cost). Returns false, with nothing changed, when the overlay is not allowed. A province the new card came from
    /// is left empty for the caller to refill.</summary>
    internal bool OverlayOnto(CardInstance target, CardInstance newCard, string via)
    {
        if (OverlayOntoError(target, newCard) is { } error)
        {
            Emit("overlayRefused", $"{via}: {error}", target.OwnerId, newCard.Id);
            return false;
        }
        var controller = target.OwnerId;
        var targetName = Definition(target).Name;
        foreach (var player in _state.Players)
        {
            player.Hand.Remove(newCard.Id); player.FateDeck.Remove(newCard.Id); player.DynastyDeck.Remove(newCard.Id);
            foreach (var province in player.Provinces.Where(p => p.CardId == newCard.Id)) province.CardId = null;
        }
        ResetCardState(newCard); // a new object: nothing of an earlier life follows it; what it carries from now is the target's
        RestoreOwner(newCard);
        TakeControl(newCard, controller);
        TransferOverlayState(target, newCard);
        RemoveFromGame(target);
        Emit("personalityOverlayed", $"{via}: {Definition(newCard).Name} was overlaid onto {targetName}.", controller, newCard.Id);
        return true;
    }

    // ---- filter-scoped grants ------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"Your [filter] have the ability ..." made by an action: every card of the player that fits the filter has the ability (or the trait), judged each time abilities
    /// are read, for the duration (GrantDuration.Turn or Permanent) and, when a source card is named, only while that card is in play.</summary>
    internal FilterGrant GrantAbilityByFilter(int playerId, GrantFilter filter, string abilityId, string duration, string source, string? sourceCardId = null)
    {
        Require(AbilityCatalog.All.TryGetValue(abilityId, out var ability) && !ability.PlayerAbility, "Only a catalogued card ability can be granted.");
        Require(duration is GrantDuration.Turn or GrantDuration.Permanent, "A filter grant lasts for the turn or permanently.");
        var grant = new FilterGrant
        {
            Id = ++P22.FilterGrantSequence,
            PlayerId = playerId,
            AbilityId = abilityId,
            Filter = filter,
            Duration = duration,
            Turn = _state.TurnNumber,
            SourceCardId = sourceCardId,
            Source = source
        };
        P22.FilterGrants.Add(grant);
        Emit("abilityGrantedByFilter", $"{source}: {Player(playerId).Name}'s {DescribeFilter(filter)} have the ability: {ability!.Label}.", playerId, sourceCardId);
        return grant;
    }

    private static string DescribeFilter(GrantFilter filter)
    {
        var words = (filter.Keywords ?? []).ToList();
        if (filter.AnyKeywords is { Length: > 0 } any) words.Add(string.Join(" or ", any));
        words.Add(filter.Type is { } type ? (type == CardType.Personality ? "Personalities" : type + "s") : "cards");
        return string.Join(' ', words);
    }

    private bool FilterGrantLive(FilterGrant grant) =>
        (grant.Duration == GrantDuration.Permanent || grant.Turn == _state.TurnNumber)
        && (grant.SourceCardId is null || _state.Cards.TryGetValue(grant.SourceCardId, out var source) && InPlay(source));

    private bool FilterMatches(GrantFilter filter, CardInstance card, string? sourceCardId) =>
        (filter.Type is null || Definition(card).Type == filter.Type) && (!filter.ExcludeSource || card.Id != sourceCardId)
        && (filter.Keywords is null || filter.Keywords.All(k => Keyword(card, k))) && (filter.AnyKeywords is not { Length: > 0 } any || any.Any(k => Keyword(card, k)));

    // AbilityIdsOf's read (through StaticGrantedAbilityIds): the abilities a card in play has from a printed filter rule or a stored filter grant. Nothing is computed while no
    // rule is registered and no grant is stored.
    private IEnumerable<string> FilterGrantedAbilityIds(CardInstance card)
    {
        var rules = CreatedCopies690Catalog.FilterGrantRules;
        if (rules.Length == 0 && P22.FilterGrants.Count == 0 || !InPlay(card)) return [];
        var granted = new List<string>();
        foreach (var grant in P22.FilterGrants)
            if (grant.PlayerId == card.OwnerId && FilterGrantLive(grant) && FilterMatches(grant.Filter, card, grant.SourceCardId)) granted.Add(grant.AbilityId);
        foreach (var rule in rules)
            foreach (var grantor in _state.Cards.Values)
                if (grantor.OwnerId == card.OwnerId && InPlay(grantor) && rule.GrantorIs(Definition(grantor)) && FilterMatches(rule.Filter, card, grantor.Id)) { granted.Add(rule.AbilityId); break; }
        return granted;
    }

    // ---- traits ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Gives a card a trait (a catalogue entry with Timing "Trait"): recorded as a GrantedAbility, so it is listed with the card's granted abilities, follows an overlay
    /// and ends with its duration.</summary>
    internal void GrantTrait(CardInstance receiver, string traitId, string source, string duration = GrantDuration.Permanent)
    {
        Require(GrantedTraits.Is(traitId), "Only a trait can be given as a trait.");
        GrantAbility(receiver, traitId, source, duration);
        Emit("traitGranted", $"{Definition(receiver).Name} {(duration == GrantDuration.Permanent ? "permanently " : "")}gains the trait: {AbilityCatalog.All[traitId].Label}.", receiver.OwnerId, receiver.Id);
    }

    /// <summary>The traits a card has: printed (or declared when it was created), granted, or given by a filter-scoped grant. A trait is not an ability, so nothing that removes or
    /// copies a card's abilities touches it.</summary>
    private IEnumerable<string> TraitIdsOf(CardInstance card) =>
        (Definition(card).Abilities ?? []).Concat(card.GrantedAbilities.Where(g => GrantHolds(g.Duration, card)).Select(g => g.AbilityId)).Concat(FilterGrantedAbilityIds(card))
            .Where(GrantedTraits.Is).Distinct(StringComparer.Ordinal);

    /// <summary>Whether a card in play has a trait: what the hook that applies a continuous trait asks.</summary>
    internal bool HasTrait(CardInstance card, string traitId) => InPlay(card) && TraitIdsOf(card).Contains(traitId, StringComparer.Ordinal);

    // FireAfterOwnActionTraits' call: an action has resolved. Every triggered trait with that trigger, on every card in play that has it, in card order: mandatory, bowed or not,
    // not limited to once a turn, and before the window in which Reactions to the same resolution are played.
    private void FireCreatedCopies690Traits(AbilityDefinition ability, int actingPlayerId, CardInstance? source)
    {
        var rules = CreatedCopies690Catalog.Traits;
        if (rules.Length == 0) return;
        var firing = new TraitFiring(ability, actingPlayerId, source);
        foreach (var card in _state.Cards.Values.Where(InPlay).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            if (!InPlay(card)) continue; // an earlier trait took it out of play
            var has = TraitIdsOf(card).ToHashSet(StringComparer.Ordinal);
            if (has.Count == 0) continue;
            foreach (var rule in rules)
            {
                if (rule.Trigger != GrantedTraits.AfterActionResolved || !has.Contains(rule.AbilityId) || !InPlay(card) || rule.Condition?.Invoke(this, firing, card) == false) continue;
                Emit("traitFired", $"{Definition(card).Name}: {AbilityCatalog.All[rule.AbilityId].Label}.", card.OwnerId, card.Id);
                rule.Run(this, firing, card);
            }
        }
    }

    // ---- the fixtures ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool CreatedCopies690TargetAllows(string category, CardInstance? source, CardInstance target) =>
        category == CreatedCopies690Abilities.OtherAttachmentOfWielder && source is { Zone: Zone.Attached, AttachedTo: { } hostId } && target.Zone == Zone.Attached && target.AttachedTo == hostId && target.Id != source.Id;

    private void ValidateCreatedCopies690(string id, GameCommand command)
    {
        if (id != CreatedCopies690Abilities.TransferAnywhere) return;
        // A transfer's attaching restrictions are part of what makes its targets legal (ValidateSelfTransfer's reasoning), so they are judged as the action is announced.
        var attachment = SlotCards(command, "attachment").Single();
        var host = SlotCards(command, "to").Single();
        Require(CanTransferTo(attachment, host, allowControlChange: true), $"{Definition(attachment).Name} cannot be transferred to {Definition(host).Name}.");
    }

    private bool? RunCreatedCopies690(RunHookContext c)
    {
        var (id, ability, command, player, source, target) = (c.id, c.ability, c.command, c.player, c.source, c.target);
        switch (id)
        {
            // The creators with a Creates spec need no case: the default arm creates what the spec says, RemoveWhen and Values included (CreatedCardEntry.cs).
            case CreatedCopies690Abilities.CreateArmed:
                return ExecuteCreate(ability, CreatedCopies690Abilities.ArmedBat, command, player);
            case CreatedCopies690Abilities.ExchangeForceChi or CreatedCopies690Abilities.ExchangePermanent:
                ExchangeStats(target!, "Force", target!, "Chi", source!.Id, id, permanent: id == CreatedCopies690Abilities.ExchangePermanent);
                return false;
            case CreatedCopies690Abilities.ExchangeTwoCards:
                ExchangeStats(source!, "Force", target!, "Chi", source!.Id, id);
                return false;
            case CreatedCopies690Abilities.CopyForce:
                CopyStat(target!, source!, "Force", source!.Id, id, permanent: true);
                return false;
            case CreatedCopies690Abilities.CopyChiForTurn:
                CopyStat(target!, source!, "Chi", source!.Id, id);
                return false;
            case CreatedCopies690Abilities.Overlay:
                if (Player(target!.OwnerId).DynastyDeck.FirstOrDefault() is { } topId && Definition(Card(topId)).Type == CardType.Personality) OverlayOnto(target!, Card(topId), Definition(source!).Name);
                return false;
            case CreatedCopies690Abilities.CopyToAttachment:
                // The receiver is the attachment itself; with one candidate it is taken, with several the player chooses (CardCopy.cs).
                return BeginCopy(source!, target!, id, [CopyStep.Ability], player.Id, "ability", c.bowedBeforeAction, GrantDuration.Turn);
            case CreatedCopies690Abilities.TransferAnywhere:
                TransferAttachment(SlotCards(command, "attachment").Single(), SlotCards(command, "to").Single(), Definition(source!).Name, allowControlChange: true);
                return false;
            case CreatedCopies690Abilities.GrantSpirits:
                GrantAbilityByFilter(player.Id, CreatedCopies690Abilities.NonhumanSpirits, CreatedCopies690Abilities.SelfBoost, GrantDuration.Turn, Definition(source!).Name);
                return false;
            case CreatedCopies690Abilities.GrantSpiritsWhileHere:
                GrantAbilityByFilter(player.Id, CreatedCopies690Abilities.NonhumanSpirits, CreatedCopies690Abilities.SelfBoost, GrantDuration.Permanent, Definition(source!).Name, source!.Id);
                return false;
            case CreatedCopies690Abilities.SelfBoost:
                ApplyModifier(source!, source!.Id, id, "Force", 1);
                return false;
            case CreatedCopies690Abilities.GiveTrait:
                GrantTrait(target!, CreatedCopies690Abilities.StraightenTrait, source!.Id);
                return false;
            default:
                return null;
        }
    }

    // Test seams (L5R.Core.Tests has InternalsVisibleTo).
    internal bool CanTransferToForTest(string attachmentId, string hostId, bool allowControlChange) => CanTransferTo(Card(attachmentId), Card(hostId), allowControlChange);
    internal void TransferAttachmentForTest(string attachmentId, string hostId, bool allowControlChange) => TransferAttachment(Card(attachmentId), Card(hostId), "test", allowControlChange);
    internal bool HasTraitForTest(string cardId, string traitId) => HasTrait(Card(cardId), traitId);
    internal CardInstance P22CardForTest(string id) => Card(id);

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterCreatedCopies690Hooks()
    {
        EngineHooks.Abilities.Register("P22Abilities", 16200, static abilities => abilities.AddCreatedCopies690());
        EngineHooks.RunEffects.Register("P22RunEffects", 16200, static (engine, context) => engine.RunCreatedCopies690(context));
        EngineHooks.TargetAllows.Register("P22TargetAllows", 16200, static (engine, context) => engine.CreatedCopies690TargetAllows(context.ability.Target, context.source, context.target));
        EngineHooks.ValidateLate.Register("P22ValidateLate", 16200, static (engine, context) => engine.ValidateCreatedCopies690(context.id, context.command));
    }
}
