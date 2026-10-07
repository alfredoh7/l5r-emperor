using System.Text.Json.Serialization;

namespace L5R.Core;

// Primitive P18 (issue #698, docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md): duel rules a card prints about the duel it issues or is in.
//
// A duel is one PendingDuel; before this file its shape was fixed (the challenged player focuses first, any number of focuses up to four, a Strike whenever a player wants, item bonuses counted, the
// duel stat and totals decide, refusal costs nothing unless a per-ability case in ResolveDuelRefused said so). DuelRules is the one place a card says otherwise, and the engine reads it where the
// rule applies, so the choices QueryDuelActions offers, the commands ResolveDuelCommand accepts and the totals ResolveDuel counts can never disagree:
//
//   ExcludeItemBonuses     "Items give no bonuses": DuelStatValue leaves out what the duelists' attached Items add to the duel stat (both sides).
//   MinFocusEach           "Each Personality must focus at least N times if possible": a Strike is refused until every side has focused N times or can focus no more.
//   ExactFocusEach         "Each Personality focuses exactly N times if possible": a side that has focused N times may not focus again (nor use an "instead of focusing" substitute); a Strike waits as above.
//   FirstToFocusPlayerId   "You have the first opportunity to focus or strike": that player, not the challenged player, acts first.
//   AutoLose               "<card> loses any duel against <title>" (or against anyone): decided before the totals count, like Daidoji Tametaka - exp's printed rule.
//   RefusalCost            to refuse the challenge, the challenged player pays this (dishonor him and move him home, or bow him); the cost is paid as he refuses and a refusal he cannot pay is not offered.
//   RefusableWhen          the challenge is refusable only while this condition on the two Personalities holds when it is issued (an honorable target, a bowed one, ...).
//
// A card's feature file puts DuelRules on the duels it issues with ONE line from its module initializer (priority >= 10000):
//     DuelRuleRegistry.ForAbility("MyCardDuelRules", 12900, "my-duel-ability", new DuelRules { ExcludeItemBonuses = true });
// or, for rules that read the board, DuelRuleRegistry.Register("MyCardDuelRules", 12900, static context => context.Engine.... ? new DuelRules {...} : null).
// Several registered answers merge (a flag is on if any says so, the larger minimum wins, the first named count/first player/cost/condition wins, auto-losses add up).
//
// The substitute duelist (Seppun Ryota's shape: "when another player's challenge would target one of your Personalities at my location, it targets me instead, if legal") is Ezuno's redirect
// on the TargetedReaction channel restricted to challenge-issuing actions: DuelRuleRegistry.Substitute(...).

/// <summary>"<c>CardId</c> loses any duel against a Personality titled <c>AgainstTitle</c>" (any opponent when null).</summary>
public sealed record DuelAutoLoss(string CardId, string? AgainstTitle = null);

/// <summary>What a refusal costs the challenged player (DuelRules.RefusalCost).</summary>
public static class DuelRefusalCosts
{
    /// <summary>Dishonor the challenged Personality and move him home.</summary>
    public const string DishonorAndMoveHome = "dishonor-and-move-home";
    /// <summary>Bow the challenged Personality (he must be unbowed).</summary>
    public const string Bow = "bow";
}

/// <summary>The built-in conditions of DuelRules.RefusableWhen (evaluated when the challenge is issued). A card with a different condition answers EngineHooks.DuelRefusableWhen.</summary>
public static class DuelRefusableConditions
{
    public const string ChallengedHonorable = "challenged-honorable";
    public const string ChallengedDishonorable = "challenged-dishonorable";
    public const string ChallengedBowed = "challenged-bowed";
    public const string ChallengedUnbowed = "challenged-unbowed";
}

/// <summary>The rules of one duel. Every property's default is "no rule"; a duel with no rules carries none (PendingDuel.Rules is null and is not saved).</summary>
public sealed record DuelRules
{
    public bool ExcludeItemBonuses { get; init; }
    public int MinFocusEach { get; init; }
    public int? ExactFocusEach { get; init; }
    public int? FirstToFocusPlayerId { get; init; }
    public List<DuelAutoLoss> AutoLose { get; init; } = [];
    /// <summary>DuelRefusalCosts.*; null when refusing is free.</summary>
    public string? RefusalCost { get; init; }
    /// <summary>DuelRefusableConditions.* or a card's own key answered by EngineHooks.DuelRefusableWhen; null when the ability's own DuelRefusable alone decides.</summary>
    public string? RefusableWhen { get; init; }

    [JsonIgnore]
    public bool IsEmpty => !ExcludeItemBonuses && MinFocusEach <= 0 && ExactFocusEach is null && FirstToFocusPlayerId is null && AutoLose.Count == 0 && RefusalCost is null && RefusableWhen is null;

