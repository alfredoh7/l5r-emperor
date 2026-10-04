namespace L5R.Core;

// ======================================================================================================================================================================================
// EMPEROR EDITION FOLLOW-UPS (issue #498): NO HIDING PLACE (oracle-l5r-5599) and THE ATTACK TARGET SELECTION STEP.
//
// "Reaction: After you announce an action with a Ranged Attack: Give one Ranged Attack from the action -1 strength. That Ranged Attack may target a card in the Defender's home (if otherwise legal)."
// Mendon's ruling: it is played BEFORE the Attack's target is selected; it does not redirect an Attack, but several cards allow re-targeting, so the general capability is built:
//
//   * THE ANNOUNCE WINDOW FOR A HAND STRATEGY. "After you announce an action" is the announce window (Tsuruchi Yakusho's "announceReaction", opened by ResolveOrDeferAnnounceReaction before any of the
//     action's costs, targets or effects): FindAnnounceWatcher now also opens it for a No Hiding Place in the announcing player's hand when the action produces a Ranged Attack during a battle, and the
//     window lists (MatchQueries) and accepts (the trigger NoHidingPlaceTrigger) that player's No Hiding Place whichever card the window was opened for.
//   * A PER-ANNOUNCED-ACTION RECORD (MatchState.AnnouncedAttackModifiers): the Reaction records, for the announced action (its player and card), a strength delta (-1) and the permission to target a card in
//     the Defender's home. The first Ranged Attack the action produces consumes it ("one Ranged Attack from the action": ResolveOrDeferAttack adds the delta and marks it applied); the permission lasts
//     until that Attack has resolved (ResolveAttackNow releases it), so a second Attack of the same action never has it. A record lapses with its battle.
//   * THE TARGET SELECTION STEP (PendingAttackTargetSelection, PendingChoice "attackTargetSelect", command "chooseAttackTarget" with TargetId, or Choice "none"): when the announce window closes and a Reaction
//     in it changed what the announced Attack may target, the Attack's target is (re)selected by its player among the targets legal NOW -- the provisional target the announcing command carried (or none)
//     is just the default: an Attack's target is selected after the announce window (core rules: announcing comes before targets are selected). Any card that allows re-targeting an announced Attack uses
//     the same step (BeginAttackTargetSelection with its own reason). Only abilities whose Attack target is the command's TargetId (Target "attack") are re-selected; the others keep their targets.
//   * DEFENDER'S-HOME TARGETS (RequireLegalAttackTarget, ResolveAttackNow): under the permission a Follower, or a Personality without Followers, in the Defender's home (an enemy card: the Defender is the
//     non-active player of the battle) is a legal target and is destroyed when the Attack hits it -- every other rule of an Attack target still applies ("if otherwise legal"). Its location is the
//     Defender's home, so reactions keyed to the battlefield (Yojimbo, Ezuno ...) read it as not in an army.
// ======================================================================================================================================================================================

/// <summary>What an announce-window Reaction changed about the Attacks of the announced action (MatchState.AnnouncedAttackModifiers): the action's player and card, the strength delta and the home permission.</summary>
public sealed class AnnouncedAttackModifier
{
    public int PlayerId { get; set; }
    public string? ActionCardId { get; set; }
    public string? ActionAbilityId { get; set; }
    public int StrengthDelta { get; set; }
    public bool MayTargetDefenderHome { get; set; }
    public string ModifierSourceId { get; set; } = "";
    public int Battle { get; set; }
    /// <summary>True once the action's first Ranged Attack took it; it is released when that Attack has resolved.</summary>
    public bool Applied { get; set; }
}

/// <summary>The target selection step of an announced Attack (MatchState.PendingAttackTargetSelection): the announced command, its player, the targets legal now and why the step opened.</summary>
public sealed class PendingAttackTargetSelection
{
    public GameCommand Command { get; set; } = new("useAbility", 0, 0);
    public int PlayerId { get; set; }
    public List<string> Candidates { get; set; } = [];
    public string Reason { get; set; } = "";
}

public sealed partial class MatchEngine
{
    // ---- the announce window ----

    private static bool AnnouncedActionHasRangedAttack(GameCommand command) =>
        command.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing != "Reaction" && AttackKind(id) == "Ranged";

    private static bool IsNoHidingPlace(CardInstance card) =>
        (Definition(card).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing == "Reaction" && d.Trigger == EmperorFollowupsAbilities.NoHidingPlaceTrigger);

    // FindAnnounceWatcher's hook: a No Hiding Place in the announcing player's hand, for an action with a Ranged Attack during a battle.
    private CardInstance? NoHidingPlaceWatcher(GameCommand command) =>
        _state.Phase == Phase.Battle && _state.CurrentBattleProvince is not null && AnnouncedActionHasRangedAttack(command)
            ? Player(command.PlayerId).Hand.Select(Card).FirstOrDefault(IsNoHidingPlace)
            : null;

