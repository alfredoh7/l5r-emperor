namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), group D (strategy-wave-61): the Strategies The Law's Strength, The Light of Justice, The Power of One, The Power of a Word,
// The Serpent's Deception, The Slow Death, The Trap is Sprung!, Undetectable Enemy, Unseen Assailant, Useful Connections, Veiled Menace, Wall of Honor, Well Prepared, Words Have
// Strength and (the ones with a longer story, each documented at its own use below) Torch's Flame Flickers, White Shore Plain, The Killing Grounds, The Shadow Court and The
// Sincerity of the Dragon. The declarative data is here (added to AbilityCatalog.All by one hook); the shared engine files carry short, commented hooks into this file.
//
// New primitives:
//   * A "MOVE HOME" COST (CostVerbs.MoveHome): "Move your performing opposed Personality home:" (Wall of Honor) -- a cost, paid whatever happens to the effects.
//   * A WATCH ON EITHER SIDE'S ASSIGNMENT (WatchVeiledMenace): "After each time he is assigned (this turn), his controller loses 1 Honor" -- Social Grace's standing watch, but for
//     an assignment to either side (Midday Shadow Court's), and paying the Honor loss to the Personality's controller.
//   * A ONE-SHOT NEGATION OF THE NEXT BOWING OF A PLAYER'S PERSONALITIES AT A BATTLEFIELD (MatchState.TorchWatches, Torch's Flame Flickers).
public sealed record TorchWatch(int PlayerId, int ProvinceIndex, int Battle, string Source)
{
    /// <summary>The command whose bowing the watch negated: every other bowing of the same effect (a unit bowed as a whole) is negated with it. Reference identity.</summary>
    public object? FiredFor { get; set; }
    public bool Fired { get; set; }
}

