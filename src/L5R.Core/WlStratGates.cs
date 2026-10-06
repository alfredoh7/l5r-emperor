using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// STRATEGY WAVE 119 (issue #674, cluster "strat-gates"): eighteen Gates of Chaos Strategies that the #668 assessment said every piece of was already built. Each was read against its Gates of Chaos
// scan; the scan wins over the catalogue text. Hook priorities 14800-14805, JsonPropertyOrder 20800.
//   * ALLIED EFFORTS (10738): "Battle: Give a target enemy Personality or Follower -3F." is Spitting Llama's action (spitting-llama-force): reused unchanged.
//   * TACTICAL SETBACK (10747), DEFENSIVE FORMATION (10721), NUBANE'S DEFENSE (10726), CHAOS OF BATTLE (10716), FRIENDS IN THE JUNGLE (10740): the small Force and Chi bonuses and penalties.
//   * BOILING POINT (10717): a Focus Effect (+1F to your Yojimbo where your Personality in the duel stands) and a two-target Battle/Open (bow, then straighten another).
//   * IAIJUTSU DOJO (10722): a duel whose loser is dishonored and bowed (ApplyDuelOutcome's case).
//   * HONOR REPLACES RAGE (10725): "-2F while attacking this turn", a per-turn marker read by the PersonalityForce hook.
//   * CLAIMING THE RUINS (10743): a search of the Fate discard pile for a non-Unique Item.
//   * EXOTIC FARMLANDS (10727), IVINDA VILLAGE (10729): the printed variable Gold cost ("*"), read back as the amount paid.
//   * LOST CARAVAN (10737), MUKUDORI'S WISDOM (10728), THE TOMB OF SUN TAO (10750): straighten a Stronghold or Holding; straighten or bow a Ring; return a Ring to its owner's hand.
//   * RECOVERING WHAT WAS LOST (10719): an Absent Engage Reaction that brings a Personality from home to the battle.
//   * THE FALSE ROUTE (10744): Surprise Attack's created battlefield (not at a province) with two Personalities moved there.
//   * THE FINAL LESSON (10749): the permanent Expendable keyword.
// The Kharmic reminder text on Ivinda Village, Mukudori's Wisdom and The Tomb of Sun Tao is the engine's own keyword action (kharmic-discard); it is not an ability of these cards.
// ======================================================================================================================================================================================

public static class WlStratGatesAbilities
{
    public const string AlliedEfforts = "spitting-llama-force";
    public const string Setback = "wl-sg-tactical-setback", Formation = "wl-sg-defensive-formation", Nubane = "wl-sg-nubanes-defense", Chaos = "wl-sg-chaos-of-battle", Friends = "wl-sg-friends-jungle",
        BoilingFocus = "wl-sg-boiling-focus", BoilingBowStraighten = "wl-sg-boiling-bow-straighten", Dojo = "wl-sg-dojo-duel", Rage = "wl-sg-honor-replaces-rage", Ruins = "wl-sg-claiming-ruins",
        Farmlands = "wl-sg-exotic-farmlands", Ivinda = "wl-sg-ivinda-cavalry", Caravan = "wl-sg-lost-caravan", MukudoriStraighten = "wl-sg-mukudori-straighten", MukudoriBow = "wl-sg-mukudori-bow",
        Tomb = "wl-sg-tomb-sun-tao", Recovering = "wl-sg-recovering-lost", FalseRoute = "wl-sg-false-route", FinalLesson = "wl-sg-final-lesson";

    // Target categories of this cluster (TargetAllows).
    public const string OwnDefendingPersonality = "wl-sg-own-defending-personality", OpposedPersonality = "wl-sg-opposed-personality", OwnUnbowedAnyLocation = "wl-sg-own-unbowed-personality-any-location",
        OtherPersonality = "wl-sg-other-personality", OwnStrongholdOrHolding = "wl-sg-own-stronghold-or-holding", CardInPlay = "wl-sg-card-in-play";

