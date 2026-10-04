namespace L5R.Core;

// ======================================================================================================================================================================================
// THE "MOVEMENT" STRATEGIES (issue #622, strategy-wave-106): Advance Warning (oracle-l5r-121, Before the Dawn), Ambush (344), Back to the Front (662), Fall Back! (2445), Treachery and Deceit (8741),
// Soul's Sacrifice (7385), Sneak Attack (7311), Inspired Devotion (3774), Vigilant Eyes (9156) -- all Emperor Edition -- and Determined Challenge (10143, Seeds of Decay). Each reads the printed
// text of its scan, not the later Oracle wording.
//
//   * ADVANCE WARNING: "Battle: Move your performing Scout Personality home: Straighten his unit. Raise or lower the current battlefield's province's strength by your Scout's Force." The move home is
//     before the colon, so it is a COST (Wall of Honor's CostVerbs.MoveHome): paid even if the effects are negated. "Raise or lower" is two offered actions sharing one use (Kusugi's shape). The Scout's
//     Force is read when the effect resolves, i.e. after he has gone home. The strength change lasts until the end of the turn (ruling R3), and a battlefield with no province has none to change.
//   * AMBUSH: Shinobi Assault's existing machinery (a battlefield with no province, both Personalities assigned, a battle fought from the Action Phase, Honor lost after it) with the printed
//     differences: any unbowed Personality performs, no first opportunity is claimed (the Defender's, as by default), and "Other Personalities will not move there" -- every movement of any OTHER
//     Personality to that battlefield is negated (the two assigned ones may still move there). The Honor is lost after the battle ends. READING TO CONFIRM: "(after this action's resolution)" is carried
//     out by the existing aside-battle mechanism, whose action tail (the reaction windows an action opens when it resolves) runs when the battle is over.
//   * BACK TO THE FRONT: "Choose your performing Personality: Move him to a battlefield he assigned to this turn. If he moved, straighten his unit." The record of the battlefields a Personality was
//     assigned to is IvoryMovementState (Assign and AssignToCreatedBattlefield note it); the battlefield is chosen with the action (GameCommand.Province, MoveSpec.AssignedThisTurn). It must still
//     exist: one of the Attack Phase's battlefields, fought or not, or the battlefield of the aside battle being fought. A move TO the current battlefield is an Absent Battle action (the Exception
//     to the Rules of Presence and Location); a move to any other needs the performer at the current battlefield (Presence), so it is refused from home.
//   * FALL BACK!: "Move him home. If he moved, straighten his unit, and you may straighten one of your other units (at any location)." The "may" is a decision made when the action resolves, only
//     if he moved (a decider choice); the offered units are the acting player's other units that have a bowed card.
//   * DETERMINED CHALLENGE / TREACHERY AND DECEIT / SOUL'S SACRIFICE: moves of an enemy (Treachery: any) Personality, through the generic MoveSpec path (Tamori Tamoko can negate each). Determined Challenge's
//     move to the current battlefield is a MovesToBattle action; the target is straightened (only he) if he moved. Treachery's target is a Personality "whose unit's total Force is 7 or higher"
//     (UnitTotalForce: his Force and his Followers'). Soul's Sacrifice's two keywords go to the performing Samurai whether or not the move happened (the sentences are independent).
//   * SNEAK ATTACK: "Reaction: After engaging: The Attacker has the first opportunity to take a Battle action or pass in this battle." The existing first-opportunity claim (ClaimFirstOpportunity) made
//     for the Attacker whoever plays it; a Defender claim still overrides it.
//   * INSPIRED DEVOTION: "Give him +2F or a Force bonus equal to the highest Chi of any Courtier or Shugenja you control": two offered actions sharing one use (the player picks when taking the action; the
//     Chi is read when it resolves). The Chi form needs a Courtier or Shugenja.
//   * VIGILANT EYES: "Give him a Force penalty equal to your Samurai's Force" (read when it resolves, until the end of the turn).
// ======================================================================================================================================================================================