public static class EmperorRemainingDAbilities
{
    public static Dictionary<string, AbilityDefinition> AddEmperorRemainingD(this Dictionary<string, AbilityDefinition> all)
    {
        // The Slow Death (oracle-l5r-8340): "As a Focus Effect: After this duel ends, give its winner +3F." / "Battle: Target an opposed unit: Straighten it." -- Sudden Movement's
        // Focus Effect; the straighten is the whole unit's (the reading of every "it" that names a unit, Moto Naleesh's bow).
        all["slow-death-focus"] = new("FocusEffect", "As a Focus Effect: After this duel ends, give its winner +3F", "none");
        all["slow-death-straighten"] = new("Battle", "Target an opposed unit: Straighten it", "rd-opposed-unit");
        // Well Prepared (oracle-l5r-9287): "Battle: If you control a Terrain, target a card in a unit: Bow or straighten the target." -- Yoritomo Hama's pair sharing one use.
        all["well-prepared-bow"] = new("Battle", "If you control a Terrain, target a card in a unit: Bow the target", "any-unit-card", UsageKey: "well-prepared");
        all["well-prepared-straighten"] = new("Battle", "If you control a Terrain, target a card in a unit: Straighten the target", "any-unit-card", UsageKey: "well-prepared");
        // Undetectable Enemy (oracle-l5r-8961): "Reaction: After another player's action targets your Oni: Negate its effects." -- Taishuu's whole-action negation, an Oni for a Political action.
        all["undetectable-enemy-negate"] = new("Reaction", "After another player's action targets your Oni: Negate its effects", "none", Trigger: ReactionTriggers.TargetedByAction);
        // Unseen Assailant (oracle-l5r-9011, a Ninja card): "Battle: Bow your performing Ninja: Ranged 3 Attack. If the Ranged Attack targeted a Personality, you may compare it against his Chi."
        all["unseen-assailant-attack"] = new("Battle", "Bow your performing Ninja: Ranged 3 Attack. If the Ranged Attack targeted a Personality, you may compare it against his Chi", "attack",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Ninja"], Costs: ActionCost.BowPerformer);
        // The Trap is Sprung! (oracle-l5r-8394): "Battle: If you have Reconnaissance, choose your performing unbowed Scout Personality, and target an enemy card: Bow it. Bow its unit if
        // you are a Crane Clan player."
        all["trap-sprung-bow"] = new("Battle", "If you have Reconnaissance, choose your performing unbowed Scout Personality, and target an enemy card: Bow it. Bow its unit if you are a Crane Clan player", "enemy-card",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Scout"]);
        // Veiled Menace (oracle-l5r-9130): "Open: Choose your performing unbowed Courtier and target a dishonorable Personality: His controller loses 2 Honor. After each time he is
        // assigned (this turn), his controller loses 1 Honor."
        all["veiled-menace-loss"] = new("Open", "Choose your performing unbowed Courtier and target a dishonorable Personality: His controller loses 2 Honor. After each time he is assigned (this turn), his controller loses 1 Honor",
            "dishonorable-personality", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Courtier"]);
        // Useful Connections (oracle-l5r-9046): "Open: Bow your performing Courtier and target one or more Personalities, up to an amount equal to your Courtier's Chi: Give Cavalry to them
        // and to each of their Followers." -- one or more (the Chi limit is a per-id Require: a slot's count is fixed data).
        all["useful-connections-cavalry"] = new("Open", "Bow your performing Courtier and target one or more Personalities, up to an amount equal to your Courtier's Chi: Give Cavalry to them and to each of their Followers", "slots",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Courtier"], Costs: ActionCost.BowPerformer,
            Slots: [new("targets", "personality", "One or more Personalities, up to your Courtier's Chi", 1, 12)]);
        // Wall of Honor (oracle-l5r-9198): "Battle: Move your performing opposed Personality home: Increase or decrease the strength of the current battlefield's province by an amount
        // equal to the number of units on the enemy side there. Gain 2 Honor." -- the move home is a cost; "increase or decrease" is two entries sharing one use (Kusugi's shape).
        all["wall-of-honor-raise"] = new("Battle", "Move your performing opposed Personality home: Increase the strength of the current battlefield's province by an amount equal to the number of units on the enemy side there. Gain 2 Honor", "none",
            Performer: "own-personality-opposed-at-battle", UsageKey: "wall-of-honor", Costs: [new(CostVerbs.MoveHome, CostSubject.Performer)]);
        all["wall-of-honor-lower"] = new("Battle", "Move your performing opposed Personality home: Decrease the strength of the current battlefield's province by an amount equal to the number of units on the enemy side there. Gain 2 Honor", "none",
            Performer: "own-personality-opposed-at-battle", UsageKey: "wall-of-honor", Costs: [new(CostVerbs.MoveHome, CostSubject.Performer)]);
        // The Law's Strength (oracle-l5r-8194, Political): "Battle: Choose your performing Courtier or Magistrate and target a Personality: Move him home. If this did not move him home,
        // destroy him."
        all["laws-strength-move"] = new("Battle", "Choose your performing Courtier or Magistrate and target a Personality: Move him home. If this did not move him home, destroy him", "personality",
            Performer: "rd-own-personality-with-any-keyword-at-battle", RequiredKeywords: ["Courtier", "Magistrate"]);
        // The Light of Justice (oracle-l5r-8199, Imperial Political): "Battle: If any enemy units are at the current battlefield, choose your performing Magistrate Personality at home: Move him to
        // the current battlefield. If he moved, straighten his unit. If any dishonorable Personalities are now opposing him, you may take an additional Battle action."
        all["light-of-justice-move"] = new("Battle", "If any enemy units are at the current battlefield, choose your performing Magistrate Personality at home: Move him to the current battlefield. If he moved, straighten his unit. If any dishonorable Personalities are now opposing him, you may take an additional Battle action", "none",
            Performer: "own-personality-with-keyword", RequiredKeywords: ["Magistrate"], MovesToBattle: true);
        // The Power of One (oracle-l5r-8265): "Battle/Open: Choose your performing Commander: Straighten his unit." / "Battle: Choose your performing unbowed Commander and target one or two
        // enemy units with a combined total Gold Cost equal to or lower than your Commander's unit's total Gold Cost: Bow the targets."
        all["power-one-straighten"] = new("BattleOrOpen", "Choose your performing Commander: Straighten his unit", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Commander"]);
        all["power-one-bow"] = new("Battle", "Choose your performing unbowed Commander and target one or two enemy units with a combined total Gold Cost equal to or lower than your Commander's unit's total Gold Cost: Bow the targets", "slots",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Commander"],
            Slots: [new("targets", "enemy-unit", "One or two enemy units", 1, 2)]);
        // The Power of a Word (oracle-l5r-8262, Political): "Reaction: After another player's Battle action targets your Courtier: The player may choose to lose 3 Honor. If he did not choose
        // this, negate the action's effects." / "Battle: Target one to three enemy dishonorable Personalities: The shadow of their shame shall fall upon their shoulders. Move them home."
        all["power-word-reaction"] = new("Reaction", "After another player's Battle action targets your Courtier: The player may choose to lose 3 Honor. If he did not choose this, negate the action's effects", "none", Trigger: ReactionTriggers.TargetedByAction);
        all["power-word-move"] = new("Battle", "Target one to three enemy dishonorable Personalities: Move them home", "slots",
            Slots: [new("targets", "enemy-personality", "One to three enemy dishonorable Personalities", 1, 3, MustBe: TargetStatus.IsDishonorable)]);
        // The Serpent's Deception (oracle-l5r-8311, Fear Kiho): "Battle: Choose your performing Monk and target an enemy Personality whose unit has lower total Force than your Monk's unit:
        // Move the target home. Your Monk feigns his injuries. He contributes Force to his army even if bowed. Lose 3 Honor."
        all["serpents-deception-move"] = new("Battle", "Choose your performing Monk and target an enemy Personality whose unit has lower total Force than your Monk's unit: Move the target home. Your Monk feigns his injuries. He contributes Force to his army even if bowed. Lose 3 Honor", "rd-enemy-personality-unit-force-lt-performer",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Monk"]);
        // Words Have Strength (oracle-l5r-9377, Political): "Reaction: After a battle's resolution, or the resolution of another player's Battle action, if the resolution or action destroyed any
        // of your Courtiers or Magistrates: Choose a player, who loses 3 Honor." Two Reactions (the two triggers); the once-per-resolution limit is a Require.
        all["words-strength-resolution"] = new("Reaction", "After a battle's resolution, if the resolution destroyed any of your Courtiers or Magistrates: Choose a player, who loses 3 Honor", "player", Trigger: MatchEngine.AfterResolutionTriggerId, UsageKey: "words-strength");
        all["words-strength-action"] = new("Reaction", "After the resolution of another player's Battle action, if the action destroyed any of your Courtiers or Magistrates: Choose a player, who loses 3 Honor", "player", Trigger: AfterActionReactionTriggers.AfterActionResolved, UsageKey: "words-strength");
        return all;
    }
}

public sealed partial class MatchEngine
{
    internal static string AfterResolutionTriggerId => AfterResolutionTrigger;

    // The Well Prepared / Power of One style "unit" helpers -------------------------------------------------------------------------------------------------------------------

    // "An opposed unit": a Personality (and so his unit) at a battlefield whose other side has at least one unit.
    private bool OpposedUnit(CardInstance card) =>
        Definition(card).Type == CardType.Personality && card.Zone == Zone.Battlefield && card.ProvinceIndex is int province && Army(1 - card.OwnerId, province).Any();

    // ---- validation (ValidateAbility, after the per-id Requires; also run for each candidate of a slot, with ability.Slots null) ----

    private void ValidateEmperorRemainingD(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // Well Prepared: "If you control a Terrain" -- a condition on taking the action.
            case "well-prepared-bow":
            case "well-prepared-straighten":
                Require(ControlsKeyword(player.Id, "Terrain"), "You do not control a Terrain.");
                break;
            // The Trap is Sprung!: "If you have Reconnaissance".
            case "trap-sprung-bow":
                Require(HasReconnaissanceAtCurrentBattlefield(player.Id), "You do not have Reconnaissance at the current battlefield.");
                break;
            // Useful Connections: "up to an amount equal to your Courtier's Chi" (the whole selection; a slot's own candidate is judged alone).
            case "useful-connections-cavalry" when ability.Slots is not null && command.PerformerId is not null:
                Require(SlotCards(command, "targets").Count() <= PersonalityChi(Card(command.PerformerId)), "You may target at most as many Personalities as your Courtier's Chi.");
                break;
            // Wall of Honor: the move home is paid as a cost, so the performer must be able to move (no lock on his movement home).
            case "wall-of-honor-raise":
            case "wall-of-honor-lower":
                if (command.PerformerId is not null) Require(!MovementLocked(Card(command.PerformerId), MovementKinds.Home), "The performing Personality may not move this turn.");
                break;
            // The Light of Justice: "If any enemy units are at the current battlefield" and "your performing Magistrate Personality at home".
            case "light-of-justice-move":
                Require(_state.CurrentBattleProvince is int justiceBattle && Army(1 - player.Id, justiceBattle).Any(), "There are no enemy units at the current battlefield.");
                if (command.PerformerId is not null) Require(Card(command.PerformerId).Zone == Zone.Home, "The performing Magistrate must be at home.");
                break;
            // The Power of One: the combined total Gold Cost of the targeted units is at most the Commander's unit's total Gold Cost. A slot's candidate is judged alone (it must fit by
            // itself), the whole selection together.
            case "power-one-bow" when command.PerformerId is not null:
            {
                var reach = UnitTotalGoldCost(Card(command.PerformerId), out _);
                if (ability.Slots is null)
                {
                    if (command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var candidate) && Definition(candidate).Type == CardType.Personality)
                        Require(UnitTotalGoldCost(candidate, out _) <= reach, "That unit's total Gold Cost is above your Commander's unit's.");
                }
                else Require(SlotCards(command, "targets").Sum(c => UnitTotalGoldCost(c, out _)) <= reach, "The targets' combined total Gold Cost is above your Commander's unit's.");
                break;
            }
            // Words Have Strength: "You may only play one copy of Words Have Strength in reaction to any one resolution."
            case "words-strength-resolution":
                Require(_state.PendingAfterResolution is { WordsHaveStrengthPlayed: false }, "Words Have Strength was already played in reaction to this resolution.");
                break;
            case "words-strength-action":
                Require(_state.LastResolvedAction is { WordsHaveStrengthPlayed: false }, "Words Have Strength was already played in reaction to this resolution.");
                break;
        }
    }

    // ---- Performer categories (the validation switch's fallback and its listing twin) ----

    private bool RemainingDPerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle) => ability.Performer switch
    {
        // The Law's Strength: "your performing Courtier or Magistrate" -- any one of the RequiredKeywords, at the battle while acting as a Battle action; bowed or not.
        "rd-own-personality-with-any-keyword-at-battle" => Definition(performer).Type == CardType.Personality && (ability.RequiredKeywords ?? []).Any(k => Keyword(performer, k)) && (!actingAsBattle || AtBattle(performer)),
        _ => false
    };

    private string?[]? RemainingDPerformers(AbilityDefinition ability, int playerId, bool actingAsBattle) => ability.Performer switch
    {
        "rd-own-personality-with-any-keyword-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality
            && (ability.RequiredKeywords ?? []).Any(k => Keyword(c, k)) && (!actingAsBattle || AtBattle(c))).Select(c => (string?)c.Id).ToArray(),
        _ => null
    };

    // ---- Target categories (the validation switch's fallback) ----

    private bool RemainingDTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        return category switch
        {
            "rd-opposed-unit" => OpposedUnit(target),
            // The Serpent's Deception: "an enemy Personality whose unit has lower total Force than your Monk's unit".
            "rd-enemy-personality-unit-force-lt-performer" => personality && target.OwnerId != player.Id && command.PerformerId is not null && UnitTotalForce(target) < UnitTotalForce(Card(command.PerformerId)),
            _ => false
        };
    }

