using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// P13 (issue #717): PHASE AND BATTLE RULE FLAGS -- a small store of rules a card's action or Event sets for a scope, each read once at the choke point it changes.
//
// A flag is a record in MatchState.RuleFlags: a kind, a scope (player | battlefield | battle | turn | game), the player it belongs to, the battlefield it is about, an amount or an argument, and
// how long it lasts. A card's feature file sets one from its ability's RunEffects with one call:
//
//     engine.SetRuleFlag(RuleFlagKinds.NoEngageForAttacker, RuleFlagScope.Battlefield, ownerId, "Lantern Oil", battlefield: province, whileInPlay: card.Id);
//
// KINDS AND WHERE EACH IS READ (one read each; nothing else in the engine knows the store exists):
//   end-action-phase                  EndActionPhase: applied the moment it is set, through BeginPhaseAfterAction (PhaseOrder.cs), so it honours the turn's phase order. The action that ended it does
//                                     not hand priority on (NextAction asks ActionPhaseJustEnded). Nothing is stored. Chagatai's Wrath.
//   no-engage-for-attacker            the Engage window: the Attacker's "Engage:" / "After engaging" actions at the battlefield are not on offer (the list of abilities) and are refused (ValidateAbility).
//                                     Lantern Oil.
//   negate-battle-action-without-units ContinueUseAbility's announce chain (RuleFlagNegatesBattleAction): each Battle action a player without units at the battlefield announces is taken, its costs
//                                     paid, its effects negated (ResolveNegatedAction), unless the flag's owner controls a Cavalry unit there. Temple Grounds.
//   no-destruction-honor-for-others   the battle resolution's Honor for destroyed units (ResolutionHonor): the other player gains nothing for destroying the flag owner's cards. Shosuro Takazaki.
//   defenders-may-not-play-terrains   PlayStrategy and the Terrain listing (RuleFlagTerrainReason), and a Terrain played through its own ability (ValidateRuleFlags). Journey's End Siege.
//   limited-as-open-once-per-turn     P4's LimitedAsOpen rule (TurnBoundaryRules.cs): a Limited action on a Strategy may be taken as an Open action once per turn. Stored there, not here. The Madness Spreads.
//   no-enlightenment-victory          StartTurn's Enlightenment test (EnlightenmentBarred). The False Path.
//   madness-loss (Amount = threshold) FinishTurn (MadnessLossAtTurnEnd): a player who ends his turn with Amount or more Madness tokens on his Personalities loses. An Empire of Madness.
//
// SCOPES. Turn lasts until the turn ends; Battle until the battle ends (the battle that was current when it was set); Game until the game ends. Player and Battlefield bind the flag to a player or a
// battlefield and last until its Until (a TurnUntil: "until your next turn begins"), or as long as the card named by WhileInPlay stays in play, or -- with neither -- until the turn ends. Until and
// WhileInPlay narrow every scope further. An expired flag is ignored at every read and dropped as the next turn begins and the next flag is set.
//
// With no flag set nothing here runs: every read tests the store for null first, the store and its counters are omitted from the saved game, and each shared edit is one call that returns false/null/the
// amount it was given.
// ======================================================================================================================================================================================

public static class RuleFlagKinds
{
    public const string EndActionPhase = "end-action-phase";
    public const string NoEngageForAttacker = "no-engage-for-attacker";
    public const string NegateBattleActionWithoutUnits = "negate-battle-action-without-units";
    public const string NoDestructionHonorForOthers = "no-destruction-honor-for-others";
    public const string DefendersMayNotPlayTerrains = "defenders-may-not-play-terrains";
    public const string LimitedAsOpenOncePerTurn = "limited-as-open-once-per-turn";
    public const string NoEnlightenmentVictory = "no-enlightenment-victory";
    public const string MadnessLoss = "madness-loss";

    public static readonly string[] All =
    [
        EndActionPhase, NoEngageForAttacker, NegateBattleActionWithoutUnits, NoDestructionHonorForOthers, DefendersMayNotPlayTerrains, LimitedAsOpenOncePerTurn, NoEnlightenmentVictory, MadnessLoss
    ];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleFlagScope { Player, Battlefield, Battle, Turn, Game }

/// <summary>One stored flag. PlayerId is the flag's owner (null: no owner, or each player for a kind that reads it so).</summary>
public sealed class RuleFlag
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public RuleFlagScope Scope { get; set; } = RuleFlagScope.Turn;
    public int? PlayerId { get; set; }
    /// <summary>The battlefield (province index) the flag is about; null: every battlefield.</summary>
    public int? Battlefield { get; set; }
    /// <summary>The battle (MatchState.BattleNumber) a Battle-scope flag was set in.</summary>
    public int? Battle { get; set; }
    /// <summary>The turn it was set in.</summary>
    public int Turn { get; set; }
    public TurnUntil? Until { get; set; }
    /// <summary>A card id the flag ends with ("while this card remains in play"), or null.</summary>
    public string? WhileInPlay { get; set; }
    public int Amount { get; set; }
    public string? Arg { get; set; }
    /// <summary>The card (title or id) whose effect it is, for the log.</summary>
    public string Source { get; set; } = "";
}

