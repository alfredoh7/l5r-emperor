namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, GROUP P3 (continued from EmperorP3.cs): the duel-won window's after-action tracking, the window before an Event resolves, the end of the Events Phase, the Personalities an
// action bowed, a Follower shield for one held action, and a Follower's "after this card bows" trait.
//   * AN ACTION'S RECORD SURVIVES ITS DUEL-WON WINDOW (Kakita Kenta): the winner of a duel goes on the action's record (WonDuelKind); when a duel-won window opens, what the duel's action
//     affected is set aside (the Reactions played in the window are actions with records of their own) and its after-action tracking runs as the window closes. Before, a duel-won window
//     skipped the duel's after-action tracking entirely.
//   * A WINDOW BEFORE AN EVENT RESOLVES (Forging Destiny): "p3BeforeEventReaction", an alternating window opened in ResolveEvent when a player holds a Reaction to it; a negated Event is
//     discarded without its effects and the reveal goes on.
//   * AFTER YOUR EVENTS PHASE (Daigotsu Miki): a trait applied as the player's Events Phase ends ("p3MikiDarkVirtue", answered by "p3MikiRemove").
//   * THE PERSONALITIES AN ACTION BOWED (Hida Defenders): recorded on the action (BowedPersonalityKind) against the bowed set before its effects -- for an Attack or a duel, the set before
//     the action began (PendingAttack.BowedBefore / PendingDuel.BowedBefore).
//   * A FOLLOWER SHIELD FOR ONE HELD ACTION (Hida Fujita): the destruction of a player's Followers by that action's own effects is negated (DestroyAttachment); a Follower that leaves play with
//     its destroyed Personality is not protected (it cannot stay in play without him).
//   * "AFTER THIS CARD BOWS: STRAIGHTEN IT" (Vigilant Riders): the after-command trait convention (SeedsOfDecayBatch5.cs: a card that bowed during the command), Blade of Perfection's shape.
// ======================================================================================================================================================================================
public sealed partial class MatchEngine
{
    public const string WonDuelKind = "won-duel";
    public const string BowedPersonalityKind = "bowed-personality";

    // ---- Kakita Kenta: "an action ... during which Kenta won a duel" ---------------------------------------------------------------------------------------------------------------------------

    /// <summary>FinishDuelResolution's hook: the duel's winner goes on the action's record.</summary>
    private void P3RecordDuelWinner(string? winnerId)
    {
        if (winnerId is not null) RecordActionEffect(winnerId, WonDuelKind);
        Wave9601NoteDuelWon(winnerId); // issue #689 (Wave9601.cs): Lessons from the Heralds
    }

    /// <summary>FinishDuelResolution's duel-won window: what the duel's action affected is set aside while the window is open, and its after-action tracking runs when the window closes
    /// (P3FinishStashedDuel). Returns whether the window opened.</summary>
    private bool P3OpenDuelWonWindow(PendingDuel duel, string winnerId, int winnerPlayerId, int closesTo)
    {
        var stash = new StashedDuelTracking
        {
            AbilityId = duel.AbilityId,
            PlayerId = duel.ChallengerPlayerId,
            SourceId = duel.SourceCardId,
            PerformerId = duel.ChallengerId,
            TargetIds = [duel.ChallengedId],
            Affected = _state.CurrentActionAffected,
            HonorLosers = _state.CurrentActionHonorLosers,
            HandRecipients = _state.CurrentActionHandRecipients,
            FavorTakers = _state.CurrentActionFavorTakers
        };
        _state.CurrentActionAffected = []; _state.CurrentActionHonorLosers = []; _state.CurrentActionHandRecipients = []; _state.CurrentActionFavorTakers = [];
        if (!OpenDuelWonReactionWindow(winnerId, winnerPlayerId, closesTo))
        {
            _state.CurrentActionAffected = stash.Affected; _state.CurrentActionHonorLosers = stash.HonorLosers; _state.CurrentActionHandRecipients = stash.HandRecipients; _state.CurrentActionFavorTakers = stash.FavorTakers;
            return false;
        }
        P3.StashedDuel = stash;
        return true;
    }