    // ---- execution (RunAbilityEffects' default): null when the id is not this pass's; false when its effects are done; true when a window opened ----

    private bool? RunEmperorRemainingD(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant,
        bool playedFromDiscard, int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        var name = source is null ? "an action" : Definition(source).Name;
        switch (id)
        {
            case "slow-death-straighten":
                StraightenUnit(target!);
                return false;
            case "well-prepared-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "well-prepared-straighten":
                Straighten(target!);
                return false;
            case "undetectable-enemy-negate":
                NegateHeldAction(source!);
                return false;
            case "unseen-assailant-attack":
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            // "Bow it. Bow its unit if you are a Crane Clan player." Each bowing is an effect a bow negation may stop card by card.
            case "trap-sprung-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                if (player.Clan == "Crane")
                    foreach (var member in UnitMembers(target!).Where(m => m.Id != target!.Id).ToArray())
                        if (!StalwartNegatesBow(member, player.Id)) member.Bowed = true;
                return false;
            case "veiled-menace-loss":
                GainHonor(target!.OwnerId, -2, name, source);
                _state.AssignmentWatches.Add(new(target.Id, WatchVeiledMenace, player.Id, _state.TurnNumber, name));
                Emit("watchSet", $"{name}: after each time this turn {Definition(target).Name} is assigned, {Player(target.OwnerId).Name} loses 1 Honor.", player.Id, target.Id);
                return false;
            case "useful-connections-cavalry":
                foreach (var chosen in SlotCards(command, "targets").ToArray())
                {
                    GrantKeyword(chosen, "Cavalry", name, GrantDuration.Turn);
                    foreach (var follower in Attachments(chosen.Id).Where(a => Definition(a).Type == CardType.Follower).ToArray()) GrantKeyword(follower, "Cavalry", name, GrantDuration.Turn);
                }
                return false;
            case "wall-of-honor-raise":
            case "wall-of-honor-lower":
            {
                var battle = _state.CurrentBattleProvince!.Value;
                var units = Army(1 - player.Id, battle).Count();
                GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, battle, id == "wall-of-honor-raise" ? units : -units, name);
                GainHonor(player.Id, 2, name, source);
                return false;
            }
            // "Move him home. If this did not move him home, destroy him": a movement that was negated or locked leaves him at the battlefield, and then he is destroyed.
            case "laws-strength-move":
                MoveHome(target!, false);
                if (InPlay(target!) && target!.Zone == Zone.Battlefield) DestroyTargetCard(target, player.Id);
                return false;
            case "power-one-straighten":
                StraightenUnit(Card(command.PerformerId!));
                return false;
            case "power-one-bow":
                foreach (var unitTarget in SlotCards(command, "targets").ToArray())
                    foreach (var member in UnitMembers(unitTarget).ToArray())
                        if (!StalwartNegatesBow(member, player.Id)) member.Bowed = true;
                return false;
            case "power-word-reaction":
                return BeginPowerOfWordChoice(source!);
            case "power-word-move":
                foreach (var shamed in SlotCards(command, "targets").ToArray()) MoveHome(shamed, false);
                return false;
            case "serpents-deception-move":
            {
                var monk = Card(command.PerformerId!);
                MoveHome(target!, false);
                monk.DeepRootsTurn = _state.TurnNumber;
                Emit("contributesWhileBowed", $"{name}: {Definition(monk).Name} contributes Force to his army even if bowed this turn.", player.Id, monk.Id);
                GainHonor(player.Id, -3, name, source);
                return false;
            }
            case "words-strength-resolution":
            case "words-strength-action":
                if (id == "words-strength-resolution" && _state.PendingAfterResolution is { } resolution) resolution.WordsHaveStrengthPlayed = true;
                if (id == "words-strength-action" && _state.LastResolvedAction is { } resolved) resolved.WordsHaveStrengthPlayed = true;
                GainHonor(command.TargetPlayerId!.Value, -3, name, source);
                return false;
            default:
                return null;
        }
    }

    // ---- Veiled Menace ----

    /// <summary>Veiled Menace: "After each time he is assigned (this turn), his controller loses 1 Honor" -- an assignment to either side; the watch stays for the turn.</summary>
    public const string WatchVeiledMenace = "veiled-menace";

    private void FireVeiledMenaceAssigned(CardInstance card)
    {
        foreach (var watch in _state.AssignmentWatches.Where(w => w.CardId == card.Id && w.Kind == WatchVeiledMenace && w.Turn == _state.TurnNumber).ToArray())
            ResolveAssignmentWatch(watch, card, "was assigned");
    }

    // ---- The Power of a Word ----

    // "After another player's Battle action targets your Courtier" (a targeted-by-action reaction).
    private bool PowerOfWordApplies(CardInstance card, ReactionSubject subject, CardInstance affected) =>
        subject.Command.PlayerId != card.OwnerId && IsBattleAction(subject.Command) && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality && Keyword(affected, "Courtier");

    // "The player may choose to lose 3 Honor. If he did not choose this, negate the action's effects": the acting player decides (EitherOr.cs).
    private bool BeginPowerOfWordChoice(CardInstance source)
    {
        var acting = OpenReactionSubject()?.Command.PlayerId ?? 1 - source.OwnerId;
        return OpenEitherOr(EitherOrSpecs.PowerOfWordLoseOrNegate, source, acting, acting, null, "window");
    }

    // The refusal negates the held action: the window it was played in is what holds it, so it is read from there.
    private void NegateHeldActionFromEitherOr(PendingEitherOr pending, CardInstance source)
    {
        var current = _state.PendingChoice;
        _state.PendingChoice = pending.Suspended ?? current;
        try { NegateHeldAction(source); }
        finally { _state.PendingChoice = current; }
    }

    // ---- The Words Have Strength facts ----

    // "If the resolution destroyed any of your Courtiers or Magistrates": read off the cards the battle's resolution destroyed (now dead).
    private bool ResolutionDestroyedCourtierOrMagistrate(PendingAfterResolution facts, int playerId) =>
        facts.DestroyedCardIds.Any(id => _state.Cards.TryGetValue(id, out var c) && c.OwnerId == playerId && Definition(c).Type == CardType.Personality
            && c.Zone == Zone.Dead && (Keyword(c, "Courtier") || Keyword(c, "Magistrate")));

    private bool ActionDestroyedCourtierOrMagistrate(ResolvedActionFacts facts, int playerId) =>
        facts.Affected.Any(a => a.Kind == DestroyedKind && Card(a.CardId) is { } c && c.OwnerId == playerId && Definition(c).Type == CardType.Personality
            && (Keyword(c, "Courtier") || Keyword(c, "Magistrate")));
}
