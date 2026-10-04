namespace L5R.Core;

// ======================================================================================================================================================================================
// IVORY "POLITICAL" BATCH (issue #622): the Emperor-era printings of thirteen cards whose primary printing is Ivory. strategy-wave-107 (Favors, Inexplicable Challenge, Confusion at Court, My Lord's
// Favor), personality-wave-120 (Daidoji Tametaka, Isawa Hibana, Komori Taruko, Sandayu, Kitsu Suki, Otomo Demiyah), follower-wave-44 (Oriole Imperial Vanguard), item-wave-40 (Record of Failure,
// Spiked Tetsubo). Every text is the scan's own, not Oracle's later Ivory template. Readings:
//
//   * FAVORS / INEXPLICABLE CHALLENGE / OTOMO DEMIYAH: "choose a player, who gains or loses N Honor" is the existing player choice (HonorChoice.Options / a gain-or-lose pair of Reactions). The Honor
//     sentence of Inexplicable Challenge is independent of the bow before it (Doji Yoshitada's ruling, #336), so it is asked after the bow, whatever the bow did. Favors' Courtier is bowed as the
//     cost; Inexplicable Challenge's Courtier is not bowed (no cost is printed) and may be anywhere, bowed or not.
//   * CONFUSION AT COURT: a Strategy Reaction played from a hand before any action resolves. It applies when discarding the Imperial Favor is one of the action's costs: a declared Favor discard,
//     or the alternative a player chose that discards it. A Favor token (Kyuden Otomo - exp) is not the Imperial Favor, so an action paid with one is not answered. Any player's action.
//   * MY LORD'S FAVOR: two actions. "Your unit" is named by its Personality (as for "enemy unit"): the Personality and every attachment of his are straightened.
//   * KOMORI TARUKO: "Even if Taruko is bowed" -- the bowed-source exception, as Moto Rani - exp.
//   * SANDAYU: straightens the Personality only ("Straighten him"), unlike Mirumoto Houken's unit.
//   * ISAWA HIBANA: "Give Hibana -3F" is before the colon, so it is a cost (payable only with 3 Force to lose; a negated action still pays it). She may name herself.
//   * DAIDOJI TAMETAKA: "You may target one of your Personalities and move him home" is an optional second target chosen with the first.
//   * KITSU SUKI: each player who controls an Ancestor gains 1 Honor (the rehonor rule for a dishonorable Suki's own controller applies). "Starting Family Honor" is the starting Honor printed on
//     the player's Stronghold. Every player with no Ancestor and Honor above that is a player the acting player may make lose 1: one listed choice, one option for each combination.
//   * ORIOLE IMPERIAL VANGUARD: a trait, so mandatory and automatic. Before the first Political action each phase that targeted his Personality resolves, its effects are negated whole
//     (taken, its costs paid: ResolveNegatedAction), unless Protection stops the negation. Any player's action, the Personality's own controller's too: it prints no "another player's".
//   * RECORD OF FAILURE: "Attaches to a Courtier paying 2 less Gold" is a discount (as Maga-yari's). The Limited action removes the Item as its cost and costs the dead Personality's OWNER Honor equal
//     to his printed (base) Personal Honor. A "dishonorably dead" Personality is one in his owner's dead pile who was dishonorable when he died.
//   * SPIKED TETSUBO: a trait. When the duel begins (the before-duel window has closed) and a participant has a Tetsubo, the other loses Duelist until the end of the turn (a keyword effect with no
//     stated duration, RULINGS R-001); his own Duelist refocus is judged afterwards, so it does not happen.
// ======================================================================================================================================================================================

public static class IvoryPoliticalAbilities
{
    public const string FavorsHonor = "favors-honor", InexplicableBow = "inexplicable-challenge-bow", ConfusionNegate = "confusion-at-court-negate";
    public const string LordsFavorTake = "lords-favor-take", LordsFavorStraighten = "lords-favor-straighten";
    public const string TametakaWeaken = "tametaka-weaken", HibanaBoost = "hibana-boost", TarukoStraighten = "taruko-straighten", SandayuStraighten = "sandayu-straighten";
    public const string SukiHonor = "suki-honor", DemiyahGain = "demiyah-gain", DemiyahLose = "demiyah-lose", RecordOfFailureHonor = "record-of-failure-honor";
    public const string OrioleEffect = "oriole-imperial-vanguard", RecordOfFailureEffect = "record-of-failure", TetsuboEffect = "spiked-tetsubo";
    public const string OwnNonWaterPersonality = "own-non-water-personality", OwnUnitLeader = "own-unit-leader", DishonorablyDeadPersonality = "dishonorably-dead-personality";
    public const string HomeSlot = "home";

