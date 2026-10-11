namespace L5R.Core;

// Issue #816: Kalani's Landing (oracle-l5r-4252, Emperor Edition #408, stronghold-wave-816), the Mantis Port Stronghold imported by issue #812. Mendon's ruling (2026-10-10, docs/decisions/2026-10-10-kalanis-landing.md):
//
//   "This card will not produce Gold when paying for an attachment."     a ConditionalProduction of 0 for PayingForAttachment (Platinum Mine's condition, read the other way)
//   "After each phase begins: Straighten this card."                      a PhaseBegins trait (the new EngineHooks.PhaseBegins channel below), every phase of either player's turn
//   "Reaction: After an action is announced, target a card (in play):      w816-kalani-reduce, offered in the announce window to the Stronghold's controller after ANY player's announced action
//    Give it -2 Gold Cost."
//
// THE PRIMITIVE. "Give it -2 Gold Cost" is an ordinary GoldCost modifier (ApplyModifier, read by CardGoldCost), which a turn's end removes. What was missing was its legality and its timing:
//   * A TARGET must be a card in play (a Home, Battlefield, Attached, Region, Celestial or Stronghold card) with a current Gold Cost above zero: a card with no printed Gold Cost has a Gold Cost of 0 and cannot be
//     targeted. The cost is read AFTER modifiers (CardGoldCost), so a card Kalani's Landing already brought to 0 is no longer a target.
//   * The WINDOW is the announce window (P3/P6), opened for the controller of any card that holds a Reaction with the AnnounceAnyAction trigger, whichever player announced the action. It opens only when the Reaction can
//     actually be taken (a legal target, not spent), and the Reaction is used once per turn (AbilityDefinition.UsesPerTurn's default of 1): once used, the window is not opened for that player again until the next turn.
//     Declining it does not spend the use.
//   * "Action" means a Limited, Open or Battle ability, a Strategy played, an Equip or a Reserve recruit. A Reaction is not announced into this window: a Reaction played inside an announce window would open a window inside
//     it, and the Reaction's own use is what the once-per-turn limit counts. (Reactions are actions elsewhere in this engine; this is a reading recorded for Mendon in the decision file.)
public static class Wave816Abilities
{
    public const string KalaniReduce = "w816-kalani-reduce";
    /// <summary>The trigger of a Reaction taken in the announce window of any player's announced action.</summary>
    public const string AnnounceAnyAction = "w816-announce-any-action";
    /// <summary>Target category: a card in play with a current Gold Cost above zero.</summary>
    public const string GoldCostCardInPlay = "w816-gold-cost-card-in-play";
    public const string KalaniEffect = "w816-kalanis-landing";
    public const string Kalani = "oracle-l5r-4252";
    /// <summary>The Gold Cost Kalani's Landing takes off.</summary>
    public const int Reduction = 2;

    public static Dictionary<string, AbilityDefinition> AddWave816(this Dictionary<string, AbilityDefinition> all)
    {
        all[KalaniReduce] = new("Reaction", "Reaction: After an action is announced, target a card (in play): Give it -2 Gold Cost", GoldCostCardInPlay, Trigger: AnnounceAnyAction);
        return all;
    }
}

internal static partial class EngineHooks
{
    /// <summary>"After each phase begins": run once as each rules phase begins (the Attack Phase's segments are one phase), after the phase is set. Used for traits that straighten, draw or count as a phase starts.</summary>
    internal static readonly OrderedHooks<Action<MatchEngine, Phase>> PhaseBegins = new();
}

public sealed partial class MatchEngine
{
    private bool Wave816Has() => _state.Cards.Values.Any(c => Definition(c).Effect == Wave816Abilities.KalaniEffect);