    // The trigger check (ValidateAbility): the announce window of an action of this player's with a Ranged Attack, during a battle.
    private bool NoHidingPlaceWindowOpen(GameCommand command) =>
        _state.PendingChoice?.Type == "announceReaction" && _state.PendingAnnouncedAction is { } announced && announced.PlayerId == command.PlayerId
            && _state.CurrentBattleProvince is not null && AnnouncedActionHasRangedAttack(announced);

    // The listing (MatchQueries' announce window): the player's No Hiding Places in hand, whichever card the window was opened for.
    private IEnumerable<(CardInstance Card, string AbilityId)> NoHidingPlaceOptions(int playerId)
    {
        if (_state.PendingAnnouncedAction is not { } announced || announced.PlayerId != playerId || _state.CurrentBattleProvince is null || !AnnouncedActionHasRangedAttack(announced)) yield break;
        foreach (var card in Player(playerId).Hand.Select(Card).Where(IsNoHidingPlace).ToArray())
            if (_state.PendingChoice?.CardId != card.Id) // the window's own card is already listed
                yield return (card, EmperorFollowupsAbilities.NoHidingPlace);
    }

    // The Reaction's effect: the record on the announced action.
    private void NoHidingPlaceReaction(PlayerState player, CardInstance source)
    {
        var announced = _state.PendingAnnouncedAction ?? throw new RuleViolation("No action was announced.");
        _state.AnnouncedAttackModifiers.RemoveAll(m => m.Battle != _state.BattleNumber);
        _state.AnnouncedAttackModifiers.Add(new AnnouncedAttackModifier
        {
            PlayerId = announced.PlayerId,
            ActionCardId = announced.CardId,
            ActionAbilityId = announced.AbilityId,
            StrengthDelta = -1,
            MayTargetDefenderHome = true,
            ModifierSourceId = source.Id,
            Battle = _state.BattleNumber
        });
        Emit("announcedAttackModified", $"{Definition(source).Name}: one Ranged Attack from {player.Name}'s action has -1 strength and may target a card in the Defender's home.", player.Id, source.Id);
    }

    // ---- the per-action record ----

    private IEnumerable<AnnouncedAttackModifier> ModifiersOf(int playerId, string? actionCardId) =>
        _state.AnnouncedAttackModifiers.Where(m => m.Battle == _state.BattleNumber && m.PlayerId == playerId && m.ActionCardId == actionCardId);

    // The Defender's home: the non-active player's home during a battle (an attached card by its host's place).
    private bool InDefendersHome(CardInstance card)
    {
        if (_state.CurrentBattleProvince is null) return false;
        var host = card.Zone == Zone.Attached && card.AttachedTo is { } hostId ? Card(hostId) : card;
        return host.Zone == Zone.Home && host.OwnerId == 1 - _state.ActivePlayerId && InPlay(card);
    }

    /// <summary>RequireLegalAttackTarget's hook: this action's Ranged Attack may target a card in the Defender's home.</summary>
    private bool MayTargetInDefendersHome(GameCommand command, CardInstance target) =>
        command.AbilityId is { } id && AttackKind(id) == "Ranged" && InDefendersHome(target) && ModifiersOf(command.PlayerId, command.CardId).Any(m => m.MayTargetDefenderHome);

    /// <summary>ResolveOrDeferAttack's hook: the first Ranged Attack of the action takes the recorded strength changes (marking them applied). Returns the delta.</summary>
    private int ConsumeAnnouncedAttackModifiers(string abilityId, int playerId)
    {
        if (AttackKind(abilityId) != "Ranged" || _runningCommand is not { } running) return 0;
        var delta = 0;
        foreach (var modifier in ModifiersOf(playerId, running.CardId).Where(m => !m.Applied))
        {
            modifier.Applied = true;
            delta += modifier.StrengthDelta;
            if (modifier.StrengthDelta != 0) Emit("attackStrengthModified", $"{Definition(Card(modifier.ModifierSourceId)).Name}: this Ranged Attack has {modifier.StrengthDelta:+#;-#;0} strength.", playerId, modifier.ModifierSourceId);
        }
        return delta;
    }

    /// <summary>ResolveAttackNow's hook: the Attack under way may hit a card in the Defender's home.</summary>
    private bool AttackMayHitInDefendersHome(int playerId, CardInstance target) =>
        (_attackCommandInResolution ?? _runningCommand) is { } command && InDefendersHome(target)
            && ModifiersOf(playerId, command.CardId).Any(m => m.Applied && m.MayTargetDefenderHome);