public static class IvoryMovementAbilities
{
    public const string AdvanceWarningRaise = "advance-warning-raise", AdvanceWarningLower = "advance-warning-lower";
    public const string AmbushBattle = "ambush-battle";
    public const string BackToTheFront = "back-to-the-front-move";
    public const string FallBack = "fall-back-move";
    public const string DeterminedChallenge = "determined-challenge-move";
    public const string TreacheryDeceit = "treachery-deceit-move";
    public const string SoulsSacrifice = "souls-sacrifice-move";
    public const string SneakAttack = "sneak-attack-claim";
    public const string InspiredDevotionFixed = "inspired-devotion-bonus", InspiredDevotionChi = "inspired-devotion-chi";
    public const string VigilantEyes = "vigilant-eyes-penalty";

    /// <summary>Treachery and Deceit: "a Personality whose unit's total Force is 7 or higher".</summary>
    public const string PersonalityUnitForce7 = "ivory-personality-unit-force-7-plus";
    /// <summary>Soul's Sacrifice: "your performing unbowed Samurai with 3 or higher Personal Honor" (at the battle).</summary>
    public const string UnbowedSamuraiHonor3 = "ivory-own-unbowed-samurai-honor-3-plus-at-battle";
    /// <summary>Fall Back!'s decision: which of the player's other units to straighten.</summary>
    public const string FallBackSpec = "ivory-fall-back-straighten";