    /// <summary>The effective number of focuses each side owes (exact wins over minimum), or 0.</summary>
    [JsonIgnore]
    public int RequiredFocuses => Math.Max(MinFocusEach, ExactFocusEach ?? 0);

    internal static DuelRules Merge(DuelRules a, DuelRules b) => new()
    {
        ExcludeItemBonuses = a.ExcludeItemBonuses || b.ExcludeItemBonuses,
        MinFocusEach = Math.Max(a.MinFocusEach, b.MinFocusEach),
        ExactFocusEach = a.ExactFocusEach ?? b.ExactFocusEach,
        FirstToFocusPlayerId = a.FirstToFocusPlayerId ?? b.FirstToFocusPlayerId,
        AutoLose = [.. a.AutoLose, .. b.AutoLose],
        RefusalCost = a.RefusalCost ?? b.RefusalCost,
        RefusableWhen = a.RefusableWhen ?? b.RefusableWhen
    };
}

/// <summary>The challenge being issued, for a callback that decides which rules it carries.</summary>
internal sealed record DuelRulesContext(MatchEngine Engine, string AbilityId, int ChallengerPlayerId, CardInstance Challenger, CardInstance Challenged, CardInstance? Source);

/// <summary>A card-specific RefusableWhen key being evaluated for a challenge.</summary>
internal sealed record DuelRefusableContext(MatchEngine Engine, string Condition, CardInstance Challenger, CardInstance Challenged);

internal static partial class EngineHooks
{
    /// <summary>The rules a challenge carries: every registered answer is merged (null or empty = none).</summary>
    internal static readonly OrderedHooks<Func<DuelRulesContext, DuelRules?>> DuelRulesFor = new();
    /// <summary>A card's own RefusableWhen condition: true/false when handled, null when not this callback's key.</summary>
    internal static readonly OrderedHooks<Func<DuelRefusableContext, bool?>> DuelRefusableWhen = new();
}

/// <summary>The one-line registration API for a card's feature file (module initializer, priority at least 10000, ids unique).</summary>
internal static class DuelRuleRegistry
{
    /// <summary>Puts the same rules on every duel the named ability issues.</summary>
    internal static void ForAbility(string hookId, int priority, string abilityId, DuelRules rules) =>
        EngineHooks.DuelRulesFor.Register(hookId, priority, context => context.AbilityId == abilityId ? rules : null);

    /// <summary>Rules that read the board: the callback returns the rules for this challenge, or null.</summary>
    internal static void Register(string hookId, int priority, Func<DuelRulesContext, DuelRules?> rules) =>
        EngineHooks.DuelRulesFor.Register(hookId, priority, rules);

    /// <summary>A card's own RefusableWhen key.</summary>
    internal static void RefusableWhen(string hookId, int priority, string condition, Func<DuelRefusableContext, bool> holds) =>
        EngineHooks.DuelRefusableWhen.Register(hookId, priority, context => context.Condition == condition ? holds(context) : null);

    /// <summary>
    /// The substitute duelist: <paramref name="abilityId"/> is a Reaction on the card (listed on its definition) that, when another player's challenge would target one of the reactor's own Personalities
    /// standing where the reactor stands, makes it target the reactor instead, if legal (RedirectAction, the way Ezuno's does). <paramref name="affectedFilter"/> narrows which of the reactor's Personalities it
    /// may stand in for; <paramref name="bowsReactor"/> adds "bow this card" as its cost. Registers the ability, its trigger and its effect.
    /// </summary>
    internal static void Substitute(string hookId, int priority, string abilityId, string label, Func<MatchEngine, CardInstance, bool>? affectedFilter = null, bool bowsReactor = false)
    {
        EngineHooks.Abilities.Register(hookId + ".Ability", priority, abilities =>
        {
            abilities.Add(abilityId, new("Reaction", label, "none", Trigger: ReactionTriggers.TargetedByAction, Costs: bowsReactor ? ActionCost.BowSource : null));
            return abilities;
        });
        EngineHooks.TargetedReaction.Register(hookId + ".Applies", priority, (engine, context) => context.abilityId == abilityId && engine.DuelSubstituteApplies(context.card, context.subject, context.affected, affectedFilter));
        EngineHooks.RunEffects.Register(hookId + ".Run", priority, (engine, context) => context.id == abilityId ? engine.RunDuelSubstitute(context.source) : null);
    }
}

public sealed partial class MatchEngine
{
    // ---- resolving the rules a challenge carries -----------------------------------------------------------------------------------------------------------------------------------------------

