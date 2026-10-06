using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// FAVOR CLUSTER (issue #667, item-wave-45): Mempo of the Amethyst Champion (oracle-l5r-9971), which alters three rulebook Favor actions, plus the "after you discard the Imperial Favor" window it needs.
//
//   ATTACHES FREE      "Attaches to an Amethyst Champion ignoring costs": AttachRequiresKeyword "Amethyst Champion" and a zero Gold price (PromoItemsAttachesFree, PromoEmperorItems.cs, asks FavorMempoAttachesFree).
//   ONE MORE USE       "You may use Favor abilities one additional time per turn": a registered ExtraUsesPerTurn hook (EngineHooks.ExtraUsesPerTurn, an additive channel read by AbilityLimits.EffectiveUsesPerTurn,
//                      zero when unhandled). A Favor ability is one that prints the Favor keyword (AbilityDefinition.ActionKeywords) or one of the four rulebook Favor actions (they print none: Shunya's Honor,
//                      the card-cycling action, Kurohito's version of it, and the Battle action). The extra use is the controller's of an attached Mempo and goes to every Favor ability he uses, whether
//                      the ability is his player's or one on a card he owns.
//   ANY UNIT           "After you announce the rulebook Favor Political Battle action: It may target any player's unit, and even if it is not attacking": read as a wider target category at announcement
//                      (the rulebook action's "enemy-attacking" category also admits any Personality at the current battlefield for the controller of an attached Mempo). Nobody else's action changes.
//   AFTER THE DISCARD  "Reaction: After you discard the Imperial Favor: Take the Imperial Favor". The Favor is discarded as a cost in several places; every one now goes through ReleaseImperialFavor,
//                      which records the player on the action. The window is the existing after-action window (afterActionResolvedReaction): opened by FinishActionEffectTracking for an action that
//                      resolved, and by CompleteNegatedAction for one that was negated (Confusion at Court: the discard is a cost and happened anyway). Optional like every Reaction.
// ======================================================================================================================================================================================

public static class FavorMempoAbilities
{
    public const string Mempo = "oracle-l5r-9971";
    public const string MempoEffect = "pe-mempo-amethyst";
    public const string TakeFavor = "pe-mempo-take-favor";

    /// <summary>The rulebook Favor actions print no Favor keyword on their ability, so they are named here.</summary>
    public static readonly string[] RulebookFavorActions = ["shunya-honor", "favor-cycle", "favor-cycle-kurohito", "favor-battle"];

    /// <summary>Mendon: "only sending a personality home from battle (favor-battle) or discarding a card to draw a card (favor-cycle) trigger its ability": the only actions Mempo's Reaction answers (Kurohito's is the card-cycling action).</summary>
    public static readonly string[] TakeBackTriggers = ["favor-battle", "favor-cycle", "favor-cycle-kurohito"];

    public static Dictionary<string, AbilityDefinition> AddFavorMempo(this Dictionary<string, AbilityDefinition> all)
    {
        // Mempo of the Amethyst Champion: "Reaction: After you discard the Imperial Favor: Take the Imperial Favor."
        all[TakeFavor] = new("Reaction", "After you discard the Imperial Favor: Take the Imperial Favor", "none", Trigger: AfterActionReactionTriggers.AfterActionResolved);
        return all;
    }
}

public sealed partial class MatchState
{
    /// <summary>The players who discarded the Imperial Favor as a cost during the action now resolving; snapshotted into ResolvedActionFacts.FavorDiscarders and cleared once the action is done.</summary>
    [JsonPropertyOrder(4667)]
    public List<int> CurrentActionFavorDiscarders { get; set; } = [];
}

public sealed partial class MatchEngine
{
    internal readonly record struct ExtraUsesContext(Dictionary<string, int> uses, AbilityDefinition ability);

    /// <summary>The additive "extra uses per turn" channel: every registered callback's value is summed (zero when unhandled).</summary>
    private int RegisteredExtraUsesPerTurn(Dictionary<string, int> uses, AbilityDefinition ability)
    {
        var context = new ExtraUsesContext(uses, ability);
        var total = 0;
        foreach (var hook in EngineHooks.ExtraUsesPerTurn.Items)
            total += hook(this, context);
        return total;
    }

    /// <summary>Synthetic AbilityId of the record a negated action leaves behind when it still discarded the Imperial Favor (its SourceId holds the negated action's ability id): only the Favor reactions read it.</summary>
    internal const string FavorNegatedSentinel = "__favor-negated__";

    // ---- the one place the Imperial Favor is discarded ------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The Imperial Favor stops being controlled. A discard paid as a cost (<paramref name="asCost"/>) is recorded on the action in progress for "after you discard the Imperial Favor".</summary>
    private void ReleaseImperialFavor(int playerId, bool asCost = true)
    {
        _state.FavorPlayerId = null;
        if (asCost && !_state.CurrentActionFavorDiscarders.Contains(playerId)) _state.CurrentActionFavorDiscarders.Add(playerId);
    }

    private List<int> TakeFavorDiscarders()
    {
        var discarders = _state.CurrentActionFavorDiscarders;
        _state.CurrentActionFavorDiscarders = [];
        return discarders;
    }

