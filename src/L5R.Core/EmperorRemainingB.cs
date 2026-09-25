namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), group B (strategy-wave-59). The declarative ability data lives here (added to AbilityCatalog.All by one hook); each card's printed
// text and the reading taken are at its entry. The execution, validation and listing code follows in the same file (a partial MatchEngine), reached from CardAbilities.cs by small
// additive hooks. Cards of the group that cannot be faithfully wired are recorded in tools/content/ee_remB_blockers.json and are not published.
public static class EmperorRemainingBSpecs
{
    /// <summary>Heavily Engaged: "Each player targets an unbowed Personality he controls there, if possible. Bow all targets." -- the acting player first, then the other (DeciderChoice.cs).</summary>
    public const string HeavilyEngagedFirst = "heavily-engaged-first", HeavilyEngagedSecond = "heavily-engaged-second";
    /// <summary>Mysterious Deaths: "Each player targets a unit he controls with the lowest total Gold Cost of all units he controls, if possible. Destroy all targets."</summary>
    public const string MysteriousDeathsFirst = "mysterious-deaths-first", MysteriousDeathsSecond = "mysterious-deaths-second";
    /// <summary>My Enemy's Mercy: "The enemy leader chooses to either bow him or move him home." (EitherOr.cs)</summary>
    public const string MercyBowOrHome = "my-enemys-mercy-bow-or-home";

    public static bool IsDeciderSpec(string spec) => spec is HeavilyEngagedFirst or HeavilyEngagedSecond or MysteriousDeathsFirst or MysteriousDeathsSecond;
    public static bool IsFirstDecider(string spec) => spec is HeavilyEngagedFirst or MysteriousDeathsFirst;
}