public sealed partial class MatchState
{
    // 22600-22699 is reserved for P13. All three are omitted while unused, so a match that never set a flag serializes exactly as before.
    [JsonPropertyOrder(22600)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RuleFlag>? RuleFlags { get; set; }
    [JsonPropertyOrder(22601)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int RuleFlagSequence { get; set; }
    /// <summary>The state Version at which an effect ended the Action Phase in the middle of a command (read by NextAction in that same command only).</summary>
    [JsonPropertyOrder(22602)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ActionPhaseEndedAtVersion { get; set; }
}

public sealed partial class MatchEngine
{
    // ---- the store -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Sets a rule flag. <paramref name="playerId"/> is the flag's owner (the player whose card it is). Battle and Battlefield scope default the battlefield to the current battle's; Battle scope needs
    /// a battle in progress. EndActionPhase is applied at once and stored nowhere; LimitedAsOpenOncePerTurn is recorded as P4's standing rule (Arg narrows the source card's type, default "Strategy").
    /// Both return null. An illegal request throws RuleViolation and changes nothing.
    /// </summary>
    internal RuleFlag? SetRuleFlag(string kind, RuleFlagScope scope = RuleFlagScope.Turn, int? playerId = null, string source = "", int? battlefield = null, int amount = 0, string? arg = null,
        TurnUntil? until = null, string? whileInPlay = null)
    {
        if (!RuleFlagKinds.All.Contains(kind)) throw new ArgumentException($"Unknown rule flag kind: {kind}", nameof(kind));
        if (kind == RuleFlagKinds.EndActionPhase) { EndActionPhase(source); return null; }
        if (kind == RuleFlagKinds.LimitedAsOpenOncePerTurn)
        {
            AddTurnScopedRule(TurnRuleKinds.LimitedAsOpen, playerId, until ?? TurnUntil.GameEnds(), source, amount: 1, arg: arg ?? "Strategy", whileInPlay: whileInPlay);
            return null;
        }
        Require(scope != RuleFlagScope.Player || playerId is not null, "A player-scope rule flag needs a player.");
        Require(kind != RuleFlagKinds.MadnessLoss || amount > 0, "A madness-loss flag needs a threshold.");
        int? battle = null;
        if (scope == RuleFlagScope.Battle)
        {
            Require(_state.CurrentBattleProvince is not null, "A battle-scope rule flag needs a battle in progress.");
            battle = _state.BattleNumber;
        }
        if (scope is RuleFlagScope.Battle or RuleFlagScope.Battlefield)
        {
            battlefield ??= _state.CurrentBattleProvince;
            Require(battlefield is not null, "A battlefield rule flag needs a battlefield.");
        }
        PruneRuleFlags();
        var flag = new RuleFlag
        {
            Id = ++_state.RuleFlagSequence,
            Kind = kind,
            Scope = scope,
            PlayerId = playerId,
            Battlefield = battlefield,
            Battle = battle,
            Turn = _state.TurnNumber,
            Until = until,
            WhileInPlay = whileInPlay,
            Amount = amount,
            Arg = arg,
            Source = source
        };
        (_state.RuleFlags ??= []).Add(flag);
        Emit("ruleFlagSet", $"{source}: {DescribeRuleFlag(flag)}.", playerId, whileInPlay);
        return flag;
    }

    internal bool RemoveRuleFlag(int id)
    {
        var removed = _state.RuleFlags?.RemoveAll(f => f.Id == id) > 0;
        if (_state.RuleFlags is { Count: 0 }) _state.RuleFlags = null;
        return removed;
    }

    private static string DescribeRuleFlag(RuleFlag flag) => flag.Kind switch
    {
        RuleFlagKinds.NoEngageForAttacker => "the Attacker may not take Engage actions",
        RuleFlagKinds.NegateBattleActionWithoutUnits => "Battle actions taken by a player without units there are negated",
        RuleFlagKinds.NoDestructionHonorForOthers => "other players gain no Honor for destroying these cards in this battle's resolution",
        RuleFlagKinds.DefendersMayNotPlayTerrains => "defending players may not play Terrains",
        RuleFlagKinds.NoEnlightenmentVictory => "no Enlightenment victory",
        RuleFlagKinds.MadnessLoss => $"a player who ends his turn with {flag.Amount} or more Madness tokens loses",
        _ => flag.Kind
    };

    private bool RuleFlagLive(RuleFlag flag)
    {
        if (flag.Until is { } until && UntilPassed(until)) return false;
        if (flag.WhileInPlay is { } cardId && !(_state.Cards.TryGetValue(cardId, out var card) && InPlay(card))) return false;
        return flag.Scope switch
        {
            RuleFlagScope.Game => true,
            RuleFlagScope.Turn => flag.Turn == _state.TurnNumber,
            RuleFlagScope.Battle => flag.Battle == _state.BattleNumber && _state.CurrentBattleProvince is not null,
            _ => flag.Until is not null || flag.WhileInPlay is not null || flag.Turn == _state.TurnNumber // Player and Battlefield: their own duration, else the turn
        };
    }

    /// <summary>How many stored flags are live now (a test seam; the flags themselves are on the state).</summary>
    internal int LiveRuleFlagCount() => _state.RuleFlags?.Count(RuleFlagLive) ?? 0;

    internal bool RuleFlagIsLive(int id) => _state.RuleFlags?.Any(f => f.Id == id && RuleFlagLive(f)) == true;

    private IEnumerable<RuleFlag> LiveRuleFlags(string kind) =>
        _state.RuleFlags is null ? [] : _state.RuleFlags.Where(f => f.Kind == kind && RuleFlagLive(f));

    /// <summary>Drops the flags that have expired; the store goes back to null (omitted from the saved game) when none is left.</summary>
    private void PruneRuleFlags()
    {
        if (_state.RuleFlags is null) return;
        _state.RuleFlags.RemoveAll(f => !RuleFlagLive(f));
        if (_state.RuleFlags.Count == 0) _state.RuleFlags = null;
    }

    // ---- end-action-phase ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// "End the Action Phase": the next printed phase of the turn's order begins now (BeginPhaseAfterAction), even if the active player had actions left; an additional action burst ends with it.
    /// False, and nothing happens, outside the Action Phase.
    /// </summary>
    internal bool EndActionPhase(string source)
    {
        if (_state.Phase != Phase.Action) return false;
        Emit("actionPhaseEnded", $"{(string.IsNullOrEmpty(source) ? "A card" : source)}: the Action Phase ends.");
        _state.AdditionalActions.RemoveAll(Forfeitable);
        BeginPhaseAfterAction();
        _state.ActionPhaseEndedAtVersion = _state.Version;
        return true;
    }

    /// <summary>NextAction's term: the action that is finishing ended the Action Phase itself, so priority already belongs to the phase that began and must not be passed on.</summary>
    private bool ActionPhaseJustEnded() => _state.ActionPhaseEndedAtVersion == _state.Version && _state.Phase != Phase.Action;

    // ---- no-engage-for-attacker ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool IsEngageAction(AbilityDefinition ability) => ability.Timing == "Reaction" && ability.Trigger is "after-engaging" or "after-engaging-at-own-battlefield";

    /// <summary>True while the Engage window is open at a battlefield a live flag covers and <paramref name="playerId"/> is the Attacker.</summary>
    internal bool EngageActionBarred(int playerId)
    {
        if (_state.RuleFlags is null || _state.PendingEngage is not { } engage || engage.Attacker != playerId) return false;
        return LiveRuleFlags(RuleFlagKinds.NoEngageForAttacker).Any(f => f.Battlefield is null || f.Battlefield == engage.ProvinceIndex);
    }

    // ---- negate-battle-action-without-units ------------------------------------------------------------------------------------------------------------------------------------------------

    private bool ControlsCavalryUnitAt(int playerId, int battlefield) =>
        Army(playerId, battlefield).Any(c => Keyword(c, "Cavalry") || _state.Cards.Values.Any(f => f.Zone == Zone.Attached && f.AttachedTo == c.Id && Keyword(f, "Cavalry")));

    /// <summary>
    /// The announce chain's term: a Battle action by a player with no unit at the current battlefield is taken, its costs paid and its effects negated, when a live flag covers the battlefield and its
    /// owner controls no Cavalry unit there. True when the action was negated and is over.
    /// </summary>
    private bool RuleFlagNegatesBattleAction(GameCommand command)
    {
        if (_state.RuleFlags is null || _state.Phase != Phase.Battle || _state.CurrentBattleProvince is not int province) return false;
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || !IsAction(ability)) return false;
        if (ability.Timing is not ("Battle" or "BattleOrOpen") || Army(command.PlayerId, province).Any()) return false;
        var flag = LiveRuleFlags(RuleFlagKinds.NegateBattleActionWithoutUnits)
            .FirstOrDefault(f => (f.Battlefield is null || f.Battlefield == province) && !(f.PlayerId is int owner && ControlsCavalryUnitAt(owner, province)));
        if (flag is null) return false;
        if (ActionProtected(command))
        {
            Emit("actionProtected", "Protection negates the negation -- this action cannot be negated.", command.PlayerId, command.CardId);
            return false;
        }
        Emit("actionNegated", $"{flag.Source}: {Player(command.PlayerId).Name}'s action ({ability.Label}) was negated: he controls no unit here.", command.PlayerId, command.CardId);
        ResolveNegatedAction(command);
        return true;
    }

    // ---- no-destruction-honor-for-others ---------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The battle resolution's Honor for destroyed units: <paramref name="amount"/> unless a live flag of the loser (whose units were destroyed) says the winner gains nothing for them.
    /// </summary>
    private int ResolutionHonor(int winner, int loser, int amount)
    {
        if (_state.RuleFlags is null || amount == 0 || winner == loser) return amount;
        var flag = LiveRuleFlags(RuleFlagKinds.NoDestructionHonorForOthers).FirstOrDefault(f => f.PlayerId == loser);
        if (flag is null) return amount;
        Emit("ruleFlagHonorWithheld", $"{flag.Source}: {Player(winner).Name} gains no Honor for destroying {Player(loser).Name}'s cards in this resolution.", winner);
        return 0;
    }

    // ---- defenders-may-not-play-terrains -----------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Why <paramref name="playerId"/> may not play a Terrain now, or null. The defending player is the one whose turn it is not; a flag with an owner binds the owner's opponents only.</summary>
    internal string? RuleFlagTerrainReason(int playerId)
    {
        if (_state.RuleFlags is null || playerId == _state.ActivePlayerId) return null;
        var flag = LiveRuleFlags(RuleFlagKinds.DefendersMayNotPlayTerrains).FirstOrDefault(f => f.PlayerId is null || f.PlayerId != playerId);
        return flag is null ? null : $"{flag.Source}: defending players may not play Terrains.";
    }

    /// <summary>PlayStrategy's term (before payment): the generic Terrain rule, beside Jahan's.</summary>
    private void RequireRuleFlagAllowsTerrain(int playerId) => Require(RuleFlagTerrainReason(playerId) is null, RuleFlagTerrainReason(playerId) ?? "");

    private void ValidateRuleFlags(CardInstance? source, PlayerState player)
    {
        // A Terrain played through its own ability from the hand is a Terrain play too.
        if (_state.RuleFlags is not null && source is { Zone: Zone.Hand } terrain && Keyword(terrain, "Terrain") && TerrainPlaysThroughOwnAbility(terrain)) RequireRuleFlagAllowsTerrain(player.Id);
    }

    // ---- no-enlightenment-victory ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>StartTurn's Enlightenment test: true when a live flag says <paramref name="playerId"/> may not win by Enlightenment.</summary>
    internal bool EnlightenmentBarred(int playerId) =>
        _state.RuleFlags is not null && LiveRuleFlags(RuleFlagKinds.NoEnlightenmentVictory).Any(f => f.PlayerId is null || f.PlayerId == playerId) || Wave733EnlightenmentBarred(playerId); // issue #733: The False Path

    // ---- madness-loss ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The Madness tokens on the Personalities in play that <paramref name="playerId"/> owns.</summary>
    internal int MadnessTokensOn(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality).Sum(c => TokenCount(c, "Madness"));

    /// <summary>FinishTurn's term: the player whose turn ends loses when a live flag's threshold of Madness tokens is on his Personalities. True when the game is over.</summary>
    private bool MadnessLossAtTurnEnd()
    {
        if (_state.RuleFlags is null || _state.WinnerId is not null) return false;
        var player = Player(_state.ActivePlayerId);
        var flag = LiveRuleFlags(RuleFlagKinds.MadnessLoss).FirstOrDefault(f => (f.PlayerId is null || f.PlayerId == player.Id) && MadnessTokensOn(player.Id) >= f.Amount);
        if (flag is null) return false;
        Emit("madnessLoss", $"{flag.Source}: {player.Name} ends his turn with {MadnessTokensOn(player.Id)} Madness tokens.", player.Id);
        Win(1 - player.Id, $"{player.Name} ended a turn with {flag.Amount} or more Madness tokens");
        return true;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterRuleFlagHooks()
    {
        EngineHooks.ValidateLate.Register("ValidateRuleFlags", 13300, static (engine, context) => engine.ValidateRuleFlags(context.source, context.player));
        EngineHooks.TurnBegins.Register("PruneRuleFlags", 13300, static (engine, _) => { engine.PruneRuleFlags(); return false; });
    }
}