    /// <summary>The Gold a Battle action's printed "*" cost may be paid up to (a bound for the payment prompt), and the most targets Ivinda Village's slot offers.</summary>
    public const int MaxVariableGold = 12;

    public static SearchSpec RuinsSearch { get; } = new("wl-sg-claiming-the-ruins", "Search your discard pile for a non-Unique Item",
        [new(SearchDeck.FateDiscard, new("non-Unique Item", [CardType.Item], NonUnique: true), SearchEffect.ToHand)]);

    public static readonly CreatedCardSpec Colonists = new(CardType.Follower, Force: 1, Keywords: ["Ashigaru"], ProxyId: "user-scan-colonists");

    public static Dictionary<string, AbilityDefinition> AddWlStratGates(this Dictionary<string, AbilityDefinition> all)
    {
        all[Setback] = new("Battle", "Absent Battle: Give a target enemy Personality -2F", "enemy-personality", Absent: true);
        all[Formation] = new("Battle", "Give one to three of your target defending Personalities +2F", "slots",
            Slots: [new("targets", OwnDefendingPersonality, "One to three of your defending Personalities", 1, 3)]);
        all[Nubane] = new("Battle", "Give one or two of your target Personalities +1F for each Ring you control", "slots",
            Slots: [new("targets", "own-personality", "One or two of your Personalities", 1, 2)]);
        all[Chaos] = new("Battle", "Give one or two target opposed Personalities a Force bonus equal to the number of different Clan alignments on Personalities at the current battlefield", "slots",
            Slots: [new("targets", OpposedPersonality, "One or two opposed Personalities", 1, 2)]);
        all[Friends] = new("BattleOrOpen", "Give your target Nonhuman Personality +2F/+2C", "own-personality-with-keyword", RequiredKeywords: ["Nonhuman"]);
        all[BoilingFocus] = new("FocusEffect", "As a Focus Effect: Give +1F to your Yojimbo at the same location as your Personality in this duel", "none");
        all[BoilingBowStraighten] = new("BattleOrOpen", "Bow your target unbowed Personality at any location. Straighten another target Personality", "slots",
            Slots: [new("bow", OwnUnbowedAnyLocation, "Your unbowed Personality to bow (at any location)"), new("straighten", OtherPersonality, "Another Personality to straighten")]);
        all[Dojo] = new("Battle", "Iaijutsu Battle: Your target unbowed Personality challenges a target enemy Personality. Dishonor and bow the duel's loser", "enemy-personality",
            Performer: "own-unbowed-personality-at-battle", IssuesDuel: true, ActionKeywords: ["Iaijutsu"]);
        all[Rage] = new("Open", "A target Personality has -2F while attacking this turn", "personality");
        all[Ruins] = new("Limited", "Put a non-Unique Item from your discard pile into your hand", "none", Search: RuinsSearch);
        all[Farmlands] = new("Battle", "Target your Personality. Create and attach to him a 1F Ashigaru Follower for every 3 Gold you paid, rounded down", "own-personality",
            Cost: GoldCostRule.Variable(0, MaxVariableGold), Creates: Colonists);
        all[Ivinda] = new("Open", "Give Cavalry to a number of target cards equal to the amount of Gold you paid", "slots",
            Cost: GoldCostRule.Variable(1, MaxVariableGold), Slots: [new("cards", CardInPlay, "Cards to give Cavalry (as many as the Gold you pay)", 1, MaxVariableGold)]);
        all[Caravan] = new("Open", "Straighten your target Stronghold or Holding", OwnStrongholdOrHolding);
        all[MukudoriStraighten] = new("BattleOrOpen", "Straighten a target Ring", "ring-in-play-or-own-discard", UsageKey: "wl-sg-mukudori");
        all[MukudoriBow] = new("BattleOrOpen", "Bow a target Ring", "ring-in-play-or-own-discard", UsageKey: "wl-sg-mukudori");
        all[Tomb] = new("Open", "Put a target Ring into its owner's hand", "ring-in-play-or-own-discard");
        all[Recovering] = new("Reaction", "Absent Engage: If he would be opposed, move your target Personality at home to the current battlefield. Give him +2F if he is a Tactician", "own-personality-at-home",
            Trigger: "after-engaging", Absent: true);
        all[FalseRoute] = new("Battle", "Create a battlefield without a Province. Move your target Personality and a target enemy Personality there; if your Personality is a Scout, straighten his unit", "slots",
            Slots: [new("own", "own-personality", "Your Personality"), new("enemy", "enemy-personality", "A target enemy Personality")]);
        all[FinalLesson] = new("Limited", "Permanently give one or two of your target unbowed Personalities Expendable", "slots",
            Slots: [new("targets", "own-unbowed-personality", "One or two of your unbowed Personalities", 1, 2)]);
        return all;
    }
}

