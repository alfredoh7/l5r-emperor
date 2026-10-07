namespace L5R.Core;

// Copying keywords and abilities (issue #338): Fukuzo's "Until the next time this game this ability is used, Fukuzo copies one
// keyword, and one ability that does not itself copy abilities, from the Personality." Two INDEPENDENT primitives, composed by
// whichever card wants both (or either):
//
//   * copy one KEYWORD from a card: the offered keywords are a snapshot of the source's keywords at the moment the action
//     resolves (printed keywords, less any removed from it, plus permanent grants -- a dead Personality keeps its permanent
//     changes), minus those the receiver already has. The player picks one (own PendingChoice "copyKeyword", own command
//     "chooseCopyKeyword", own view data);
//   * copy one ABILITY from a card: the same snapshot idea over the source's ability ids (printed, and granted -- a permanent
//     grant or an earlier copy), minus abilities that themselves copy abilities, minus abilities that trigger on their own card
//     entering play, minus those the receiver already has. Own PendingChoice "copyAbility", own command "chooseCopyAbility".
//
// A card asks for whichever steps it needs (BeginCopy's `steps`): Fukuzo asks for both, in printed order (keyword, then ability),
// a future keyword-only or ability-only card asks for one. A step with nothing to offer is skipped, one with a single candidate is
// taken automatically (the printed "copies one" is not optional), and only a real choice opens a decision. The copied keyword is a
// GrantedKeyword and the copied ability a GrantedAbility, both with Duration "Copy" and Source = the copying ability's id, so the
// NEXT use of that ability ends the old copies and makes new ones ("until the next time this game this ability is used"). A copy
// with another duration (e.g. "Turn" for "this turn") is made the same way and never replaced.
//
// A copied ability is live: MatchEngine.AbilityIdsOf adds it to the card's printed abilities everywhere abilities are found
// (validation, the offered actions, reaction windows), so it is usable, and limited (its usage stamp lives on the receiver, under the
// ability's own UsageKey), exactly as if printed on the receiver. Every reading is in docs/decisions/2026-09-19-copy-and-either-or-
// primitives.md.

/// <summary>The durations a grant can have. "Copy" (issue #338): a copied keyword/ability, replaced the next time the copying
/// ability is used.</summary>
public static class GrantDuration
{
    public const string Permanent = "Permanent";
    public const string Turn = "Turn";
    public const string Battle = "Battle";
    public const string Attachment = "Attachment";
    public const string Copy = "Copy";
}

/// <summary>The two independent copy steps, in the order a card that asks for both runs them.</summary>
public static class CopyStep
{
    public const string Keyword = "keyword";
    public const string Ability = "ability";
}

/// <summary>Engine-private context of the copy in progress (MatchState.PendingCopy). KeywordChoices and AbilityChoices are the
/// snapshot taken when the action resolved (the source may already have left the game as the action's cost); Steps are the
/// steps still to answer, the first of which is the open decision.</summary>
public sealed class PendingCopy
{
    public int PlayerId { get; set; }
    /// <summary>The card that gains the copies (Fukuzo).</summary>
    public string ReceiverId { get; set; } = "";
    /// <summary>The card whose action this is; it resumes when the copy is finished.</summary>
    public string ActionCardId { get; set; } = "";
    /// <summary>The copying ability's id: the Source of every grant made, so its next use can replace them.</summary>
    public string CopierAbilityId { get; set; } = "";
    public string Duration { get; set; } = GrantDuration.Copy;
    public string SourceDefinitionId { get; set; } = "";
    public string SourceName { get; set; } = "";
    public List<string> Steps { get; set; } = [];
    public List<string> KeywordChoices { get; set; } = [];
    public List<string> AbilityChoices { get; set; } = [];
    public string Origin { get; set; } = "ability";
    public List<string>? BowedBefore { get; set; }
}

/// <summary>One thing that may be copied: for a keyword, Value and Label are the keyword; for an ability, Value is the ability id,
/// Label the engine's text for it and Detail its timing ("Open", "Battle", "Reaction: before an action resolves").</summary>
public sealed record CopyChoiceView(string Value, string Label, string? Detail);

/// <summary>PRIVATE to the choosing player (null in everyone else's view). The open step, Step "keyword" or "ability", with its
/// Choices; RemainingSteps lists the steps after this one (so a wizard can say "then choose an ability"); Duration says how long the
/// copy lasts ("Copy": until the copying ability is next used). The source is public information (a card in a discard pile, or
/// removed from the game), so nothing here is hidden -- it is private only because only the chooser has a decision.</summary>
public sealed record CopyView(string Step, string[] RemainingSteps, string ReceiverId, string ReceiverName, string SourceDefinitionId,
    string SourceName, string Label, string Prompt, string Duration, CopyChoiceView[] Choices);