public static class EmperorRemainingBAbilities
{
    public static Dictionary<string, AbilityDefinition> AddEmperorRemainingB(this Dictionary<string, AbilityDefinition> all)
    {
        // Heart of Rokugan (oracle-l5r-3016, a Terrain): "Before this battle resolves: Give each of your Human Personalities and Followers at the current battlefield +1F, and give each
        // Nonhuman Personality and Follower there -1F." -- a mandatory member of the before-resolution set. Human is "without the Nonhuman keyword" (core rules); "there" names no owner,
        // so the -1F is every Nonhuman's at the battlefield, either side's, and the +1F is the Terrain's controller's own Humans. "Battle: If there are no Terrains ... Put this card into
        // play there" is the generic Terrain play rule.
        all["heart-of-rokugan-sweep"] = new("BeforeResolution", "Before this battle resolves: Give each of your Human Personalities and Followers at the current battlefield +1F, and give each Nonhuman Personality and Follower there -1F",
            "none", Trigger: "before-battle-resolves");
        // Hard Pressed (oracle-l5r-2972, a Terrain): "Before this battle's resolution: Move a unit home from the current battlefield. You may bow it." -- the Terrain's controller chooses the
        // unit (either side's, mandatory while any unit stands there); a unit is moved through its Personality (core rules, Units), and "bow it" bows that Personality.
        all["hard-pressed-move"] = new("BeforeResolution", "Before this battle's resolution: Move a unit home from the current battlefield. You may bow it",
            "none", Trigger: "before-battle-resolves");
        // Ogre Savagery (oracle-l5r-5681, Fear): "Battle: Choose your performing unbowed Nonhuman Shadowlands Personality and target an enemy Personality: Bow him."
        all["ogre-savagery-bow"] = new("Battle", "Choose your performing unbowed Nonhuman Shadowlands Personality and target an enemy Personality: Bow him", "enemy-personality",
            Performer: "own-unbowed-nonhuman-shadowlands-at-battle");
        // Relentless Conviction (oracle-l5r-6247, Unique): "Battle: Target a dishonorable Personality: Destroy him." -- either side's.
        all["relentless-conviction-destroy"] = new("Battle", "Target a dishonorable Personality: Destroy him", "dishonorable-personality");
        // Murderous Intent (oracle-l5r-5444): "Battle/Open: Choose your performing Paragon: Give him +1F for each of his keywords except 'Soul of' keywords. Lose 4 Honor." The keywords are
        // counted as the action resolves: each distinct keyword the Paragon has now, printed or granted (Keyword()), a "Soul of ..." keyword excepted.
        all["murderous-intent-boost"] = new("BattleOrOpen", "Choose your performing Paragon: Give him +1F for each of his keywords except \"Soul of\" keywords. Lose 4 Honor", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Paragon"]);
        // In the Heart of Battle (oracle-l5r-3725, Bushido Virtue, Duty): "Battle/Open: Choose your performing Samurai: Straighten him and give him +3F. Destroy him before this turn ends."
        // -- the delayed destruction is the one every "until before this turn ends" card shares (DelayedDestruction.cs): carried out at the top of EndTurn if he is still in play.
        all["heart-of-battle-surge"] = new("BattleOrOpen", "Choose your performing Samurai: Straighten him and give him +3F. Destroy him before this turn ends", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Samurai"]);
        // Guided by Honor (oracle-l5r-2919): "Battle: If any enemy units are at the current battlefield, choose your performing unbowed Paragon: Move him to the current battlefield. If he moved,
        // you may target and bow an enemy Personality with equal or lower Personal Honor." The condition sits before the colon, so it gates taking the action (Moving and Unmoving's own reading);
        // the optional target is a slot of 0 to 1 cards chosen with the action (Running Battle's precedent), used only if he moved. An action that moves a Personality to the battle (MovesToBattle).
        all["guided-by-honor-move"] = new("Battle", "If any enemy units are at the current battlefield, choose your performing unbowed Paragon: Move him to the current battlefield. If he moved, you may target and bow an enemy Personality with equal or lower Personal Honor",
            "slots", Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Paragon"],
            Slots: [new("target", "enemy-personality-honor-lte-performer", "An enemy Personality with equal or lower Personal Honor (optional)", 0, 1)],
            Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield), MovesToBattle: true);
        // Nerve Strike (oracle-l5r-5532, Kiho): "Battle: Bow your performing Monk and target an enemy Personality with equal or lower Chi: Reduce his Force and the Force of each of his Followers to
        // 0. Cards in that unit may not perform actions until this battle ends." -- the lock is scoped to the battle (ActionLock.cs, duration "Battle").
        all["nerve-strike-freeze"] = new("Battle", "Bow your performing Monk and target an enemy Personality with equal or lower Chi: Reduce his Force and the Force of each of his Followers to 0. Cards in that unit may not perform actions until this battle ends",
            "enemy-personality-chi-lte-performer", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Monk"], Costs: ActionCost.BowPerformer);
        // Heavily Engaged (oracle-l5r-3037): "Battle: Even if you control no units at the current battlefield: Each player targets an unbowed Personality he controls there, if possible. Bow all
        // targets." (core rules: the card's text wins over the Rule of Presence.) Each player's choice is a decision of his own (DeciderChoice.cs), the acting player's first.
        all["heavily-engaged-bow"] = new("Battle", "Even if you control no units at the current battlefield: Each player targets an unbowed Personality he controls there, if possible. Bow all targets", "none", Absent: true);
        // Mysterious Deaths (oracle-l5r-5468, Unique, Kolat): "Limited: Each player targets a unit he controls with the lowest total Gold Cost of all units he controls, if possible. Destroy all
        // targets. Remove this card from the game. Lose 4 Honor."
        all["mysterious-deaths-destroy"] = new("Limited", "Each player targets a unit he controls with the lowest total Gold Cost of all units he controls, if possible. Destroy all targets. Remove this card from the game. Lose 4 Honor", "none");
        // My Enemy's Mercy (oracle-l5r-5457, Bushido Virtue, Compassion): "Battle: Choose your performing unbowed Samurai and target an enemy Personality with equal or lower Personal Honor: The enemy
        // leader chooses to either bow him or move him home. Gain 2 Honor." "Him" is read as the target (the enemy Personality: the enemy's own leader picks between two lesser evils for him);
        // the enemy leader is the other player of the battle (core rules: Leader).
        all["my-enemys-mercy-choose"] = new("Battle", "Choose your performing unbowed Samurai and target an enemy Personality with equal or lower Personal Honor: The enemy leader chooses to either bow him or move him home. Gain 2 Honor",
            "enemy-personality-honor-lte-performer", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Samurai"]);
        // Opportunistic Advance (oracle-l5r-5766): "Battle: Choose your performing unbowed Commander and target another Personality in his army: Move the target to a battlefield." -- another Personality of
        // the Commander's army (his controller's at the current battlefield); the battlefield is chosen among the other unresolved ones (Paralyzing Touch's "a different battlefield").
        all["opportunistic-advance-move"] = new("Battle", "Choose your performing unbowed Commander and target another Personality in his army: Move the target to a battlefield", "own-personality-in-performer-army",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Commander"], Move: new(MoveSpec.Target, MoveSpec.AnyUnresolved));
        // Introspection (oracle-l5r-3793): "Reaction: After one of your Rings enters play: Search your Fate deck for a card. Put it in your hand." Yung's trigger, played from a hand.
        all["introspection-search"] = new("Reaction", "After one of your Rings enters play: Search your Fate deck for a card. Put it in your hand", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved, Search: SearchCatalog.Introspection);
        // Martial Instruction (oracle-l5r-4843, Kiho): "Open: Bow your performing Monk and remove this card from the game: Search your Fate deck for a Kiho Battle Strategy, show it, and put it in your
        // hand." A Battle Strategy is a Strategy with a printed Battle action (Battle or Battle/Open).
        all["martial-instruction-search"] = new("Open", "Bow your performing Monk and remove this card from the game: Search your Fate deck for a Kiho Battle Strategy, show it, and put it in your hand", "none",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Monk"], Costs: [new(CostVerb.Bow, CostSubject.Performer), new(CostVerbs.Remove)], Search: SearchCatalog.MartialInstruction);
        // Meeting the Keepers (oracle-l5r-5018, Void): "Reaction: After the resolution of an action from one of your Rings: Search your Dynasty deck for an Event, show it, and put it on top of the deck
        // after shuffling." and "Open: Bow your target Ring: Draw two cards."
        all["keepers-search"] = new("Reaction", "After the resolution of an action from one of your Rings: Search your Dynasty deck for an Event, show it, and put it on top of the deck after shuffling", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved, Search: SearchCatalog.MeetingTheKeepers);
        all["keepers-draw"] = new("Open", "Bow your target Ring: Draw two cards", "own-unbowed-ring", Costs: ActionCost.BowTarget);
        // Ninja Tricks (oracle-l5r-5585, Ninja): "Reaction: After an action on a Strategy is announced, bow your performing Ninja Personality at the current battlefield: Negate the action's effects.
        // Lose 2 Honor." A Reaction played from a hand in the before-action window (any player's action whose source is a Strategy); the performing Ninja is bowed as the cost.
        all["ninja-tricks-negate"] = new("Reaction", "After an action on a Strategy is announced, bow your performing Ninja Personality at the current battlefield: Negate the action's effects. Lose 2 Honor", "none",
            Trigger: "before-action-resolves", Performer: "own-unbowed-personality-with-keyword-explicitly-at-battle", RequiredKeywords: ["Ninja"], Costs: ActionCost.BowPerformer);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- Performer categories (both switches: validation in ValidateAbility, listing in AbilityActions) ------------------------------------------------------------------------------
    private bool EmperorRemainingBPerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle) => ability.Performer switch
    {
        // Ogre Savagery: "your performing unbowed Nonhuman Shadowlands Personality" -- Shadowlands is a Clan alignment, not a printed keyword (HasClanAffiliation).
        "own-unbowed-nonhuman-shadowlands-at-battle" => Definition(performer).Type == CardType.Personality && !performer.Bowed && Keyword(performer, "Nonhuman") && HasClanAffiliation(performer, "Shadowlands")
            && (!actingAsBattle || AtBattle(performer)),
        _ => false
    };

    private string?[] EmperorRemainingBPerformers(AbilityDefinition ability, int playerId, bool actingAsBattle) => ability.Performer switch
    {
        "own-unbowed-nonhuman-shadowlands-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && EmperorRemainingBPerformerAllows(ability, c, actingAsBattle)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray(),
        _ => [null]
    };

    // ---- Target categories (ShadowsEmbraceBatch2TargetAllows' default) --------------------------------------------------------------------------------------------------------------
    private bool EmperorRemainingBTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        return category switch
        {
            // Opportunistic Advance: "another Personality in his army" -- a Personality of the performing Commander's controller at the current battlefield, not the Commander himself.
            "own-personality-in-performer-army" => personality && target.OwnerId == player.Id && target.Id != command.PerformerId && AtBattle(target),
            _ => false
        };
    }

    // ---- the per-card conditions of taking the action (ValidateAbility, after the per-id Requires) ---------------------------------------------------------------------------------------
    private void ValidateEmperorRemainingB(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // Guided by Honor: "If any enemy units are at the current battlefield".
            case "guided-by-honor-move":
                Require(_state.CurrentBattleProvince is int current && Army(1 - player.Id, current).Any(), "Guided by Honor requires enemy units at the current battlefield.");
                break;
            // Mysterious Deaths is a Limited action of the Action Phase like any other (ValidateAbility's timing check); nothing more is required to take it.
        }
    }

    // ---- execution (RunAbilityEffects' default): null when the id is not this pass's; false when its effects are done; true when a window or choice opened and the action waits ----------------
    private bool? RunEmperorRemainingB(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant,
        bool playedFromDiscard, int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case "ogre-savagery-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "relentless-conviction-destroy":
                DestroyTargetCard(target!, player.Id);
                return false;
            // Murderous Intent: +1F per keyword now (the Honor loss is an effect of the action, so a negation stops it with the rest).
            case "murderous-intent-boost":
            {
                var paragon = Card(command.PerformerId!);
                var count = KeywordCountExceptSoulOf(paragon);
                if (count > 0) ApplyModifier(paragon, source!.Id, id, "Force", count);
                Emit("murderousIntent", $"{Definition(paragon).Name} has {count} keyword{(count == 1 ? "" : "s")} besides \"Soul of\" keywords: +{count}F.", player.Id, paragon.Id);
                GainHonor(player.Id, -4, Definition(source!).Name, source);
                return false;
            }
            // In the Heart of Battle.
            case "heart-of-battle-surge":
            {
                var samurai = Card(command.PerformerId!);
                Straighten(samurai);
                ApplyModifier(samurai, source!.Id, id, "Force", 3);
                if (!_state.PendingDelayedDestructions.Contains(samurai.Id)) _state.PendingDelayedDestructions.Add(samurai.Id);
                Emit("destructionScheduled", $"{Definition(samurai).Name} will be destroyed before this turn ends.", player.Id, samurai.Id);
                return false;
            }
            // Guided by Honor: the move (his own; a negated or blocked move leaves him where he was), then the optional bow only if he really moved.
            case "guided-by-honor-move":
            {
                var paragon = Card(command.PerformerId!);
                var wasAtBattle = AtBattle(paragon);
                ExecuteMove(command, ability, source, target);
                var moved = !wasAtBattle && AtBattle(paragon);
                if (moved && SlotCards(command, "target").FirstOrDefault() is { } enemy && InPlay(enemy) && !StalwartNegatesBow(enemy, player.Id)) enemy.Bowed = true;
                return false;
            }
            // Nerve Strike.
            case "nerve-strike-freeze":
                NerveStrike(target!, source!, id);
                return false;
            // Heavily Engaged / Mysterious Deaths: each player's decision (DeciderChoice.cs).
            case "heavily-engaged-bow":
                return BeginDeciderChoice(EmperorRemainingBSpecs.HeavilyEngagedFirst, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            case "mysterious-deaths-destroy":
                return BeginDeciderChoice(EmperorRemainingBSpecs.MysteriousDeathsFirst, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            // My Enemy's Mercy: the Honor is gained whatever the enemy leader chooses; the choice then belongs to the enemy leader (EitherOr.cs).
            case "my-enemys-mercy-choose":
            {
                GainHonor(player.Id, 2, Definition(source!).Name, source);
                if (NegatesEffect(target!, "all"))
                {
                    Emit("effectsNegated", $"{Definition(target!).Name}: the action's effects were negated before they resolved.", target!.OwnerId, target.Id);
                    return false;
                }
                SpendHandPlayedCard(player, source);
                if (!consumedGrant) _state.AdditionalActions.RemoveAll(preExistingGrants.Contains);
                return OpenEitherOr(EmperorRemainingBSpecs.MercyBowOrHome, source!, player.Id, 1 - player.Id, target, "action", bowedBeforeAction,
                    stayWithActor: _state.AdditionalActions.Any(g => g.PlayerId == player.Id && Forfeitable(g)), performerId: command.PerformerId);
            }
            // Introspection / Meeting the Keepers' Reaction: a Strategy played from a hand is spent first; the window it was played in is set aside until the search is over (ReactionsAreActions.cs).
            case "introspection-search":
            case "keepers-search":
                SpendHandPlayedCard(player, source);
                SuspendOpenWindow();
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            // Martial Instruction: the Monk was bowed and the card removed as the costs.
            case "martial-instruction-search":
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            case "keepers-draw":
                Draw(player); Draw(player);
                return false;
            // Ninja Tricks: the deferred action's effects are negated (a Protected action's are not), and its player loses 2 Honor either way.
            case "ninja-tricks-negate":
                NegateDeferredAction(source!);
                GainHonor(player.Id, -2, Definition(source!).Name, source);
                return false;
            // Opportunistic Advance: the target moves to the chosen battlefield.
            case "opportunistic-advance-move":
                ExecuteMove(command, ability, source, target);
                return false;
            default:
                return null;
        }
    }

    // ---- Ninja Tricks / Meeting the Keepers: their triggers -----------------------------------------------------------------------------------------------------------------------------
    // A Strategy in a hand that may answer an action (the before-action window otherwise reads only cards in play).
    private static bool IsHandBeforeActionReactor(CardInstance card) =>
        Definition(card).Type == CardType.Strategy && (Definition(card).Abilities ?? []).Contains("ninja-tricks-negate");

    // "After an action on a Strategy is announced": any player's action whose source is a Strategy (not the very card being played), with a Ninja of the reactor's own, unbowed, at the current battlefield to
    // perform the Reaction. The Reaction itself is an action on a Strategy: another copy may answer it.
    private bool NinjaTricksApplies(CardInstance card, GameCommand command)
    {
        if (card.Zone != Zone.Hand || Definition(card).Type != CardType.Strategy || command.AbilityId is null || command.CardId == card.Id) return false;
        if (!AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || !IsAction(acting)) return false;
        if (command.CardId is null || !_state.Cards.TryGetValue(command.CardId, out var actingSource) || Definition(actingSource).Type != CardType.Strategy) return false;
        var owner = card.OwnerId;
        return _state.Phase == Phase.Battle && _state.CurrentBattleProvince is not null && Player(owner).Hand.Contains(card.Id)
            && _state.Cards.Values.Any(c => c.OwnerId == owner && Definition(c).Type == CardType.Personality && !c.Bowed && AtBattle(c) && Keyword(c, "Ninja"));
    }

    // "After the resolution of an action from one of your Rings": the resolved action's source is a Ring of the reactor's, whichever action it was.
    private bool RingActionResolved(ResolvedActionFacts facts, int playerId) =>
        facts.PlayerId == playerId && facts.SourceId is { } sourceId && _state.Cards.TryGetValue(sourceId, out var ring) && Definition(ring).Type == CardType.Ring && ring.OwnerId == playerId;

    // "+1F for each of his keywords except 'Soul of' keywords": the distinct keywords the card has now, printed or granted, less any it has lost.
    private int KeywordCountExceptSoulOf(CardInstance card) =>
        (Definition(card).Keywords ?? []).Concat(card.GrantedKeywords.Select(g => g.Keyword)).Distinct(StringComparer.Ordinal)
            .Count(k => !k.StartsWith("Soul of", StringComparison.OrdinalIgnoreCase) && Keyword(card, k));

    // ---- Nerve Strike -------------------------------------------------------------------------------------------------------------------------------------------------------------
    private const string ActionLockUntilBattleEnd = "Battle";

    private void NerveStrike(CardInstance target, CardInstance source, string id)
    {
        var unit = Attachments(target.Id).Where(a => Definition(a).Type == CardType.Follower).ToArray();
        foreach (var follower in unit) if (CardForce(follower) > 0) ApplyModifier(follower, source.Id, id, "Force", -CardForce(follower));
        if (CardForce(target) > 0) ApplyModifier(target, source.Id, id, "Force", -CardForce(target));
        foreach (var card in Attachments(target.Id).Prepend(target).ToArray()) LockActions(card, source.Id, ActionLockUntilBattleEnd);
        Emit("nerveStrike", $"{Definition(target).Name}'s Force and the Force of each of his Followers is 0, and the cards in his unit may not perform actions until this battle ends.", target.OwnerId, target.Id);
    }

    // CompleteBattleTransition's hook: the lock lapses with the battle.
    private void EndBattleActionLocks()
    {
        foreach (var card in _state.Cards.Values) card.ActionLocks.RemoveAll(l => l.Duration == ActionLockUntilBattleEnd);
    }

    // ---- Heart of Rokugan ---------------------------------------------------------------------------------------------------------------------------------------------------------
    private void HeartOfRokuganSweep(CardInstance terrain, PendingBeforeBattleResolve pending)
    {
        foreach (var personality in _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == pending.ProvinceIndex && Definition(c).Type == CardType.Personality)
                     .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            foreach (var card in Attachments(personality.Id).Where(a => Definition(a).Type == CardType.Follower).Prepend(personality).ToArray())
            {
                if (!InPlay(card)) continue;
                if (Keyword(card, "Nonhuman")) ApplyModifier(card, terrain.Id, "heart-of-rokugan-sweep", "Force", -1);
                else if (card.OwnerId == terrain.OwnerId) ApplyModifier(card, terrain.Id, "heart-of-rokugan-sweep", "Force", 1);
            }
        Emit("beforeBattleEffect", $"{Definition(terrain).Name}: each Human of {Player(terrain.OwnerId).Name}'s at this battlefield has +1F and each Nonhuman there -1F.", terrain.OwnerId, terrain.Id);
    }

    // ---- Hard Pressed: its before-resolution trait, answered through "beforeBattleOptional" ------------------------------------------------------------------------------------------------
    private bool IsHardPressedChoice(PendingBeforeBattleResolve pending) =>
        pending.ActiveSourceId is { } itemId && _state.Cards.TryGetValue(itemId, out var source) && BeforeResolutionAbilityId(source) == "hard-pressed-move";

    // "A unit at the current battlefield": the Personality that leads it, either side's.
    private IEnumerable<CardInstance> HardPressedCandidates(int provinceIndex) =>
        _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == provinceIndex && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal);

    // Opens the choice, or (no unit stands there) does nothing. True when the choice is open.
    private bool BeginHardPressed(CardInstance terrain, PendingBeforeBattleResolve pending)
    {
        if (!HardPressedCandidates(pending.ProvinceIndex).Any())
        {
            Emit("beforeBattleEffect", $"{Definition(terrain).Name}: no unit stands at this battlefield.", terrain.OwnerId, terrain.Id);
            return false;
        }
        _state.PendingChoice = new("beforeBattleOptional", terrain.OwnerId, terrain.Id, $"Before this battle resolves: move a unit home from this battlefield, and you may bow it ({Definition(terrain).Name}).");
        _state.PriorityPlayerId = terrain.OwnerId;
        return true;
    }

    // "use" names the unit (TargetId); CardIds = [that id] adds "you may bow it". There is no declining: the move is mandatory.
    private void ResolveHardPressed(GameCommand command, PendingBeforeBattleResolve pending, int playerId)
    {
        Require(command.Choice == "use", "Hard Pressed must move a unit home.");
        var legal = HardPressedCandidates(pending.ProvinceIndex).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        Require(command.TargetId is not null && legal.Contains(command.TargetId), "Choose a unit at this battlefield.");
        var bow = command.CardIds is { Length: > 0 };
        Require(!bow || command.CardIds is [var same] && same == command.TargetId, "Bow only the unit that moved.");
        var unit = Card(command.TargetId);
        MoveHome(unit, false);
        Emit("beforeBattleEffect", $"Hard Pressed: {Definition(unit).Name}'s unit was moved home.", playerId, unit.Id);
        if (bow && InPlay(unit) && !StalwartNegatesBow(unit, playerId)) { unit.Bowed = true; Emit("beforeBattleEffect", $"Hard Pressed: {Definition(unit).Name} was bowed.", playerId, unit.Id); }
    }

    private IEnumerable<ActionOption> HardPressedOptions(int playerId, string? sourceId)
    {
        if (_state.PendingBeforeBattleResolve is not { } pending || _state.PendingChoice?.PlayerId != playerId) yield break;
        foreach (var unit in HardPressedCandidates(pending.ProvinceIndex))
        {
            var name = Definition(unit).Name;
            yield return new("beforeBattleOptional", $"Move {name} home", true, null, new GameCommand("beforeBattleOptional", playerId, _state.Version, TargetId: unit.Id, Choice: "use"), unit.Id);
            yield return new("beforeBattleOptional", $"Move {name} home and bow him", true, null, new GameCommand("beforeBattleOptional", playerId, _state.Version, TargetId: unit.Id, CardIds: [unit.Id], Choice: "use"), unit.Id);
        }
    }

    // ---- Heavily Engaged / Mysterious Deaths (DeciderChoice.cs hooks) ---------------------------------------------------------------------------------------------------------------
    private string EmperorRemainingBDeciderPrompt(PendingDeciderChoice p) => p.SpecId switch
    {
        EmperorRemainingBSpecs.HeavilyEngagedFirst or EmperorRemainingBSpecs.HeavilyEngagedSecond =>
            $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose an unbowed Personality you control at the current battlefield. It is bowed.",
        _ => $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose a unit you control with the lowest total Gold Cost of all your units. It is destroyed."
    };

    // Heavily Engaged: the decider's unbowed Personalities at the current battlefield. Mysterious Deaths: the units (Personalities in play, at home or a battlefield) whose total Gold Cost
    // (the Personality and everything attached, UnitTotalGoldCost) is the lowest of all the decider's units.
    private (List<string> Candidates, int Min, int Max) EmperorRemainingBDeciderOptions(string specId, int deciderId)
    {
        if (specId is EmperorRemainingBSpecs.HeavilyEngagedFirst or EmperorRemainingBSpecs.HeavilyEngagedSecond)
            return (_state.CurrentBattleProvince is int battle ? Army(deciderId, battle).Where(c => !c.Bowed).Select(c => c.Id).Order(StringComparer.Ordinal).ToList() : [], 1, 1);
        var units = _state.Cards.Values.Where(c => c.OwnerId == deciderId && InPlay(c) && Definition(c).Type == CardType.Personality).ToArray();
        if (units.Length == 0) return ([], 1, 1);
        var lowest = units.Min(c => UnitTotalGoldCost(c, out _));
        return (units.Where(c => UnitTotalGoldCost(c, out _) == lowest).Select(c => c.Id).Order(StringComparer.Ordinal).ToList(), 1, 1);
    }

    private void ApplyEmperorRemainingBDeciderChoice(PendingDeciderChoice p)
    {
        var source = Card(p.SourceCardId); var name = Definition(source).Name;
        var chosen = p.EarlierChosen.Concat(p.Chosen).Distinct().ToList();
        if (EmperorRemainingBSpecs.IsFirstDecider(p.SpecId))
        {
            // The first player's target is named; the other player's is asked next (with nothing to choose, nothing is asked).
            var second = p.SpecId == EmperorRemainingBSpecs.HeavilyEngagedFirst ? EmperorRemainingBSpecs.HeavilyEngagedSecond : EmperorRemainingBSpecs.MysteriousDeathsSecond;
            var other = 1 - p.ActingPlayerId;
            var (candidates, min, max) = EmperorRemainingBDeciderOptions(second, other);
            var next = new PendingDeciderChoice
            {
                SpecId = second, SourceCardId = p.SourceCardId, ActingPlayerId = p.ActingPlayerId, DeciderPlayerId = other, Candidates = candidates, Min = min, Max = max, EarlierChosen = chosen, Paused = p.Paused
            };
            // Nothing to decide (no candidate, or only the one a mandatory choice needs): announced and applied, as for the first decider (BeginDeciderChoice).
            if (candidates.Count <= min)
            {
                if (candidates.Count > 0) Emit("deciderAutoResolved", $"{name}: {Player(other).Name} has one choice.", other, source.Id);
                else Emit("deciderAutoResolved", $"{name}: {Player(other).Name} has nothing to choose.", other, source.Id);
                next.Chosen = candidates.Take(max).ToList();
                ApplyEmperorRemainingBDeciderChoice(next);
                return;
            }
            OpenDeciderChoice(next);
            return;
        }
        if (p.SpecId == EmperorRemainingBSpecs.HeavilyEngagedSecond)
        {
            foreach (var id in chosen)
            {
                var card = Card(id);
                if (InPlay(card) && !StalwartNegatesBow(card, p.ActingPlayerId)) card.Bowed = true;
            }
            Emit("heavilyEngaged", $"{name}: {chosen.Count} Personalit{(chosen.Count == 1 ? "y was" : "ies were")} bowed.", p.ActingPlayerId, source.Id);
            return;
        }
        // Mysterious Deaths: "Destroy all targets. Remove this card from the game. Lose 4 Honor."
        DestroyUnits(chosen.Select(Card).Where(InPlay).ToArray(), false, p.ActingPlayerId);
        if (source.Zone is Zone.FateDiscard) RemoveFromGame(source);
        GainHonor(p.ActingPlayerId, -4, name, source);
    }

    // ---- My Enemy's Mercy (EitherOr.cs hooks) --------------------------------------------------------------------------------------------------------------------------------------
    private EitherOrOption[] MercyOptions(CardInstance? affected)
    {
        var name = Definition(affected!).Name;
        return [
            new("bow", $"Bow {name}", $"{name} bows.", EitherOrKind.Choose, 0, InPlay(affected!) && !affected!.Bowed, $"{name} is already bowed."),
            new("home", $"Move {name} home", $"{name}'s unit moves home.", EitherOrKind.Choose, 0, affected!.Zone == Zone.Battlefield, $"{name} is not at a battlefield.")
        ];
    }

    private void ApplyMercy(PendingEitherOr p, EitherOrOption option, PlayerState decider, CardInstance? affected)
    {
        var source = Card(p.SourceCardId);
        Emit("eitherOrChosen", $"{decider.Name} chose: {option.Label} ({Definition(source).Name}).", decider.Id, affected!.Id);
        if (!InPlay(affected)) return;
        if (option.Key == "bow") { if (!StalwartNegatesBow(affected, p.ActingPlayerId)) affected.Bowed = true; }
        else if (affected.Zone == Zone.Battlefield) MoveHome(affected, false);
    }
}