    /// <summary>PassReaction's hook as a duel-won window closes: the duel's action is now truly done. True when an after-action window opened over it.</summary>
    private bool P3FinishStashedDuel(int resumeTo)
    {
        if (P3.StashedDuel is not { } stash) return false;
        P3.StashedDuel = null;
        _state.CurrentActionAffected = stash.Affected; _state.CurrentActionHonorLosers = stash.HonorLosers; _state.CurrentActionHandRecipients = stash.HandRecipients; _state.CurrentActionFavorTakers = stash.FavorTakers;
        return FinishActionEffectTracking(stash.AbilityId, stash.PlayerId, stash.SourceId, resumeTo, stash.PerformerId, stash.TargetIds);
    }

    // ---- Forging Destiny: the window before an Event resolves -----------------------------------------------------------------------------------------------------------------------------------

    private ActionOption[] BeforeEventOptions(int playerId, string eventId)
    {
        var saved = _state.PendingChoice;
        _state.PendingChoice = new("p3BeforeEventReaction", playerId, eventId, "Before this Event resolves: use a Reaction, or pass.");
        try { return AbilityActions(playerId).Where(o => o.Enabled).ToArray(); }
        finally { _state.PendingChoice = saved; }
    }

    /// <summary>ResolveEvent's hook. Null: resolve the Event as usual. True: a window opened before it (the reveal waits). False: its effects were negated -- it was discarded and the reveal goes on.</summary>
    private bool? P3BeforeEvent(PlayerState player, ProvinceState province, CardInstance card)
    {
        if (P3.EventAskedCardId == card.Id)
        {
            P3.EventAskedCardId = null;
            if (P3.EventNegatedCardId != card.Id) return null;
            P3.EventNegatedCardId = null;
            province.CardId = null; card.ProvinceIndex = null; card.Zone = Zone.DynastyDiscard; card.FaceUp = true;
            Emit("eventResolved", $"{Definition(card).Name}'s effects were negated; it was discarded.", player.Id, card.Id);
            Refill(player, province);
            return false;
        }
        bool Holds(int p) => BeforeEventOptions(p, card.Id).Length > 0;
        if (!Holds(0) && !Holds(1)) return null;
        P3.EventWindowCardId = card.Id; P3.EventWindowPlayerId = player.Id; P3.EventNegatedCardId = null;
        var first = FirstReactionOpportunity(Holds);
        OpenAlternatingReactionWindow(new("p3BeforeEventReaction", first, card.Id, $"Before {Definition(card).Name} resolves: either player may use a Reaction, or pass.", player.Id), first, "Before-Event reaction window opened.");
        return true;
    }

    /// <summary>A command in the before-Event window: a Reaction, or a pass; two passes in succession close it and the Event resolves (or, negated, is discarded) and the reveal goes on.</summary>
    private void ResolveP3BeforeEventCommand(GameCommand command)
    {
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Play a Reaction before the Event resolves, or pass.");
        if (command.Type == "useAbility")
        {
            UseAbility(command);
            if (_state.PendingChoice is null && P3.EventWindowCardId is not null) RestoreBeforeEventWindow();
            return;
        }
        Emit("reactionPassed", $"{Player(_state.PriorityPlayerId).Name} passed the reaction opportunity.", _state.PriorityPlayerId);
        if (++_state.ConsecutivePasses < 2) { _state.PriorityPlayerId = 1 - _state.PriorityPlayerId; return; }
        Emit("reactionClosed", "Both players passed; the reaction window closed.");
        var player = Player(P3.EventWindowPlayerId);
        var card = Card(P3.EventWindowCardId!);
        P3.EventWindowCardId = null;
        _state.PendingChoice = null; _state.ConsecutivePasses = 0; _state.PriorityPlayerId = player.Id;
        P3.EventAskedCardId = card.Id;
        var province = player.Provinces[card.ProvinceIndex!.Value];
        if (!ResolveEvent(player, province, card)) ContinueEventsReveal(player);
    }