    // ---- Mempo -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool FavorMempoControlled(int playerId) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && c.Zone == Zone.Attached && Definition(c).Effect == FavorMempoAbilities.MempoEffect
            && c.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) && InPlay(host));

    private bool IsFavorAbility(AbilityDefinition ability)
    {
        if (ability.ActionKeywords?.Contains("Favor") == true) return true;
        foreach (var id in FavorMempoAbilities.RulebookFavorActions)
            if (AbilityCatalog.All.TryGetValue(id, out var rulebook) && ReferenceEquals(rulebook, ability)) return true;
        return false;
    }

    // Whose ability uses this dictionary is: a player's own (the rulebook actions) or one of his cards'.
    private int? OwnerOfUses(Dictionary<string, int> uses)
    {
        foreach (var player in _state.Players)
            if (ReferenceEquals(player.AbilityUses, uses)) return player.Id;
        foreach (var card in _state.Cards.Values)
            if (ReferenceEquals(card.AbilityUses, uses)) return card.OwnerId;
        return null;
    }

    // ExtraUsesPerTurn's callback: "You may use Favor abilities one additional time per turn."
    private int FavorMempoExtraUses(Dictionary<string, int> uses, AbilityDefinition ability) =>
        ability.UsesPerTurn != AbilityUseLimit.Unlimited && !ability.OncePerGame && IsFavorAbility(ability) && OwnerOfUses(uses) is { } owner && FavorMempoControlled(owner) ? 1 : 0;

    // TargetAllows' "enemy-attacking" arm: "It may target any player's unit, and even if it is not attacking" -- any Personality (at the current battlefield, which every Battle target must be).
    private bool FavorMempoAllowsAnyUnit(string abilityId, PlayerState player, CardInstance target) =>
        abilityId == "favor-battle" && Definition(target).Type == CardType.Personality && FavorMempoControlled(player.Id);

    // Test seams: the additive channel for a player's own use dictionary, and the helper every Favor discard goes through (returning what it recorded).
    internal int ExtraUsesForTest(int playerId, string abilityId) => RegisteredExtraUsesPerTurn(Player(playerId).AbilityUses, AbilityCatalog.All[abilityId]);
    internal List<int> ReleaseFavorForTest(int playerId, bool asCost) { ReleaseImperialFavor(playerId, asCost); return [.. _state.CurrentActionFavorDiscarders]; }

    private bool FavorMempoAttachesFree(CardInstance card) => Definition(card).Id == FavorMempoAbilities.Mempo;

    // ---- the window --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // AfterActionResolvedApplies' first word: the Reaction itself, and the record a negated Favor action leaves (which no other Reaction may read).
    private bool? FavorMempoAfterActionApplies(CardInstance source, string abilityId, int playerId, ResolvedActionFacts facts)
    {
        if (abilityId != FavorMempoAbilities.TakeFavor) return facts.AbilityId == FavorNegatedSentinel ? false : null;
        return source.Zone == Zone.Attached && source.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) && InPlay(host)
            && facts.FavorDiscarders.Contains(playerId) && _state.FavorPlayerId != playerId
            && (facts.AbilityId == FavorNegatedSentinel ? facts.SourceId : facts.AbilityId) is { } actionId && FavorMempoAbilities.TakeBackTriggers.Contains(actionId); // only the two rulebook actions that move a Personality home or cycle a card
    }

    // CompleteNegatedAction's step: a negated Favor action still paid its cost, so "after you discard the Imperial Favor" still happened. True when the window opened over it.
    private bool OpenFavorDiscardAfterNegation(string abilityId, int actingPlayerId, int closesTo)
    {
        var discarders = TakeFavorDiscarders();
        if (discarders.Count == 0 || !discarders.Any(FavorMempoControlled)) return false;
        var previous = _state.LastResolvedAction;
        _state.LastResolvedAction = new ResolvedActionFacts { AbilityId = FavorNegatedSentinel, SourceId = abilityId, PlayerId = actingPlayerId, FavorDiscarders = discarders };
        if (OpenAfterActionResolvedWindow(closesTo)) return true;
        _state.LastResolvedAction = previous;
        return false;
    }

    private bool? RunFavorMempo(RunHookContext context)
    {
        if (context.id != FavorMempoAbilities.TakeFavor) return null;
        var player = context.player;
        TakeImperialFavor(player.Id);
        Emit("favorTaken", $"{player.Name} took the Imperial Favor ({Definition(context.source!).Name}).", player.Id, context.source!.Id);
        return false;
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterFavorMempoHooks()
    {
        EngineHooks.Abilities.Register("AddFavorMempo", 12667, static abilities => abilities.AddFavorMempo());
        EngineHooks.RunEffects.Register("RunFavorMempo", 12667, static (engine, context) => engine.RunFavorMempo(context));
        EngineHooks.ExtraUsesPerTurn.Register("FavorMempoExtraUses", 12667, static (engine, context) => engine.FavorMempoExtraUses(context.uses, context.ability));
    }
}
