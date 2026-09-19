namespace L5R.Core;

// The one ordered set of "before this battle resolves" effects (Battle Phase spec 5; rules lead: "Before resolution effects resolve in the order
// decided by the active player ... after both players have passed consecutively but before army forces are tallied"). There is no alternating
// reaction round: every source below joins ONE set that the active player orders (PendingBeforeBattleResolve, MatchEngine.ResolveBattle), and
// each is read live when its turn comes, so an earlier effect is already reflected in the next.
//   - a card's trait (item id = the card id; mandatory or "you may", decided by its owner) -- MatchEngine.BeforeBattleResolveSourceIds
//   - a DELAYED effect a Battle action registered ("Before this battle resolves, bow him") -- item id "delayed:N"
//   - a printed "Reaction: Before a battle resolves" (Steady Resolve, Akodo Dairuko) -- item id "reaction:CARD"; optional, its owner uses or declines it
// A delayed effect belongs to its battle: it lapses with it, applied or not.
public sealed partial class MatchEngine
{
    private const string DelayedItemPrefix = "delayed:", ReactionItemPrefix = "reaction:";

    private static bool IsBeforeResolutionReaction(AbilityDefinition ability) => ability.Timing == "Reaction" && ability.Trigger == "before-battle-resolves";

    // The primitive a Battle action calls to say "before this battle resolves, do X to him". Kind: "bow", "straighten", "destroy" or "modifier"
    // (stat, amount).
    private void RegisterDelayedBattleEffect(int playerId, CardInstance? source, string kind, CardInstance target, string? stat = null, int amount = 0)
    {
        var effect = new DelayedBattleEffect(_state.NextDelayedEffectId++, kind, playerId, source?.Id, target.Id, stat, amount);
        _state.DelayedBattleEffects.Add(effect);
        Emit("delayedEffectRegistered", $"Before this battle resolves: {DescribeDelayedEffect(effect)}.", playerId, source?.Id);
    }

    private string DescribeDelayedEffect(DelayedBattleEffect effect)
    {
        var target = Definition(Card(effect.TargetId)).Name;
        return effect.Kind switch
        {
            "bow" => $"bow {target}",
            "straighten" => $"straighten {target}",
            "destroy" => $"destroy {target}",
            _ => $"give {target} {(effect.Amount >= 0 ? "+" : "")}{effect.Amount} {effect.Stat}",
        };
    }

    // Every item of the set at this battlefield, in a deterministic order: card traits (id order), delayed effects (registration order), then
    // the Reactions a player could use right now (id order).
    private string[] BeforeResolutionItemIds(int provinceIndex) =>
        BeforeBattleResolveSourceIds(provinceIndex)
            .Concat(_state.DelayedBattleEffects.Select(e => DelayedItemPrefix + e.Id))
            .Concat(_state.Cards.Values.Where(c => BeforeResolutionReactionAbilityId(c) is { } id && BeforeResolutionReactionOptions(c, id).Length > 0)
                .Select(c => ReactionItemPrefix + c.Id).Order(StringComparer.Ordinal))
            .ToArray();