    // A Reaction played in the window hands the opportunity to the other player (NextAction), the window staying open.
    private void RestoreBeforeEventWindow()
    {
        var card = Card(P3.EventWindowCardId!);
        _state.PendingChoice = new("p3BeforeEventReaction", _state.PriorityPlayerId, card.Id, $"Before {Definition(card).Name} resolves: either player may use a Reaction, or pass.", P3.EventWindowPlayerId);
        _state.ConsecutivePasses = 0;
    }

    // ---- Daigotsu Miki: "After your Events Phase: You may remove a Dark Virtue in your hand or discard pile from the game. If you did not remove one, destroy Miki." --------------------------------

    private IEnumerable<CardInstance> MikiDarkVirtues(PlayerState player) =>
        _state.Cards.Values.Where(c => c.OwnerId == player.Id && IsDarkVirtue(c) && (c.Zone == Zone.Hand && player.Hand.Contains(c.Id) || c.Zone == Zone.FateDiscard)).OrderBy(c => c.Id, StringComparer.Ordinal);

    /// <summary>ContinueEventsReveal's hook as its player's Events Phase ends: each of his Mikis in play asks in turn. True when a decision is open (the phase ends from there).</summary>
    private bool P3AfterEventsPhase(PlayerState player)
    {
        P3.MikiHandled.RemoveAll(k => !k.EndsWith($"|{_state.TurnNumber}", StringComparison.Ordinal));
        foreach (var miki in _state.Cards.Values.Where(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Effect == EmperorP3Abilities.MikiEffect).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            var key = $"{miki.Id}|{_state.TurnNumber}";
            if (P3.MikiHandled.Contains(key)) continue;
            P3.MikiHandled.Add(key);
            if (!MikiDarkVirtues(player).Any())
            {
                Emit("mikiDestroyed", $"{Definition(miki).Name}: {player.Name} has no Dark Virtue to remove from the game.", player.Id, miki.Id);
                DestroyUnits([miki]);
                continue;
            }
            P3.MikiAsking = miki.Id;
            _state.PendingChoice = new("p3MikiDarkVirtue", player.Id, miki.Id, $"{Definition(miki).Name}: you may remove a Dark Virtue in your hand or discard pile from the game. If you do not, {Definition(miki).Name} is destroyed.");
            _state.PriorityPlayerId = player.Id; _state.ConsecutivePasses = 0;
            return true;
        }
        return false;
    }

    private void ResolveP3MikiCommand(GameCommand command)
    {
        var miki = Card(P3.MikiAsking ?? throw new RuleViolation("No decision is open."));
        var player = Player(miki.OwnerId);
        Require(command.Type == "p3MikiRemove" && command.PlayerId == player.Id, "Remove a Dark Virtue from the game, or decline.");
        if (command.TargetId is { } chosen)
        {
            Require(MikiDarkVirtues(player).Any(c => c.Id == chosen), "Choose a Dark Virtue in your hand or discard pile.");
            var card = Card(chosen);
            if (card.Zone == Zone.Hand) player.Hand.Remove(card.Id);
            RemoveFromGame(card);
            Emit("darkVirtueRemoved", $"{Definition(miki).Name}: {player.Name} removed {Definition(card).Name} from the game.", player.Id, card.Id);
        }
        else
        {
            Emit("mikiDestroyed", $"{Definition(miki).Name}: {player.Name} removed no Dark Virtue.", player.Id, miki.Id);
            if (InPlay(miki)) DestroyUnits([miki]);
        }
        P3.MikiAsking = null;
        _state.PendingChoice = null;
        if (P3AfterEventsPhase(player)) return;
        SetPhase(Phase.Action, player.Id);
    }