    public static Dictionary<string, AbilityDefinition> AddIvoryMovement(this Dictionary<string, AbilityDefinition> all)
    {
        // Advance Warning (Before the Dawn): the move home is a cost; "raise or lower" is two offered actions of one use.
        all[AdvanceWarningRaise] = new("Battle", "Move your performing Scout Personality home: Straighten his unit. Raise the current battlefield's province's strength by your Scout's Force", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Scout"], UsageKey: "advance-warning", Costs: [new(CostVerbs.MoveHome, CostSubject.Performer)]);
        all[AdvanceWarningLower] = new("Battle", "Move your performing Scout Personality home: Straighten his unit. Lower the current battlefield's province's strength by your Scout's Force", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Scout"], UsageKey: "advance-warning", Costs: [new(CostVerbs.MoveHome, CostSubject.Performer)]);
        // Ambush: Shinobi Assault's shape (EmperorRulingR1Cards.cs) with any unbowed Personality as the performer.
        all[AmbushBattle] = new("Limited", "Choose your performing unbowed Personality and target another player's Personality: Create a battlefield (not at any province). Assign your Personality to attack there. Assign the target to defend there, even if he is bowed. Other Personalities will not move there. Fight a battle there (after this action's resolution). After the battle ends, lose 5 Honor",
            "enemy-personality", Performer: "own-unbowed-personality");
        // Back to the Front: the battlefield is chosen with the action. Absent: a move to the current battlefield needs neither presence nor the performer there (the Exception); ValidateIvoryMovementLate
        // requires the performer at the current battlefield for any other destination.
        all[BackToTheFront] = new("Battle", "Choose your performing Personality: Move him to a battlefield he assigned to this turn. If he moved, straighten his unit", "none",
            Performer: "own-personality", Absent: true, Move: new(MoveSpec.Performer, MoveSpec.AssignedThisTurn));
        // Fall Back!: the optional straighten of another unit is a decision at resolution (a decider choice).
        all[FallBack] = new("Battle", "Choose your performing Personality: Move him home. If he moved, straighten his unit, and you may straighten one of your other units (at any location)", "none",
            Performer: "own-personality-at-battle");
        // Determined Challenge (Seeds of Decay): a move to the current battlefield (an Absent Battle action for either player, MovesToBattle).
        all[DeterminedChallenge] = new("Battle", "Choose your performing unbowed attacking Personality and target an enemy Personality: Move the target to the current battlefield. Straighten him if he moved", "enemy-personality-at-any-location",
            Performer: "own-unbowed-attacking-personality-at-battle", Move: new(MoveSpec.Target, MoveSpec.CurrentBattlefield), MovesToBattle: true);
        all[TreacheryDeceit] = new("Battle", "Target a Personality whose unit's total Force is 7 or higher: Move him home", PersonalityUnitForce7, Move: new(MoveSpec.Target, MoveSpec.Home));
        all[SoulsSacrifice] = new("Battle", "Choose your performing unbowed Samurai with 3 or higher Personal Honor and target an enemy Personality: Move him home. Permanently give your Samurai Shadowlands and Undead", "enemy-personality",
            Performer: UnbowedSamuraiHonor3, Move: new(MoveSpec.Target, MoveSpec.Home));
        all[SneakAttack] = new("Reaction", "After engaging: The Attacker has the first opportunity to take a Battle action or pass in this battle", "none", Trigger: "after-engaging");
        all[InspiredDevotionFixed] = new("BattleOrOpen", "Choose your performing Yojimbo: Give him +2F", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Yojimbo"], UsageKey: "inspired-devotion");
        all[InspiredDevotionChi] = new("BattleOrOpen", "Choose your performing Yojimbo: Give him a Force bonus equal to the highest Chi of any Courtier or Shugenja you control", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Yojimbo"], UsageKey: "inspired-devotion");
        all[VigilantEyes] = new("Open", "Choose your performing unbowed Samurai and target a Personality: Give him a Force penalty equal to your Samurai's Force", "personality",
            Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Samurai"]);
        return all;
    }
}

/// <summary>The battlefields each Personality was assigned to, for the Attack Phase (or aside battle) they belong to: Back to the Front's "a battlefield he assigned to this turn".</summary>
public sealed class IvoryMovementState
{
    /// <summary>Which phase the record is for (IvoryAssignmentKey); a different phase starts a fresh record.</summary>
    public string Key { get; set; } = "";
    public Dictionary<string, List<int>> Assigned { get; set; } = [];
}

public sealed partial class MatchState
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1220)]
    public IvoryMovementState IvoryMovement { get; set; } = new();
}

public sealed partial class MatchEngine
{
    private IvoryMovementState IvoryMove => _state.IvoryMovement;

    // The phase a record of assignments belongs to: a battle fought inside the Action Phase has its own (its battlefield index is reused), otherwise the Attack Phase of this turn.
    private string IvoryAssignmentKey() => R1.Aside is { } aside ? $"aside|{P2PhaseKey()}|{aside.Battlefield}" : P2PhaseKey();

    /// <summary>Assign() and AssignToCreatedBattlefield note every assignment here.</summary>
    private void NoteIvoryAssignment(CardInstance card, int battlefield)
    {
        var key = IvoryAssignmentKey();
        if (IvoryMove.Key != key) { IvoryMove.Key = key; IvoryMove.Assigned = []; }
        if (!IvoryMove.Assigned.TryGetValue(card.Id, out var list)) IvoryMove.Assigned[card.Id] = list = [];
        if (!list.Contains(battlefield)) list.Add(battlefield);
    }

    private int[] IvoryAssignedBattlefields(CardInstance card) =>
        IvoryMove.Key == IvoryAssignmentKey() && IvoryMove.Assigned.TryGetValue(card.Id, out var list) ? [.. list] : [];

    /// <summary>The battlefields that exist now: the current one, the others still to be fought and (in an Attack Phase) the ones already fought. MoveDestinations' list for AssignedThisTurn.</summary>
    private int[] IvoryExistingBattlefields(int current) =>
        R1.Aside is not null ? [current] : [.. P2OtherBattlefields(current).Append(current).Distinct().Order()];

    // Tamori Tamoko's negation of "his movement" reads this: the actions of this batch that move their TARGET.
    private static string? IvoryMovementMoveKind(string abilityId) =>
        abilityId is IvoryMovementAbilities.DeterminedChallenge or IvoryMovementAbilities.TreacheryDeceit or IvoryMovementAbilities.SoulsSacrifice ? "move" : null;

    // Ambush: "Other Personalities will not move there" -- while the Ambush battle is fought, every Personality but the two it assigned is negated when it would move to its battlefield.
    private bool IvoryAmbushNegatesMove(CardInstance card, int battlefield) =>
        R1.Aside is { } aside && aside.Battlefield == battlefield && aside.Paused.Command is { AbilityId: IvoryMovementAbilities.AmbushBattle } ambush
        && Definition(card).Type == CardType.Personality && card.Id != ambush.PerformerId && card.Id != ambush.TargetId;

    // ---- categories ----

    private bool IvoryMovementTargetAllows(string category, CardInstance target) => category switch
    {
        // Treachery and Deceit: any Personality in play whose unit's total Force is 7 or higher.
        IvoryMovementAbilities.PersonalityUnitForce7 => Definition(target).Type == CardType.Personality && InPlay(target) && UnitTotalForce(target) >= 7,
        _ => false
    };

    private bool IvoryMovementPerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle) =>
        ability.Performer == IvoryMovementAbilities.UnbowedSamuraiHonor3 && IvorySamuraiPerformer(performer, actingAsBattle);

    private bool IvorySamuraiPerformer(CardInstance performer, bool actingAsBattle) =>
        Definition(performer).Type == CardType.Personality && !performer.Bowed && Keyword(performer, "Samurai") && CardPersonalHonor(performer) >= 3 && (!actingAsBattle || AtBattle(performer));

    private string?[]? IvoryMovementPerformers(AbilityDefinition ability, int playerId, bool actingAsBattle) => ability.Performer == IvoryMovementAbilities.UnbowedSamuraiHonor3
        ? _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && IvorySamuraiPerformer(c, actingAsBattle)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray()
        : null;

    // ---- validation ----

    private void ValidateIvoryMovement(string id, AbilityDefinition ability, GameCommand command, PlayerState player)
    {
        switch (id)
        {
            // The move home is a cost: a Scout who may not move cannot pay it (Wall of Honor).
            case IvoryMovementAbilities.AdvanceWarningRaise or IvoryMovementAbilities.AdvanceWarningLower:
                if (command.PerformerId is not null) Require(!MovementLocked(Card(command.PerformerId), MovementKinds.Home), "The performing Scout may not move this turn.");
                break;
            // Ambush is Shinobi Assault's shape: a battle can only be fought from the Action Phase (and not from inside another), and both Personalities must be assignable.
            case IvoryMovementAbilities.AmbushBattle:
                Require(_state.Phase == Phase.Action && R1.Aside is null, "A battle can only be fought from the Action Phase.");
                if (command.PerformerId is not null) Require(MayBeAssignedToCreatedBattlefield(Card(command.PerformerId), attacking: true, mayBeBowed: false), "The performing Personality may not be assigned to attack.");
                if (command.TargetId is not null) Require(MayBeAssignedToCreatedBattlefield(Card(command.TargetId), attacking: false, mayBeBowed: true), "The target may not be assigned to defend.");
                break;
            // "...the highest Chi of any Courtier or Shugenja you control": there must be one.
            case IvoryMovementAbilities.InspiredDevotionChi:
                Require(IvoryHighestCourtierShugenjaChi(player.Id) is not null, "You control no Courtier or Shugenja.");
                break;
        }
    }

    // Back to the Front (after the target and destination checks): the battlefield must be one the performer was assigned to this turn and that still exists; a battlefield other than the current one
    // needs the performer at the current battlefield (the Rule of Presence), the current one is the Exception's move to the battle.
    private void ValidateIvoryMovementLate(string id, GameCommand command)
    {
        if (id != IvoryMovementAbilities.BackToTheFront || command.PerformerId is null || command.Province is not int destination || _state.CurrentBattleProvince is not int current) return;
        var performer = Card(command.PerformerId);
        Require(IvoryAssignedBattlefields(performer).Contains(destination), "Choose a battlefield he was assigned to this turn.");
        if (destination != current) Require(AtBattle(performer), "To move to a different battlefield he must be at the current battlefield.");
    }

    private int? IvoryHighestCourtierShugenjaChi(int playerId)
    {
        var chis = _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && (Keyword(c, "Courtier") || Keyword(c, "Shugenja"))).Select(PersonalityChi).ToArray();
        return chis.Length == 0 ? null : chis.Max();
    }

