namespace L5R.Core;

public sealed partial class MatchEngine
{
    private bool HandleRegisteredShinden(string type, GameCommand command)
    {
        if (type != "shinden") return false;
        ResolveShinden(command);
        return true;
    }
    private bool HandleRegisteredPreservingHonor(string type, GameCommand command)
    {
        if (type != "preservingHonor") return false;
        ResolvePreservingHonor(command);
        return true;
    }
    private bool HandleRegisteredRikateDetach(string type, GameCommand command)
    {
        if (type != "rikateDetach") return false;
        ResolveRikateDetach(command);
        return true;
    }
    private bool HandleRegisteredRinShame(string type, GameCommand command)
    {
        if (type != "rinShame") return false;
        ResolveRinShame(command);
        return true;
    }
    private bool HandleRegisteredYourClanNeedsYouProvince(string type, GameCommand command)
    {
        if (type != "yourClanNeedsYouProvince") return false;
        ResolveYourClanNeedsYouProvince(command);
        return true;
    }
    private bool HandleRegisteredDeclineStraighten(string type, GameCommand command)
    {
        if (type != "declineStraighten") return false;
        ResolveDeclineStraighten(command);
        return true;
    }
    private bool HandleRegisteredConqueror(string type, GameCommand command)
    {
        if (type != "conqueror") return false;
        ResolveConqueror(command);
        return true;
    }
    private bool HandleRegisteredBeforeBattleOrder(string type, GameCommand command)
    {
        if (type != "beforeBattleOrder") return false;
        ResolveBeforeBattleOrder(command);
        return true;
    }
    private bool HandleRegisteredBeforeBattleOptional(string type, GameCommand command)
    {
        if (type != "beforeBattleOptional") return false;
        ResolveBeforeBattleOptional(command);
        return true;
    }
    private bool HandleRegisteredEngageReaction(string type, GameCommand command)
    {
        if (type != "engageReaction") return false;
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the Engage window first: play an Engage action or Reaction, or pass.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        return true;
    }
    private bool HandleRegisteredEncircledOrder(string type, GameCommand command)
    {
        if (type != "encircledOrder") return false;
        ResolveEncircledOrder(command);
        return true;
    }
    private bool HandleRegisteredEncircledSelect(string type, GameCommand command)
    {
        if (type != "encircledSelect") return false;
        ResolveEncircledSelect(command);
        return true;
    }
    private bool HandleRegisteredBrashDraw(string type, GameCommand command)
    {
        if (type != "brashDraw") return false;
        ResolveBrashDraw(command);
        return true;
    }
    private bool HandleRegisteredOverconfidentDraw(string type, GameCommand command)
    {
        if (type != "overconfidentDraw") return false;
        ResolveOverconfidentDraw(command);
        return true;
    }
    private bool HandleRegisteredHarshChoices(string type, GameCommand command)
    {
        if (type != "harshChoices") return false;
        ResolveHarshChoices(command);
        return true;
    }
    private bool HandleRegisteredForcedDiscard(string type, GameCommand command)
    {
        if (type != "forcedDiscard") return false;
        ResolveForcedDiscard(command);
        return true;
    }
    private bool HandleRegisteredProclaim(string type, GameCommand command)
    {
        if (type != "proclaim") return false;
        // "useAbility" here resolves the entering card's own entering-play Reaction (if it
        // has one) -- ValidateAbility's "Reaction" case gates it to this exact window/card.
        // The window stays open afterward (UseAbility's own NextAction flips priority without
        // clearing PendingChoice), so Proclaim can still follow, in either order, until passed.
        Require(command.Type is "proclaim" or "useAbility" or "declineReaction" or "pass", "Resolve the Proclaim reaction window first.");
        if (command.Type == "proclaim") Proclaim(command);
        else if (command.Type == "useAbility") { ApplyPendingEntryEffects(); UseAbility(command); }
        else PassReaction();
        return true;
    }
    private bool HandleRegisteredBowReaction(string type, GameCommand command)
    {
        if (type != "bowReaction") return false;
        // Togashi Hizumi / Tamori Ruya (issue #216): same shape as entryReaction below --
        // this specific watching card's own reaction, or pass.
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the bow reaction window first.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        return true;
    }
    private bool HandleRegisteredMoveReaction(string type, GameCommand command)
    {
        if (type != "moveReaction") return false;
        // Legion of the Bat (issue #279): same shape as bowReaction above -- this specific
        // watching card's own reaction, or pass.
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the move reaction window first.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        return true;
    }
    private bool HandleRegisteredNikutaiReaction(string type, GameCommand command)
    {
        if (type != "nikutaiReaction") return false;
        // Nikutai (issue #279): same shape as bowReaction/moveReaction above -- this specific
        // watching card's own reaction, or pass. "Even if this card is bowed" (ValidateAbility's
        // own bowed-source exemption list) is what lets it actually use the reaction here.
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the Nikutai reaction window first.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        return true;
    }
    private bool HandleRegisteredEntryReaction(string type, GameCommand command)
    {
        if (type != "entryReaction") return false;
        // Same window as "proclaim" above, but for a card that has its own entering-play
        // Reaction and was not itself Proclaim-eligible (e.g. bought with Clan Discount).
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the entry reaction window first.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        FinishPendingAttachTracking(); // Hida Takeuchi (issue #477 batch 2): the Equip whose attached Follower opened this window is now finished.
        ContinueRegret(); // Regret (RegretEmbracingVirtue.cs): the next card begins once this one's entry window has closed.
        return true;
    }
    private bool HandleRegisteredAttackReaction(string type, GameCommand command)
    {
        if (type != "attackReaction") return false;
        // Open to either side (unlike the entry-reaction windows above) -- the top-level
        // priority check already enforces whose turn it is to act or pass.
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the pending attack first.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        if (_state.PendingChoice is null && _state.PendingAttack is { } closing)
        {
            if (closing.Command is not null && OpenForcedDiscardForTargetedTrait(closing.Command, "attack")) return true;
            ResolveAttack();
        }
        return true;
    }
    private bool HandleRegisteredDuelReaction(string type, GameCommand command)
    {
        if (type != "duelReaction") return false;
        // Dueling (issue #204): "a reaction to being challenged or entering a duel," offered
        // after the challenge is accepted but before focusing starts -- open to either side,
        // same shape as attackReaction/additionalActionReaction above.
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the duel reaction window first.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        if (_state.PendingChoice is null && _state.PendingDuel is { Stage: "Reaction" }) BeginFocusing();
        // Bend like a Reed (Emperor Edition remaining-cards pass, group A, issue #492): the window before the Focus Effects resolve closed.
        else if (_state.PendingChoice is null && _state.PendingDuel is { Stage: "FocusEffectsReaction" }) ContinueToOrderingEffects();
        return true;
    }
    private bool HandleRegisteredAfterResolutionReaction(string type, GameCommand command)
    {
        if (type != "afterResolutionReaction") return false;
        // After a battle's resolution (issue #417): either player may play a Reaction that names it, or pass; two consecutive passes close the window (PassReaction).
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Play a Reaction to the battle's resolution or pass.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        return true;
    }
    private bool HandleRegisteredAfterActionResolvedReaction(string type, GameCommand command)
    {
        if (type != "afterActionResolvedReaction") return false;
        // After an action's resolution (issue #437): either player may play a Reaction that names it, or pass; two consecutive passes close the window (PassReaction),
        // the same shape as afterResolutionReaction above -- there is nothing special to do when it closes (no special-cased branch in PassReaction), priority just
        // returns to whoever this window's own ActingPlayerId (FinishActionEffectTracking's own closesTo) named.
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Play a Reaction to the action's resolution or pass.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        return true;
    }
    private bool HandleRegisteredBattleRecruit(string type, GameCommand command)
    {
        if (type != "battleRecruit") return false;
        // After a battle resolved (issue #379): the players holding a Ring's recruit grant of this battle may put it into play (a
        // Battle-timed useAbility, spending the grant) or pass; two consecutive passes close the window (PassReaction).
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Put a Ring into play or pass.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        return true;
    }
    private bool HandleRegisteredDuelWonReaction(string type, GameCommand command)
    {
        if (type != "duelWonReaction") return false;
        // A duel is a single, complete action from challenge to consequences (issue #209):
        // once it has fully resolved, the winning card's own controller may react to having
        // won it -- the same shape as entryReaction, closing on two consecutive passes.
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the duel-won reaction window first.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        return true;
    }
    private bool HandleRegisteredBattleActionReaction(string type, GameCommand command)
    {
        if (type != "battleActionReaction") return false;
        // Stormless Fury (issue #276 follow-up): scoped to the one specific offered card by
        // id, same single-watcher shape as bowReaction/beforeActionReaction above -- only that
        // card's own controller may use it, or either side may pass to close the window.
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Resolve the battle-action reaction window first.");
        if (command.Type == "useAbility") UseAbility(command); else PassReaction();
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPendingCoreHooksHooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredShinden", 1000, static (engine, type, command) => engine.HandleRegisteredShinden(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredPreservingHonor", 1100, static (engine, type, command) => engine.HandleRegisteredPreservingHonor(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredRikateDetach", 1200, static (engine, type, command) => engine.HandleRegisteredRikateDetach(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredRinShame", 1300, static (engine, type, command) => engine.HandleRegisteredRinShame(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredYourClanNeedsYouProvince", 1400, static (engine, type, command) => engine.HandleRegisteredYourClanNeedsYouProvince(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredDeclineStraighten", 1500, static (engine, type, command) => engine.HandleRegisteredDeclineStraighten(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredConqueror", 2400, static (engine, type, command) => engine.HandleRegisteredConqueror(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredBeforeBattleOrder", 2500, static (engine, type, command) => engine.HandleRegisteredBeforeBattleOrder(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredBeforeBattleOptional", 2600, static (engine, type, command) => engine.HandleRegisteredBeforeBattleOptional(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredEngageReaction", 2800, static (engine, type, command) => engine.HandleRegisteredEngageReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredEncircledOrder", 2900, static (engine, type, command) => engine.HandleRegisteredEncircledOrder(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredEncircledSelect", 3000, static (engine, type, command) => engine.HandleRegisteredEncircledSelect(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredBrashDraw", 3300, static (engine, type, command) => engine.HandleRegisteredBrashDraw(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredOverconfidentDraw", 3400, static (engine, type, command) => engine.HandleRegisteredOverconfidentDraw(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredHarshChoices", 3500, static (engine, type, command) => engine.HandleRegisteredHarshChoices(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredForcedDiscard", 4000, static (engine, type, command) => engine.HandleRegisteredForcedDiscard(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredProclaim", 4300, static (engine, type, command) => engine.HandleRegisteredProclaim(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredBowReaction", 4400, static (engine, type, command) => engine.HandleRegisteredBowReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredMoveReaction", 4500, static (engine, type, command) => engine.HandleRegisteredMoveReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredNikutaiReaction", 4600, static (engine, type, command) => engine.HandleRegisteredNikutaiReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredEntryReaction", 4700, static (engine, type, command) => engine.HandleRegisteredEntryReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredAttackReaction", 4800, static (engine, type, command) => engine.HandleRegisteredAttackReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredDuelReaction", 5300, static (engine, type, command) => engine.HandleRegisteredDuelReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredAfterResolutionReaction", 5400, static (engine, type, command) => engine.HandleRegisteredAfterResolutionReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredAfterActionResolvedReaction", 5600, static (engine, type, command) => engine.HandleRegisteredAfterActionResolvedReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredBattleRecruit", 5700, static (engine, type, command) => engine.HandleRegisteredBattleRecruit(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredDuelWonReaction", 5800, static (engine, type, command) => engine.HandleRegisteredDuelWonReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredBattleActionReaction", 5900, static (engine, type, command) => engine.HandleRegisteredBattleActionReaction(type, command));
    }
}
