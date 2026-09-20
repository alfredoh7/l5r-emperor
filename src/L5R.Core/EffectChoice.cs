namespace L5R.Core;

// Conditional effect choice (issue #337): an action whose effect branches on a condition evaluated when it RESOLVES, and that offers the
// acting player an engine-listed choice only on the "may" branch. Mirumoto Yumaru is the first card:
//
//   "Target an enemy Personality: If Yumaru has a Weapon, you may move her home and you may move the Personality home. Otherwise, move
//    both of them home."
//
// Mendon's ruling (verbatim in the decision doc): with a Weapon the player chooses one of THREE effects -- send Yumaru home, send the
// enemy Personality home, send both; without one the player "is not presented with any choices" and both are sent home.
//
// The shape is data on the ability (AbilityDefinition.Branch): a Condition, the Options offered when it holds (each a named list of
// effects), and the Otherwise option that simply happens when it does not. The flow when the action resolves:
//
//   1. The condition is read NOW -- after every "before this action resolves" reaction has had its say (a Weapon a reaction removed no
//      longer earns the choice; one it added does), not when the action was announced.
//   2. It fails: the Otherwise effects apply and the action carries on to its ordinary end. No decision exists, so none is asked.
//   3. It holds: a public "effectChoice" decision opens for the acting player, listing each option (QueryActions offers one
//      "chooseEffect" action per option, so a client that lists offered actions already shows it). The action waits.
//   4. The chosen option's effects apply, the negations a reaction set for this action are honoured exactly as they would have been
//      inline, and the action then completes through the same tail every action ends with (CompleteAbilityResolution).
//
// Nothing about the choice is hidden: the options, the chosen option and its effects are public, as for any resolved action.

/// <summary>One thing the player may choose: a stable id (GameCommand.Choice names it), the label QueryActions shows, and the effects it
/// applies in order (BranchEffect names).</summary>
public sealed record EffectOption(string Id, string Label, string[] Effects);

/// <summary>A branch. When <paramref name="Condition"/> (a BranchCondition) holds at resolution the player picks one of
/// <paramref name="Options"/>; otherwise <paramref name="Otherwise"/> applies with no choice.</summary>
public sealed record ConditionalChoice(string Condition, EffectOption[] Options, EffectOption Otherwise);

/// <summary>Conditions a branch may test at resolution. The source is the ability's own card, the target its chosen target.</summary>
public static class BranchCondition
{
    /// <summary>The source card has at least one attached Weapon.</summary>
    public const string SourceHasWeapon = "source-has-weapon";
    /// <summary>The target is a dishonorable Personality (issue #336: Doji Yoshitada's second sentence, judged after his first has resolved).</summary>
    public const string TargetDishonorable = "target-dishonorable";
    /// <summary>The target actually moved (issue #336: Fear Me!'s "If he moved"); the ability's own effect says whether it did.</summary>
    public const string TargetMoved = "target-moved";
}

/// <summary>Effects an option may apply. Each goes through the same helper an ordinary effect would, so a negation a reaction set
/// (Tamori Tamoko's movement, Champion of Thunder's "all") still applies to it.</summary>
public static class BranchEffect
{
    /// <summary>The source card's unit moves home.</summary>
    public const string MoveSourceHome = "move-source-home";
    /// <summary>The target's unit moves home.</summary>
    public const string MoveTargetHome = "move-target-home";
    /// <summary>A player gains (positive) or loses (negative) Honor because of the action (issue #336): "choose a player who gains or loses 2 Honor". Who
    /// is relative to the player who took the action ("self" is him, "opponent" the other player), so the option's label reads the same for both seats.
    /// Applied by ApplyPlayerHonorChoice, so a gain for a dishonorable performer's own controller rehonors him instead.</summary>
    public static string Honor(bool self, int amount) => $"honor:{(self ? "self" : "opponent")}:{amount}";
}

/// <summary>The four answers to "choose a player who gains or loses N Honor" (issue #336): each player, each direction. Shared by every card that prints it.</summary>
public static class HonorChoice
{
    public static EffectOption[] Options(int amount) =>
    [
        new("self-gain", $"You gain {amount} Honor", [BranchEffect.Honor(true, amount)]),
        new("opponent-gain", $"Your opponent gains {amount} Honor", [BranchEffect.Honor(false, amount)]),
        new("self-lose", $"You lose {amount} Honor", [BranchEffect.Honor(true, -amount)]),
        new("opponent-lose", $"Your opponent loses {amount} Honor", [BranchEffect.Honor(false, -amount)]),
    ];
    /// <summary>What happens when the condition fails: nothing, and nothing is asked.</summary>
    public static readonly EffectOption None = new("none", "No Honor change", []);
}

/// <summary>Everything a paused action needs to finish once the player has chosen (MatchState.PendingEffectChoice). Public state, no hidden
/// information: the command is the one both players already saw announced.</summary>
public sealed class PendingEffectChoice
{
    public GameCommand Command { get; set; } = null!;
    public int PlayerId { get; set; }
    /// <summary>Negations a before-action reaction set for this command. They were cleared when the deferred command returned; they are
    /// restored while the chosen effects apply, so a negated move stays negated.</summary>
    public List<NegatedEffect> Negated { get; set; } = [];
    public bool ConsumedGrant { get; set; }
    public bool PlayedFromDiscard { get; set; }
    public int? OpponentHonorBefore { get; set; }
    public List<string> BowedBefore { get; set; } = [];
    public List<AdditionalActionGrant> PreExistingGrants { get; set; } = [];
}

