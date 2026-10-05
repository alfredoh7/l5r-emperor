namespace L5R.Core;

// ======================================================================================================================================================================================
// PROMOTIONAL-CELESTIAL, GROUP A: personality-wave-9101 (Jahan, Kamalakar, Masatane, Miya Masatsuko, Tashiki, Yotsu Ueda). Every text is the scan's own. Readings:
//
//   * JAHAN: a passive trait. While he is at the current battlefield, every other player's Terrain play is refused: the generic playStrategy Terrain rule, and a Terrain played through its
//     own ability from the hand (Unstable Ground and the like). "Other players" is every player but Jahan's controller; "play" is read as including both entries. A Terrain already in play is
//     untouched, and so is a Terrain its controller plays while Jahan is elsewhere.
//   * KAMALAKAR: the trait is Oriole Imperial Vanguard's shape (IvoryPolitical.cs): a mandatory trait fired as a Political action that targets him is announced, whoever takes it (it prints no
//     "another player's"), negating the action whole, once each TURN where Oriole's is once each phase. Protection stops the negation but the turn's use is still spent. His Battle action is two
//     abilities sharing no limit: home, or a battlefield other than the current one that has an enemy unit (MoveSpec.AnyUnresolved, filtered here).
//   * MASATANE / YOTSU UEDA: plain destroys (Michitsu's and Longtooth's shapes). Masatane's action is an Iaijutsu one; Ueda's Force cap reads the target's current Force.
//   * MIYA MASATSUKO: the scan prints "Political Limited: Discard the Imperial Favor: Draw an additional card during your next End Phase." -- a Political action (no Favor keyword), the Favor
//     discard declared as a cost. The draw is the End Phase counter Clearing Crew uses (QueueExtraEndPhaseDraws); a Limited is only taken on its controller's own turn, so "your next End Phase"
//     is that turn's.
//   * TASHIKI: Second City Port's straighten, paid by bowing Tashiki himself, on a Holding with the Market or Port keyword.
// ======================================================================================================================================================================================

public static class PromoCelestialAAbilities
{
    public const string KamalakarMoveHome = "kamalakar-move-home", KamalakarMoveBattlefield = "kamalakar-move-battlefield", MasataneDestroy = "masatane-destroy";
    public const string MasatsukoDraw = "masatsuko-draw", TashikiStraighten = "tashiki-straighten", UedaDestroy = "ueda-destroy";
    public const string UedaTarget = "enemy-card-without-followers-force-lte-4";
    public const string JahanEffect = "promo-jahan-no-terrain", KamalakarEffect = "promo-kamalakar-political-shield";

