using System.Text.RegularExpressions;
using L5R.Core;

namespace L5R.AI;

/// <summary>Whose cards an ability is aimed at, read from its target category.</summary>
public enum TargetSide { None, Enemy, Own, Player, Any }

/// <summary>What an ability does to the board, in the order Mendon asked the bot to prefer: destroy, then bow, then penalize, then help itself.</summary>
public enum EffectRank { Neutral = 0, Help = 1, Penalize = 2, Bow = 3, Destroy = 4 }

/// <summary>
/// Issue #642: a card-agnostic reading of one ability, derived only from public rules data the engine already ships with every ability
/// (its printed label, its target category, its timing and trigger). Nothing here names a card, so a card wired tomorrow is classified the
/// moment it exists: the bot is never biased against a card merely because nobody wrote a rule for it. A label that none of the verbs
/// matches reads as <see cref="EffectRank.Neutral"/> and still gets the neutral default score (never below passing).
/// </summary>
public sealed record AbilityIntent(
    EffectRank Rank, TargetSide Side, bool Negates, bool Draws, bool TakesFavor, bool TakesFavorBowing, bool DiscardsFavor,
    bool HonorGain, bool HonorLoss, bool IsReaction, bool IsAttack, bool Unknown)
{
    public static readonly AbilityIntent None = new(EffectRank.Neutral, TargetSide.None, false, false, false, false, false, false, false, false, false, true);

    private static readonly Regex Negate = new(@"\bnegate", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Destroy = new(@"\bdestroy(?!\s+(this|itself|the duel))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Bow = new(@"\bbow\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Penalize = new(@"(-\s?\d|\bpenalty\b|\bdishonor\b|\bmove (him|her|it|them|a target|the target)?\s*home\b|\blower\b|\bloses?\b|\bdiscard\b[^.:]*\b(from|of) (an? )?(enemy|opponent|his|her))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Help = new(@"(\+\s?\d|\bbonus\b|\bstraighten\b|\bgains?\b|\btake\b|\bput\b|\bsearch\b|\bcreate\b|\bproduce\b|\bdraw\b|\brally\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Draw = new(@"\bdraw\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TakeFavor = new(@"(take the imperial favor|lobby|take favor|add a favor token)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DiscardFavor = new(@"discard (the )?(imperial )?favor", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GainHonor = new(@"\bgains?\s+\d+\s+honor|\bgain\s+\d+\s+honor", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EitherHonor = new(@"gains?\s+or\s+loses?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LoseHonor = new(@"\bloses?\s+\d+\s+honor", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>The part of a label after its last colon: the effect, as opposed to the costs before it.</summary>
    internal static string EffectText(string label)
    {
        var i = label.LastIndexOf(':');
        return i >= 0 && i + 1 < label.Length ? label[(i + 1)..] : label;
    }

    private static string CostText(string label)
    {
        var i = label.LastIndexOf(':');
        return i > 0 ? label[..i] : "";
    }

    public static AbilityIntent Of(string? abilityId)
    {
        if (abilityId is null || !AbilityCatalog.All.TryGetValue(abilityId, out var ability)) return None;
        return Of(ability);
    }

    public static AbilityIntent Of(AbilityDefinition ability)
    {
        var label = ability.Label ?? "";
        var effect = EffectText(label);
        var cost = CostText(label);
        var target = ability.Target ?? "none";
        var side = target.StartsWith("enemy", StringComparison.Ordinal) ? TargetSide.Enemy
            : target.StartsWith("own", StringComparison.Ordinal) ? TargetSide.Own
            : target == "player" ? TargetSide.Player
            : target is "none" or "hand" or "attack" ? TargetSide.None
            : TargetSide.Any;
        var isAttack = target == "attack";
        var negates = Negate.IsMatch(effect) || Negate.IsMatch(label) && ability.Timing == "Reaction";
        var rank = EffectRank.Neutral;
        if (isAttack || Destroy.IsMatch(effect)) rank = EffectRank.Destroy;
        else if (Bow.IsMatch(effect) && side != TargetSide.Own) rank = EffectRank.Bow;
        else if (Penalize.IsMatch(effect) && side != TargetSide.Own) rank = EffectRank.Penalize;
        else if (Help.IsMatch(effect) || side == TargetSide.Own) rank = EffectRank.Help;
        var takesFavor = TakeFavor.IsMatch(label);
        var takesBowing = takesFavor && (Bow.IsMatch(cost) || label.Contains("lobby", StringComparison.OrdinalIgnoreCase) && Bow.IsMatch(label));
        var either = EitherHonor.IsMatch(effect);
        var honorGain = GainHonor.IsMatch(effect) || either;
        var honorLoss = (LoseHonor.IsMatch(effect) || either) && side != TargetSide.Own;
        return new(rank, side, negates, Draw.IsMatch(effect), takesFavor, takesBowing, DiscardFavor.IsMatch(label), honorGain, honorLoss,
            ability.Timing == "Reaction", isAttack, false);
    }
}
