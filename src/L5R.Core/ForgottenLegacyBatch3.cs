namespace L5R.Core;

public static class ForgottenLegacyBatch3Triggers
{
    /// <summary>Entrenched Position: "After the Attacker announces a Battle action before the Defender's first normal opportunity to act or pass" -- played from the
    /// Defender's hand in the "announceReaction" window (MatchEngine.ResolveOrDeferEntrenchedReaction).</summary>
    public const string Entrenched = "attacker-battle-action-announced-early";
}

// Forgotten Legacy pass (issue #467), batch 3: the execution/validation helpers of the Strategies wired in strategy-wave-54. Each ability is documented at its
// own definition in CardAbilities.cs; this file holds only what is not already a shared primitive elsewhere.
public sealed partial class MatchEngine
{
    private const string EntrenchedTrigger = ForgottenLegacyBatch3Triggers.Entrenched;

    // ---- Entrenched Position: "After the Attacker announces a Battle action before the Defender's first normal opportunity to act or pass: Negate its effects" ----
    // The Defender's first normal opportunity comes the first time the Attacker cedes priority in the battle's action round: by default the Defender holds the first
    // opportunity outright (the Attacker cannot announce anything before it), so the window only ever exists while the Attacker holds a first opportunity he claimed
    // (Naval Invasion, an Engage claim; CloseEngageWindow sets MatchState.AttackerLeadBattle) and has not yet passed or ended his action (burst) -- NextAction/Pass clear
    // it. An Attacker's Battle action announced then (an ordinary useAbility; an action taken on an additional-action grant goes through its own before-additional-
    // action window instead, as every announce-time window here) is held in Tsuruchi Yakusho's own "announceReaction" window, offered to the Defender holding this card.
    private void EndAttackerLeadIfCeded(int actingPlayerId, bool keepsPriority)
    {
        if (_state.AttackerLeadBattle >= 0 && actingPlayerId == _state.ActivePlayerId && !keepsPriority) _state.AttackerLeadBattle = -1;
    }