public sealed partial class MatchEngine
{
    private static bool IsCopyChoice(string type) => type is "copyKeyword" or "copyAbility";

    // The ability ids a card has now: printed, plus granted (a permanent grant, a copy), plus a controller-wide STATIC grant (issue #458, The Towers of
    // the Yogo: "Each of your Scorpion Clan Personalities has the ability ..." -- StaticAbilityGrants.cs). The ONE place a granted ability is joined
    // to the printed ones; every scan of "the abilities this card has" goes through it.
    private IEnumerable<string> AbilityIdsOf(CardInstance card) => RemovedAbilitiesFilter(card, P5AbilitiesRemoved(card) ? [] : // Unsettling Doubts (issue #503): "Permanently remove its abilities".
                                                                                                                                // Issue #501: a copy of a card does not have the printed abilities that "will not be copied" (Shadow Dragon's, Sachina's, Matsu Nimuro's).
        (Definition(card).Abilities ?? []).Where(id => !IsCopyCard(card) || !(AbilityCatalog.All.TryGetValue(id, out var printed) && printed.NotCopyable)).Concat(card.GrantedAbilities.Where(g => GrantHolds(g.Duration, card)).Select(g => g.AbilityId)).Concat(StaticGrantedAbilityIds(card)).Distinct(StringComparer.Ordinal).Where(GrantedTraits.IsNot)); // issue #667 (AbilityRemoval.cs): permanently removed abilities are left out. P22 (issue #690): a trait is not an ability.

    // A grant that is still "on" a card that is not in play: a permanent one, or a copy (which lasts until the copying ability is
    // used again, wherever the receiver is). A turn or attachment grant belongs to a card in play -- it does not follow a Personality
    // into a discard pile (its attachments leave with it), so it is neither a current keyword nor a current ability there.
    private static bool GrantHolds(string duration, CardInstance card) => duration is GrantDuration.Permanent or GrantDuration.Copy || InPlay(card);

    /// <summary>The keywords a card has, as a list: printed then granted, with any removed keyword left out (a dead Personality keeps
    /// its permanent changes: its grants and removals are read as they stand). Conditional live grants (Isawa Kojiro's Naval while
    /// he has a Spell) are effects, not keywords the card has, and are not listed.</summary>
    private string[] CurrentKeywordList(CardInstance card) =>
        (Definition(card).Keywords ?? []).Concat(card.GrantedKeywords.Where(g => GrantHolds(g.Duration, card)).Select(g => g.Keyword))
            .Where(keyword => !KeywordRemoved(card, keyword)).Distinct(StringComparer.Ordinal).ToArray();

    private IEnumerable<string> CurrentAbilityIds(CardInstance card) =>
        (Definition(card).Abilities ?? []).Concat(card.GrantedAbilities.Where(g => GrantHolds(g.Duration, card)).Select(g => g.AbilityId)).Distinct(StringComparer.Ordinal);

    // "An ability that does not itself copy abilities" -- and one that triggers on its own card ENTERING play is not offered either:
    // that moment has already happened for the receiver, so the copy could never do anything.
    // P22 (issue #690): a trait is not an ability (core rules, Reading the Cards), so one catalogued as a trait (GrantedTraits.Timing) is not offered either.
    private static bool CopyableAbility(AbilityDefinition ability) => !ability.CopiesAbilities && !ability.NotCopyable && ability.Trigger != "own-enters-play" && ability.Timing != GrantedTraits.Timing;

    private string[] CopyableKeywords(CardInstance source, CardInstance receiver) =>
        CurrentKeywordList(source).Where(keyword => !Keyword(receiver, keyword)).ToArray();

    private string[] CopyableAbilityIds(CardInstance source, CardInstance receiver)
    {
        var has = AbilityIdsOf(receiver).ToHashSet(StringComparer.Ordinal);
        return CurrentAbilityIds(source).Where(id => AbilityCatalog.All.TryGetValue(id, out var ability) && CopyableAbility(ability) && !has.Contains(id)).ToArray();
    }