    private string? BeforeResolutionReactionAbilityId(CardInstance card) =>
        card.Zone is Zone.Hand || InPlay(card)
            ? AbilityIdsOf(card).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var def) && IsBeforeResolutionReaction(def)) : null;

    // The plays of a card's before-resolution Reaction that are legal right now: asked with the window open, exactly as its owner will be.
    private ActionOption[] BeforeResolutionReactionOptions(CardInstance card, string abilityId)
    {
        var saved = _state.PendingChoice;
        _state.PendingChoice = new("beforeBattleReaction", card.OwnerId, card.Id, "Before this battle resolves: use this Reaction, or pass.");
        try { return AbilityActions(card.OwnerId).Where(o => o.Enabled && o.Command?.AbilityId == abilityId && o.CardId == card.Id).ToArray(); }
        finally { _state.PendingChoice = saved; }
    }

    private string BeforeResolutionItemLabel(string itemId)
    {
        if (itemId.StartsWith(DelayedItemPrefix, StringComparison.Ordinal))
            return _state.DelayedBattleEffects.FirstOrDefault(e => DelayedItemPrefix + e.Id == itemId) is { } effect ? DescribeDelayedEffect(effect) : itemId;
        return Definition(Card(itemId.StartsWith(ReactionItemPrefix, StringComparison.Ordinal) ? itemId[ReactionItemPrefix.Length..] : itemId)).Name;
    }

    private BeforeResolutionEffectView[] BuildBeforeResolutionEffects(PendingBeforeBattleResolve? pending)
    {
        if (pending is null) return [];
        return pending.RemainingSourceIds.Concat(pending.ActiveSourceId is null ? [] : [pending.ActiveSourceId]).Select(id =>
        {
            if (id.StartsWith(DelayedItemPrefix, StringComparison.Ordinal))
            {
                var effect = _state.DelayedBattleEffects.FirstOrDefault(e => DelayedItemPrefix + e.Id == id);
                return new BeforeResolutionEffectView(id, BattleSteps.EffectDelayed, effect?.PlayerId ?? 0, effect?.SourceId, BeforeResolutionItemLabel(id));
            }
            var reaction = id.StartsWith(ReactionItemPrefix, StringComparison.Ordinal);
            var card = Card(reaction ? id[ReactionItemPrefix.Length..] : id);
            return new BeforeResolutionEffectView(id, reaction ? BattleSteps.EffectReaction : BattleSteps.EffectTrait, card.OwnerId, card.Id, Definition(card).Name);
        }).ToArray();
    }

    // A delayed effect's turn in the set: it applies to its target if he is still in play, then it is spent.
    private void StartDelayedItem(PendingBeforeBattleResolve pending, string itemId)
    {
        var effect = _state.DelayedBattleEffects.First(e => DelayedItemPrefix + e.Id == itemId);
        _state.DelayedBattleEffects.Remove(effect);
        var target = Card(effect.TargetId);
        Emit("beforeBattleEffect", $"Before this battle resolves: {DescribeDelayedEffect(effect)}.", effect.PlayerId, effect.SourceId);
        if (InPlay(target))
            switch (effect.Kind)
            {
                case "bow": if (!StalwartNegatesBow(target, effect.PlayerId)) target.Bowed = true; break;
                case "straighten": Straighten(target); break;
                case "destroy": DestroyUnits([target], false, effect.PlayerId); break;
                default: ApplyModifier(target, effect.SourceId ?? "delayed-effect", "delayed-effect", effect.Stat!, effect.Amount); break;
            }
        FinishBeforeBattleEffect(pending);
    }

    // A Reaction's turn in the set: its owner is asked to use it or pass. Nothing is asked when it can no longer be used (an earlier effect
    // may have bowed its source, sent its performer home, ...).
    private void StartReactionItem(PendingBeforeBattleResolve pending, string itemId)
    {
        var card = Card(itemId[ReactionItemPrefix.Length..]);
        var abilityId = BeforeResolutionReactionAbilityId(card);
        if (abilityId is null || BeforeResolutionReactionOptions(card, abilityId).Length == 0) { FinishBeforeBattleEffect(pending); return; }
        pending.ActiveItemSpent = false;
        Emit("beforeBattleEffect", $"Before this battle resolves: {Definition(card).Name} may be used.", card.OwnerId, card.Id);
        _state.PendingChoice = new("beforeBattleReaction", card.OwnerId, card.Id, $"Before this battle resolves: use {Definition(card).Name}, or pass.");
        _state.PriorityPlayerId = card.OwnerId; _state.ConsecutivePasses = 0;
    }

    // Apply()'s handler for the Reaction window: the owner uses the Reaction (an ordinary action, validated by ValidateAbility) or passes; either
    // way that item is spent, and a window the Reaction itself opened is dealt with first (FinishSpentBeforeBattleItem, run again from Apply()).
    private void ResolveBeforeBattleReaction(GameCommand command)
    {
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Use the Reaction or pass.");
        _state.PendingBeforeBattleResolve!.ActiveItemSpent = true;
        if (command.Type == "useAbility") UseAbility(command);
        FinishSpentBeforeBattleItem();
    }

    private void FinishSpentBeforeBattleItem()
    {
        if (_state.PendingBeforeBattleResolve is not { ActiveItemSpent: true } pending || _state.PendingChoice is not { Type: "beforeBattleReaction" }) return;
        _state.PendingChoice = null; pending.ActiveItemSpent = false;
        FinishBeforeBattleEffect(pending);
    }
}