    private IEnumerable<ActionOption> P3MikiActions(int playerId)
    {
        if (_state.PendingChoice is not { Type: "p3MikiDarkVirtue" } pending || pending.PlayerId != playerId) yield break;
        foreach (var card in MikiDarkVirtues(Player(playerId)))
            yield return new("p3MikiRemove", $"Remove {Definition(card).Name} ({(card.Zone == Zone.Hand ? "hand" : "discard pile")}) from the game", true, null,
                new GameCommand("p3MikiRemove", playerId, _state.Version, TargetId: card.Id), pending.CardId, [card.Id]);
        yield return new("p3MikiRemove", $"Remove nothing: {Definition(Card(pending.CardId!)).Name} is destroyed", true, null, new GameCommand("p3MikiRemove", playerId, _state.Version), pending.CardId, []);
    }

    // ---- Hida Defenders: the Personalities an action bowed go on its record ------------------------------------------------------------------------------------------------------------------

    private List<string> P3BowedPersonalityIds() => _state.Cards.Values.Where(c => c.Bowed && Definition(c).Type == CardType.Personality).Select(c => c.Id).ToList();

    /// <summary>Records every Personality in play that is bowed now and was not in <paramref name="bowedBefore"/>.</summary>
    private void P3RecordBowedPersonalities(IEnumerable<string>? bowedBefore)
    {
        if (bowedBefore is null) return;
        var before = bowedBefore.ToHashSet(StringComparer.Ordinal);
        foreach (var card in _state.Cards.Values.Where(c => c.Bowed && InPlay(c) && Definition(c).Type == CardType.Personality && !before.Contains(c.Id)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            RecordActionEffect(card.Id, BowedPersonalityKind);
    }

    // ---- Hida Fujita: "Negate the destruction of your Followers from its effects" (DestroyAttachment's hook) ------------------------------------------------------------------------------------

    private HeldActionKey? ResolvingActionKey() =>
        KeyOf(_runningCommand ?? _attackCommandInResolution) ?? (_state.PendingDuel is { } duel ? new(duel.ChallengerPlayerId, duel.SourceCardId, duel.AbilityId) : null);

    private bool P3FollowerShielded(CardInstance card) =>
        Definition(card).Type == CardType.Follower && P3.FollowerShields.Count > 0 && ResolvingActionKey() is { } resolving
        && P3.FollowerShields.Any(s => s.PlayerId == card.OwnerId && s.Turn == _state.TurnNumber && s.Action == resolving) && !HiromiUnnegatable();

    // ---- Vigilant Riders: "After this card bows: Straighten it." (a trait) -------------------------------------------------------------------------------------------------------------------

    /// <summary>ApplyAfterCommandTraits' hook (the after-command traits of SeedsOfDecayBatch5.cs, Blade of Perfection's and Ronin Ambushers' shape): a Vigilant Riders that bowed during the command
    /// straightens (unless its straightening is refused).</summary>
    private void P3AfterCommandTrait(CardInstance card, bool wasBowed)
    {
        if (Definition(card).Effect != EmperorP3Abilities.RidersEffect || !card.Bowed || wasBowed || StraightenRefused(card)) return;
        Straighten(card);
        Emit("traitStraightened", $"{Definition(card).Name} bowed, and straightens.", card.OwnerId, card.Id);
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredP3BeforeEventReaction(string type, GameCommand command)
    {
        if (type != "p3BeforeEventReaction") return false;
        ResolveP3BeforeEventCommand(command);
        return true;
    }
    private bool HandleRegisteredP3MikiDarkVirtue(string type, GameCommand command)
    {
        if (type != "p3MikiDarkVirtue") return false;
        ResolveP3MikiCommand(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP3WindowsHooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredP3BeforeEventReaction", 2000, static (engine, type, command) => engine.HandleRegisteredP3BeforeEventReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredP3MikiDarkVirtue", 2100, static (engine, type, command) => engine.HandleRegisteredP3MikiDarkVirtue(type, command));
    }
}