    private IEnumerable<CardInstance> Wave816InPlay() =>
        _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Effect == Wave816Abilities.KalaniEffect).OrderBy(c => c.Id, StringComparer.Ordinal);

    // ---- target legality ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave816TargetAllows(TargetAllowsContext context) =>
        context.ability.Target == Wave816Abilities.GoldCostCardInPlay && InPlay(context.target) && CardGoldCost(context.target) > 0;

    // ---- the effect --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave816(RunHookContext c)
    {
        if (c.id != Wave816Abilities.KalaniReduce || c.target is not { } target || c.source is not { } source) return null;
        var before = CardGoldCost(target);
        ApplyModifier(target, source.Id, c.id, "GoldCost", -Wave816Abilities.Reduction);
        var after = CardGoldCost(target);
        Emit("goldCostReduced", after < before
            ? $"{Definition(source).Name}: {Definition(target).Name} has -{Wave816Abilities.Reduction} Gold Cost until the end of the turn (now {after})."
            : $"{Definition(source).Name}: the Gold Cost reduction of {Definition(target).Name} was negated.", c.player.Id, target.Id);
        return false;
    }

    // ---- the announce window -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The actions that are announced into the window: an ability that is an action other than a Reaction, a Strategy played, an Equip, a Reserve recruit.</summary>
    internal bool Wave816Announces(GameCommand command) => command.Type switch
    {
        "useAbility" => command.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing != "Reaction" && IsAction(ability),
        "playStrategy" or "equip" or "recruitReserve" => true,
        _ => false
    };

    /// <summary>The next Kalani's Landing, of either player, whose Reaction can be taken in the announce window of this command and that has not been asked for this announcement.</summary>
    private CardInstance? Wave816AnnounceWatcher(GameCommand command)
    {
        if (!Wave816Has() || !Wave816Announces(command)) return null;
        var record = AnnounceRecord(command);
        foreach (var card in Wave816InPlay().Where(c => !record.Asked.Contains(c.Id)))
        {
            if (ActionLocked(card) || !Unspent(card, Wave816Abilities.KalaniReduce) || !Wave816ReactionUsable(command, card)) continue;
            record.Asked.Add(card.Id);
            return card;
        }
        return null;
    }

    /// <summary>AnnounceReactionUsable, for a watcher whose controller is not the one announcing: the window is asked about as it will be listed, with the hand on the watcher's controller, so a window with nothing to play never opens.</summary>
    private bool Wave816ReactionUsable(GameCommand command, CardInstance watcher)
    {
        var savedPriority = _state.PriorityPlayerId;
        _state.PriorityPlayerId = watcher.OwnerId;
        try { return AnnounceReactionUsable(command, watcher); }
        finally { _state.PriorityPlayerId = savedPriority; }
    }

    /// <summary>ValidateAbility's trigger check: the announce window is open for this card's controller.</summary>
    private bool Wave816AnnounceWindowOpen(GameCommand command) =>
        _state.PendingChoice is { Type: "announceReaction" } pending && pending.CardId == command.CardId && command.PlayerId == pending.PlayerId && _state.PendingAnnouncedAction is not null;

    // ---- the Stronghold's traits ------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"This card will not produce Gold when paying for an attachment": it produces 0 for a Follower, Item or Spell being attached and paid for, and its printed 4 otherwise.</summary>
    private ConditionalProduction? Wave816ConditionalProduction(CardInstance card) =>
        Definition(card).Effect == Wave816Abilities.KalaniEffect ? new ConditionalProduction(0, ProductionCondition.PayingForAttachment) : null;

    /// <summary>"After each phase begins: Straighten this card." Both players' phases; a Stronghold that is not bowed has nothing to straighten.</summary>
    private void Wave816PhaseBegins()
    {
        foreach (var card in Wave816InPlay().Where(c => c.Bowed).ToArray()) Straighten(card);
    }

    private void FirePhaseBegins(Phase from, Phase to)
    {
        if (PhaseFamily(from) == PhaseFamily(to)) return;
        foreach (var hook in EngineHooks.PhaseBegins.Items) hook(this, to);
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave816Hooks()
    {
        EngineHooks.Abilities.Register("Wave816Abilities", 13816, static abilities => abilities.AddWave816());
        EngineHooks.TargetAllows.Register("Wave816TargetAllows", 13816, static (engine, context) => engine.Wave816TargetAllows(context));
        EngineHooks.RunEffects.Register("Wave816RunEffects", 13816, static (engine, context) => engine.RunWave816(context));
        EngineHooks.ConditionalProductions.Register("Wave816ConditionalProductions", 13816, static (engine, card) => engine.Wave816ConditionalProduction(card));
        EngineHooks.PhaseBegins.Register("Wave816PhaseBegins", 13816, static (engine, _) => engine.Wave816PhaseBegins());
    }
}