    private DuelRules? ResolveDuelRules(string abilityId, int challengerPlayerId, CardInstance challenger, CardInstance challenged, CardInstance? source)
    {
        DuelRules? merged = null;
        var context = new DuelRulesContext(this, abilityId, challengerPlayerId, challenger, challenged, source);
        foreach (var hook in EngineHooks.DuelRulesFor.Items)
            if (hook(context) is { IsEmpty: false } rules) merged = merged is null ? rules : DuelRules.Merge(merged, rules);
        return merged;
    }

    /// <summary>RefusableWhen, judged when the challenge is issued. No condition (or no rules) leaves the ability's own DuelRefusable alone.</summary>
    private bool DuelRefusableConditionHolds(DuelRules? rules, CardInstance challenger, CardInstance challenged)
    {
        if (rules?.RefusableWhen is not { } condition) return true;
        switch (condition)
        {
            case DuelRefusableConditions.ChallengedHonorable: return !challenged.Dishonorable;
            case DuelRefusableConditions.ChallengedDishonorable: return challenged.Dishonorable;
            case DuelRefusableConditions.ChallengedBowed: return challenged.Bowed;
            case DuelRefusableConditions.ChallengedUnbowed: return !challenged.Bowed;
        }
        var context = new DuelRefusableContext(this, condition, challenger, challenged);
        foreach (var hook in EngineHooks.DuelRefusableWhen.Items)
            if (hook(context) is { } answer) return answer;
        return false; // an unknown condition never opens a refusal
    }

    // ---- refusal with a cost -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Why the challenged player cannot pay the refusal's cost right now, or null when there is none or it can be paid.</summary>
    private string? DuelRefusalCostProblem(PendingDuel duel)
    {
        if (duel.Rules?.RefusalCost is not { } cost) return null;
        var challenged = Card(duel.ChallengedId);
        var name = Definition(challenged).Name;
        if (!InPlay(challenged)) return $"{name} is not in play, so he cannot pay to refuse.";
        return cost switch
        {
            DuelRefusalCosts.DishonorAndMoveHome => challenged.Dishonorable ? $"{name} is already dishonorable, so he cannot be dishonored to refuse." : null,
            DuelRefusalCosts.Bow => challenged.Bowed ? $"{name} is already bowed, so he cannot be bowed to refuse." : null,
            _ => $"Unknown refusal cost '{cost}'."
        };
    }

    private static string DuelRefusalCostLabel(PendingDuel duel) => duel.Rules?.RefusalCost switch
    {
        DuelRefusalCosts.DishonorAndMoveHome => " (dishonor him and move him home)",
        DuelRefusalCosts.Bow => " (bow him)",
        _ => ""
    };

    /// <summary>Pays the refusal's cost as the challenged player refuses. A cost is not an effect: no card effect's watch or shield negates it.</summary>
    private void PayDuelRefusalCost(PendingDuel duel)
    {
        if (duel.Rules?.RefusalCost is not { } cost) return;
        var challenged = Card(duel.ChallengedId);
        if (!InPlay(challenged)) return;
        Emit("duelRefusalCost", $"{Player(duel.ChallengedPlayerId).Name} pays to refuse: {Definition(challenged).Name}{DuelRefusalCostLabel(duel)}.", duel.ChallengedPlayerId, challenged.Id);
        switch (cost)
        {
            case DuelRefusalCosts.DishonorAndMoveHome: Dishonor(challenged); MoveHome(challenged, false); break;
            case DuelRefusalCosts.Bow: challenged.Bowed = true; break;
        }
    }

    // ---- focusing: minimum, exact count, first opportunity -----------------------------------------------------------------------------------------------------------------------------

    private static int DuelFocusCount(PendingDuel duel, int playerId) => (playerId == duel.ChallengerPlayerId ? duel.ChallengerFocused : duel.ChallengedFocused).Count;

    /// <summary>Whether the player may still take a focus (or a stand-in for one) under ExactFocusEach; always true without it. Substitutes ("instead of focusing", R-027) are not focuses and are not counted.</summary>
    private bool DuelFocusSlotOpen(PendingDuel duel, int playerId) => duel.Rules?.ExactFocusEach is not { } exact || DuelFocusCount(duel, playerId) < exact;

    /// <summary>Whether the player has any way left to focus at all: under the cap, with a card to focus from.</summary>
    private bool DuelCanStillFocus(PendingDuel duel, int playerId) =>
        DuelFocusCount(duel, playerId) + P2FocusSubstitutions(playerId) < 4 && DuelFocusSlotOpen(duel, playerId)
        && (Player(playerId).Hand.Count > 0 || Player(playerId).FateDeck.Count > 0
            || CanFocusDiscard(playerId, duel) && _state.Cards.Values.Any(c => c.OwnerId == playerId && c.Zone is Zone.FateDiscard or Zone.DynastyDiscard));