/// <summary>Honor Replaces Rage's marker: the Personality, and the turn whose attack it is weakened in.</summary>
public sealed record WlAttackPenalty(string CardId, int Turn);

/// <summary>What this cluster keeps between commands.</summary>
public sealed class WlStratGatesState
{
    public List<WlAttackPenalty> AttackPenalties { get; set; } = [];
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(20800)]
    public WlStratGatesState WlStratGates { get; set; } = new();
}

public sealed partial class MatchEngine
{
    private const int WlRageForce = -2;

    // ---- target categories -----------------------------------------------------------------------------------------------------------------------------------------------

    private bool WlStratGatesTargetAllows(TargetAllowsContext context)
    {
        var (ability, command, player, target) = (context.ability, context.command, context.player, context.target);
        var type = Definition(target).Type;
        var personality = type == CardType.Personality;
        var own = target.OwnerId == player.Id;
        var actingAsBattle = ability.Timing == "Battle" || ability.Timing == "BattleOrOpen" && _state.Phase == Phase.Battle;
        switch (ability.Target)
        {
            case WlStratGatesAbilities.OwnDefendingPersonality: return personality && own && player.Id != _state.ActivePlayerId;
            case WlStratGatesAbilities.OpposedPersonality: return personality && _state.CurrentBattleProvince is int battle && Army(1 - target.OwnerId, battle).Any();
            case WlStratGatesAbilities.OwnUnbowedAnyLocation: return personality && own && !target.Bowed && (!actingAsBattle || AtAnyLocation(target));
            case WlStratGatesAbilities.OtherPersonality: return personality && SlotCards(command, "bow").All(chosen => chosen.Id != target.Id);
            case WlStratGatesAbilities.OwnStrongholdOrHolding: return own && type is CardType.Stronghold or CardType.Holding;
            case WlStratGatesAbilities.CardInPlay: return InPlay(target);
            default: return false;
        }
    }

    // ---- validation ------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateWlStratGates(ValidationHookContext context)
    {
        var (id, command, player) = (context.id, context.command, context.player);
        switch (id)
        {
            // "Target a Ring": only one in play (the listing category also names the player's own Rings in the Fate discard pile).
            case WlStratGatesAbilities.MukudoriStraighten or WlStratGatesAbilities.MukudoriBow or WlStratGatesAbilities.Tomb when command.TargetId is { } ringId && _state.Cards.TryGetValue(ringId, out var ring):
                Require(Definition(ring).Type == CardType.Ring && InPlay(ring), "Target a Ring in play.");
                break;
            // "If he would be opposed": the other side has units at the battlefield he would join.
            case WlStratGatesAbilities.Recovering:
                Require(_state.CurrentBattleProvince is int battle && Army(1 - player.Id, battle).Any(), "He would not be opposed at this battlefield.");
                break;
            // "a number of target cards equal to the amount of Gold you paid".
            case WlStratGatesAbilities.Ivinda:
                Require(SlotCards(command, "cards").Count() == PaidGoldAmount(command), "Choose as many cards as the Gold you pay.");
                break;
        }
    }

