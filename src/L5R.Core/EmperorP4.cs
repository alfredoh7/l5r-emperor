namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, GROUP P4: TARGET AND SLOT MODEL, MOVEMENT AND ASSIGNMENT LOCKS, ONE-SHOT WATCHES AND SHIELDS. Cards of Before the Dawn, Embers of War, Forgotten Legacy and The Shadow's Embrace that the
// earlier passes left unwired (blocker categories TARGET, LOCK and WATCH, docs/decisions/2026-09-25-blocker-categories.md). Every card was read letter by letter against its scan (the scan is
// authoritative); where the catalogue's Oracle text differs the scan's wording is published.
//
// Primitives (each checked against docs/rules/EMPEROR_EDITION_CORE_RULES.md; none conflicts with it):
//   * TARGETS BY COMPARISON: an enemy card with lower Gold Cost than the performer (Games of Will); an enemy Personality with fewer keywords than the performer, "Soul of" keywords ignored
//     (Preceding Reputation, KeywordCountExceptSoulOf); an enemy Personality or Follower with Force lower than the performing Yojimbo's Force plus the highest Chi among all Shugenja and Courtiers
//     his controller controls (Yojimbo Assemble, read live at announcement and again at resolution).
//   * A CONDITIONAL SLOT COUNT (Bayushi Tenzan): "Target a Personality, or one or two Personalities if you control a Courtier" -- a slot of one to two whose second card is legal only while the
//     acting player controls a Courtier (the condition is checked at announcement, like every constraints-block condition).
//   * A COUNT-DEPENDENT EFFECT (Gozaru no Oni): "Target one or two enemy cards: Bow them. If you only targeted one card, destroy it." -- the slot's filled count is read at resolution.
//   * A DESTRUCTION OUTCOME OVERRIDE (Gozaru no Oni): "After Gozaru is destroyed: It becomes discarded (not dead)." A trait: after DestroyUnits has made him dead, he is moved to his Dynasty discard pile.
//   * EFFECT-BLOCK TARGETS CHOSEN AT RESOLUTION (core rules, Action Resolution: "Targeting named in the effects block is not required up front -- it happens during resolution"): Pillar Tattoo's "you may
//     target and bow an enemy card without attachments", Tsuruchi Nobukatsu's "you may target and straighten a Holding" (after his Ranged Attack, inline or deferred, Lessons Never Forgotten's shape)
//     and Too Much for Mortals' "bow him or destroy one of his attachments" are listed choices of the acting player (EffectChoice.cs), each option naming its card.
//   * A PERFORMER-KEYED FOLLOW-UP (Shinjo Sarang): a Follower of the acting player's at any location is transferred to the CHOSEN performer and straightened if that performer is opposed or Spider Clan.
//   * GAMES OF WILL'S CONTRIBUTION SHIELD: "Negate all current and new effects from other players' cards that prevent him from contributing Force to his army" -- the per-turn stamp
//     (P4State.ContributionShields) that ForceContributionNegated reads; the player who made a card stop contributing is recorded as it happens (NoteForceContributionNegated).
// ======================================================================================================================================================================================
public static class EmperorP4Abilities
{
    // Personalities.
    public const string TenzanBattle = "tenzan-penalty-battle", TenzanLimited = "tenzan-penalty-limited";
    public const string GozaruBow = "gozaru-bow";
    public const string GozaruDiscardedEffect = "gozaru-becomes-discarded";
    public const string SarangTransfer = "sarang-transfer";
    public const string NobukatsuRanged = "nobukatsu-inexp-ranged";
    // Strategies.
    public const string GamesOfWillBow = "games-of-will-bow", GamesOfWillNegate = "games-of-will-negate";
    public const string PillarTattoo = "pillar-tattoo-boost";
    public const string PrecedingReputation = "preceding-reputation-bow";
    public const string TooMuchForMortals = "too-much-for-mortals";
    public const string YojimboAssemble = "yojimbo-assemble-bow";
    public const string RideTheBreeze = "ride-the-breeze-move";