    private bool ResolveOrDeferEntrenchedReaction(GameCommand command)
    {
        if (_state.Phase != Phase.Battle || _state.CurrentBattleProvince is null || _state.PendingChoice is not null) return false;
        if (_state.AttackerLeadBattle != _state.BattleNumber || command.PlayerId != _state.ActivePlayerId) return false;
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || ability.Timing is not ("Battle" or "BattleOrOpen")) return false;
        if (EntrenchedWatcher(command) is not { } watcher) return false;
        _state.PendingAnnouncedAction = command;
        _state.PendingChoice = new("announceReaction", watcher.OwnerId, watcher.Id, "The Attacker announced a Battle action before your first opportunity: you may negate it (Entrenched Position), or pass.");
        _state.ConsecutivePasses = 0;
        _state.PriorityPlayerId = watcher.OwnerId;
        Emit("reactionWindow", "Announce reaction window opened.", watcher.OwnerId, watcher.Id);
        return true;
    }

    // Issue #898: every Defender's card that can negate the announced action is asked in turn (each is marked asked on the announcement's record), not only the first found in the hand.
    private CardInstance? EntrenchedWatcher(GameCommand command)
    {
        var record = AnnounceRecord(command);
        var defender = 1 - command.PlayerId;
        var watcher = Player(defender).Hand.Select(Card).Where(c => !record.Asked.Contains(c.Id)).OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault(c => (Definition(c).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var d)
            && d.Timing == "Reaction" && d.Trigger == EntrenchedTrigger && !(c.AbilityUses.TryGetValue(d.UsageKey ?? a, out var used) && used == _state.TurnNumber)));
        if (watcher is not null) record.Asked.Add(watcher.Id);
        return watcher;
    }

    // The window's close, for an announcement that was held for the Defender (the Attacker still leads the battle): the next of the Defender's watchers is asked.
    private CardInstance? NextEntrenchedWatcher(GameCommand pending) =>
        _state.Phase == Phase.Battle && _state.AttackerLeadBattle == _state.BattleNumber && pending.PlayerId == _state.ActivePlayerId && pending.AbilityId is not null && AbilityCatalog.All.TryGetValue(pending.AbilityId, out var ability) && ability.Timing is "Battle" or "BattleOrOpen"
            ? EntrenchedWatcher(pending) : null;

    private void NegateAnnouncedAction(CardInstance negatingCard)
    {
        if (_state.PendingAnnouncedAction is not { } announced) return;
        if (ActionProtected(announced))
        {
            Emit("actionProtected", "Protection negates the negation -- this action cannot be negated.", negatingCard.OwnerId, negatingCard.Id);
            return;
        }
        _state.AnnouncedActionNegated = true;
        Emit("actionNegated", $"{Definition(negatingCard).Name} negates {Player(announced.PlayerId).Name}'s action ({AbilityCatalog.All[announced.AbilityId!].Label}).", negatingCard.OwnerId, negatingCard.Id);
    }
    // ---- A Game of Dice: "Draw two additional cards before your turn ends" ----
    // Limited is only ever taken on the acting player's own turn, so "before your turn ends" is this turn's End Phase draw: Ingenuity's own counter
    // (MatchState.ExtraEndPhaseDraws, drawn by EndTurn right after the ordinary End Phase draw and before the hand-size check).
    private void QueueExtraEndPhaseDraws(PlayerState player, int count, string sourceName)
    {
        _state.ExtraEndPhaseDraws[player.Id] += count;
        Emit("extraDrawQueued", $"{player.Name} draws {count} additional card{(count == 1 ? "" : "s")} before the turn ends ({sourceName}).", player.Id);
    }

    // ---- Consideration: "After the next time this turn another player's action targets him, negate its effects" ----
    // Hibernation's own one-shot target shield (TargetShield.cs), scoped to the turn and to ANY action of another player's (not only a Battle action). A mandatory
    // trait, not a Reaction: it fires when the action is announced with its targets, before any reaction window, negates the whole action (its costs are still
    // paid, ResolveNegatedAction) and is spent -- only the next such action this turn is turned away. An action of the shielded card's own controller does not
    // spend it. An action that cannot be negated (Protection) still spends it, since the shield's own trigger ("the next time ... targets him") has happened.
    private void RaiseTurnTargetShield(CardInstance card, string sourceName)
    {
        card.TargetShieldTurn = _state.TurnNumber;
        Emit("targetShieldRaised", $"The next action of another player's that targets {Definition(card).Name} this turn will be negated ({sourceName}).", card.OwnerId, card.Id);
    }

    private bool TurnTargetShieldNegates(GameCommand command)
    {
        if (command.AbilityId is not { } id || !AbilityCatalog.All.TryGetValue(id, out var ability) || !IsAction(ability)) return false;
        var shielded = ActionTargetIds(command).Where(t => _state.Cards.ContainsKey(t)).Select(Card)
            .FirstOrDefault(c => c.TargetShieldTurn == _state.TurnNumber && c.OwnerId != command.PlayerId && InPlay(c));
        if (shielded is null) return false;
        shielded.TargetShieldTurn = -1;
        if (ActionProtected(command))
        {
            Emit("actionProtected", $"Protection: {Player(command.PlayerId).Name}'s action on {Definition(shielded).Name} cannot be negated (Consideration's shield is spent).", shielded.OwnerId, shielded.Id);
            return false;
        }
        Emit("actionNegated", $"{Definition(shielded).Name} was shielded (Consideration): {Player(command.PlayerId).Name}'s action ({ability.Label}) is negated.", shielded.OwnerId, shielded.Id);
        ResolveNegatedAction(command);
        return true;
    }

    // ---- Creating Order: "negate all current and new Force bonuses and penalties on him from actions" ----
    // Tireless Efforts' own shape (ForcePenalties.cs) widened to bonuses as well as penalties, and narrowed to what ACTIONS give: the current Force modifiers whose
    // effect is an action ability (a trait's standing modifier -- Mirumoto Yozo's, Apprentice Shinobi's -- is not "from an action" and stays) are removed, and for
    // the rest of the turn no Force modifier or Force token is applied to him while an action is resolving (the same "an action is resolving" read RecordHonorLoss
    // uses). As with Tireless Efforts, the Force tokens already on him are left alone: a token is a card-state object that outlasts the turn, not a turn-long bonus.
    private void NegateActionForceModifiers(CardInstance card)
    {
        var removed = card.Modifiers.RemoveAll(m => m.Stat == "Force" && AbilityCatalog.All.TryGetValue(m.Effect, out var giving) && IsAction(giving));
        card.ForceModifiersNegatedTurn = _state.TurnNumber;
        Emit("forceModifiersNegated", $"{Definition(card).Name}: every Force bonus and penalty on him from actions is negated for the turn{(removed > 0 ? $" ({removed} removed)" : "")}.", card.OwnerId, card.Id);
    }

    private bool ActionForceModifierNegated(CardInstance card, string? stat, int amount) =>
        stat == "Force" && amount != 0 && card.ForceModifiersNegatedTurn == _state.TurnNumber
        && (_runningCommand is not null || _state.PendingAttack is not null || _state.PendingDuel is not null);

    // ---- Feint & Strike's Battle action ----
    // "If your Commander has a Follower, you may target and destroy an attachment in the Personality's unit. Move him home." The optional attachment slot is only
    // listed while the condition holds (the feint-strike-attachment category); it is re-read here in case the Commander lost his Follower before resolution.
    private void FeintAndStrike(GameCommand command, int playerId)
    {
        var enemy = SlotCards(command, "target").Single();
        var commander = Card(command.PerformerId!);
        if (Attachments(commander.Id).Any(a => Definition(a).Type == CardType.Follower)
            && SlotCards(command, "attachment").FirstOrDefault() is { } attachment && attachment.Zone == Zone.Attached && attachment.AttachedTo == enemy.Id)
            DestroyUnitCard(attachment, playerId);
        if (InPlay(enemy)) MoveHome(enemy, false);
    }
}
