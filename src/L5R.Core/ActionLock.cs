namespace L5R.Core;

// "May not perform actions" (issue #336): Daigotsu Hotako's Fear Open, and the same gap on Tamori House Guard, Hiruma Nikaru and
// Kuon's Legacy. The reading, with its alternative, is in docs/decisions/2026-09-19-hotako-yoshitada-predator-liuxeung.md.
public sealed partial class MatchEngine
{
    private const string ActionLockUntilTurnEnd = "Turn";
    private const string ActionLockPhasePrefix = "Phase:";

    // "Until the current phase ends" (Kakita Munemori - exp, issue #431): the lock names the phase INSTANCE it belongs to (PrintedPhases.Instance: this turn's Action Phase, this turn's Attack Phase)
    // and counts only while that instance is the current one, so it lapses by itself when the phase ends, with nothing to clear.
    private string CurrentPhaseLock() => ActionLockPhasePrefix + PrintedPhases.Instance(_state.TurnNumber, _state.Phase);
    private bool LockInForce(ActionLock actionLock) => !actionLock.Duration.StartsWith(ActionLockPhasePrefix, StringComparison.Ordinal) || actionLock.Duration == CurrentPhaseLock();
    private bool HasActionLock(CardInstance card) => card.ActionLocks.Any(LockInForce);

    // Places the lock. A locked card can still be targeted, moved, bowed and so on; it only stops being able to perform. A card that
    // is locked twice is locked once as far as the rules go (the second entry just lasts as long as the first).
    private void LockActions(CardInstance card, string sourceId, string duration = ActionLockUntilTurnEnd)
    {
        card.ActionLocks.Add(new(sourceId, duration));
        Emit("actionsLocked", $"{Definition(card).Name} may not perform actions {(duration == ActionLockUntilTurnEnd ? "this turn" : "until the phase ends")}.", card.OwnerId, card.Id);
    }

    // The card is locked, or it is an attachment (Item, Spell, Follower) of a locked Personality: an action printed on it is
    // performed by, or through, that Personality.
    private bool ActionLocked(CardInstance card) =>
        HasActionLock(card) || card.Zone == Zone.Attached && card.AttachedTo is string hostId && _state.Cards.TryGetValue(hostId, out var host) && HasActionLock(host);

    // ValidateAbility's check, for every action there is -- an ability on a card, a Reaction (a Reaction IS an action: IsAction, ReactionsAreActions.cs, the one
    // classification), a Strategy naming its performer -- because they all reach the engine as an ability command. A trait or a mandatory effect is not an
    // action (IsAction is false for it) and is never barred. Who performs is the card the ability names as its performer when that is a
    // Personality or a Follower ("choose your performing Samurai"), otherwise the card the ability is on (an Item's or Spell's action is
    // performed through the Personality it is attached to, which ActionLocked follows). A performer that is only a cost (a Holding to bow
    // instead of the source) does not perform: the action is still the source's.
    private void RequireCanPerform(GameCommand command, AbilityDefinition ability, CardInstance? source)
    {
        if (!IsAction(ability)) return;
        var named = ability.Performer is not null && command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var chosen) ? chosen : null;
        var actor = named is not null && Definition(named).Type is CardType.Personality or CardType.Follower ? named : source;
        if (actor is not null) Require(!ActionLocked(actor), $"{Definition(actor).Name} may not perform actions this turn.");
        // "Choose one of his abilities; it may not be used" (Forgotten Legacy pass, issue #467, batch 1): the one printed ability of the source card, locked for the turn.
        RequireAbilityNotDisabled(command, source);
        if (ability.Slots is not null)
            foreach (var performing in PerformingSlotCards(command, ability)) Require(!ActionLocked(performing), $"{Definition(performing).Name} may not perform actions this turn.");
        // "A Stronghold's abilities may not be used" (Interference): a card that is not a unit -- a Stronghold, Holding, Region or Celestial -- performs nothing itself, so the lock on IT bars
        // every ability printed on it, whoever the ability names as its performer (Shinden Asahina's is performed by a Shugenja).
        if (source is not null && HasActionLock(source) && Definition(source).Type is CardType.Stronghold or CardType.Holding or CardType.Region or CardType.Celestial)
            Require(false, $"{Definition(source).Name}'s abilities may not be used this turn.");
        RequireBurningDreamsAllows(command, ability, source); // Burning Dreams (Forgotten Legacy pass, issue #467): Nonhumans may not perform Political actions.
        RequireChiisaiAllows(command, ability, source); // Chiisai (The Shadow's Embrace pass, issue #476 batch 1): may not perform Iaijutsu or Political actions.
    }

    // The reason the lock alone would refuse this command, or null. A Reaction listed inside a reaction window is offered before ValidateAbility ever
    // sees it (only some carry their own validation), so the listing asks the lock directly.
    private string? ActionLockRejection(GameCommand command)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return null;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var card) ? card : null;
        try { RequireCanPerform(command, ability, source); return null; }
        catch (RuleViolation e) { return e.Message; }
    }
}