    private static readonly ActionCost DiscardFavor = new(CostVerbs.DiscardFavor);

    public static Dictionary<string, AbilityDefinition> AddIvoryPolitical(this Dictionary<string, AbilityDefinition> all)
    {
        // Favors (oracle-l5r-2492): "Limited: Bow your performing Courtier: Choose a player. He gains or loses 2 Honor."
        all[FavorsHonor] = new("Limited", "Political Limited: Bow your performing Courtier: Choose a player. He gains or loses 2 Honor", "none",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Courtier"], ActionKeywords: ["Political"], Costs: ActionCost.BowPerformer,
            Branch: new(BranchCondition.Always, HonorChoice.Options(2), HonorChoice.None));
        // Inexplicable Challenge (oracle-l5r-3743): "Battle: Choose your performing Courtier at any location and target an enemy card without attachments: Bow it. Choose a player, who gains or
        // loses 2 Honor." The player is a follow-up asked after the bow.
        all[InexplicableBow] = new("Battle", "Political Battle: Choose your performing Courtier at any location and target an enemy card without attachments: Bow it. Choose a player, who gains or loses 2 Honor", "slots",
            Performer: "own-personality-with-keyword", RequiredKeywords: ["Courtier"], ActionKeywords: ["Political"],
            Slots: [new("target", "enemy-card-without-attachments", "An enemy card without attachments"),
                new("honor", "player", "A player gains or loses 2 Honor", 0, 0, Resolution: new(BranchCondition.Always, HonorChoice.Options(2), HonorChoice.None))]);
        // Confusion at Court (oracle-l5r-1450): "Reaction: Before an action resolves, if discarding the Imperial Favor is one of its base costs: Negate the action's effects."
        all[ConfusionNegate] = new("Reaction", "Political Reaction: Before an action resolves, if discarding the Imperial Favor is one of its base costs: Negate the action's effects", "none",
            Trigger: "before-action-resolves", ActionKeywords: ["Political"]);
        // My Lord's Favor (oracle-l5r-10160): "Open: Choose your performing Personality: Take the Imperial Favor." and "Battle/Open: Discard the Imperial Favor and target your unit: Straighten it."
        all[LordsFavorTake] = new("Open", "Political Open: Choose your performing Personality: Take the Imperial Favor", "none",
            Performer: "own-personality", ActionKeywords: ["Political"]);
        all[LordsFavorStraighten] = new("BattleOrOpen", "Favor Political Battle/Open: Discard the Imperial Favor and target your unit: Straighten it", OwnUnitLeader,
            ActionKeywords: ["Favor", "Political"], Costs: [DiscardFavor]);
        // Daidoji Tametaka (oracle-l5r-1682): "Battle: Target an enemy Personality or Follower: Give it -4F. You may target one of your Personalities and move him home."
        all[TametakaWeaken] = new("Battle", "Target an enemy Personality or Follower: Give it -4F. You may target one of your Personalities and move him home", "slots",
            Slots: [new("target", "enemy-force", "An enemy Personality or Follower"), new(HomeSlot, "own-personality", "One of your Personalities to move home (optional)", 0, 1)]);
        // Isawa Hibana (oracle-l5r-10305): "Battle/Open: Give Hibana -3F and target your Personality without the Water keyword: Give him +3F."
        all[HibanaBoost] = new("BattleOrOpen", "Give Hibana -3F and target your Personality without the Water keyword: Give him +3F", OwnNonWaterPersonality,
            Costs: [new(CostVerbs.ForcePenalty, CostSubject.Source, Amount: 3)]);
        // Komori Taruko (oracle-l5r-4537): "Open: Even if Taruko is bowed: Straighten her."
        all[TarukoStraighten] = new("Open", "Open: Even if Taruko is bowed: Straighten her", "none");
        // Sandayu (oracle-l5r-6467): "Battle/Open: Target a Personality with a Weapon: Straighten him."
        all[SandayuStraighten] = new("BattleOrOpen", "Target a Personality with a Weapon: Straighten him", "personality-with-weapon");
        // Kitsu Suki (oracle-l5r-4396): "Limited: Each player who controls an Ancestor gains 1 Honor. For any player who controls no Ancestors, and with higher Honor than his starting Family
        // Honor, you may cause him to lose 1 Honor."
        all[SukiHonor] = new("Limited", "Limited: Each player who controls an Ancestor gains 1 Honor. For any player who controls no Ancestors, and with higher Honor than his starting Family Honor, you may cause him to lose 1 Honor", "none");
        // Otomo Demiyah (oracle-l5r-5812): "Political Reaction: After Demiyah enters play: Choose a player, who gains or loses 1 Honor." (Kitsuki Horume's pair of Reactions sharing one use.)
        all[DemiyahGain] = new("Reaction", "Political Reaction: After Demiyah enters play: choose a player, he gains 1 Honor", "player", Trigger: "own-enters-play", UsageKey: "demiyah-gain-lose", ActionKeywords: ["Political"]);
        all[DemiyahLose] = new("Reaction", "Political Reaction: After Demiyah enters play: choose a player, he loses 1 Honor", "player", Trigger: "own-enters-play", UsageKey: "demiyah-gain-lose", ActionKeywords: ["Political"]);
        // Record of Failure (oracle-l5r-6213): "Political Limited: Remove this card from the game and target a dishonorably dead Personality: His owner loses Honor equal to the Personality's base
        // Personal Honor."
        all[RecordOfFailureHonor] = new("Limited", "Political Limited: Remove this card from the game and target a dishonorably dead Personality: His owner loses Honor equal to the Personality's base Personal Honor",
            DishonorablyDeadPersonality, ActionKeywords: ["Political"], Costs: ActionCost.RemoveSource);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private const string OriolePhaseKey = "oriole-political-negate";

    // ---- hooks' predicates -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool IvoryPoliticalEvenIfBowed(string id) => id == IvoryPoliticalAbilities.TarukoStraighten;

    private static bool IvoryPoliticalBowsTarget(string id) => id == IvoryPoliticalAbilities.InexplicableBow;

    private bool IvoryPoliticalTargetAllows(string category, PlayerState player, CardInstance target) => category switch
    {
        IvoryPoliticalAbilities.OwnNonWaterPersonality => target.OwnerId == player.Id && Definition(target).Type == CardType.Personality && !Keyword(target, "Water"),
        IvoryPoliticalAbilities.OwnUnitLeader => target.OwnerId == player.Id && Definition(target).Type == CardType.Personality,
        _ => false
    };

    /// <summary>EquipGoldCost's term: Record of Failure "attaches to a Courtier paying 2 less Gold" (a discount, as Maga-yari's, not a restriction).</summary>
    private int IvoryPoliticalAttachDiscount(CardInstance card, CardInstance wielder) =>
        Definition(card).Effect == IvoryPoliticalAbilities.RecordOfFailureEffect && Keyword(wielder, "Courtier") ? 2 : 0;

    /// <summary>Record of Failure's target: a Personality in his owner's dead pile who died dishonorable.</summary>
    private static bool IsDishonorablyDead(CardInstance card) => card.Zone == Zone.Dead && card.Dishonorable;

    // ---- Static: Oriole Imperial Vanguard's +1PH ---------------------------------------------------------------------------------------------------------------------------------

    /// <summary>StaticPersonalHonorBonus's term: "This Personality has +1PH" for each Oriole Imperial Vanguard attached to him.</summary>
    private int IvoryPoliticalPersonalHonorBonus(CardInstance card) =>
        Definition(card).Type == CardType.Personality && InPlay(card) ? Attachments(card.Id).Count(a => Definition(a).Effect == IvoryPoliticalAbilities.OrioleEffect) : 0;

    // ---- Oriole Imperial Vanguard: the announced Political action is negated ----------------------------------------------------------------------------------------------------

    // A mandatory trait, fired as the action is announced (before any reaction window, like Hibernation): the first Political action each phase that targets a Personality carrying an Oriole is
    // negated whole. The phase's use is spent by the targeting (Protection stopping the negation does not give it back). True when the action was negated and is over.
    private bool OriolePoliticalNegates(GameCommand command)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || !IsAction(ability)) return false;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        if (!ActionCarriesKeyword(ability, source, "Political")) return false;
        foreach (var targeted in ActionTargetIds(command).Where(id => _state.Cards.ContainsKey(id)).Select(Card).Where(c => Definition(c).Type == CardType.Personality && InPlay(c)))
        {
            var oriole = Attachments(targeted.Id).Where(a => Definition(a).Effect == IvoryPoliticalAbilities.OrioleEffect).OrderBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault();
            if (oriole is null || !TrySpendPhaseUse(oriole.AbilityUses, OriolePhaseKey)) continue;
            if (ActionProtected(command))
            {
                Emit("actionProtected", $"Protection negates the negation -- {Definition(oriole).Name} cannot negate this action.", oriole.OwnerId, oriole.Id);
                return false;
            }
            Emit("actionNegated", $"{Definition(oriole).Name} negates {Player(command.PlayerId).Name}'s action ({ability.Label}) before it resolves.", oriole.OwnerId, oriole.Id);
            ResolveNegatedAction(command);
            return true;
        }
        return false;
    }

    // ---- Confusion at Court ----------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>BeforeActionReactionApplies' case: Confusion at Court in its owner's hand answers any action that discards the Imperial Favor as one of its costs.</summary>
    private bool ConfusionAtCourtApplies(CardInstance card, GameCommand command)
    {
        if (card.Zone != Zone.Hand || Definition(card).Type != CardType.Strategy || command.AbilityId is null || command.CardId == card.Id) return false;
        if (!AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || !IsAction(acting) || !Player(card.OwnerId).Hand.Contains(card.Id)) return false;
        return IsFavorDiscardAbility(command.AbilityId) || ChoseFavorDiscardOption(command) && command.CostOption != FavorTokenCostOption;
    }

    // ---- Isawa Hibana: the cost ----------------------------------------------------------------------------------------------------------------------------------------------------

    // "Give Hibana -3F" is a cost, paid in full or not at all (core rules, Pay Costs): the generic check covers a wielder's penalty, not the source's own.
    private void ValidateIvoryPolitical(string id, GameCommand command)
    {
        if (id != IvoryPoliticalAbilities.HibanaBoost || command.CardId is null || !_state.Cards.TryGetValue(command.CardId, out var hibana)) return;
        Require(CardForce(hibana) >= 3 && hibana.ForcePenaltiesNegatedTurn != _state.TurnNumber, $"{Definition(hibana).Name} does not have 3 Force to lose.");
    }

    // ---- Spiked Tetsubo ---------------------------------------------------------------------------------------------------------------------------------------------------------

    // BeginFocusing's hook: the duel has begun, so each participant has entered it. A Tetsubo on a participant costs the other his Duelist keyword for the turn.
    private void ApplyDuelEntryTraits(PendingDuel duel)
    {
        foreach (var (wielderId, otherId) in new[] { (duel.ChallengerId, duel.ChallengedId), (duel.ChallengedId, duel.ChallengerId) })
        {
            if (!_state.Cards.TryGetValue(wielderId, out var wielder) || !_state.Cards.TryGetValue(otherId, out var other)) continue;
            foreach (var tetsubo in Attachments(wielder.Id).Where(a => Definition(a).Effect == IvoryPoliticalAbilities.TetsuboEffect).OrderBy(a => a.Id, StringComparer.Ordinal))
            {
                if (!Keyword(other, "Duelist")) continue;
                Emit("duelEntryTrait", $"{Definition(tetsubo).Name}: {Definition(other).Name} loses Duelist.", tetsubo.OwnerId, tetsubo.Id);
                RemoveKeywordsAndClans(other, TemporaryRemovals.Turn, ["Duelist"], []);
            }
        }
    }

    // ---- Kitsu Suki -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool ControlsAncestor(int playerId) => _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Keyword(c, "Ancestor"));

    private int StartingFamilyHonor(int playerId) => Definition(Card(Player(playerId).StrongholdId)).StartingHonor;

    // The players Suki's second sentence may make lose Honor, read after the first sentence's gains (which only reach players with an Ancestor).
    private int[] SukiCandidates() =>
        _state.Players.Where(p => !ControlsAncestor(p.Id) && p.Honor > StartingFamilyHonor(p.Id)).Select(p => p.Id).ToArray();

    private EffectOption[] SukiOptions(int actingPlayerId, int[] candidates)
    {
        // The effects name the player relative to the acting player ("honor:self" / "honor:opponent"), as every player-Honor option does.
        string Effect(int id) => BranchEffect.Honor(id == actingPlayerId, -1);
        var options = new List<EffectOption> { new("none", "Nobody loses Honor", []) };
        foreach (var id in candidates) options.Add(new($"lose-{id}", $"{(id == actingPlayerId ? "You lose" : "Your opponent loses")} 1 Honor", [Effect(id)]));
        if (candidates.Length == 2) options.Add(new("lose-both", "You and your opponent each lose 1 Honor", candidates.Select(Effect).ToArray()));
        return [.. options];
    }

    // ---- RunEffects -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunIvoryPolitical(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant,
        bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case IvoryPoliticalAbilities.FavorsHonor:
                return BeginConditionalChoice(command, ability, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            case IvoryPoliticalAbilities.InexplicableBow:
                if (InPlay(target!) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return BeginConditionalChoice(command, ability, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            case IvoryPoliticalAbilities.ConfusionNegate:
                NegateDeferredAction(source!);
                return false;
            case IvoryPoliticalAbilities.LordsFavorTake:
                TakeImperialFavor(player.Id);
                Emit("favorTaken", $"{player.Name} took the Imperial Favor.", player.Id);
                return false;
            case IvoryPoliticalAbilities.LordsFavorStraighten:
                StraightenUnit(target!);
                return false;
            case IvoryPoliticalAbilities.TametakaWeaken:
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                foreach (var mover in SlotCards(command, IvoryPoliticalAbilities.HomeSlot).ToArray()) if (InPlay(mover)) MoveHome(mover, false);
                return false;
            case IvoryPoliticalAbilities.HibanaBoost:
                ApplyModifier(target!, source!.Id, id, "Force", 3);
                return false;
            case IvoryPoliticalAbilities.TarukoStraighten:
                Straighten(source!);
                return false;
            case IvoryPoliticalAbilities.SandayuStraighten:
                Straighten(target!);
                return false;
            case IvoryPoliticalAbilities.DemiyahGain: ApplyPlayerHonorChoice(source!, command.TargetPlayerId!.Value, 1); return false;
            case IvoryPoliticalAbilities.DemiyahLose: ApplyPlayerHonorChoice(source!, command.TargetPlayerId!.Value, -1); return false;
            case IvoryPoliticalAbilities.RecordOfFailureHonor:
            {
                var owed = Definition(target!).PersonalHonor;
                if (owed > 0) GainHonor(target!.OwnerId, -owed, Definition(source!).Name, source);
                return false;
            }
            case IvoryPoliticalAbilities.SukiHonor:
            {
                foreach (var p in _state.Players.ToArray())
                    if (ControlsAncestor(p.Id)) ApplyPlayerHonorChoice(source!, p.Id, 1);
                var candidates = SukiCandidates();
                if (candidates.Length == 0) return false;
                return BeginListedEffectChoice(command, SukiOptions(player.Id, candidates), $"{Definition(source!).Name}: choose who, if anyone, loses 1 Honor.",
                    consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            }
            default:
                return null;
        }
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterIvoryPoliticalHooks()
    {
        EngineHooks.Abilities.Register("AddIvoryPolitical", 11200, static abilities => abilities.AddIvoryPolitical());
        EngineHooks.TargetAllows.Register("IvoryPoliticalTargetAllows", 11200, static (engine, context) => engine.IvoryPoliticalTargetAllows(context.ability.Target, context.player, context.target));
        EngineHooks.BowsTarget.Register("IvoryPoliticalBowsTarget", 11200, static context => IvoryPoliticalBowsTarget(context.abilityId));
        EngineHooks.ValidateMiddle.Register("ValidateIvoryPolitical", 11200, static (engine, context) => engine.ValidateIvoryPolitical(context.id, context.command));
        EngineHooks.RunEffects.Register("RunIvoryPolitical", 11200, static (engine, context) => engine.RunIvoryPolitical(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
    }
}