    // "Until the next time this game this ability is used": using the copying ability again ends what its last use copied -- whether
    // or not the new use finds anything to copy. Only grants with Duration "Copy" made by THIS copying ability are ended.
    private void EndPreviousCopies(CardInstance receiver, string copierAbilityId, string step)
    {
        var ended = step == CopyStep.Keyword
            ? receiver.GrantedKeywords.RemoveAll(g => g.Duration == GrantDuration.Copy && g.Source == copierAbilityId)
            : receiver.GrantedAbilities.RemoveAll(g => g.Duration == GrantDuration.Copy && g.Source == copierAbilityId);
        if (ended > 0)
            Emit("copyEnded", $"{Definition(receiver).Name} stops copying the {(step == CopyStep.Keyword ? "keyword" : "ability")} from before.", receiver.OwnerId, receiver.Id);
    }

    /// <summary>Starts a copy: <paramref name="receiver"/> copies, from <paramref name="source"/>, one keyword and/or one ability
    /// (<paramref name="steps"/>), for <paramref name="playerId"/>'s <paramref name="copierAbilityId"/> action. The source is read NOW,
    /// so it may already have left the game as the action's cost. Returns true when a decision was opened (the action does not finish
    /// until it is answered: FinishCopy resumes it); false when nothing needed deciding, and the caller finishes the action itself.</summary>
    private bool BeginCopy(CardInstance receiver, CardInstance source, string copierAbilityId, string[] steps, int playerId, string origin,
        IEnumerable<string>? bowedBefore = null, string duration = GrantDuration.Copy, string? abilityActionKeyword = null)
    {
        var copy = new PendingCopy
        {
            PlayerId = playerId,
            ReceiverId = receiver.Id,
            ActionCardId = receiver.Id,
            CopierAbilityId = copierAbilityId,
            Duration = duration,
            SourceDefinitionId = source.DefinitionId,
            SourceName = Definition(source).Name,
            Steps = steps.ToList(),
            Origin = origin,
            BowedBefore = bowedBefore?.ToList()
        };
        // The previous copies end first, so the snapshot is taken against the receiver as it will stand: a keyword or ability the old
        // copy provided is not "already had" and may be chosen again.
        if (duration == GrantDuration.Copy) foreach (var step in steps) EndPreviousCopies(receiver, copierAbilityId, step);
        if (steps.Contains(CopyStep.Keyword)) copy.KeywordChoices = CopyableKeywords(source, receiver).ToList();
        if (steps.Contains(CopyStep.Ability))
        {
            copy.AbilityChoices = CopyableAbilityIds(source, receiver).ToList();
            // Asahina Kitiaru (Emperor Edition Personality pass, issue #477): "one of his Political abilities" -- only abilities carrying the action keyword are offered.
            if (abilityActionKeyword is not null) copy.AbilityChoices = copy.AbilityChoices.Where(id => AbilityCatalog.All[id].ActionKeywords?.Contains(abilityActionKeyword, StringComparer.Ordinal) == true || Keyword(source, abilityActionKeyword)).ToList();
        }
        Emit("copyBegun", $"{Definition(receiver).Name} copies from {copy.SourceName}.", playerId, receiver.Id);
        return AdvanceCopy(copy);
    }

    // Runs the remaining steps in order: nothing to offer is skipped, exactly one candidate is taken, anything more opens a decision
    // for the copying player and stops. True when a decision is open (PendingCopy and PendingChoice are set).
    private bool AdvanceCopy(PendingCopy copy)
    {
        var receiver = Card(copy.ReceiverId);
        while (copy.Steps.Count > 0)
        {
            var step = copy.Steps[0];
            var choices = step == CopyStep.Keyword ? copy.KeywordChoices : copy.AbilityChoices;
            var noun = step == CopyStep.Keyword ? "keyword" : "ability";
            if (choices.Count == 0)
            {
                Emit("copyNothing", $"{Definition(receiver).Name} has no {noun} to copy from {copy.SourceName}.", copy.PlayerId, receiver.Id);
                copy.Steps.RemoveAt(0);
                continue;
            }
            if (choices.Count == 1)
            {
                ApplyCopy(copy, receiver, step, choices[0]);
                copy.Steps.RemoveAt(0);
                continue;
            }
            _state.PendingCopy = copy;
            _state.PendingChoice = new(step == CopyStep.Keyword ? "copyKeyword" : "copyAbility", copy.PlayerId, receiver.Id,
                $"{Definition(receiver).Name}: choose the {noun} to copy from {copy.SourceName}.");
            _state.PriorityPlayerId = copy.PlayerId; _state.ConsecutivePasses = 0;
            return true;
        }
        _state.PendingCopy = null;
        return false;
    }

