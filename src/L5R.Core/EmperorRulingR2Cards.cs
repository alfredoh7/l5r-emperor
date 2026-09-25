namespace L5R.Core;

// ======================================================================================================================================================================================
// THE CARDS OF THE DELAY PASS (Emperor Edition ruling pass R2, issue #492; the mechanism is EmperorRulingR2.cs): Halls of the Forgotten (a Stronghold), the Strategies The Honor of the Lion, The
// Loyalty of the Scorpion, The Compassion of the Unicorn and The Courage of the Mantis (strategy-wave-63), The Strength of the Spider's Focus Effect (strategy-wave-13, published with its Battle half
// only until this mechanism existed) and the duel half of the delay: "As a Focus Effect: You may choose (now) to delay the effects of losing this duel until after the next Dynasty Phase begins".
//
// THE DUEL'S DELAY. The Focus Effects of a duel resolve after the duel's own outcome, but "(now)" is a choice about the outcome itself, so the delay Focus Effects are answered BEFORE the outcome
// applies (ResolveDuel, in the order the active player chose): each asks its player -- when one of that player's Personalities lost -- whether to delay. On a yes, every effect the duel's outcome (and a
// Duelist-tie's, Kakita Ichigiku's extra destruction ...) applies to that Personality's unit is recorded through the delay mechanism and applied when the next Dynasty Phase begins.
// READING: "the effects of losing this duel" are the effects on the delaying player's own losing Personality (a winner has none to delay), and are those of the duel's outcome; the Focus Effects of other
// cards resolve as they do.
// ======================================================================================================================================================================================
public static class EmperorRulingR2Abilities
{
    /// <summary>The Focus Effects "As a Focus Effect: You may choose (now) to delay the effects of losing this duel until after the next Dynasty Phase begins."</summary>
    public static readonly string[] DelayFocusEffects = ["honor-lion-focus", "loyalty-scorpion-focus", "compassion-unicorn-focus", "courage-mantis-focus", "honesty-phoenix-focus", "strength-spider-focus"];