public sealed partial class MatchEngine
{
    // The branch an ability carries: its own Branch (Yumaru), or the conditional follow-up slot of a multi-target ability (TargetSlots.cs).
    private static ConditionalChoice? FollowUpOf(AbilityDefinition ability) => ability.Branch ?? ability.Slots?.FirstOrDefault(s => s.Resolution is not null)?.Resolution;

    private bool BranchConditionMet(string condition, CardInstance source, CardInstance? target, bool targetMoved) => condition switch
    {
        BranchCondition.SourceHasWeapon => Attachments(source.Id).Any(a => Keyword(a, "Weapon")),
        BranchCondition.TargetDishonorable => target is not null && MatchesTargetStatus(target, TargetStatus.IsDishonorable),
        BranchCondition.TargetMoved => targetMoved,
        _ => throw new InvalidOperationException($"Unknown branch condition '{condition}'.")
    };

    private void ApplyBranchEffect(string effect, GameCommand command, CardInstance source, CardInstance? target)
    {
        if (effect.StartsWith("honor:", StringComparison.Ordinal))
        {
            var parts = effect.Split(':');
            var affected = parts[1] == "self" ? command.PlayerId : 1 - command.PlayerId;
            // The performer, not the Strategy, is who the rehonor rule looks at when the action names one.
            var performer = command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var named) ? named : source;
            ApplyPlayerHonorChoice(performer, affected, int.Parse(parts[2]));
            return;
        }
        switch (effect)
        {
            case BranchEffect.MoveSourceHome: MoveHome(source, false); break;
            case BranchEffect.MoveTargetHome: MoveHome(target!, false); break;
            default: throw new InvalidOperationException($"Unknown branch effect '{effect}'.");
        }
    }

    // Called from the ability's execution case. True when the action paused for a decision (the caller returns without finishing);
    // false when the Otherwise branch already applied and the action carries on.
    // targetMoved: only for a condition the ability's own effect settles (Fear Me!'s "if he moved"). A conditional follow-up is prompted ONLY when
    // its condition holds (Mendon's ruling, issue #336): the sentence is independent of the one before it, so a failed condition opens nothing.
    private bool BeginConditionalChoice(GameCommand command, AbilityDefinition ability, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants, bool targetMoved = false)
    {
        var branch = FollowUpOf(ability)!;
        var source = Card(command.CardId);
        var target = command.TargetId is null ? null : Card(command.TargetId);
        if (!BranchConditionMet(branch.Condition, source, target, targetMoved))
        {
            foreach (var effect in branch.Otherwise.Effects) ApplyBranchEffect(effect, command, source, target);
            return false;
        }
        _state.PendingEffectChoice = new()
        {
            Command = command, PlayerId = command.PlayerId, Negated = _state.NegatedEffects.ToList(), ConsumedGrant = consumedGrant,
            PlayedFromDiscard = playedFromDiscard, OpponentHonorBefore = opponentHonorBefore, BowedBefore = bowedBefore.ToList(), PreExistingGrants = preExistingGrants.ToList()
        };
        _state.PendingChoice = new("effectChoice", command.PlayerId, source.Id,
            $"{Definition(source).Name}: choose one — {string.Join("; ", branch.Options.Select(o => o.Label))}.");
        _state.PriorityPlayerId = command.PlayerId; _state.ConsecutivePasses = 0;
        return true;
    }

    private void ResolveEffectChoice(GameCommand command)
    {
        var pending = _state.PendingEffectChoice ?? throw new RuleViolation("No effect choice is open.");
        Require(command.Type == "chooseEffect" && command.PlayerId == pending.PlayerId, "Choose one of the offered effects.");
        var ability = AbilityCatalog.All[pending.Command.AbilityId!];
        var option = FollowUpOf(ability)!.Options.FirstOrDefault(o => o.Id == command.Choice);
        Require(option is not null, "Choose one of the offered effects.");
        var source = Card(pending.Command.CardId);
        var target = pending.Command.TargetId is null ? null : Card(pending.Command.TargetId);
        _state.PendingChoice = null; _state.PendingEffectChoice = null;
        Emit("effectChosen", $"{Player(pending.PlayerId).Name} chose: {option!.Label}.", pending.PlayerId, source.Id);
        _state.NegatedEffects = pending.Negated.ToList();
        foreach (var effect in option.Effects) ApplyBranchEffect(effect, pending.Command, source, target);
        _state.NegatedEffects.Clear();
        CompleteAbilityResolution(pending.Command, ability, pending.ConsumedGrant, pending.PlayedFromDiscard, pending.OpponentHonorBefore,
            pending.BowedBefore.ToHashSet(StringComparer.Ordinal), pending.PreExistingGrants);
    }

    // The offered answers to an open effectChoice: one "chooseEffect" action per option, each a complete command.
    private IEnumerable<ActionOption> EffectChoiceActions(int playerId)
    {
        if (_state.PendingEffectChoice is not { } pending || pending.PlayerId != playerId) yield break;
        var ability = AbilityCatalog.All[pending.Command.AbilityId!];
        foreach (var option in FollowUpOf(ability)!.Options)
            yield return new("chooseEffect", option.Label, true, null, new GameCommand("chooseEffect", playerId, _state.Version, Choice: option.Id), pending.Command.CardId);
    }
}