    /// <summary>The Attack that took the record has resolved: it is released (a later Attack of the same action never has it).</summary>
    private void ReleaseAnnouncedAttackModifiers(int playerId)
    {
        if ((_attackCommandInResolution ?? _runningCommand) is not { } command) return;
        _state.AnnouncedAttackModifiers.RemoveAll(m => m.Applied && m.PlayerId == playerId && m.ActionCardId == command.CardId);
    }

    // ---- the target selection step ----

    /// <summary>Called as the announce window closes: when a Reaction in it changed what the announced Attack may target, its player (re)selects the target now. True when the step opened.</summary>
    private bool BeginAttackTargetSelection(GameCommand announced)
    {
        if (ModifiersOf(announced.PlayerId, announced.CardId).FirstOrDefault(m => m.MayTargetDefenderHome && !m.Applied) is not { } permission) return false;
        return OpenAttackTargetSelection(announced, $"{Definition(Card(permission.ModifierSourceId)).Name}: choose the Ranged Attack's target now"); // issue #622: also Lookout's
    }

    /// <summary>The general step: the announced Attack's player selects its target among those legal now (or none). Only an ability whose Attack target is the command's TargetId is re-selected.</summary>
    private bool OpenAttackTargetSelection(GameCommand announced, string reason)
    {
        if (announced.AbilityId is not { } id || !AbilityCatalog.All.TryGetValue(id, out var ability) || ability.Target != "attack" || announced.Province is not null) return false;
        var player = Player(announced.PlayerId);
        var source = announced.CardId is null ? null : Card(announced.CardId);
        var candidates = _state.Cards.Values.Where(c => InPlay(c) && c.OwnerId != player.Id && LegalAnnouncedAttackTarget(id, ability, announced, player, source, c))
            .Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
        _state.PendingAttackTargetSelection = new PendingAttackTargetSelection { Command = announced, PlayerId = player.Id, Candidates = candidates, Reason = reason };
        _state.PendingChoice = new("attackTargetSelect", player.Id, announced.CardId, $"{reason} ({candidates.Count} legal), or none.");
        _state.PriorityPlayerId = player.Id; _state.ConsecutivePasses = 0;
        Emit("attackTargetSelection", $"{player.Name} selects the target of the announced Attack.", player.Id, announced.CardId);
        return true;
    }

    private bool LegalAnnouncedAttackTarget(string id, AbilityDefinition ability, GameCommand announced, PlayerState player, CardInstance? source, CardInstance target)
    {
        try { RequireLegalAttackTarget(id, ability, announced with { TargetId = target.Id }, player, source, target); return true; }
        catch (RuleViolation) { return false; }
    }

    private void ResolveAttackTargetSelection(GameCommand command)
    {
        var pending = _state.PendingAttackTargetSelection ?? throw new RuleViolation("No Attack target is being selected.");
        Require(command.Type == "chooseAttackTarget" && command.PlayerId == pending.PlayerId, "Choose the Attack's target, or none.");
        string? targetId = null;
        if (command.Choice != "none") { Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose one of the offered targets, or none."); targetId = command.TargetId; }
        _state.PendingAttackTargetSelection = null; _state.PendingChoice = null;
        var player = Player(pending.PlayerId);
        Emit("attackTargetSelected", targetId is null ? $"{player.Name}'s Attack targets no card." : $"{player.Name}'s Attack targets {Definition(Card(targetId)).Name}.", player.Id, targetId);
        ContinueAnnouncedAction(pending.Command with { TargetId = targetId });
    }

    private IEnumerable<ActionOption> AttackTargetSelectionActions(int playerId)
    {
        if (_state.PendingAttackTargetSelection is not { } p || p.PlayerId != playerId) yield break;
        foreach (var id in p.Candidates)
            yield return new("chooseAttackTarget", $"Attack · {Definition(Card(id)).Name}{(InDefendersHome(Card(id)) ? " (in the Defender's home)" : "")}", true, null,
                new GameCommand("chooseAttackTarget", playerId, _state.Version, CardId: p.Command.CardId, TargetId: id), id);
        yield return new("chooseAttackTarget", "Attack no target", true, null, new GameCommand("chooseAttackTarget", playerId, _state.Version, CardId: p.Command.CardId, Choice: "none"), p.Command.CardId);
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredAttackTargetSelect(string type, GameCommand command)
    {
        if (type != "attackTargetSelect") return false;
        ResolveAttackTargetSelection(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorFollowupsAttackHooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredAttackTargetSelect", 200, static (engine, type, command) => engine.HandleRegisteredAttackTargetSelect(type, command));
    }
}
