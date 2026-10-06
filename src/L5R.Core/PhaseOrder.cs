using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// THE PHASE-ORDER MARKER (issue #667; Mendon's ruling on the cards that reorder the turn).
//
// A turn is the six printed phases (PrintedPhases): Straighten, Events, Action, Attack, Dynasty, End. They used to be hard wired -- the Action Phase's last pass started the Attack Phase, the end of
// the Attack Phase started the Dynasty Phase, the end of the Dynasty Phase ended the turn. They now follow a MARKER in the match state: MatchState.PhaseOrder.Order, the order the turn is running in.
// It is the default order unless a card changed it, and the default behaves exactly as the hard wiring did.
//
//   * The cards that reorder: The Deciding Moment ("The Dynasty Phase comes before the Attack Phase this turn") and Nocturnal Attack ("The option to create an Attack Phase comes after the Dynasty
//     Phase this turn") -- one primitive, RequestDynastyBeforeAttack. A card searching for others found no more (no Straighten/Events/Action/End reorder is printed anywhere).
//   * WHICH PHASES MAY BE SWITCHED is data, PhaseOrderRules.AllowedSwaps: only the Attack and Dynasty Phases. Any other order (a Straighten Phase that is not first, an End Phase that is not last,
//     the Action Phase after the Attack Phase, ...) is rejected and changes nothing.
//   * The marker is changed only before either phase of the swap has begun this turn (PhaseOrderState.Begun), and only for this turn: it returns to the default as the End Phase begins (and again as
//     the next Straighten Phase begins), so the following turn runs in the default order. The Attack Phase being "an option" is unchanged -- it still opens with the declare-or-decline decision.
//   * The view carries the marker (MatchView.PhaseOrder, null while the default is running) so both players' clients draw the phase header in the order the turn is actually running in.
//
// READINGS (not confirmed rulings; recorded in docs/decisions/2026-10-05-667-phase-order.md):
//   * "Five Rings in play" is the Enlightenment test for the other player: all five elemental Rings (Fire, Water, Air, Earth, Void -- no False Ring, none stripped of Enlightenment) in play under his control.
//   * Playing a second reorder the same turn (the order is already Dynasty-first) is allowed and changes nothing; neither card ever restores the default.
//   * A reorder after the Dynasty Phase has begun (Twist of Fate's additional action) or after the Attack Phase has begun is refused.
// ======================================================================================================================================================================================

/// <summary>The data of the marker: the printed phases, their default order, and which of them may be switched.</summary>
public static class PhaseOrderRules
{
    public const string Straighten = "Straighten", Events = "Events", Action = "Action", Attack = "Attack", Dynasty = "Dynasty", End = "End";

    /// <summary>The default order of a turn (the order of PrintedPhases).</summary>
    public static readonly string[] Default = [Straighten, Events, Action, Attack, Dynasty, End];

    /// <summary>The pairs of printed phases that a card may switch. Nothing else in the turn is movable.</summary>
    public static readonly (string First, string Second)[] AllowedSwaps = [(Attack, Dynasty)];

    /// <summary>Every order a turn may run in: the default with any subset of the allowed swaps applied.</summary>
    public static IEnumerable<string[]> LegalOrders()
    {
        for (var mask = 0; mask < 1 << AllowedSwaps.Length; mask++)
        {
            var order = (string[])Default.Clone();
            for (var i = 0; i < AllowedSwaps.Length; i++)
            {
                if ((mask & 1 << i) == 0) continue;
                var a = Array.IndexOf(order, AllowedSwaps[i].First); var b = Array.IndexOf(order, AllowedSwaps[i].Second);
                (order[a], order[b]) = (order[b], order[a]);
            }
            yield return order;
        }
    }

    public static bool IsLegal(IReadOnlyList<string>? order) => order is { Count: 6 } && LegalOrders().Any(legal => legal.SequenceEqual(order));

    /// <summary>The printed phase's name for an engine phase (the six of PrintedPhases); null for Finished.</summary>
    public static string? NameOf(Phase phase) => PrintedPhases.Of(phase) is var index && index < Default.Length ? Default[index] : null;
}

/// <summary>The marker kept in the match state.</summary>
public sealed class PhaseOrderState
{
    /// <summary>The order the turn is running in: the six printed phases by name. The default list unless a card changed it this turn.</summary>
    public List<string> Order { get; set; } = [.. PhaseOrderRules.Default];
    /// <summary>The printed phases that have begun this turn (cleared as the Straighten Phase begins).</summary>
    public List<string> Begun { get; set; } = [];
    /// <summary>The turn whose order was changed, -1 while the default is running.</summary>
    public int ChangedTurn { get; set; } = -1;
    [JsonIgnore] public bool IsDefault => Order.SequenceEqual(PhaseOrderRules.Default);
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(1600)]
    public PhaseOrderState PhaseOrder { get; set; } = new();
}