    // ---- effects ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWlStratGates(RunHookContext context)
    {
        var (id, ability, command, player, source, target) = (context.id, context.ability, context.command, context.player, context.source, context.target);
        switch (id)
        {
            case WlStratGatesAbilities.Setback:
                ApplyModifier(target!, source!.Id, id, "Force", -2);
                return false;
            case WlStratGatesAbilities.Formation:
                foreach (var each in SlotCards(command, "targets").ToArray()) ApplyModifier(each, source!.Id, id, "Force", 2);
                return false;
            case WlStratGatesAbilities.Nubane:
                {
                    var rings = _state.Cards.Values.Count(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Ring);
                    foreach (var each in SlotCards(command, "targets").ToArray()) ApplyModifier(each, source!.Id, id, "Force", rings);
                    return false;
                }
            case WlStratGatesAbilities.Chaos:
                {
                    // Every Personality at the current battlefield, either side's: the different Clan alignments among them (an Unaligned one has none).
                    var alignments = _state.Cards.Values.Where(c => Definition(c).Type == CardType.Personality && InPlay(c) && AtBattle(c))
                        .SelectMany(ClanAlignments).Distinct(StringComparer.Ordinal).Count();
                    foreach (var each in SlotCards(command, "targets").ToArray()) ApplyModifier(each, source!.Id, id, "Force", alignments);
                    Emit("chaosOfBattle", $"{Definition(source!).Name}: {alignments} different Clan alignment{(alignments == 1 ? "" : "s")} at the battlefield.", player.Id, source!.Id);
                    return false;
                }
            case WlStratGatesAbilities.Friends:
                ApplyModifier(target!, source!.Id, id, "Force", 2);
                ApplyModifier(target!, source.Id, id, "Chi", 2);
                return false;
            case WlStratGatesAbilities.BoilingBowStraighten:
                {
                    var bowed = SlotCards(command, "bow").Single();
                    if (!StalwartNegatesBow(bowed, player.Id)) bowed.Bowed = true;
                    Straighten(SlotCards(command, "straighten").Single());
                    return false;
                }
            case WlStratGatesAbilities.Rage:
                _state.WlStratGates.AttackPenalties.RemoveAll(p => p.Turn != _state.TurnNumber);
                _state.WlStratGates.AttackPenalties.Add(new(target!.Id, _state.TurnNumber));
                Emit("honorReplacesRage", $"{Definition(target).Name} has -2F while attacking this turn.", player.Id, target.Id);
                return false;
            case WlStratGatesAbilities.Ruins:
                SpendHandPlayedCard(player, source);
                BeginSearch(ability.Search!, source!, player.Id, "ability", context.bowedBeforeAction);
                return true;
            case WlStratGatesAbilities.Farmlands:
                {
                    // "For every 3 Gold you paid, rounded down": the Gold chosen for the variable cost.
                    var count = PaidGoldAmount(command) / 3;
                    if (count < 1)
                    {
                        Emit("exoticFarmlandsNone", $"{Definition(source!).Name}: fewer than 3 Gold was paid, so no Follower is created.", player.Id, source!.Id);
                        return false;
                    }
                    SpendHandPlayedCard(player, source);
                    return ExecuteCreate(ability, ability.Creates! with { Count = count }, command, player);
                }
            case WlStratGatesAbilities.Ivinda:
                foreach (var each in SlotCards(command, "cards").ToArray()) GrantKeyword(each, "Cavalry", source!.Id, GrantDuration.Turn);
                return false;
            case WlStratGatesAbilities.Caravan:
                Straighten(target!);
                return false;
            case WlStratGatesAbilities.MukudoriStraighten:
                Straighten(target!);
                return false;
            case WlStratGatesAbilities.MukudoriBow:
                target!.Bowed = true;
                Emit("ringBowed", $"{Definition(target).Name} was bowed.", target.OwnerId, target.Id);
                return false;
            case WlStratGatesAbilities.Tomb:
                ReturnToHand(target!);
                target!.Bowed = false;
                return false;
            case WlStratGatesAbilities.Recovering:
                {
                    var battle = _state.CurrentBattleProvince!.Value;
                    TryMoveToBattlefield(target!, battle);
                    if (Keyword(target!, "Tactician")) ApplyModifier(target!, source!.Id, id, "Force", 2);
                    return false;
                }
            case WlStratGatesAbilities.FalseRoute:
                {
                    var own = SlotCards(command, "own").Single();
                    var enemy = SlotCards(command, "enemy").Single();
                    var battlefield = CreateAdditionalBattlefield(null);
                    TryMoveToBattlefield(own, battlefield);
                    TryMoveToBattlefield(enemy, battlefield);
                    if (Keyword(own, "Scout")) StraightenUnit(own);
                    return false;
                }
            case WlStratGatesAbilities.FinalLesson:
                foreach (var each in SlotCards(command, "targets").ToArray()) GrantKeyword(each, "Expendable", source!.Id, GrantDuration.Permanent);
                return false;
            default:
                return null;
        }
    }