    // Target and performer categories.
    public const string LowerGoldCostThanPerformer = "p4-enemy-card-lower-gold-cost-than-performer";
    public const string FewerKeywordsThanPerformer = "p4-enemy-personality-fewer-keywords-than-performer";
    public const string YojimboAssembleTarget = "p4-enemy-card-force-lt-yojimbo-plus-chi";
    public const string OwnFollowerForPerformer = "p4-own-follower-any-location-for-performer";
    public const string UnbowedSpiritPerformer = "p4-own-unbowed-spirit-at-battle";

    public static Dictionary<string, AbilityDefinition> AddEmperorP4(this Dictionary<string, AbilityDefinition> all)
    {
        // Bayushi Tenzan (oracle-l5r-910): "Battle/Limited: Target a Personality, or one or two Personalities if you control a Courtier: You may target a Shadowlands Personality. Give each target -3F.
        // If this is the Combat Segment, dishonor them." Two designators, two ids sharing one use (Hawado's split); the Battle one is the Combat Segment's.
        const string tenzan = "Target a Personality, or one or two Personalities if you control a Courtier: You may target a Shadowlands Personality. Give each target -3F. If this is the Combat Segment, dishonor them";
        all[TenzanBattle] = new("Battle", tenzan, "slots", UsageKey: "tenzan-penalty", Slots: [new("targets", "personality", "One Personality, or one or two if you control a Courtier", 1, 2)]);
        all[TenzanLimited] = new("Limited", tenzan, "slots", UsageKey: "tenzan-penalty", Slots: [new("targets", "personality", "One Personality, or one or two if you control a Courtier", 1, 2)]);
        // Gozaru no Oni (oracle-l5r-2880): "Battle: Target one or two enemy cards: Bow them. If you only targeted one card, destroy it."
        all[GozaruBow] = new("Battle", "Target one or two enemy cards: Bow them. If you only targeted one card, destroy it", "slots",
            Slots: [new("targets", "enemy-card", "One or two enemy cards", 1, 2)]);
        // Shinjo Sarang (oracle-l5r-6934): "Battle: Choose your performing Personality, and target your Follower at any location: Transfer it to the Personality. Straighten the Follower if the
        // Personality is opposed or Spider Clan."
        all[SarangTransfer] = new("Battle", "Choose your performing Personality, and target your Follower at any location: Transfer it to the Personality. Straighten the Follower if the Personality is opposed or Spider Clan",
            OwnFollowerForPerformer, Performer: "own-personality-at-battle");
        // Tsuruchi Nobukatsu (oracle-l5r-8861): "Battle: Ranged 4 Attack. If the Ranged Attack destroyed a card, you may target and straighten a Holding."
        all[NobukatsuRanged] = new("Battle", "Ranged 4 Attack. If the Ranged Attack destroyed a card, you may target and straighten a Holding", "attack");
        // Games of Will (oracle-l5r-2771): "Battle: Choose your performing unbowed Personality and target an enemy card with lower Gold Cost: Bow it." and "Battle: Choose your performing
        // Personality: Negate all current and new Force penalties to him from actions. Negate all current and new effects from other players' cards that prevent him from contributing Force to his army."
        all[GamesOfWillBow] = new("Battle", "Choose your performing unbowed Personality and target an enemy card with lower Gold Cost: Bow it", LowerGoldCostThanPerformer,
            Performer: "own-unbowed-personality-at-battle");
        all[GamesOfWillNegate] = new("Battle", "Choose your performing Personality: Negate all current and new Force penalties to him from actions. Negate all current and new effects from other players' cards that prevent him from contributing Force to his army",
            "none", Performer: "own-personality-at-battle");
        // Pillar Tattoo (oracle-l5r-5956): "Battle: Choose your performing unbowed Tattooed Monk: Give him +3F. If he is Earth, you may target and bow an enemy card without attachments."
        all[PillarTattoo] = new("Battle", "Choose your performing unbowed Tattooed Monk: Give him +3F. If he is Earth, you may target and bow an enemy card without attachments", "none",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Tattooed", "Monk"]);
        // Preceding Reputation (oracle-l5r-6043): "Battle: Choose your performing unbowed Personality and target an enemy Personality with fewer keywords, ignoring "Soul of" keywords: Bow all cards
        // without attachments in his unit."
        all[PrecedingReputation] = new("Battle", "Choose your performing unbowed Personality and target an enemy Personality with fewer keywords, ignoring \"Soul of\" keywords: Bow all cards without attachments in his unit",
            FewerKeywordsThanPerformer, Performer: "own-unbowed-personality-at-battle");
        // Too Much for Mortals (oracle-l5r-8634): "Battle: Choose your performing unbowed Spirit and target an enemy Personality: Bow him or destroy one of his attachments."
        all[TooMuchForMortals] = new("Battle", "Choose your performing unbowed Spirit and target an enemy Personality: Bow him or destroy one of his attachments", "enemy-personality",
            Performer: UnbowedSpiritPerformer);
        // Yojimbo Assemble (oracle-l5r-9507): "Battle: Choose your performing unbowed Yojimbo, and target an enemy card with Force lower than your Yojimbo's Force plus the highest Chi among all
        // Shugenja and Courtiers you control: Bow the target. Destroy it if it has no attachments."
        all[YojimboAssemble] = new("Battle", "Choose your performing unbowed Yojimbo, and target an enemy card with Force lower than your Yojimbo's Force plus the highest Chi among all Shugenja and Courtiers you control: Bow the target. Destroy it if it has no attachments",
            YojimboAssembleTarget, Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Yojimbo"]);
        // Ride the Breeze (oracle-l5r-6317): "Battle: Choose your performing unbowed Monk and target an enemy Personality: Move him home. If he moved and your Monk is Air, negate the Personality's
        // (future) movement."
        all[RideTheBreeze] = new("Battle", "Choose your performing unbowed Monk and target an enemy Personality: Move him home. If he moved and your Monk is Air, negate the Personality's (future) movement",
            "enemy-personality", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Monk"]);
        return all;
    }
}

/// <summary>What group P4 keeps between commands (issue #503).</summary>
public sealed class EmperorP4State
{
    /// <summary>Games of Will: the cards whose contribution to their army other players' cards may not prevent, with the turn it lasts.</summary>
    public Dictionary<string, int> ContributionShields { get; set; } = [];
    /// <summary>The player whose effect last stopped each card contributing Force (Defensive Blockade's stamp), keyed by card id.</summary>
    public Dictionary<string, int> ContributionNegatedBy { get; set; } = [];
    /// <summary>Tsuruchi Nobukatsu: whether the Attack of the action now resolving destroyed a card.</summary>
    public bool AttackDestroyedACard { get; set; }
    /// <summary>Drawing on the Mountain: the cards whose next straightening this game is negated.</summary>
    public List<string> NextStraightenNegated { get; set; } = [];
    /// <summary>Scars of War: one mark per copy on a Holding, and whether each marked Holding was bowed when last seen.</summary>
    public List<P4Watch> ScarsMarks { get; set; } = [];
    public Dictionary<string, bool> ScarsLastBowed { get; set; } = [];
    /// <summary>Show of Restraint: the one-shot shield against the next card effect that moves him home or bows him, with its turn.</summary>
    public Dictionary<string, int> RestraintShields { get; set; } = [];
    /// <summary>The Menacing Sky's watches.</summary>
    public List<P4Watch> MenacingSkyWatches { get; set; } = [];
    /// <summary>The Snow Has Teeth: the province ceilings, the players' one-shot move shields and the Terrain watches.</summary>
    public List<P4ProvinceCap> ProvinceCaps { get; set; } = [];
    public List<P4Watch> SnowMoveShields { get; set; } = [];
    public List<P4Watch> SnowTerrainWatches { get; set; } = [];
    /// <summary>No Time for Games: the watches on the next Honor loss from another player's card effect.</summary>
    public List<P4Watch> NoTimeWatches { get; set; } = [];
    /// <summary>Secret Reserve: the Items still to be offered a transfer, in order.</summary>
    public List<string> TransferQueue { get; set; } = [];
}

public sealed partial class MatchEngine
{
    private EmperorP4State P4 => _state.P4;
    private const string P4StraightenEffect = "p4-straighten:", P4BowEffect = "p4-bow:", P4DestroyEffect = "p4-destroy:";

    // ---- hooks into the shared switches (CardAbilities.cs) ------------------------------------------------------------------------------------------------------------------------------

    private static int? P4AttackStrength(string abilityId) => abilityId == EmperorP4Abilities.NobukatsuRanged ? 4 : null;
    private static string? P4AttackKind(string abilityId) => abilityId == EmperorP4Abilities.NobukatsuRanged ? "Ranged" : P4MoreAttackKind(abilityId);

    private static bool P4BowsTarget(string abilityId) => abilityId is EmperorP4WatchAbilities.FirmCensure or EmperorP4Abilities.GozaruBow or EmperorP4Abilities.GamesOfWillBow or EmperorP4Abilities.PrecedingReputation
        or EmperorP4Abilities.TooMuchForMortals or EmperorP4Abilities.YojimboAssemble || P4MoreBowsTarget(abilityId);
    private static bool P4DestroysTarget(string abilityId) => abilityId is EmperorP4Abilities.GozaruBow or EmperorP4Abilities.YojimboAssemble || P4MoreDestroysTarget(abilityId);

    // Target categories (ValidateAbility's default chain; slot candidates are judged through it too).
    private bool EmperorP4TargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var type = Definition(target).Type;
        var personality = type == CardType.Personality;
        var own = target.OwnerId == player.Id;
        CardInstance? Performer() => command.PerformerId is { } id && _state.Cards.TryGetValue(id, out var p) ? p : null;
        switch (category)
        {
            // Games of Will: "an enemy card with lower Gold Cost" than the performer's own Gold Cost (current Gold Costs).
            case EmperorP4Abilities.LowerGoldCostThanPerformer:
                return !own && InPlay(target) && Performer() is { } gowPerformer && CardGoldCost(target) < CardGoldCost(gowPerformer);
            // Preceding Reputation: "an enemy Personality with fewer keywords, ignoring 'Soul of' keywords" than the performer.
            case EmperorP4Abilities.FewerKeywordsThanPerformer:
                return personality && !own && Performer() is { } reputed && KeywordCountExceptSoulOf(target) < KeywordCountExceptSoulOf(reputed);
            // Yojimbo Assemble: an enemy Personality or Follower (the cards with a Force of their own) with Force lower than the Yojimbo's plus the highest Chi among his controller's Shugenja and Courtiers.
            case EmperorP4Abilities.YojimboAssembleTarget:
                return !own && type is CardType.Personality or CardType.Follower && Performer() is { } yojimbo && CardForce(target) < YojimboAssembleThreshold(yojimbo, player.Id);
            // Shinjo Sarang: "your Follower at any location" that could be transferred to the chosen performer.
            case EmperorP4Abilities.OwnFollowerForPerformer:
                return own && type == CardType.Follower && target.Zone == Zone.Attached && AtAnyLocation(Card(target.AttachedTo)) && Performer() is { } host && CanTransferTo(target, host);
            default:
                return P4WatchTargetAllows(category, command, player, source, target);
        }
    }

    // A target category exempt from "every Battle-timing target must be at the current battlefield" (its own location rule is part of the category).
    private static bool P4TargetAtAnyLocation(string category) => category == EmperorP4Abilities.OwnFollowerForPerformer || Wave736Abilities.IsAnyLocationTarget(category) || Wave737Abilities.IsAnyLocationTarget(category);

    // Performer categories (validation and the QueryActions listing).
    private bool? EmperorP4PerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle) => ability.Performer switch
    {
        // Too Much for Mortals: "your performing unbowed Spirit" -- a Personality or a Follower with the keyword.
        EmperorP4Abilities.UnbowedSpiritPerformer => Definition(performer).Type is CardType.Personality or CardType.Follower && InPlay(performer) && !performer.Bowed && Keyword(performer, "Spirit")
            && (!actingAsBattle || AtBattle(performer)),
        _ => EmperorP4MorePerformerAllows(ability, performer, actingAsBattle)
    };

    private string?[]? EmperorP4Performers(AbilityDefinition ability, int playerId, bool actingAsBattle) => ability.Performer switch
    {
        EmperorP4Abilities.UnbowedSpiritPerformer => _state.Cards.Values.Where(c => c.OwnerId == playerId && EmperorP4PerformerAllows(ability, c, actingAsBattle) == true)
            .OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray(),
        _ => EmperorP4MorePerformers(ability, playerId, actingAsBattle)
    };

    // The per-card conditions of taking the action (ValidateAbility; also run for each slot candidate).
    private void ValidateEmperorP4(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        ValidateEmperorP4More(id, command);
        switch (id)
        {
            // "or one or two Personalities if you control a Courtier": a second target only while the acting player controls a Courtier.
            case EmperorP4Abilities.TenzanBattle:
            case EmperorP4Abilities.TenzanLimited:
                if (command.Selections?.FirstOrDefault(s => s.Slot == "targets")?.CardIds is { Length: > 1 })
                    Require(ControlsCourtier(player.Id), "Tenzan may target a second Personality only while you control a Courtier.");
                break;
        }
    }

    private bool ControlsCourtier(int playerId) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, "Courtier"));

    /// <summary>Yojimbo Assemble: the Yojimbo's Force plus the highest Chi among all Shugenja and Courtiers the player controls (0 when he controls none).</summary>
    private int YojimboAssembleThreshold(CardInstance yojimbo, int playerId)
    {
        var chi = _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && (Keyword(c, "Shugenja") || Keyword(c, "Courtier")))
            .Select(PersonalityChi).DefaultIfEmpty(0).Max();
        return CardForce(yojimbo) + chi;
    }

    // ---- execution: null when the id is not this group's; false when its effects are done; true when a decision opened (or an Attack's window) and the action waits ------------------------------

    private bool? RunEmperorP4(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        var name = source is null ? "" : Definition(source).Name;
        switch (id)
        {
            case EmperorP4Abilities.TenzanBattle:
            case EmperorP4Abilities.TenzanLimited:
                {
                    var targets = SlotCards(command, "targets").ToArray();
                    foreach (var personality in targets.Where(InPlay)) ApplyModifier(personality, source!.Id, id, "Force", -3);
                    // "If this is the Combat Segment" -- the Battle designator's use.
                    if (id == EmperorP4Abilities.TenzanBattle)
                        foreach (var personality in targets.Where(InPlay)) Dishonor(personality, player.Id);
                    return false;
                }
            case EmperorP4Abilities.GozaruBow:
                {
                    var targets = SlotCards(command, "targets").ToArray();
                    foreach (var card in targets.Where(InPlay)) BowByEffect(card, player.Id);
                    // "If you only targeted one card, destroy it" -- the number of targets chosen, whatever became of them.
                    if (targets.Length == 1 && InPlay(targets[0])) DestroyTargetCard(targets[0], player.Id);
                    return false;
                }
            case EmperorP4Abilities.SarangTransfer:
                {
                    var performer = Card(command.PerformerId!);
                    if (target is null || target.Zone != Zone.Attached || !InPlay(performer) || !CanTransferTo(target, performer))
                    {
                        Emit("transferFailed", $"{name}: the Follower can no longer be transferred to {Definition(performer).Name}.", player.Id, source?.Id);
                        return false;
                    }
                    TransferAttachment(target, performer, name);
                    if (Opposed(performer) || HasClanAffiliation(performer, "Spider")) Straighten(target);
                    return false;
                }
            case EmperorP4Abilities.NobukatsuRanged:
                P4.AttackDestroyedACard = false;
                if (ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant)) return true;
                return BeginNobukatsuFollowUp(command, player.Id, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            case EmperorP4Abilities.GamesOfWillBow:
                if (target is not null && InPlay(target)) BowByEffect(target, player.Id);
                return false;
            case EmperorP4Abilities.GamesOfWillNegate:
                {
                    var performer = Card(command.PerformerId!);
                    NegateForcePenalties(performer);
                    P4.ContributionShields[performer.Id] = _state.TurnNumber;
                    Emit("forceContributionShielded", $"{name}: effects from other players' cards that prevent {Definition(performer).Name} from contributing Force to his army are negated this turn.", player.Id, performer.Id);
                    return false;
                }
            case EmperorP4Abilities.PillarTattoo:
                {
                    var monk = Card(command.PerformerId!);
                    ApplyModifier(monk, source!.Id, id, "Force", 3);
                    if (!InPlay(monk) || !Keyword(monk, "Earth")) return false;
                    var candidates = _state.Cards.Values.Where(c => c.OwnerId != player.Id && InPlay(c) && AtBattle(c)
                        && (Definition(c).Type == CardType.Follower || Definition(c).Type == CardType.Personality && !HasAttachments(c))).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
                    if (candidates.Length == 0) return false;
                    var options = candidates.Select(c => new EffectOption($"bow-{c.Id}", $"Bow {Definition(c).Name} ({c.Id})", [P4BowEffect + c.Id]))
                        .Append(new EffectOption("none", "Bow nothing", [])).ToArray();
                    SpendHandPlayedCard(player, source);
                    return BeginP4ListedChoice(command, player.Id, options, $"{name}: {Definition(monk).Name} is Earth -- you may target and bow an enemy card without attachments.",
                        consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
                }
            case EmperorP4Abilities.PrecedingReputation:
                if (target is not null && InPlay(target))
                    foreach (var member in UnitMembers(target).ToArray())
                        if (InPlay(member) && !HasAttachments(member)) BowByEffect(member, player.Id);
                return false;
            case EmperorP4Abilities.TooMuchForMortals:
                {
                    if (target is null || !InPlay(target)) return false;
                    var attachments = Attachments(target.Id).Where(InPlay).OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
                    var options = new[] { new EffectOption("bow", $"Bow {Definition(target).Name}", [P4BowEffect + target.Id]) }
                        .Concat(attachments.Select(a => new EffectOption($"destroy-{a.Id}", $"Destroy {Definition(a).Name} ({a.Id})", [P4DestroyEffect + a.Id]))).ToArray();
                    SpendHandPlayedCard(player, source);
                    return BeginP4ListedChoice(command, player.Id, options, $"{name}: bow {Definition(target).Name} or destroy one of his attachments.",
                        consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
                }
            case EmperorP4Abilities.YojimboAssemble:
                if (target is null || !InPlay(target)) return false;
                BowByEffect(target, player.Id);
                if (InPlay(target) && !HasAttachments(target)) DestroyTargetCard(target, player.Id);
                return false;
            case EmperorP4Abilities.RideTheBreeze:
                {
                    if (target is null || !InPlay(target)) return false;
                    var wasAway = target.Zone == Zone.Battlefield;
                    MoveHome(target, false);
                    var moved = wasAway && target.Zone == Zone.Home;
                    var monk = Card(command.PerformerId!);
                    // "negate the Personality's (future) movement": the movement after the one the card caused, until the end of the turn (Strength of the Tsunami's ruling, MovementLocks.cs).
                    if (moved && Keyword(monk, "Air")) LockMovement(target, source!.Id, MovementKinds.Home | MovementKinds.ToBattlefield);
                    return false;
                }
            default:
                return RunEmperorP4Watches(id, command, player, source, target) ?? RunEmperorP4Units(id, ability, command, player, source, target)
                    ?? RunEmperorP4More(id, ability, command, player, source, target, consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
        }
    }

    // ---- listed choices of the acting player (EffectChoice.cs) ------------------------------------------------------------------------------------------------------------------------------

    // Pauses the action for deciderId to pick one of the listed options; the action finishes once it is answered (ResolveEffectChoice). A single option applies at once.
    private bool BeginP4ListedChoice(GameCommand command, int deciderId, EffectOption[] options, string prompt, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        if (options.Length == 0) return false;
        if (options.Length == 1)
        {
            foreach (var effect in options[0].Effects) ApplyEmperorP4BranchEffect(effect, command);
            return false;
        }
        _state.PendingEffectChoice = new()
        {
            Command = command,
            PlayerId = deciderId,
            Negated = _state.NegatedEffects.ToList(),
            ConsumedGrant = consumedGrant,
            PlayedFromDiscard = playedFromDiscard,
            OpponentHonorBefore = opponentHonorBefore,
            BowedBefore = bowedBefore.ToList(),
            PreExistingGrants = preExistingGrants.ToList(),
            Options = options.ToList()
        };
        _state.PendingChoice = new("effectChoice", deciderId, command.CardId, prompt, command.PlayerId);
        _state.PriorityPlayerId = deciderId; _state.ConsecutivePasses = 0;
        return true;
    }

    // ApplyBranchEffect's arm for this group's listed options; false for any other effect.
    private bool ApplyEmperorP4BranchEffect(string effect, GameCommand command)
    {
        if (effect.StartsWith(P4BowEffect, StringComparison.Ordinal))
        {
            var card = Card(effect[P4BowEffect.Length..]);
            if (InPlay(card)) BowByEffect(card, command.PlayerId);
            return true;
        }
        if (effect.StartsWith(P4DestroyEffect, StringComparison.Ordinal))
        {
            var card = Card(effect[P4DestroyEffect.Length..]);
            if (InPlay(card)) DestroyTargetCard(card, command.PlayerId);
            return true;
        }
        if (effect.StartsWith(P4StraightenEffect, StringComparison.Ordinal))
        {
            var card = Card(effect[P4StraightenEffect.Length..]);
            if (InPlay(card)) Straighten(card);
            return true;
        }
        return ApplyEmperorP4MoreBranchEffect(effect, command);
    }

    // ---- Tsuruchi Nobukatsu ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    // ResolveAttackNow's hook: whether this Attack destroyed its target (a card in play before, gone after).
    private void AfterP4Attack(string abilityId, string? targetId, bool targetInPlayBefore)
    {
        if (abilityId != EmperorP4Abilities.NobukatsuRanged) return;
        P4.AttackDestroyedACard = targetId is not null && targetInPlayBefore && !InPlay(Card(targetId));
    }

    // "If the Ranged Attack destroyed a card, you may target and straighten a Holding." Asked only when the Attack destroyed a card and some Holding is bowed (straightening an unbowed one does nothing).
    private bool BeginNobukatsuFollowUp(GameCommand command, int playerId, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore,
        List<AdditionalActionGrant> preExistingGrants)
    {
        if (!P4.AttackDestroyedACard) return false;
        P4.AttackDestroyedACard = false;
        var holdings = _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type == CardType.Holding && c.Bowed).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        if (holdings.Length == 0) return false;
        var source = command.CardId is null ? null : Card(command.CardId);
        var options = holdings.Select(h => new EffectOption($"straighten-{h.Id}", $"Straighten {Definition(h).Name} ({h.Id}, {Player(h.OwnerId).Name}'s)", [P4StraightenEffect + h.Id]))
            .Append(new EffectOption("none", "Straighten no Holding", [])).ToArray();
        return BeginP4ListedChoice(command, playerId, options, $"{(source is null ? "Tsuruchi Nobukatsu" : Definition(source).Name)}: the Ranged Attack destroyed a card -- you may target and straighten a Holding.",
            consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
    }

    // The deferred Attack's own completion (ResolveAttack), Lessons Never Forgotten's shape: every card bowed now counts as bowed before (an Attack bows nothing).
    private bool BeginP4FollowUpAfterDeferredAttack(PendingAttack attack, List<AdditionalActionGrant> grantsBeforeAttack)
    {
        if (attack.AbilityId != EmperorP4Abilities.NobukatsuRanged || attack.Command is null) return false;
        var bowedNow = _state.Cards.Values.Where(c => c.Bowed).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        return BeginNobukatsuFollowUp(attack.Command, attack.PlayerId, attack.Additional, false, null, bowedNow, grantsBeforeAttack);
    }

    // ---- Gozaru no Oni: "After Gozaru is destroyed: It becomes discarded (not dead)." -------------------------------------------------------------------------------------------------------

    // DestroyUnits' hook, after the card has become dead: the trait moves him to his owner's Dynasty discard pile.
    private void P4AfterDestroyed(CardInstance card)
    {
        if (Definition(card).Effect != EmperorP4Abilities.GozaruDiscardedEffect || card.Zone != Zone.Dead) return;
        card.Zone = Zone.DynastyDiscard;
        Emit("becameDiscarded", $"{Definition(card).Name} was destroyed: it becomes discarded (not dead).", card.OwnerId, card.Id);
    }

    // ---- Games of Will: the contribution shield ---------------------------------------------------------------------------------------------------------------------------------------------

    // NegateForceContribution's hook: who made the card stop contributing (the player whose action is resolving).
    private void NoteForceContributionNegated(CardInstance card)
    {
        if (_runningCommand is { } running) P4.ContributionNegatedBy[card.Id] = running.PlayerId;
        else P4.ContributionNegatedBy.Remove(card.Id);
    }

    // ForceContributionNegated's hook: true when Games of Will negates the effect that stops the card contributing (an effect from another player's card).
    private bool GamesOfWillShields(CardInstance card) =>
        P4.ContributionShields.TryGetValue(card.Id, out var turn) && turn == _state.TurnNumber
        && (!P4.ContributionNegatedBy.TryGetValue(card.Id, out var by) || by != card.OwnerId);
}

// This property keeps its original JSON name and tail position in old saved MatchState payloads.
public sealed partial class MatchState
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1004)]
    public EmperorP4State P4 { get; set; } = new();
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP4Hooks()
    {
        EngineHooks.Abilities.Register("AddEmperorP4", 2600, static abilities => abilities.AddEmperorP4());
        EngineHooks.AttackStrengthAfter.Register("P4AttackStrength", 400, static (engine, context) => P4AttackStrength(context.abilityId));
        EngineHooks.AttackKind.Register("P4AttackKind", 400, static context => P4AttackKind(context.abilityId));
        EngineHooks.PerformerAllows.Register("EmperorP4PerformerAllows", 800, static (engine, context) => engine.EmperorP4PerformerAllows(context.ability, context.performer, context.actingAsBattle) == true);
        EngineHooks.TargetAllows.Register("EmperorP4TargetAllows", 1400, static (engine, context) => engine.EmperorP4TargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.PerformerCandidates.Register("EmperorP4Performers", 700, static (engine, context) => engine.EmperorP4Performers(context.ability, context.playerId, context.actingAsBattle));
        EngineHooks.DestroysTarget.Register("P4DestroysTarget", 500, static context => P4DestroysTarget(context.abilityId));
        EngineHooks.BowsTarget.Register("P4BowsTarget", 700, static context => P4BowsTarget(context.abilityId));
        EngineHooks.ValidateEarly.Register("ValidateEmperorP4", 1200, static (engine, context) => engine.ValidateEmperorP4(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorP4", 2100, static (engine, context) => engine.RunEmperorP4(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
    }
}