public static partial class PhaseOrderAbilities
{
    public const string DecidingMomentOrder = "pe-deciding-moment-order", DecidingMomentDraw = "pe-deciding-moment-draw", NocturnalAttack = "nocturnal-attack-order";

    public static Dictionary<string, AbilityDefinition> AddPhaseOrder(this Dictionary<string, AbilityDefinition> all)
    {
        // The Deciding Moment (oracle-l5r-10119): "Limited: If another player has 40 or more Honor, has five Rings in play, or you have -20 or lower Honor: The Dynasty Phase comes before the Attack Phase this turn."
        all[DecidingMomentOrder] = new("Limited", "If another player has 40 or more Honor, has five Rings in play, or you have -20 or lower Honor: The Dynasty Phase comes before the Attack Phase this turn", "none");
        // "Battle: Draw a card."
        all[DecidingMomentDraw] = new("Battle", "Draw a card", "none");
        // Nocturnal Attack (oracle-l5r-10385): "Limited: Bow your performing Ninja Personality: The option to create an Attack Phase comes after the Dynasty Phase this turn."
        all[NocturnalAttack] = new("Limited", "Bow your performing Ninja Personality: The option to create an Attack Phase comes after the Dynasty Phase this turn", "none",
            Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Ninja"], Costs: ActionCost.BowPerformer);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private PhaseOrderState PhaseOrder => _state.PhaseOrder;
    private int OrderIndex(string name) => PhaseOrder.Order.IndexOf(name);

    /// <summary>The printed phase that follows `name` in the order the turn is running in; null after the last.</summary>
    private string? PrintedPhaseAfter(string name) => OrderIndex(name) is var at && at >= 0 && at + 1 < PhaseOrder.Order.Count ? PhaseOrder.Order[at + 1] : null;
    private bool DynastyBeforeAttack() => OrderIndex(PhaseOrderRules.Dynasty) < OrderIndex(PhaseOrderRules.Attack);

    // ---- the transitions the marker drives -------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The Action Phase is over (both players passed in succession): the next printed phase of the order begins.</summary>
    private void BeginPhaseAfterAction()
    {
        if (PrintedPhaseAfter(PhaseOrderRules.Action) == PhaseOrderRules.Dynasty) SetPhase(Phase.Dynasty, _state.ActivePlayerId);
        else SetPhase(Phase.AttackDeclaration, _state.ActivePlayerId);
    }

    /// <summary>The Attack Phase is over (declined, or its last battle resolved): in the default order the Dynasty Phase begins with `priority`; with the Dynasty Phase already done the turn ends.</summary>
    private void BeginPhaseAfterAttack(int priority)
    {
        if (PrintedPhaseAfter(PhaseOrderRules.Attack) == PhaseOrderRules.Dynasty) { SetPhase(Phase.Dynasty, priority); return; }
        StraightenWaveMen(); // Wave Man: "After an Attack Phase ends" -- SetPhase does it as the Dynasty Phase begins, which is no longer the end of the Attack Phase.
        EndTurn();
    }

    /// <summary>The Dynasty Phase is over (the player ended it, and any "before it ends" window is closed): the Attack Phase begins when the order puts it next, otherwise the turn ends.</summary>
    private void EndDynastyPhase()
    {
        if (PrintedPhaseAfter(PhaseOrderRules.Dynasty) == PhaseOrderRules.Attack) SetPhase(Phase.AttackDeclaration, _state.ActivePlayerId);
        else EndTurn();
    }

    /// <summary>Wave Man (issue #279): a trait, mandatory, as an Attack Phase ends.</summary>
    private void StraightenWaveMen()
    {
        foreach (var waveMan in _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Effect == "wave-man-attack-phase-straighten").ToArray())
            StraightenBowedUnit(waveMan);
    }

    /// <summary>Called by SetPhase: the printed phase that begins is recorded, the Straighten Phase starts a clean marker and the End Phase puts the default order back.</summary>
    private void NotePhaseOrder(Phase phase)
    {
        var name = PhaseOrderRules.NameOf(phase);
        if (name is null) return;
        var marker = PhaseOrder;
        if (name == PhaseOrderRules.Straighten) { marker.Begun.Clear(); ResetPhaseOrder(); }
        if (!marker.Begun.Contains(name)) marker.Begun.Add(name);
        if (name == PhaseOrderRules.End) ResetPhaseOrder();
    }

    private void ResetPhaseOrder()
    {
        if (PhaseOrder.IsDefault && PhaseOrder.ChangedTurn < 0) return;
        PhaseOrder.Order = [.. PhaseOrderRules.Default]; PhaseOrder.ChangedTurn = -1;
        Emit("phaseOrderReset", "The turn's phases return to their usual order.");
    }

    // ---- the request -------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The refusal for a requested order, or null when it may be set: it must be a legal order, and every phase it moves must still be ahead of the turn.</summary>
    private string? PhaseOrderRefusal(IReadOnlyList<string> wanted)
    {
        if (!PhaseOrderRules.IsLegal(wanted)) return "Those phases may not be reordered: only the Attack and Dynasty Phases can be switched.";
        if (wanted.SequenceEqual(PhaseOrder.Order)) return null;
        for (var i = 0; i < wanted.Count; i++)
            if (wanted[i] != PhaseOrder.Order[i] && PhaseOrder.Begun.Contains(wanted[i])) return $"The {wanted[i]} Phase has already begun this turn.";
        return null;
    }

    /// <summary>The Deciding Moment and Nocturnal Attack: the Dynasty Phase comes before the Attack Phase this turn.</summary>
    private string? PhaseOrderDynastyFirstRefusal() => PhaseOrderRefusal(DynastyFirstOrder());
    private static string[] DynastyFirstOrder() => [PhaseOrderRules.Straighten, PhaseOrderRules.Events, PhaseOrderRules.Action, PhaseOrderRules.Dynasty, PhaseOrderRules.Attack, PhaseOrderRules.End];

    private void RequestDynastyBeforeAttack(PlayerState player, CardInstance? source)
    {
        var wanted = DynastyFirstOrder();
        Require(PhaseOrderRefusal(wanted) is null, PhaseOrderRefusal(wanted) ?? "");
        if (wanted.SequenceEqual(PhaseOrder.Order)) return; // already so: nothing to change
        PhaseOrder.Order = [.. wanted]; PhaseOrder.ChangedTurn = _state.TurnNumber;
        Emit("phaseOrderChanged", $"{(source is null ? "A card" : Definition(source).Name)}: the Dynasty Phase now comes before the Attack Phase this turn.", player.Id, source?.Id);
    }

    // ---- The Deciding Moment's condition ----------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"Another player has 40 or more Honor, has five Rings in play, or you have -20 or lower Honor." Five Rings is the Enlightenment test (see the file header).</summary>
    private bool DecidingMomentCondition(PlayerState player)
    {
        if (player.Honor <= -20) return true;
        var other = Player(1 - player.Id);
        return other.Honor >= 40 || RingElements.All(e => _state.Cards.Values.Any(c => c.OwnerId == other.Id && InPlay(c) && Definition(c).Type == CardType.Ring && Keyword(c, e) && !Keyword(c, "Fudo") && CountsForEnlightenment(c)));
    }

    private void ValidatePhaseOrder(string id, PlayerState player)
    {
        if (id == PhaseOrderAbilities.DecidingMomentOrder) Require(DecidingMomentCondition(player), "Another player needs 40 or more Honor or five Rings in play, or you -20 or lower Honor.");
        if (id is PhaseOrderAbilities.DecidingMomentOrder or PhaseOrderAbilities.NocturnalAttack) Require(PhaseOrderDynastyFirstRefusal() is null, PhaseOrderDynastyFirstRefusal() ?? "");
    }

    // null: not this feature's. false: the effects are done.
    private bool? RunPhaseOrder(string id, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case PhaseOrderAbilities.DecidingMomentOrder or PhaseOrderAbilities.NocturnalAttack: RequestDynastyBeforeAttack(player, source); return false;
            case PhaseOrderAbilities.DecidingMomentDraw: Draw(player); return false;
            default: return null;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPhaseOrderHooks()
    {
        EngineHooks.Abilities.Register("AddPhaseOrder", 12500, static abilities => abilities.AddPhaseOrder());
        EngineHooks.ValidateLate.Register("ValidatePhaseOrder", 12500, static (engine, context) => engine.ValidatePhaseOrder(context.id, context.player));
        EngineHooks.RunEffects.Register("RunPhaseOrder", 12500, static (engine, context) => engine.RunPhaseOrder(context.id, context.player, context.source));
    }
}