    /// <summary>"If possible": a side has met the rule's count when it has focused that often or can focus no more.</summary>
    private bool DuelMeetsRequiredFocuses(PendingDuel duel, int playerId) =>
        duel.Rules is not { RequiredFocuses: > 0 and var required } || DuelFocusCount(duel, playerId) >= required || !DuelCanStillFocus(duel, playerId);

    /// <summary>Why this player may not Strike yet, or null. A Strike ends focusing for both sides, so both must have met the required count -- unless the player himself has nothing left to focus with (the only way
    /// to leave a position in which neither a focus nor a Strike would be legal).</summary>
    private string? DuelStrikeRefusal(PendingDuel duel, int playerId)
    {
        if (duel.Rules is not { RequiredFocuses: > 0 } rules) return null;
        if (DuelMeetsRequiredFocuses(duel, duel.ChallengerPlayerId) && DuelMeetsRequiredFocuses(duel, duel.ChallengedPlayerId)) return null;
        if (!DuelCanStillFocus(duel, playerId)) return null;
        return rules.ExactFocusEach is { } exact
            ? $"Each Personality must focus exactly {exact} time{(exact == 1 ? "" : "s")} if possible before a Strike."
            : $"Each Personality must focus at least {rules.MinFocusEach} time{(rules.MinFocusEach == 1 ? "" : "s")} if possible before a Strike.";
    }

    /// <summary>The player who acts first in the focusing: the rule's FirstToFocusPlayerId, otherwise the challenged player.</summary>
    private int DuelFirstFocuser(PendingDuel duel) => duel.Rules?.FirstToFocusPlayerId is { } first && (first == duel.ChallengerPlayerId || first == duel.ChallengedPlayerId) ? first : duel.ChallengedPlayerId;

    // ---- items give no bonuses -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>What the Items attached to the duelist add to the duel stat (the same terms PersonalityChi / PersonalityForce add), which an ExcludeItemBonuses duel does not count.</summary>
    private int ExcludedItemDuelBonus(CardInstance duelist, string stat)
    {
        if (_state.PendingDuel?.Rules is not { ExcludeItemBonuses: true }) return 0;
        var items = Attachments(duelist.Id).Where(c => Definition(c).Type == CardType.Item).ToArray();
        return stat switch
        {
            "Force" => items.Sum(c => Definition(c).Force + ItemForceToWielder(c, duelist) + StatModifier(c, "Force")),
            "PersonalHonor" => items.Sum(c => StatModifier(c, "PersonalHonor")),
            _ => items.Sum(c => Definition(c).Chi + StatModifier(c, "Chi"))
        };
    }

    // ---- automatic loss ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"X loses any duel against Y": applied to the outcome after the totals and tie-breaks, like Daidoji Tametaka - exp's printed rule. A participant who auto-loses loses and the other wins; if both do, both lose.</summary>
    private (string? WinnerId, List<string> LoserIds, string Reason) DuelRulesAutoLoseOutcome(PendingDuel duel, string? winnerId, List<string> loserIds, string reason)
    {
        if (duel.Rules is not { AutoLose.Count: > 0 } rules) return (winnerId, loserIds, reason);
        bool Loses(string cardId, string opponentId) =>
            rules.AutoLose.Any(a => a.CardId == cardId && (a.AgainstTitle is null || Definition(Card(opponentId)).Name == a.AgainstTitle));
        var challengerLoses = Loses(duel.ChallengerId, duel.ChallengedId); var challengedLoses = Loses(duel.ChallengedId, duel.ChallengerId);
        if (challengerLoses && challengedLoses) return (null, [duel.ChallengerId, duel.ChallengedId], "Both lose automatically");
        if (challengerLoses) return (duel.ChallengedId, [duel.ChallengerId], $"{Definition(Card(duel.ChallengerId)).Name} loses automatically");
        if (challengedLoses) return (duel.ChallengerId, [duel.ChallengedId], $"{Definition(Card(duel.ChallengedId)).Name} loses automatically");
        return (winnerId, loserIds, reason);
    }

    // ---- the substitute duelist --------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The reactor stands in for one of its controller's Personalities at its location when another player's challenge-issuing action would target him and the challenge is legal against the reactor.</summary>
    internal bool DuelSubstituteApplies(CardInstance reactor, ReactionSubject subject, CardInstance affected, Func<MatchEngine, CardInstance, bool>? affectedFilter) =>
        subject.Command.AbilityId is { } actingId && AbilityCatalog.All.TryGetValue(actingId, out var acting) && acting.IssuesDuel
        && (affectedFilter is null || affectedFilter(this, affected))
        && YojimboRedirectApplies(reactor, subject, affected, null);

    internal bool? RunDuelSubstitute(CardInstance? reactor)
    {
        if (reactor is null) return false;
        RedirectAction(HeldAction(), reactor.Id, reactor);
        return false;
    }
}