    // ---- effects ----

    // null: not this batch's ability. false: done. true: the action paused (Ambush's battle, Fall Back!'s decision) and finishes from there.
    private bool? RunIvoryMovement(RunHookContext c)
    {
        var (id, command, player, source, target) = (c.id, c.command, c.player, c.source, c.target);
        switch (id)
        {
            case IvoryMovementAbilities.AdvanceWarningRaise or IvoryMovementAbilities.AdvanceWarningLower:
                {
                    var scout = Card(command.PerformerId!);
                    StraightenUnit(scout);
                    var force = CardForce(scout);
                    if (CurrentProvinceIndex() is int province)
                        GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, province, id == IvoryMovementAbilities.AdvanceWarningRaise ? force : -force, Definition(source!).Name);
                    return false;
                }
            case IvoryMovementAbilities.AmbushBattle:
                {
                    var performer = Card(command.PerformerId!);
                    SpendHandPlayedCard(player, source);
                    var battlefield = CreateAdditionalBattlefield(null);
                    BeginAsideBattle(battlefield, player.Id, command, c.consumedGrant, c.playedFromDiscard, c.opponentHonorBeforeBattleAction, c.bowedBeforeAction, c.preExistingGrants, firstOpportunity: null, () =>
                    {
                        AssignToCreatedBattlefield(performer, battlefield, attacking: true, mayBeBowed: false);
                        AssignToCreatedBattlefield(target!, battlefield, attacking: false, mayBeBowed: true); // "even if he is bowed"
                    }, loseHonorAfter: 5);
                    return true;
                }
            case IvoryMovementAbilities.BackToTheFront:
                {
                    var performer = Card(command.PerformerId!);
                    var (zone, province) = (performer.Zone, performer.ProvinceIndex);
                    ExecuteMove(command, c.ability, source, null);
                    if (performer.Zone != zone || performer.ProvinceIndex != province) StraightenUnit(performer);
                    return false;
                }
            case IvoryMovementAbilities.FallBack:
                {
                    var performer = Card(command.PerformerId!);
                    var zone = performer.Zone;
                    MoveHome(performer, false);
                    if (performer.Zone == zone) return false; // he did not move: nothing more happens
                    StraightenUnit(performer);
                    // "...and you may straighten one of your other units (at any location)": asked now, when it resolves; nothing is asked when no other unit has anything to straighten.
                    if (IvoryFallBackCandidates(command).Candidates.Count == 0) return false;
                    return BeginDeciderChoice(IvoryMovementAbilities.FallBackSpec, command, c.consumedGrant, c.playedFromDiscard, c.opponentHonorBeforeBattleAction, c.bowedBeforeAction, c.preExistingGrants);
                }
            case IvoryMovementAbilities.DeterminedChallenge:
                {
                    // "Straighten him if he moved": a target already at the battlefield did not move.
                    var (zone, province) = (target!.Zone, target.ProvinceIndex);
                    if (ExecuteMove(command, c.ability, source, target) && (target.Zone != zone || target.ProvinceIndex != province)) Straighten(target);
                    return false;
                }
            case IvoryMovementAbilities.TreacheryDeceit:
                ExecuteMove(command, c.ability, source, target);
                return false;
            case IvoryMovementAbilities.SoulsSacrifice:
                {
                    ExecuteMove(command, c.ability, source, target);
                    var samurai = Card(command.PerformerId!);
                    GiveKeywordPermanently(samurai, "Shadowlands", source!.Id);
                    GiveKeywordPermanently(samurai, "Undead", source.Id);
                    return false;
                }
            case IvoryMovementAbilities.SneakAttack:
                ClaimFirstOpportunity(_state.PendingEngage!.Attacker, "Engage", source);
                return false;
            case IvoryMovementAbilities.InspiredDevotionFixed:
                ApplyModifier(Card(command.PerformerId!), source!.Id, id, "Force", 2);
                return false;
            case IvoryMovementAbilities.InspiredDevotionChi:
                ApplyModifier(Card(command.PerformerId!), source!.Id, id, "Force", IvoryHighestCourtierShugenjaChi(player.Id) ?? 0);
                return false;
            case IvoryMovementAbilities.VigilantEyes:
                ApplyModifier(target!, source!.Id, id, "Force", -CardForce(Card(command.PerformerId!)));
                return false;
            default:
                return null;
        }
    }

    // ---- Fall Back!'s decision ----

    private (List<string> Candidates, int Min, int Max) IvoryFallBackCandidates(GameCommand command)
    {
        var performerId = command.PerformerId;
        var host = performerId is null ? null : UnitHostId(Card(performerId));
        var units = _state.Cards.Values
            .Where(p => p.OwnerId == command.PlayerId && InPlay(p) && Definition(p).Type == CardType.Personality && p.Id != host && (p.Bowed || Attachments(p.Id).Any(a => a.Bowed)))
            .Select(p => p.Id).Order(StringComparer.Ordinal).ToList();
        return (units, 0, 1);
    }

    private bool ApplyIvoryFallBack(PendingDeciderChoice p)
    {
        if (p.SpecId != IvoryMovementAbilities.FallBackSpec) return false;
        foreach (var id in p.Chosen) if (InPlay(Card(id))) StraightenUnit(Card(id));
        if (p.Chosen.Count == 0) Emit("fallBackNoUnit", $"{Definition(Card(p.SourceCardId)).Name}: no other unit was straightened.", p.ActingPlayerId, p.SourceCardId);
        return true;
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterIvoryMovementHooks()
    {
        EngineHooks.Abilities.Register("AddIvoryMovement", 11100, static abilities => abilities.AddIvoryMovement());
        EngineHooks.TargetAllows.Register("IvoryMovementTargetAllows", 11100, static (engine, context) => engine.IvoryMovementTargetAllows(context.ability.Target, context.target));
        EngineHooks.PerformerAllows.Register("IvoryMovementPerformerAllows", 11100, static (engine, context) => engine.IvoryMovementPerformerAllows(context.ability, context.performer, context.actingAsBattle));
        EngineHooks.PerformerCandidates.Register("IvoryMovementPerformers", 11100, static (engine, context) => engine.IvoryMovementPerformers(context.ability, context.playerId, context.actingAsBattle));
        EngineHooks.ValidateMiddle.Register("ValidateIvoryMovement", 11100, static (engine, context) => engine.ValidateIvoryMovement(context.id, context.ability, context.command, context.player));
        EngineHooks.ValidateLate.Register("ValidateIvoryMovementLate", 11100, static (engine, context) => engine.ValidateIvoryMovementLate(context.id, context.command));
        EngineHooks.RunEffects.Register("RunIvoryMovement", 11100, static (engine, context) => engine.RunIvoryMovement(context));
        EngineHooks.DeciderPrompt.Register("IvoryFallBackPrompt", 11100, static (engine, p) => p.SpecId == IvoryMovementAbilities.FallBackSpec
            ? $"{Definition(engine.Card(p.SourceCardId)).Name}: you may straighten one of your other units (at any location)." : null);
        EngineHooks.DeciderOptions.Register("IvoryFallBackOptions", 11100, static (engine, specId, deciderId, command) => specId == IvoryMovementAbilities.FallBackSpec ? engine.IvoryFallBackCandidates(command) : null);
        EngineHooks.ApplyDecider.Register("ApplyIvoryFallBack", 11100, static (engine, p) => engine.ApplyIvoryFallBack(p));
        EngineHooks.DeciderVerb.Register("IvoryFallBackVerb", 11100, static (engine, specId) => specId == IvoryMovementAbilities.FallBackSpec ? "Straighten" : null);
        EngineHooks.DeciderIdAfter.Register("IvoryFallBackDecider", 11100, static (engine, specId, command) => specId == IvoryMovementAbilities.FallBackSpec ? command.PlayerId : null);
    }
}