    public static Dictionary<string, AbilityDefinition> AddEmperorRulingR2(this Dictionary<string, AbilityDefinition> all)
    {
        const string delayText = "As a Focus Effect: You may choose (now) to delay the effects of losing this duel until after the next Dynasty Phase begins";
        // Halls of the Forgotten (oracle-l5r-2950, a Crab Stronghold): "Reaction: After another player's Battle action targets your Berserker: Delay its effects on him until after this battle ends."
        // Every effect the action applies to him is recorded (the recording layer) and applied once the battle has ended, in the order it was applied, or negated if it cannot be.
        all["halls-forgotten-delay"] = new("Reaction", "After another player's Battle action targets your Berserker: Delay its effects on him until after this battle ends", "none", Trigger: ReactionTriggers.TargetedByAction);
        // The Honor of the Lion (oracle-l5r-8138): the Focus Effect / "Battle/Open: Choose your performing unbowed honorable Lion Clan Personality: Permanently give him a bonus raising his PH to 4 and the
        // ability, 'Battle: Target an enemy Personality with equal or lower Personal Honor: Bow him. Gain 2 Honor.'"
        all["honor-lion-focus"] = new("FocusEffect", delayText, "none");
        all["honor-lion-boost"] = new("BattleOrOpen", "Choose your performing unbowed honorable Lion Clan Personality: Permanently give him a bonus raising his PH to 4 and the ability, \"Battle: Target an enemy Personality with equal or lower Personal Honor: Bow him. Gain 2 Honor.\"", "none",
            Performer: "rr2-own-unbowed-honorable-clan-personality-at-battle", RequiredKeywords: ["Lion"]);
        all["honor-lion-bow"] = new("Battle", "Target an enemy Personality with equal or lower Personal Honor: Bow him. Gain 2 Honor", "rr2-enemy-personality-honor-lte-source");
        // The Loyalty of the Scorpion (oracle-l5r-8207): the Focus Effect / "Reaction: After an action targets your Scorpion Clan Personality, destroy your performing Scorpion Clan Personality: Negate either
        // the bowing, movement, or destruction of the target from the action's effects." -- three Reactions sharing one use (Hama's pair shape), one per effect it may negate.
        all["loyalty-scorpion-focus"] = new("FocusEffect", delayText, "none");
        foreach (var (suffix, what) in new[] { ("bow", "bowing"), ("move", "movement"), ("destroy", "destruction") })
            all[$"loyalty-scorpion-{suffix}"] = new("Reaction", $"After an action targets your Scorpion Clan Personality, destroy your performing Scorpion Clan Personality: Negate the {what} of the target from the action's effects", "none",
                Trigger: ReactionTriggers.TargetedByAction, UsageKey: "loyalty-scorpion", Performer: "rr2-own-clan-personality", RequiredKeywords: ["Scorpion"],
                Costs: [new(CostVerbs.Sacrifice, CostSubject.Performer)]);
        // The Compassion of the Unicorn (oracle-l5r-7991): the Focus Effect / "Battle/Open: Choose your performing unbowed Unicorn Clan Personality and target another unit: Straighten it. Gain 1 Honor, or 2
        // Honor if you do not control the target." -- a unit is targeted through its Personality; "another" is another than the performer's own.
        all["compassion-unicorn-focus"] = new("FocusEffect", delayText, "none");
        all["compassion-unicorn-straighten"] = new("BattleOrOpen", "Choose your performing unbowed Unicorn Clan Personality and target another unit: Straighten it. Gain 1 Honor, or 2 Honor if you do not control the target", "rr2-other-unit-than-performer",
            Performer: "rr2-own-unbowed-clan-personality-at-battle", RequiredKeywords: ["Unicorn"]);
        // The Courage of the Mantis (oracle-l5r-7995): the Focus Effect / "Battle: If any enemy units are at the current battlefield, choose your performing unbowed Mantis Clan Personality: Move him there.
        // You may take an additional Battle action." -- Broken Alliance's move (MovesToBattle, the enemy-units Require), a Mantis Clan performer wherever he stands, an unconditional additional Battle action.
        all["courage-mantis-focus"] = new("FocusEffect", delayText, "none");
        all["courage-mantis-move"] = new("Battle", "If any enemy units are at the current battlefield, choose your performing unbowed Mantis Clan Personality: Move him there. You may take an additional Battle action", "none",
            Performer: "rr2-own-unbowed-clan-personality-any-location", RequiredKeywords: ["Mantis"], Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield), MovesToBattle: true);
        // The Honesty of the Phoenix (oracle-l5r-8137, the corrected scan, issue #494): the Focus Effect / "Battle/Open: If you are a Phoenix Clan player, choose your performing unbowed Phoenix Clan
        // Personality: Name a card. After the next resolution this turn of your action from another Strategy or Ring with that title, draw a card, and choose a player, who discards a card. Remove this
        // card from the game." -- The Sincerity of the Dragon's shape, keyed on a title instead of a Focus Value: the Phoenix Clan player condition (ValidateEmperorRulingR2), the removal as part of the
        // action even when it is negated (CompleteNegatedAction), and the card named from the titles of the Strategies and Rings that can be played (engine-listed options).
        all["honesty-phoenix-focus"] = new("FocusEffect", delayText, "none");
        all["honesty-phoenix-name"] = new("BattleOrOpen", "If you are a Phoenix Clan player, choose your performing unbowed Phoenix Clan Personality: Name a card. After the next resolution this turn of your action from another Strategy or Ring with that title, draw a card, and choose a player, who discards a card. Remove this card from the game", "none",
            Performer: "rr2-own-unbowed-clan-personality-at-battle", RequiredKeywords: ["Phoenix"]);
        // The Strength of the Spider (oracle-l5r-8362): the Focus Effect its wave-13 publication lacked.
        all["strength-spider-focus"] = new("FocusEffect", delayText, "none");
        return all;
    }
}

public sealed partial class MatchEngine
{
    private static bool RulingR2BowsTarget(string abilityId) => abilityId is "honor-lion-bow";
    private static bool RulingR2DestroysTarget(string abilityId) => false;
    private static string? RulingR2MoveKind(string abilityId) => null;

    // ---- validation ----

