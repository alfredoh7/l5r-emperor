using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #766: Foothold of the Mad (oracle-l5r-10562, Coils of Madness; profile stronghold-wave-48, tools/content/package_stronghold_wave_48.py). Mendon's ruling (2026-10-08) is in
// docs/decisions/2026-10-08-foothold-of-the-mad.md. The card:
//
//   "If there is no other Foothold of the Mad player, you go first. You do not lose Honor from cards you own. Fallen Personalities join you for 2 less Gold. Personalities and Followers have -1F for each of
//    their Madness tokens."  /  "Limited, bow: Give a target card a Madness token."  /  "Limited, bow: Your Holdings enter play paying 2 less Gold (this turn)."
//
//   Going first          Ruling: a Foothold player counts as starting Honor 39 when the first player is chosen; then, before the first turn, every Foothold player's Honor is set to 0. Both happen at
//                        setup (FirstPlayerAtSetup, ApplyFootholdStartHonor), are not an action or a Reaction, and emit nothing, so no client shows them.
//   No Honor loss        HonorLossNegated: any loss from a card the Foothold player owns.
//   Fallen discount      EntryCostTerms (P2, CostTerms.cs): -2 for a Fallen Personality recruited by a Foothold player.
//   Madness tokens       Ruling: while a Foothold of the Mad is a Stronghold in play (either player's), a Madness token placed on a Personality or Follower IS a -1F token (CreateToken), so it aggregates in
//                        StatModifier like any stat token and a Force read costs nothing more. It is created after the Force-penalty negations are asked, because the penalty is a rule of the Stronghold, not
//                        an effect of the action that gave the token (Tireless Efforts, Creating Order and Aka-beko negate the effect, never the token itself).
//   The two Limited      foothold-madness / foothold-holdings; the second leaves a mark for the turn (MatchState.FootholdHoldingDiscounts) that the same cost term reads for Holdings.
public static class FootholdAbilities
{
    public const string Effect = "foothold-of-the-mad", MadnessAbility = "foothold-madness", HoldingsAbility = "foothold-holdings";

    /// <summary>The Honor a Foothold of the Mad player counts as starting with when the first player is chosen (Mendon's ruling).</summary>
    public const int GoingFirstHonor = 39;

    public static Dictionary<string, AbilityDefinition> AddFoothold(this Dictionary<string, AbilityDefinition> all)
    {
        all[MadnessAbility] = new("Limited", "Limited, bow this card: Give a target card a Madness token", "any-card", Costs: ActionCost.BowSource);
        all[HoldingsAbility] = new("Limited", "Limited, bow this card: Your Holdings enter play paying 2 less Gold (this turn)", "none", Costs: ActionCost.BowSource);
        return all;
    }
}

public sealed partial class MatchState
{
    /// <summary>Foothold of the Mad's second Limited ability: the turn number of each player's "Holdings enter play paying 2 less Gold (this turn)" (player id to turn). Omitted until one is used.</summary>
    [JsonPropertyOrder(23100)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, int>? FootholdHoldingDiscounts { get; set; }
}

public sealed partial class MatchEngine
{
    private bool IsFootholdPlayer(int playerId) => Definition(Card(Player(playerId).StrongholdId)).Effect == FootholdAbilities.Effect;

    /// <summary>Whether any player's Stronghold is Foothold of the Mad (the Madness-token rule is global).</summary>
    private bool FootholdInPlay() => _state.Players.Any(p => p.StrongholdId is { Length: > 0 } && IsFootholdPlayer(p.Id));

    // ---- setup (static: the engine does not exist yet) --------------------------------------------------------------------------------------------------------------------------------------

    private static bool IsFootholdStronghold(MatchState state, PlayerState player) =>
        state.Cards.TryGetValue(player.StrongholdId, out var card) && CardCatalog.Get(card.DefinitionId).Effect == FootholdAbilities.Effect;

    /// <summary>The player who goes first: the highest starting Honor, a Foothold of the Mad player counting as 39. An exact tie is the caller's (tieBreaker is the player id taken).</summary>
    internal static int FirstPlayerAtSetup(MatchState state, int tieBreaker)
    {
        int Honor(PlayerState p) => IsFootholdStronghold(state, p) ? FootholdAbilities.GoingFirstHonor : p.Honor;
        var (a, b) = (Honor(state.Players[0]), Honor(state.Players[1]));
        return a == b ? tieBreaker : a > b ? 0 : 1;
    }

    /// <summary>After the start player is chosen and before the first turn: a Foothold of the Mad player's Honor is 0. Automatic and silent (no event, so no client shows it).</summary>
    internal static void ApplyFootholdStartHonor(MatchState state)
    {
        foreach (var player in state.Players)
            if (IsFootholdStronghold(state, player)) player.Honor = 0;
    }

    // ---- passive terms ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"You do not lose Honor from cards you own": the caller has already checked the source is the losing player's own card.</summary>
    private static bool FootholdNegatesLoss(string strongholdEffect) => strongholdEffect == FootholdAbilities.Effect;

    private IEnumerable<CostTerm> FootholdEntryCost(EntryCostContext context)
    {
        if (context.Kind is not (EntryCostKind.Recruit or EntryCostKind.Reserve) || !IsFootholdPlayer(context.PayerId)) yield break;
        var type = Definition(context.Card).Type;
        if (type == CardType.Personality && Keyword(context.Card, "Fallen")) yield return new(-2, "Foothold of the Mad: Fallen Personalities join you for 2 less Gold");
        if (type == CardType.Holding && _state.FootholdHoldingDiscounts is { } marks && marks.TryGetValue(context.PayerId, out var turn) && turn == _state.TurnNumber)
            yield return new(-2, "Foothold of the Mad: your Holdings enter play paying 2 less Gold");
    }

    internal void MadnessForTest(string cardId) => CreateToken(Card(cardId), "Madness", "test");

    private bool? RunFoothold(RunHookContext c)
    {
        switch (c.id)
        {
            case FootholdAbilities.MadnessAbility:
                CreateToken(c.target!, "Madness", c.id);
                return false;
            case FootholdAbilities.HoldingsAbility:
                var marks = _state.FootholdHoldingDiscounts ??= [];
                foreach (var stale in marks.Where(m => m.Value != _state.TurnNumber).Select(m => m.Key).ToArray()) marks.Remove(stale);
                marks[c.player.Id] = _state.TurnNumber;
                return false;
            default:
                return null;
        }
    }

    [System.Runtime.CompilerServices.ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    internal static void RegisterFootholdHooks()
    {
        EngineHooks.Abilities.Register("FootholdAbilities", 15500, static abilities => abilities.AddFoothold());
        EngineHooks.EntryCostTerms.Register("FootholdEntry", 15500, static context => context.Engine.FootholdEntryCost(context));
        EngineHooks.RunEffects.Register("FootholdRunEffects", 15500, static (engine, context) => engine.RunFoothold(context));
    }
}