    public static Dictionary<string, AbilityDefinition> AddPromoCelestialA(this Dictionary<string, AbilityDefinition> all)
    {
        // Kamalakar (oracle-l5r-4253): "Battle: Kamalakar confounds the enemy. Move Kamalakar home or to a battlefield with enemy units." (Hawado's two halves; "confounds the enemy" is flavour.)
        all[KamalakarMoveHome] = new("Battle", "Move Kamalakar home", "none", Move: new(MoveSpec.Source, MoveSpec.Home));
        all[KamalakarMoveBattlefield] = new("Battle", "Move Kamalakar to a battlefield with enemy units", "none", Move: new(MoveSpec.Source, MoveSpec.AnyUnresolved));
        // Masatane (oracle-l5r-4852): "Iaijutsu Battle: Target an enemy attachment: Destroy it."
        all[MasataneDestroy] = new("Battle", "Iaijutsu Battle: Target an enemy attachment: Destroy it", "enemy-attachment", ActionKeywords: ["Iaijutsu"]);
        // Miya Masatsuko (oracle-l5r-5198): "Political Limited: Discard the Imperial Favor: Draw an additional card during your next End Phase."
        all[MasatsukoDraw] = new("Limited", "Political Limited: Discard the Imperial Favor: Draw an additional card during your next End Phase", "none", ActionKeywords: ["Political"], Costs: [new(CostVerbs.DiscardFavor)]);
        // Tashiki (oracle-l5r-7822): "Open: Bow Tashiki and target a Market or Port Holding: Straighten it." (Second City Port's target category.)
        all[TashikiStraighten] = new("Open", "Open: Bow Tashiki and target a Market or Port Holding: Straighten it", "market-or-port-holding", Costs: ActionCost.BowSource);
        // Yotsu Ueda (oracle-l5r-9680): "Battle: Target an enemy Follower or Personality without Followers with Force equal to or lower than 4: Destroy it."
        all[UedaDestroy] = new("Battle", "Battle: Target an enemy Follower or Personality without Followers with Force equal to or lower than 4: Destroy it", UedaTarget);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private const string KamalakarTurnKey = "promo-kamalakar-political-negate";

    // ---- Jahan ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>True when another player's Jahan is at the current battlefield, so `playerId` may not play a Terrain.</summary>
    private bool JahanBarsTerrain(int playerId) =>
        _state.CurrentBattleProvince is not null
        && _state.Cards.Values.Any(c => c.OwnerId != playerId && InPlay(c) && Definition(c).Effect == PromoCelestialAAbilities.JahanEffect && Definition(c).Type == CardType.Personality && AtBattle(c));

    private const string JahanReason = "Jahan is at the current battlefield: other players may not play Terrains.";

    /// <summary>PlayStrategy's term (before payment): the generic Terrain rule.</summary>
    private void RequireJahanAllowsTerrain(int playerId) => Require(!JahanBarsTerrain(playerId), JahanReason);

    /// <summary>The action list's term for a Terrain in hand: why it is not offered, or null.</summary>
    private string? JahanTerrainReason(int playerId) => JahanBarsTerrain(playerId) ? JahanReason : null;

    private void ValidatePromoCelestialA(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        // A Terrain played through its own ability from the hand is a Terrain play too.
        if (source is { Zone: Zone.Hand } terrain && Keyword(terrain, "Terrain") && TerrainPlaysThroughOwnAbility(terrain)) RequireJahanAllowsTerrain(player.Id);
        if (id == PromoCelestialAAbilities.KamalakarMoveBattlefield)
            Require(command.Province is int province && source is not null && Army(1 - source.OwnerId, province).Any(), "Choose a battlefield with enemy units.");
    }

    // ---- Kamalakar's trait ----------------------------------------------------------------------------------------------------------------------------------------------------------

    // Fired as the action is announced (CardAbilities.cs, beside Oriole Imperial Vanguard): the first Political action each turn that targets an in-play Kamalakar is negated whole
    // (taken, its costs paid: ResolveNegatedAction), unless Protection stops the negation. True when the action was negated and is over.
    private bool KamalakarPoliticalNegates(GameCommand command)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || !IsAction(ability)) return false;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        if (!ActionCarriesKeyword(ability, source, "Political")) return false;
        foreach (var targeted in ActionTargetIds(command).Where(id => _state.Cards.ContainsKey(id)).Select(Card)
                     .Where(c => InPlay(c) && Definition(c).Type == CardType.Personality && Definition(c).Effect == PromoCelestialAAbilities.KamalakarEffect).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            if (targeted.AbilityUses.TryGetValue(KamalakarTurnKey, out var turn) && turn == _state.TurnNumber) continue;
            targeted.AbilityUses[KamalakarTurnKey] = _state.TurnNumber;
            if (ActionProtected(command))
            {
                Emit("actionProtected", $"Protection negates the negation -- {Definition(targeted).Name} cannot negate this action.", targeted.OwnerId, targeted.Id);
                return false;
            }
            Emit("actionNegated", $"{Definition(targeted).Name} negates {Player(command.PlayerId).Name}'s action ({ability.Label}) before it resolves.", targeted.OwnerId, targeted.Id);
            ResolveNegatedAction(command);
            return true;
        }
        return false;
    }

    // ---- targets and effects ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool PromoCelestialATargetAllows(string category, PlayerState player, CardInstance target) => category == PromoCelestialAAbilities.UedaTarget
        && target.OwnerId != player.Id
        && (Definition(target).Type == CardType.Follower || Definition(target).Type == CardType.Personality && !Attachments(target.Id).Any(a => Definition(a).Type == CardType.Follower))
        && CardForce(target) <= 4;

    private static bool PromoCelestialADestroysTarget(string id) => id is PromoCelestialAAbilities.MasataneDestroy or PromoCelestialAAbilities.UedaDestroy;

    private bool? RunPromoCelestialA(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case PromoCelestialAAbilities.MasataneDestroy:
            case PromoCelestialAAbilities.UedaDestroy:
                DestroyTargetCard(target!, player.Id);
                return false;
            case PromoCelestialAAbilities.TashikiStraighten:
                Straighten(target!);
                return false;
            case PromoCelestialAAbilities.MasatsukoDraw:
                QueueExtraEndPhaseDraws(player, 1, Definition(source!).Name);
                return false;
            case PromoCelestialAAbilities.KamalakarMoveHome:
            case PromoCelestialAAbilities.KamalakarMoveBattlefield:
                ExecuteMove(command, ability, source, null);
                return false;
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterPromoCelestialAHooks()
    {
        EngineHooks.Abilities.Register("AddPromoCelestialA", 12100, static abilities => abilities.AddPromoCelestialA());
        EngineHooks.TargetAllows.Register("PromoCelestialATargetAllows", 12100, static (engine, context) => engine.PromoCelestialATargetAllows(context.ability.Target, context.player, context.target));
        EngineHooks.DestroysTarget.Register("PromoCelestialADestroysTarget", 12100, static context => PromoCelestialADestroysTarget(context.abilityId));
        EngineHooks.ValidateEarly.Register("ValidatePromoCelestialA", 12100, static (engine, context) => engine.ValidatePromoCelestialA(context.id, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunPromoCelestialA", 12100, static (engine, context) => engine.RunPromoCelestialA(context.id, context.ability, context.command, context.player, context.source, context.target));
    }
}
