using L5R.Core;

namespace L5R.AI;

// Issue #642: the completer. The engine offers each action with whatever it could fill in itself (the command) and the choices a player still has to make (targets,
// slots, a payment, a cost option, a destination, a player). The bot answers every one of them from that metadata alone, never from a card's name.
public static partial class TrainingOpponent
{
    /// <summary>Fills in every choice an action still needs, once its target (if any) is known.</summary>
    private static GameCommand Complete(Ctx x, ActionOption a, GameCommand command)
    {
        var intent = AbilityIntent.Of(command.AbilityId);
        // Multi-target actions: one selection per up-front slot.
        if (a.Slots is { Length: > 0 } slots && command.Selections is null) command = command with { Selections = BuildSelections(x, slots, intent) };
        // "Choose a player": a gain goes to the bot, a loss to its opponent, either way by its Honor profile.
        if (command.TargetPlayerId is null && command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) && ability.Target == "player")
            command = command with { TargetPlayerId = ChoosePlayer(x, intent) };
        // Gold: the engine's own first payment for the quote; a variable cost pays its minimum.
        if (a.Gold is { Error: null } gold)
        {
            if (gold.MinAmount is int min && command.GoldAmount is null) command = command with { GoldAmount = min };
            if (gold.Cost > 0 && (command.SourceIds is null || command.SourceIds.Length == 0))
            {
                var options = gold.MinAmount is int m && gold.PaymentsByAmount is not null && gold.PaymentsByAmount.TryGetValue(m, out var byAmount) && byAmount.Length > 0 ? byAmount : gold.Payments;
                if (options.Length > 0) command = command with { SourceIds = options[0].SourceIds };
            }
        }
        if (a.AlternativeCost is { } alternative && command.CostOption is null) command = command with { CostOption = alternative.Id };
        if (a.Type == "useAbility" && a.Provinces is { Length: > 0 } provinces && command.Province is null) command = command with { Province = provinces[0] };
        return command;
    }

    private static int ChoosePlayer(Ctx x, AbilityIntent intent)
    {
        if (intent.HonorGain && intent.HonorLoss) return x.Profile.PrefersOwnGain(x.Self.Honor, x.Opponent.Honor) ? x.Self.Id : x.Opponent.Id;
        if (intent.HonorGain) return x.Self.Id;
        if (intent.HonorLoss || intent.Side == TargetSide.Enemy || intent.Rank >= EffectRank.Penalize) return x.Opponent.Id;
        return x.Self.Id;
    }

    private static SlotSelection[] BuildSelections(Ctx x, TargetSlotOption[] slots, AbilityIntent intent)
    {
        var chosen = new List<SlotSelection>();
        foreach (var slot in slots.Where(s => s.Timing == TargetSlotTimings.UpFront && s.Kind != TargetSlotKinds.FollowUp))
        {
            if (slot.Kind == TargetSlotKinds.Player)
            {
                var wanted = ChoosePlayer(x, intent);
                chosen.Add(new(slot.Slot, PlayerId: slot.PlayerIds.Contains(wanted) ? wanted : slot.PlayerIds.FirstOrDefault(wanted)));
                continue;
            }
            // Take as many cards as the slot allows (at least what it requires, at most what is legal), best candidates first.
            var count = Math.Min(slot.CardIds.Length, Math.Max(slot.Min, Math.Min(slot.Max, slot.Min == 0 ? 1 : slot.Max)));
            var label = slot.Label.ToLowerInvariant();
            var side = label.Contains("enemy", StringComparison.Ordinal) || label.Contains("another player", StringComparison.Ordinal) || label.Contains("opponent", StringComparison.Ordinal) ? TargetSide.Enemy
                : label.Contains("your", StringComparison.Ordinal) ? TargetSide.Own : intent.Side;
            var picks = Rank(x, slot.CardIds, intent with { Side = side }, slot.Label).Take(count).Select(c => c.Id).ToList();
            foreach (var id in slot.CardIds) if (picks.Count < count && !picks.Contains(id)) picks.Add(id);
            chosen.Add(new(slot.Slot, picks.Take(count).ToArray()));
        }
        return chosen.ToArray();
    }

    /// <summary>The best card among the offered ids for what the ability does: its enemies when it hurts, its own when it helps.</summary>
    private static CardView? PickCard(Ctx x, string[] ids, AbilityIntent intent, string label) => Rank(x, ids, intent, label).FirstOrDefault();

    private static IEnumerable<CardView> Rank(Ctx x, string[] ids, AbilityIntent intent, string label)
    {
        var cards = ids.Select(id => x.Card(id)).OfType<CardView>().ToArray();
        var harm = intent.Rank >= EffectRank.Penalize || intent.Side == TargetSide.Enemy;
        var wantEnemy = intent.Side == TargetSide.Enemy || intent.Side != TargetSide.Own && harm;
        var preferred = cards.Where(c => (c.OwnerId != x.Self.Id) == wantEnemy).ToArray();
        if (preferred.Length == 0) preferred = cards;
        var straighten = label.Contains("straighten", StringComparison.OrdinalIgnoreCase);
        return preferred
            .OrderBy(c => wantEnemy ? c.Bowed : straighten ? !c.Bowed : c.Bowed)
            .ThenByDescending(c => c.Definition?.Type == CardType.Personality)
            .ThenByDescending(c => c.Force)
            .ThenBy(c => c.Id, StringComparer.Ordinal);
    }

    /// <summary>Mendon's attack rule (issue #642): see <see cref="AttackPlanner"/>. Units are the unbowed Personalities that may be assigned, each counted with its attached Followers.</summary>
    private static Dictionary<string, int>? PlanAttack(Ctx x, CardView[] units, int[] provinces)
    {
        var unitForces = units.Select(c => new AttackPlanner.Unit(c.Id, ArmyForce(x.Self.Home.Where(h => h.Id == c.Id || h.AttachedTo == c.Id)))).ToArray();
        var targets = x.Opponent.Provinces.Where(p => !p.Destroyed && provinces.Contains(p.Index)).Select(p => new AttackPlanner.Target(p.Index, p.Strength)).ToArray();
        return AttackPlanner.Plan(unitForces, targets, ArmyForce(x.Opponent.Home));
    }
}