    // ---- Boiling Point's Focus Effect ------------------------------------------------------------------------------------------------------------------------------------

    // ApplyDuelFocusEffectCore's hook: each of the Focus Effect's owner's Yojimbo at the same location as his Personality in this duel gets +1F.
    private void ApplyWlStratGatesFocusEffect(string abilityId, int ownerId)
    {
        if (abilityId != WlStratGatesAbilities.BoilingFocus || _state.PendingDuel is not { } duel) return;
        var mine = Card(duel.ChallengerPlayerId == ownerId ? duel.ChallengerId : duel.ChallengedId);
        if (LocationOf(mine) is not { } location) return;
        foreach (var yojimbo in _state.Cards.Values.Where(c => c.OwnerId == ownerId && Definition(c).Type == CardType.Personality && InPlay(c) && Keyword(c, "Yojimbo") && LocationOf(c) == location).ToArray())
            ApplyModifier(yojimbo, abilityId, abilityId, "Force", 1);
    }

    // ---- Iaijutsu Dojo's outcome -----------------------------------------------------------------------------------------------------------------------------------------

    // ApplyDuelOutcome's case: "Dishonor and bow the duel's loser."
    private void WlDojoDuelOutcome(List<string> loserIds, int challengerPlayerId)
    {
        foreach (var loserId in loserIds)
        {
            var loser = Card(loserId);
            if (!InPlay(loser)) continue;
            Dishonor(loser, challengerPlayerId);
            if (!StalwartNegatesBow(loser, challengerPlayerId)) loser.Bowed = true;
        }
    }

    // ---- Honor Replaces Rage ---------------------------------------------------------------------------------------------------------------------------------------------

    // PersonalityForce's hook: -2F for each of its plays this turn while he is attacking.
    private int WlStratGatesPersonalityForce(CardInstance card) =>
        IsAttacking(card) ? _state.WlStratGates.AttackPenalties.Count(p => p.CardId == card.Id && p.Turn == _state.TurnNumber) * WlRageForce : 0;

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlStratGatesHooks()
    {
        EngineHooks.Abilities.Register("AddWlStratGates", 14800, static abilities => abilities.AddWlStratGates());
        EngineHooks.Searches.Register("WlStratGatesSearches", 14800, static () => [WlStratGatesAbilities.RuinsSearch]);
        EngineHooks.TargetAllows.Register("WlStratGatesTargetAllows", 14800, static (engine, context) => engine.WlStratGatesTargetAllows(context));
        EngineHooks.ValidateLate.Register("ValidateWlStratGates", 14800, static (engine, context) => engine.ValidateWlStratGates(context));
        EngineHooks.RunEffects.Register("RunWlStratGates", 14800, static (engine, context) => engine.RunWlStratGates(context));
        EngineHooks.PersonalityForce.Register("WlStratGatesPersonalityForce", 14800, static (engine, context) => engine.WlStratGatesPersonalityForce(context.card));
    }
}