    private void ValidateEmperorRulingR2(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // The Courage of the Mantis: "If any enemy units are at the current battlefield" -- a condition on taking the action.
            case "courage-mantis-move":
                Require(_state.CurrentBattleProvince is int mantisBattle && Army(1 - player.Id, mantisBattle).Any(), "There are no enemy units at the current battlefield.");
                break;
            // The Honesty of the Phoenix: "If you are a Phoenix Clan player" -- a condition on taking the action (the corrected scan, issue #494).
            case "honesty-phoenix-name":
                Require(player.Clan == "Phoenix", "You must be a Phoenix Clan player.");
                break;
        }
    }

    // ---- Performer categories (the validation switch's fallback and its listing twin) ----

    private bool RulingR2PerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle) => ability.Performer switch
    {
        // The Honor of the Lion: "your performing unbowed honorable Lion Clan Personality" (a Clan check, named by RequiredKeywords), at the battle while acting as a Battle action.
        "rr2-own-unbowed-honorable-clan-personality-at-battle" => Definition(performer).Type == CardType.Personality && !performer.Bowed && MatchesTargetStatus(performer, TargetStatus.IsHonorable)
            && HasClanAffiliation(performer, ability.RequiredKeywords![0]) && (!actingAsBattle || AtBattle(performer)),
        // The Compassion of the Unicorn: "your performing unbowed Unicorn Clan Personality".
        "rr2-own-unbowed-clan-personality-at-battle" => Definition(performer).Type == CardType.Personality && !performer.Bowed && HasClanAffiliation(performer, ability.RequiredKeywords![0]) && (!actingAsBattle || AtBattle(performer)),
        // The Courage of the Mantis: "your performing unbowed Mantis Clan Personality", wherever he stands (the action moves him to the battlefield).
        "rr2-own-unbowed-clan-personality-any-location" => Definition(performer).Type == CardType.Personality && !performer.Bowed && HasClanAffiliation(performer, ability.RequiredKeywords![0]),
        // The Loyalty of the Scorpion: "your performing Scorpion Clan Personality" -- bowed or not, wherever he stands (he is destroyed as the cost).
        "rr2-own-clan-personality" => Definition(performer).Type == CardType.Personality && HasClanAffiliation(performer, ability.RequiredKeywords![0]),
        _ => false
    };

    private string?[]? RulingR2Performers(AbilityDefinition ability, int playerId, bool actingAsBattle) => ability.Performer is { } category && category.StartsWith("rr2-", StringComparison.Ordinal)
        ? _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && RulingR2PerformerAllows(ability, c, actingAsBattle)).Select(c => (string?)c.Id).ToArray()
        : null;

    // ---- Target categories (the validation switch's fallback) ----

    private bool RulingR2TargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        return category switch
        {
            // The granted ability of The Honor of the Lion: "an enemy Personality with equal or lower Personal Honor" than the Personality that holds it.
            "rr2-enemy-personality-honor-lte-source" => personality && target.OwnerId != player.Id && source is not null && CardPersonalHonor(target) <= CardPersonalHonor(source),
            // The Compassion of the Unicorn: "another unit" -- a unit is targeted through its Personality, and another than the performer's own.
            "rr2-other-unit-than-performer" => personality && command.PerformerId is not null && target.Id != command.PerformerId,
            _ => false
        };
    }

    // ---- reactions to an action that targets a card ----

    private bool RulingR2TargetedReactionApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected)
    {
        var command = subject.Command;
        switch (abilityId)
        {
            // Halls of the Forgotten: another player's Battle action targets one of its controller's Berserkers.
            case "halls-forgotten-delay":
                return command.PlayerId != card.OwnerId && IsBattleAction(command) && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality && Keyword(affected, "Berserker");
            // The Loyalty of the Scorpion: an action (any player's) targets one of its controller's Scorpion Clan Personalities, the effect asked about is one the action could inflict, and a Scorpion Clan
            // Personality of the controller's is there to be destroyed.
            case "loyalty-scorpion-bow":
            case "loyalty-scorpion-move":
            case "loyalty-scorpion-destroy":
                return affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality && HasClanAffiliation(affected, "Scorpion")
                    && (abilityId switch
                    {
                        "loyalty-scorpion-bow" => BowsTarget(command.AbilityId!),
                        "loyalty-scorpion-move" => MoveOrStraightenKind(command.AbilityId!) == "move",
                        _ => subject.IsAttack || DestroysTarget(command.AbilityId!)
                    })
                    && _state.Cards.Values.Any(c => c.OwnerId == card.OwnerId && InPlay(c) && Definition(c).Type == CardType.Personality && HasClanAffiliation(c, "Scorpion"));
            default:
                return false;
        }
    }

    // ---- execution (RunAbilityEffects' default): null when the id is not this pass's; false when its effects are done; true when a window opened ----

    private bool? RunEmperorRulingR2(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant,
        bool playedFromDiscard, int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        var name = source is null ? "an action" : Definition(source).Name;
        switch (id)
        {
            // Halls of the Forgotten: from now until the held action has resolved, every effect it applies to the targeted Berserker is recorded, to be applied after this battle ends.
            case "halls-forgotten-delay":
                ArmDelayForHeldAction(Card(HeldAction().TargetId!), DelayUntil.EndOfBattle, name);
                return false;
            // The Honor of the Lion: a bonus raising his Personal Honor to 4 (none if it is 4 or more already) and the ability, both permanently.
            case "honor-lion-boost":
            {
                var lion = Card(command.PerformerId!);
                var raise = Math.Max(0, 4 - CardPersonalHonor(lion));
                if (raise > 0) AddPermanentModifier(lion, source!.Id, id, "PersonalHonor", raise);
                GrantAbility(lion, "honor-lion-bow", source!.Id, GrantDuration.Permanent);
                Emit("abilityGranted", $"{Definition(lion).Name} permanently gains a Personal Honor bonus (up to 4) and Battle: {AbilityCatalog.All["honor-lion-bow"].Label}.", player.Id, lion.Id);
                return false;
            }
            case "honor-lion-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                GainHonor(player.Id, 2, name, source);
                return false;
            // The Loyalty of the Scorpion: the performing Personality was destroyed as the cost; the target's bowing, movement or destruction from the held action is negated.
            case "loyalty-scorpion-bow":
                NegateEffects(Card(HeldAction().TargetId!), "bow");
                return false;
            case "loyalty-scorpion-move":
                NegateEffects(Card(HeldAction().TargetId!), "move");
                return false;
            case "loyalty-scorpion-destroy":
                NegateEffects(Card(HeldAction().TargetId!), "destroy");
                return false;
            // The Compassion of the Unicorn: the target's whole unit is straightened; 1 Honor, or 2 if the acting player does not control the target.
            case "compassion-unicorn-straighten":
                StraightenUnit(target!);
                GainHonor(player.Id, target!.OwnerId == player.Id ? 1 : 2, name, source);
                return false;
            // The Courage of the Mantis: the move (his own; a negated or blocked move leaves him where he was), then an additional Battle action.
            case "courage-mantis-move":
                ExecuteMove(command, ability, source, target);
                GrantAdditionalAction(player.Id, null);
                return false;
            // The Honesty of the Phoenix: the acting player names a card (a choice among the titles of the Strategies and Rings that can be played); the watch is set once he has.
            case "honesty-phoenix-name":
            {
                var titles = HonestyTitles();
                return BeginListedEffectChoice(command, titles.Select(t => new EffectOption($"name:{t}", $"Name {t}", [$"{HonestyNameEffect}{t}"])).ToArray(),
                    $"{name}: name a card. After the next resolution this turn of your action from another Strategy or Ring with that title, you draw a card and choose a player who discards a card.",
                    consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            }
            default:
                return null;
        }
    }

    // ---- The Honesty of the Phoenix ----

    internal const string HonestyNameEffect = "honesty-name:";

    // Every title of a Strategy or a Ring that a deck may contain: naming any other card could never match an action.
    private static string[] HonestyTitles() =>
        CardCatalog.PlayableFromDecks.Where(c => c.Id.StartsWith("oracle-l5r-", StringComparison.Ordinal) && c.Type is CardType.Strategy or CardType.Ring).Select(c => c.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    // ApplyBranchEffect's arm: naming the card sets the watch for the turn (The Sincerity of the Dragon's SincerityWatch, keyed on the title).
    private bool ApplyRulingR2BranchEffect(string effect, GameCommand command)
    {
        if (!effect.StartsWith(HonestyNameEffect, StringComparison.Ordinal)) return false;
        var title = effect[HonestyNameEffect.Length..];
        var source = Card(command.CardId);
        _state.SincerityWatches.RemoveAll(w => w.Turn != _state.TurnNumber);
        _state.SincerityWatches.Add(new(command.PlayerId, 0, _state.TurnNumber, source.Id, title));
        Player(source.OwnerId).Hand.Remove(source.Id); RemoveFromGame(source); // "Remove this card from the game": part of the action's resolution (a negated action removes it too: CompleteNegatedAction).
        Emit("watchSet", $"{Definition(source).Name}: {Player(command.PlayerId).Name} named {title}. After the next resolution this turn of his action from another Strategy or Ring with that title, he draws a card and chooses a player who discards a card.", command.PlayerId, source.Id);
        return true;
    }

    // ---- the duel's delay ----

    private static bool IsDelayFocusEffect(string abilityId) => EmperorRulingR2Abilities.DelayFocusEffects.Contains(abilityId);

    private string? FocusEffectAbilityOf(string focusCardId) =>
        (Definition(Card(focusCardId)).Abilities ?? []).FirstOrDefault(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing == "FocusEffect");

    // Called by ResolveDuel once the winner and the losers are known and before the outcome applies: asks, in the order the Focus Effects were ordered, each delay Focus Effect's player whether to delay
    // -- only when one of that player's Personalities lost (a tie: both did), and only until that player has delayed once. True when it paused for an answer (ResolveDelayDecision continues the duel).
    private bool BeginDuelLossDelayDecisions(PendingDuel duel, List<string> loserIds)
    {
        while (duel.DelayDecisionIndex < duel.OrderedFocusEffects.Count)
        {
            var index = duel.DelayDecisionIndex++;
            var focusId = duel.OrderedFocusEffects[index];
            if (FocusEffectAbilityOf(focusId) is not { } abilityId || !IsDelayFocusEffect(abilityId)) continue;
            var focus = Card(focusId); var owner = focus.OwnerId;
            if (!loserIds.Any(id => Card(id).OwnerId == owner) || duel.DelayedLosses.Any(d => d.PlayerId == owner)) continue;
            _state.PendingFocusChoice = new PendingFocusChoice { Index = index, FocusCardId = focusId, PlayerId = owner, Candidates = [DelayLossKey], AbilityId = abilityId, DelayDecision = true };
            _state.PendingChoice = new("focusEffectChoice", owner, focusId, $"{Definition(focus).Name} (Focus Effect): {Player(owner).Name}, you may choose now to delay the effects of losing this duel until after the next Dynasty Phase begins.");
            _state.PriorityPlayerId = owner; _state.ConsecutivePasses = 0;
            Emit("focusEffectChoiceOffered", $"{Player(owner).Name} may delay the effects of losing this duel ({Definition(focus).Name}).", owner, focusId);
            return true;
        }
        return false;
    }

    private const string DelayLossKey = "delay";

    // The answer: "delay" (TargetId) or "decline" (Choice). The duel then resolves (again), asking the next delay Focus Effect if there is one.
    private void ResolveDelayDecision(GameCommand command, PendingFocusChoice pending)
    {
        var duel = _state.PendingDuel!; var focus = Card(pending.FocusCardId);
        Require(command.Choice == "decline" || command.TargetId == DelayLossKey, "Choose to delay the effects of losing this duel, or decline.");
        if (command.Choice == "decline") Emit("focusEffectDeclined", $"{Player(pending.PlayerId).Name} did not delay the effects of losing the duel ({Definition(focus).Name}).", pending.PlayerId, pending.FocusCardId);
        else
        {
            duel.DelayedLosses.Add(new(pending.PlayerId, DelayUntil.NextDynastyPhase, pending.FocusCardId, false));
            Emit("duelLossDelayed", $"{Player(pending.PlayerId).Name} delays the effects of losing this duel until after the next Dynasty Phase begins ({Definition(focus).Name}).", pending.PlayerId, pending.FocusCardId);
        }
        _state.PendingChoice = null; _state.PendingFocusChoice = null;
        ResolveDuel();
    }

    private IEnumerable<ActionOption> DelayDecisionActions(PendingFocusChoice p)
    {
        yield return new("chooseFocusEffect", "Delay the effects of losing this duel", true, null, new GameCommand("chooseFocusEffect", p.PlayerId, _state.Version, CardId: p.FocusCardId, TargetId: DelayLossKey), p.FocusCardId);
        yield return new("chooseFocusEffect", "Do not delay", true, null, new GameCommand("chooseFocusEffect", p.PlayerId, _state.Version, CardId: p.FocusCardId, Choice: "decline"), p.FocusCardId);
    }

    /// <summary>Runs <paramref name="apply"/> (a duel's outcome) with the effects on each delaying player's losing Personalities -- and the attachments of their units -- captured for the delay they chose.</summary>
    private void WithDuelLossDelays(PendingDuel duel, IEnumerable<string> loserIds, Action apply)
    {
        if (duel.DelayedLosses.Count == 0) { apply(); return; }
        var scopes = new List<DelayScope>();
        foreach (var loserId in loserIds)
        {
            if (duel.DelayedLosses.FirstOrDefault(d => d.PlayerId == Card(loserId).OwnerId) is not { } choice) continue;
            var reason = $"losing the duel ({Definition(Card(choice.SourceId)).Name})";
            foreach (var member in Attachments(loserId).Prepend(Card(loserId))) scopes.Add(new(member.Id, choice.Until, null, null, duel.ChallengerPlayerId, reason));
        }
        _delayScopes.AddRange(scopes);
        try { apply(); }
        finally { foreach (var scope in scopes) _delayScopes.Remove(scope); }
    }
}
