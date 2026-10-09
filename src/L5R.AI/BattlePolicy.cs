using System.Text.RegularExpressions;
using L5R.Core;

namespace L5R.AI;

/// <summary>
/// Issue #788 (Mendon's bot changes): the pure, card-agnostic rules the practice bot follows inside a battle. Everything here reads only the permitted
/// <see cref="MatchView"/> data and printed catalogue facts, never a card's name, so a card wired tomorrow is covered the moment it exists.
/// </summary>
public static class BattlePolicy
{
    /// <summary>The Force picture of the battle the viewer is in. Strength is null at a battlefield that is not a Province (an extra battlefield).</summary>
    public sealed record Odds(bool IsAttacker, int Own, int Enemy, int? Strength)
    {
        /// <summary>The attacker wins with more Force; a tie goes to the defender.</summary>
        public bool Winning => IsAttacker ? Own > Enemy : Own >= Enemy;
        /// <summary>The condition Mendon's pass rule waits for: the defender wins, or the attacker wins AND the Province falls (the engine's own test: more than defenders plus Strength).</summary>
        public bool Satisfied => IsAttacker ? Strength is int s ? Own > Enemy + s : Own > Enemy : Own >= Enemy;
        /// <summary>How much of the bot's own Force it can lose while still meeting <see cref="Satisfied"/>; negative when not satisfied.</summary>
        public int Margin => IsAttacker ? (Strength is int s ? Own - Enemy - s - 1 : Own - Enemy - 1) : Own - Enemy;
        /// <summary>For a defender: whether the Province would fall if the bot's Force dropped by <paramref name="lost"/> (the attacker beats defenders plus Strength).</summary>
        public bool WouldFallAfterLosing(int lost) => !IsAttacker && Strength is int s && Enemy > Own - lost + s;
    }

    /// <summary>The battle the viewer is in, or null when there is none.</summary>
    public static Odds? Assess(MatchView view)
    {
        var province = view.BattleStep?.BattlefieldProvince ?? view.CurrentBattleProvince;
        if (province is not int index) return null;
        if (view.Phase is not (Phase.Battle or Phase.BattleSelection) && view.BattleStep is null) return null;
        var attackerId = view.BattleStep?.AttackerPlayerId ?? view.ActivePlayerId;
        var self = view.Players.SingleOrDefault(p => p.Id == view.ViewerId);
        var other = view.Players.SingleOrDefault(p => p.Id != view.ViewerId);
        if (self is null || other is null) return null;
        var isAttacker = attackerId == view.ViewerId;
        var defender = isAttacker ? other : self;
        int? strength = defender.Provinces.FirstOrDefault(p => p.Index == index && !p.Destroyed)?.Strength;
        return new(isAttacker, Force(self.Army.Where(c => c.ProvinceIndex == index)), Force(other.Army.Where(c => c.ProvinceIndex == index)), strength);
    }

    public static int Force(IEnumerable<CardView> cards) => cards.Where(c => !c.Bowed && c.Definition?.Type is CardType.Personality or CardType.Follower).Sum(c => Math.Max(0, c.Force));

    // ---- reading abilities -------------------------------------------------------------------------------------------------------------------------------------------