    private void ApplyCopy(PendingCopy copy, CardInstance receiver, string step, string value)
    {
        if (step == CopyStep.Keyword)
        {
            if (!Keyword(receiver, value)) receiver.GrantedKeywords.Add(new(value, copy.CopierAbilityId, copy.Duration, copy.SourceDefinitionId, copy.SourceName));
            Emit("keywordCopied", $"{Definition(receiver).Name} copies the keyword {value} from {copy.SourceName}.", receiver.OwnerId, receiver.Id);
            return;
        }
        if (!AbilityIdsOf(receiver).Contains(value, StringComparer.Ordinal)) GrantAbility(receiver, value, copy.CopierAbilityId, copy.Duration, copy.SourceDefinitionId, copy.SourceName);
        Emit("abilityCopied", $"{Definition(receiver).Name} copies an ability from {copy.SourceName}: {AbilityCatalog.All[value].Label}.", receiver.OwnerId, receiver.Id);
    }

    private void ResolveCopyCommand(GameCommand command)
    {
        var copy = _state.PendingCopy ?? throw new RuleViolation("No copy is in progress.");
        Require(command.PlayerId == copy.PlayerId, "Only the player copying may answer.");
        var step = copy.Steps[0];
        var receiver = Card(copy.ReceiverId);
        if (step == CopyStep.Keyword)
        {
            Require(command.Type == "chooseCopyKeyword", "Choose the keyword to copy.");
            Require(command.Choice is not null && copy.KeywordChoices.Contains(command.Choice, StringComparer.Ordinal), "Choose one of the offered keywords.");
        }
        else
        {
            Require(command.Type == "chooseCopyAbility", "Choose the ability to copy.");
            Require(command.Choice is not null && copy.AbilityChoices.Contains(command.Choice, StringComparer.Ordinal), "Choose one of the offered abilities.");
        }
        ApplyCopy(copy, receiver, step, command.Choice!);
        copy.Steps.RemoveAt(0);
        if (!AdvanceCopy(copy)) FinishCopy(copy);
    }

    // Every path out of a copy: the pending choice is over and the action that opened it finishes like any other.
    private void FinishCopy(PendingCopy copy)
    {
        _state.PendingCopy = null;
        FinishDeferredAction(copy.PlayerId, copy.Origin, copy.ActionCardId, copy.BowedBefore);
    }

    // ---- what the chooser sees ----

    private CopyView? BuildCopyView(int viewerId)
    {
        if (_state.PendingCopy is not { } copy || copy.PlayerId != viewerId || _state.PendingChoice is null || copy.Steps.Count == 0) return null;
        var step = copy.Steps[0];
        var receiver = Card(copy.ReceiverId);
        var choices = step == CopyStep.Keyword
            ? copy.KeywordChoices.Select(k => new CopyChoiceView(k, k, null)).ToArray()
            : copy.AbilityChoices.Select(id => new CopyChoiceView(id, AbilityCatalog.All[id].Label, DescribeTiming(AbilityCatalog.All[id]))).ToArray();
        var label = step == CopyStep.Keyword ? "Copy a keyword" : "Copy an ability";
        return new(step, copy.Steps.Skip(1).ToArray(), receiver.Id, Definition(receiver).Name, copy.SourceDefinitionId, copy.SourceName, label,
            _state.PendingChoice.Prompt, copy.Duration, choices);
    }

    private static string DescribeTiming(AbilityDefinition ability) =>
        ability.Timing == "Reaction" ? "Reaction" : ability.Timing == "BattleOrOpen" ? "Battle/Open" : ability.Timing;

    // ---- the chooser's legal actions ----

    /// <summary>One action per offered keyword or ability: the player always has a legal answer while a copy is open, so no bot or
    /// legal-actions-driven client can stall in it.</summary>
    private IEnumerable<ActionOption> CopyActions(int playerId)
    {
        if (_state.PendingCopy is not { } copy || copy.PlayerId != playerId || copy.Steps.Count == 0) yield break;
        var step = copy.Steps[0];
        var type = step == CopyStep.Keyword ? "chooseCopyKeyword" : "chooseCopyAbility";
        foreach (var value in step == CopyStep.Keyword ? copy.KeywordChoices : copy.AbilityChoices)
            yield return new(type, step == CopyStep.Keyword ? $"Copy the keyword {value}" : $"Copy: {AbilityCatalog.All[value].Label}", true, null,
                new(type, playerId, _state.Version, Choice: value), copy.ReceiverId);
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredCopyChoice(string type, GameCommand command)
    {
        if (!IsCopyChoice(type)) return false;
        ResolveCopyCommand(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterCardCopyHooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredCopyChoice", 400, static (engine, type, command) => engine.HandleRegisteredCopyChoice(type, command));
    }
}