    private static readonly Regex SendHome = new(@"\b(send|sends|move|moves)\b[^.:]*\bhome\b", RegexOptions.IgnoreCase);
    private static readonly Regex Move = new(@"\bmove\b", RegexOptions.IgnoreCase);
    private static readonly Regex AttackX = new(@"(?:Ranged|Melee)\s+(?:Attack\s+)?(\d+)|(\d+)\s+(?:Ranged|Melee)|Attack\s+(\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex Augment = new(@"\+\s?(\d+)", RegexOptions.IgnoreCase);

    private static string Effect(AbilityDefinition ability) => AbilityIntent.EffectText(ability.Label ?? "");

    public static bool SendsHome(string? abilityId) => abilityId is not null && AbilityCatalog.All.TryGetValue(abilityId, out var a) && SendHome.IsMatch(Effect(a));
    public static bool Moves(string? abilityId) => abilityId is not null && AbilityCatalog.All.TryGetValue(abilityId, out var a) && Move.IsMatch(Effect(a)) && !SendHome.IsMatch(Effect(a));

    /// <summary>The intents of every ability printed on a card (empty for a card with none).</summary>
    public static IEnumerable<(string Id, AbilityDefinition Ability, AbilityIntent Intent)> Printed(CardView card)
    {
        foreach (var id in card.Definition?.Abilities ?? [])
            if (AbilityCatalog.All.TryGetValue(id, out var ability)) yield return (id, ability, AbilityIntent.Of(ability));
    }

    /// <summary>Destroys, bows or moves cards: the abilities that threaten the bot's army.</summary>
    public static bool IsThreatening(string id, AbilityDefinition ability, AbilityIntent intent) =>
        intent.Rank is EffectRank.Destroy or EffectRank.Bow && intent.Side != TargetSide.Own || SendHome.IsMatch(Effect(ability)) && intent.Side != TargetSide.Own || Move.IsMatch(Effect(ability)) && intent.Side != TargetSide.Own;

    /// <summary>An enemy Personality that can destroy, bow or move cards in the bot's army: it is able to act (not bowed, not locked) and prints such an ability.</summary>
    public static bool IsThreat(CardView card) =>
        card.Definition?.Type == CardType.Personality && !card.Bowed && !card.ActionLocked && Printed(card).Any(p => IsThreatening(p.Id, p.Ability, p.Intent));

    /// <summary>A card that can still use at least one printed ability: unbowed (or an ability that works bowed is rare enough to ignore) and not action-locked.</summary>
    public static bool HasUsableAbility(CardView card) =>
        !card.Bowed && !card.ActionLocked && Printed(card).Any(p => p.Ability.Timing is "Battle" or "BattleOrOpen" or "Reaction" or "Open" or "Limited");

    /// <summary>The enemy is not a real threat this battle: nobody at the battlefield, or only bowed Personalities and Personalities with no usable printed ability, or a hand of at most two cards.</summary>
    public static bool Unthreatened(IEnumerable<CardView> enemyArmy, int enemyHandCount)
    {
        var personalities = enemyArmy.Where(c => c.Definition?.Type == CardType.Personality).ToArray();
        return enemyHandCount <= 2 || personalities.Length == 0 || personalities.All(c => c.Bowed || !HasUsableAbility(c));
    }

    // ---- attacks ------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The Force an Attack ability can destroy up to (its printed X), or null when the label gives no number.</summary>
    public static int? AttackStrength(string? abilityId)
    {
        if (abilityId is null || !AbilityCatalog.All.TryGetValue(abilityId, out var ability) || ability.Target != "attack") return null;
        var m = AttackX.Match(ability.Label ?? "");
        if (!m.Success) return null;
        foreach (var g in m.Groups.Cast<Group>().Skip(1)) if (g.Success) return int.Parse(g.Value);
        return null;
    }

    /// <summary>The bonus an ability adds to a Ranged or Melee Attack ("+2" in an ability that names an Attack), or 0.</summary>
    public static int AttackAugment(AbilityDefinition ability)
    {
        var label = Effect(ability);
        if (!label.Contains("Attack", StringComparison.OrdinalIgnoreCase) || ability.Target == "attack") return 0;
        var m = Augment.Match(label);
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    /// <summary>A printed gold cost summed over a card and everything attached to it (Items, Spells, Followers): the "unit" total Mendon asks the bot to compare.</summary>
    public static int UnitGold(CardView card, IEnumerable<CardView> all) =>
        (card.Definition?.GoldCost ?? 0) + all.Where(c => c.AttachedTo == card.Id).Sum(c => c.Definition?.GoldCost ?? 0);

    /// <summary>
    /// Issue #788, rule 2: the Attack target ranking. The highest-Force target whose Force the Attack can destroy (Force at most <paramref name="strength"/>, which the caller raises
    /// by every Attack augment still available); ties go to the highest total printed gold cost of the target's unit. Targets above the strength rank below every destroyable
    /// one, weakest first, so a failed Attack at least aims at the card nearest to falling.
    /// </summary>
    public static double AttackTargetScore(CardView target, int strength, IEnumerable<CardView> all)
    {
        var gold = Math.Min(UnitGold(target, all), 99);
        return target.Force <= strength ? 1000 + target.Force * 100 + gold : 500 - target.Force;
    }

    // ---- bowing -------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Issue #788, rule 3: which card a bow ability should take, best first. When the ability may bow a card with attachments, the unbowed Personality with the most attachments
    /// (ties: the highest combined printed gold cost). Otherwise the ranking is: a Personality that destroys, an attachment that destroys, an attachment that bows, a Personality
    /// that bows, a Personality with another printed battle ability, any other Personality, then anything else (a melee or ranged Attack counts as destroying). Every tie falls to
    /// higher Force, then higher printed gold, then the id, so a replay always agrees.
    /// </summary>
    public static IOrderedEnumerable<CardView> BowOrder(IEnumerable<CardView> candidates, IReadOnlyCollection<CardView> all, bool mayHaveAttachments)
    {
        var pool = candidates.Where(c => !c.Bowed).ToArray();
        if (pool.Length == 0) pool = candidates.ToArray();
        var attachments = pool.ToDictionary(c => c.Id, c => all.Count(a => a.AttachedTo == c.Id));
        if (mayHaveAttachments && pool.Any(c => c.Definition?.Type == CardType.Personality && attachments[c.Id] > 0))
            return pool.OrderByDescending(c => c.Definition?.Type == CardType.Personality).ThenByDescending(c => attachments[c.Id])
                .ThenByDescending(c => UnitGold(c, all)).ThenBy(c => c.Id, StringComparer.Ordinal);
        return pool.OrderBy(Tier).ThenByDescending(c => c.Force).ThenByDescending(c => UnitGold(c, all)).ThenBy(c => c.Id, StringComparer.Ordinal);

        static int Tier(CardView c)
        {
            var person = c.Definition?.Type == CardType.Personality;
            var printed = Printed(c).ToArray();
            var destroys = printed.Any(p => p.Intent.Rank == EffectRank.Destroy && p.Intent.Side != TargetSide.Own);
            var bows = printed.Any(p => p.Intent.Rank == EffectRank.Bow && p.Intent.Side != TargetSide.Own);
            var battle = printed.Any(p => p.Ability.Timing is "Battle" or "BattleOrOpen");
            if (person && destroys) return 0;
            if (!person && destroys) return 1;
            if (!person && bows) return 2;
            if (person && bows) return 3;
            if (person && battle) return 4;
            return person ? 5 : 6;
        }
    }
}
