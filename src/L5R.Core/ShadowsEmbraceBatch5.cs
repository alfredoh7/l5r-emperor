namespace L5R.Core;

// The Shadow's Embrace pass (issue #476), batch 5: the primitives of spell-wave-22 and item-wave-14. The shared engine files carry short, commented hooks into this
// file (each names its card); the catalog entries, Target categories and Attack strengths/kinds live in CardAbilities.cs beside every other ability's, as they must
// (one dictionary literal, one switch each). Blood of the Preserver's two choices are DeciderChoice.cs specs, its application is here.
//
// Nothing here adds a MatchState field: the few records these cards keep are stamps in a card's own AbilityUses under keys no ability uses (Moshi Ino's and
// Burning Dreams' shape), each compared with the turn number, so they lapse by themselves.
public sealed partial class MatchEngine
{
    // ---- Effect keys --------------------------------------------------------------------------------------------------------------------------------
    internal const string ChannelTheMoonEffect = "channel-the-moon";
    internal const string PurifyTheSoulEffect = "purify-the-soul";
    internal const string SetTheFieldsAflameEffect = "set-fields-aflame";
    internal const string ThundersStaminaEffect = "thunders-stamina";

    // Touch the Emptiness: "negate the effects of the next action the target performs" -- the turn it was given in (an ongoing effect with no printed duration lasts the turn).
    private const string TouchTheEmptinessKey = "se5:touch-emptiness-negate-next";
    // Celestial Sword of the Phoenix: "Remove him from the game before the turn ends" -- the turn he was brought into play in.
    private const string PhoenixSwordRemovalKey = "se5:phoenix-sword-remove-before-turn-ends";
    // Blood of the Preserver: the turn the unit was removed in; it enters play again after the next turn begins. Stamped on the Personality and on each attachment.
    private const string BloodOfThePreserverKey = "se5:blood-preserver-removed";
    // Purify the Soul's per-card negation kind (MatchState.NegatedEffects): the held action's effects that give Kolat, Ninja or Shadowlands to that Personality.
    private const string PurifyTheSoulKind = "grant-kolat-ninja-shadowlands";
    private static readonly string[] PurifiedKeywords = ["Kolat", "Ninja", "Shadowlands"];

    private CardInstance? WielderOf(CardInstance attachment) =>
        attachment is { Zone: Zone.Attached, AttachedTo: { } hostId } && _state.Cards.TryGetValue(hostId, out var host) ? host : null;

    // Every Spell of this batch with the given Effect attached to the Personality.
    private IEnumerable<CardInstance> AttachedWithEffect(CardInstance personality, string effect) =>
        Attachments(personality.Id).Where(c => Definition(c).Type == CardType.Spell && Definition(c).Effect == effect);

    // ---- Force (PersonalityForce / FollowerForce hook) ----------------------------------------------------------------------------------------------
    private int ShadowsEmbraceBatch5Force(CardInstance card)
    {
        var force = 0;
        if (Definition(card).Type == CardType.Personality)
        {
            // Set the Fields Aflame: "While this Shugenja is Air or Fire, he has +1F."
            if (Keyword(card, "Air") || Keyword(card, "Fire")) force += AttachedWithEffect(card, SetTheFieldsAflameEffect).Count();
            // Thunder's Stamina: "This Shugenja has +3F." (Strength of the Tsunami's shape: only a Shugenja it is attached to.)
            if (Keyword(card, "Shugenja")) force += 3 * AttachedWithEffect(card, ThundersStaminaEffect).Count();
        }
        // Channel the Moon: "While this Shugenja is Shadowlands, your opposed Nonhumans have +1F." -- each copy on a Shadowlands Shugenja of the card's controller, for a Nonhuman
        // Personality or Follower of his that is opposed (at a battlefield where the enemy army has a unit).
        if (Definition(card).Type is CardType.Personality or CardType.Follower && Keyword(card, "Nonhuman") && Opposed(card))
            force += _state.Cards.Values.Count(c => c.Zone == Zone.Attached && c.OwnerId == card.OwnerId && Definition(c).Effect == ChannelTheMoonEffect
                && WielderOf(c) is { } shugenja && (Keyword(shugenja, "Shadowlands") || HasClanAffiliation(shugenja, "Shadowlands")));
        return force;
    }

    // Core rules, "Opposed": at a battlefield where the enemy army has at least one unit. A Follower stands where its Personality does.
    private bool Opposed(CardInstance card)
    {
        var unit = Definition(card).Type == CardType.Follower ? WielderOf(card) : card;
        return unit is { Zone: Zone.Battlefield, ProvinceIndex: int province } && Army(1 - unit.OwnerId, province).Any();
    }

    // ---- Thunder's Stamina (AfterResolution.ResolutionWillNotBow hook) ------------------------------------------------------------------------------
    // "While this Shugenja is Thunder, battle resolution will not bow cards in this unit." -- asked of the unit's Personality as it returns home.
    private bool ThundersStaminaUnitWillNotBow(CardInstance unitHead) =>
        Keyword(unitHead, "Thunder") && AttachedWithEffect(unitHead, ThundersStaminaEffect).Any();

    // ---- Purify the Soul ----------------------------------------------------------------------------------------------------------------------------
    // "Attaches to an Air Shugenja paying 2 less Gold." (EquipGoldCost hook) -- the discount is read against the Personality it is attached to.
    private int PurifyTheSoulDiscount(CardInstance card, CardInstance wielder) =>
        Definition(card).Effect == PurifyTheSoulEffect && Keyword(wielder, "Air") && Keyword(wielder, "Shugenja") ? 2 : 0;

    // The actions whose effects give Kolat, Ninja or Shadowlands to the Personality they target -- curated like AbilityMayCauseHonorLoss (the grant is made inside each
    // action's own case, so it cannot be predicted in general): Asako Izuna's three Reactions and Essence of Undeath (the Personality it brings into play).
    private static bool GivesKolatNinjaOrShadowlands(string? abilityId) => abilityId is "izuna-kolat" or "izuna-ninja" or "izuna-shadowlands" or "essence-of-undeath-recruit";

    // BeforeActionReactionApplies' case: the held action would give one of the keywords to the affected Personality, and that Personality is (or, for a Personality the
    // action brings into play under its taker's control, will be) controlled by Purify the Soul's controller.
    private bool PurifyTheSoulApplies(CardInstance card, GameCommand command, CardInstance affected) =>
        GivesKolatNinjaOrShadowlands(command.AbilityId) && Definition(affected).Type == CardType.Personality
        && (InPlay(affected) ? affected.OwnerId == card.OwnerId : command.PlayerId == card.OwnerId);

    // GrantKeyword's hook: while the held action resolves, its grant of Kolat, Ninja or Shadowlands to a Personality Purify the Soul protects does not happen.
    private bool PurifyTheSoulNegatesGrant(CardInstance card, string keyword)
    {
        if (!PurifiedKeywords.Contains(keyword, StringComparer.Ordinal) || !_state.NegatedEffects.Any(n => n.Kind == PurifyTheSoulKind && n.CardId == card.Id)) return false;
        Emit("effectNegated", $"Purify the Soul: {Definition(card).Name} is not given {keyword}.", card.OwnerId, card.Id);
        return true;
    }

    // ---- Touch the Emptiness (ContinueUseAbility hook, beside Consideration's shield) --------------------------------------------------------------
    // "Negate the effects of the next action the target performs": the performer is TargetBars.cs's (the named performer, else the card the action is on, an Item or Spell
    // acting through its wielder). The action is still taken and its costs paid (ResolveNegatedAction), and the negation is spent; an action that cannot be negated
    // (Protection) spends it too, since its trigger -- the next action he performs -- has happened.
    private bool TouchTheEmptinessNegates(GameCommand command)
    {
        if (command.AbilityId is not { } id || !AbilityCatalog.All.TryGetValue(id, out var ability) || !IsAction(ability)) return false;
        if (PerformingCard(command, ability) is not { } performer || !performer.AbilityUses.TryGetValue(TouchTheEmptinessKey, out var turn)) return false;
        performer.AbilityUses.Remove(TouchTheEmptinessKey);
        if (turn != _state.TurnNumber) return false;
        if (ActionProtected(command) || Protected(performer))
        {
            Emit("actionProtected", $"Protection: {Definition(performer).Name}'s action cannot be negated (Touch the Emptiness is spent).", performer.OwnerId, performer.Id);
            return false;
        }
        Emit("actionNegated", $"Touch the Emptiness: the effects of {Definition(performer).Name}'s action ({ability.Label}) are negated.", performer.OwnerId, performer.Id);
        ResolveNegatedAction(command);
        return true;
    }

    // ---- Ward of Air (TargetedActionReactionApplies' case) -------------------------------------------------------------------------------------------
    // "After another player's Battle action targets this Shugenja, or any Personality you control if this Shugenja is Air": the action could destroy him (an Attack always
    // could, an ability on DestroysTarget's curated list), exactly as for Taming the Beast.
    private bool WardOfAirApplies(CardInstance card, ReactionSubject subject, CardInstance affected) =>
        subject.Command.PlayerId != card.OwnerId && IsBattleAction(subject.Command) && Definition(affected).Type == CardType.Personality
        && WielderOf(card) is { } shugenja && (affected.Id == shugenja.Id || Keyword(shugenja, "Air") && affected.OwnerId == card.OwnerId)
        && (subject.IsAttack || DestroysTarget(subject.Command.AbilityId!));

    // ---- Celestial Sword of the Phoenix --------------------------------------------------------------------------------------------------------------
    // The Target category "own-honorably-dead-clan-personality": the acting player's own dead, honorable Personality of the Clan RequiredKeywords names.
    private bool IsOwnHonorablyDeadClanPersonality(CardInstance card, int playerId, AbilityDefinition ability) =>
        card.OwnerId == playerId && card.Zone == Zone.Dead && !card.Dishonorable && Definition(card).Type == CardType.Personality
        && (ability.RequiredKeywords ?? []).All(clan => HasClanAffiliation(card, clan));

    // EndTurn's hook ("before the turn ends", beside the delayed destructions): each Personality the sword brought into play this turn and still in play leaves the game with
    // his unit (core rules: when a Personality leaves play, his whole unit leaves play the same way).
    private void ResolveShadowsEmbraceBatch5TurnEnd()
    {
        foreach (var brought in _state.Cards.Values.Where(c => c.AbilityUses.TryGetValue(PhoenixSwordRemovalKey, out var turn) && turn == _state.TurnNumber).ToArray())
        {
            brought.AbilityUses.Remove(PhoenixSwordRemovalKey);
            if (!InPlay(brought) || Definition(brought).Type != CardType.Personality) continue;
            foreach (var attachment in Attachments(brought.Id).ToArray()) RemoveFromGame(attachment);
            RemoveFromGame(brought);
            Emit("unitRemovedFromGame", $"Celestial Sword of the Phoenix: {Definition(brought).Name} is removed from the game before the turn ends.", brought.OwnerId, brought.Id);
        }
    }

    // ---- Blood of the Preserver ---------------------------------------------------------------------------------------------------------------------
    private List<string> BloodOfThePreserverCandidates(IReadOnlyCollection<string> alreadyChosen) =>
        _state.CurrentBattleProvince is int province
            ? _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == province && Definition(c).Type == CardType.Personality && !alreadyChosen.Contains(c.Id))
                .Select(c => c.Id).Order(StringComparer.Ordinal).ToList()
            : [];

    private string BloodOfThePreserverPrompt(PendingDeciderChoice p) =>
        $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose zero to two Personalities at the current battlefield. They are removed from the game with their units, and enter play again in their owners' homes after the next turn begins.";

    // The first choice (the acting player's) hands on to the other player's; the second removes every chosen unit.
    private void ApplyBloodOfThePreserverChoice(PendingDeciderChoice p)
    {
        var chosen = p.EarlierChosen.Concat(p.Chosen).Distinct().ToList();
        if (p.SpecId == DeciderSpecs.BloodOfThePreserverFirst)
        {
            var other = 1 - p.ActingPlayerId;
            OpenDeciderChoice(new PendingDeciderChoice
            {
                SpecId = DeciderSpecs.BloodOfThePreserverSecond, SourceCardId = p.SourceCardId, ActingPlayerId = p.ActingPlayerId, DeciderPlayerId = other,
                Candidates = BloodOfThePreserverCandidates(chosen), Min = 0, Max = 2, EarlierChosen = chosen, Paused = p.Paused
            });
            return;
        }
        foreach (var id in chosen)
        {
            var personality = Card(id);
            if (personality.Zone != Zone.Battlefield) continue;
            var attachments = Attachments(personality.Id).ToArray();
            // Set aside as they stand: the attachments keep their host and every card keeps its tokens ("cards and tokens stay attached to them").
            foreach (var card in attachments.Prepend(personality))
            {
                card.Zone = Zone.RemovedFromGame; card.ProvinceIndex = null;
                card.AbilityUses[BloodOfThePreserverKey] = _state.TurnNumber;
            }
            Emit("unitRemovedFromGame", $"Blood of the Preserver: {Definition(personality).Name}'s unit is removed from the game until after the next turn begins.", personality.OwnerId, personality.Id);
        }
    }

    // StartTurn's hook ("after the next turn begins"): each removed unit enters play in its owner's home, ignoring costs, restrictions and entering-play effects -- no Gold,
    // no Unique/Honor check, no entry window, no Proclaim. It is a new entry (ResetCardState: nothing of its earlier stay in play follows it) except for what the card keeps
    // in so many words: its attached cards and the tokens on them. Then the Personality gets three +1F Blood tokens.
    private void ResolveShadowsEmbraceBatch5TurnBegins()
    {
        var removed = _state.Cards.Values.Where(c => c.Zone == Zone.RemovedFromGame && c.AbilityUses.TryGetValue(BloodOfThePreserverKey, out var turn) && turn < _state.TurnNumber).ToArray();
        foreach (var personality in removed.Where(c => Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var attachments = removed.Where(c => c.AttachedTo == personality.Id && Definition(c).Type != CardType.Personality).ToArray();
            RestoreOwner(personality);
            foreach (var card in attachments.Prepend(personality))
            {
                var tokens = card.Tokens.ToList();
                ResetCardState(card);
                card.Tokens.AddRange(tokens);
            }
            personality.Zone = Zone.Home; personality.ProvinceIndex = null; personality.FaceUp = true;
            foreach (var attachment in attachments)
            {
                RestoreOwner(attachment);
                TakeControl(attachment, personality.OwnerId);
                attachment.Zone = Zone.Attached; attachment.ProvinceIndex = null; attachment.FaceUp = true;
                if (Definition(attachment).AttachGrantsKeyword is string granted) GrantKeyword(personality, granted, attachment.Id, GrantDuration.Attachment);
            }
            for (var i = 0; i < 3; i++) CreateToken(personality, "Blood", "blood-preserver-remove", "Force", 1);
            Emit("unitReturned", $"Blood of the Preserver: {Definition(personality).Name}'s unit enters play in {Player(personality.OwnerId).Name}'s home with three +1F Blood tokens.", personality.OwnerId, personality.Id);
        }
        // An attachment whose Personality is gone (it cannot be: they leave and return together) is simply released from the stamp.
        foreach (var card in removed) card.AbilityUses.Remove(BloodOfThePreserverKey);
    }

    // ---- Validation hook (ValidateAbility, after the per-id Requires) --------------------------------------------------------------------------------
    private void ValidateShadowsEmbraceBatch5(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // Beseech Sakkaku: "you may destroy this card if he is Earth" -- the destroying half needs an Earth Shugenja.
            case "beseech-sakkaku-destroy":
                Require(source is not null && WielderOf(source) is { } beseecher && Keyword(beseecher, "Earth"), "Only an Earth Shugenja may destroy Beseech Sakkaku.");
                break;
            // Celestial Sword of the Phoenix: "ignoring costs" waives the Gold, not the restrictions -- a Unique or Singular Personality of which the player already controls a copy
            // may not be brought into play.
            case "celestial-sword-phoenix-return" when command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var dead):
                Require(!(Keyword(dead, "Unique") || Keyword(dead, "Singular")) || !ControlsCopyOf(player.Id, Definition(dead).Name),
                    $"You already control this {(Keyword(dead, "Unique") ? "Unique" : "Singular")} card.");
                break;
        }
    }

    // ---- Execution hook (RunAbilityEffects' default) -------------------------------------------------------------------------------------------------
    // Null when the ability is not this batch's; true when the action paused (an Attack's reaction window, a choice, an entry window) and finishes from there.
    private bool? RunShadowsEmbraceBatch5(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant,
        bool playedFromDiscard, int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            // Blood of the Preserver: the Item was removed as the cost; the acting player chooses first, then the other player (DeciderChoice.cs).
            case "blood-preserver-remove":
                return BeginDeciderChoice(DeciderSpecs.BloodOfThePreserverFirst, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            // The Celestial Swords' "After this card enters play: Gain 2 Honor".
            case "celestial-sword-crab-honor": case "celestial-sword-crane-honor": case "celestial-sword-phoenix-honor":
                GainHonor(player.Id, 2, Definition(source!).Name, source);
                return false;
            // Celestial Sword of the Crab: "Straighten this unit" -- the Personality it is attached to, and every card attached to him.
            case "celestial-sword-crab-straighten":
                if (WielderOf(source!) is { } crabWielder) StraightenUnit(crabWielder);
                return false;
            // Celestial Sword of the Crane: "Give the target -4F." and, when the Favor was discarded as the cost, "gain 1 Honor".
            case "celestial-sword-crane-weaken":
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                return false;
            case "celestial-sword-crane-favor":
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                GainHonor(player.Id, 1, Definition(source).Name, source);
                return false;
            // Celestial Sword of the Phoenix: "Bring him into play at this card's location, ignoring costs" (the wielder's: his battlefield, else home) and "remove him from the game
            // before the turn ends" (the stamp EndTurn reads).
            case "celestial-sword-phoenix-return":
            {
                var wielder = WielderOf(source!);
                var battlefield = wielder is { Zone: Zone.Battlefield } ? wielder.ProvinceIndex : null;
                return BringDeadPersonalityIntoPlay(player, target!, Definition(source!).Name, battlefield, entered =>
                {
                    entered.AbilityUses[PhoenixSwordRemovalKey] = _state.TurnNumber;
                    Emit("removalScheduled", $"{Definition(entered).Name} will be removed from the game before the turn ends.", entered.OwnerId, entered.Id);
                });
            }
            case "string-colonies-attack":
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            // Beseech Sakkaku: 1 Honor, or -- when the Spell was destroyed as the cost -- Honor equal to the Spirit's current Personal Honor.
            case "beseech-sakkaku-honor":
                GainHonor(player.Id, 1, "Beseech Sakkaku", source);
                return false;
            case "beseech-sakkaku-destroy":
                GainHonor(player.Id, CardPersonalHonor(target!), "Beseech Sakkaku", source);
                return false;
            // Channel the Moon: the ability is given to the performing Nonhuman for the turn; then "Lose 3 Honor" (an effect, not a cost).
            case "channel-moon-grant":
            {
                var nonhuman = Card(command.PerformerId!);
                GrantAbility(nonhuman, "channel-moon-melee", source!.Id);
                Emit("abilityGranted", $"{Definition(nonhuman).Name} has \"Battle: Melee Attack with strength equal to this card's Force\" this turn.", player.Id, nonhuman.Id);
                GainHonor(player.Id, -3, "Channel the Moon", source);
                return false;
            }
            case "channel-moon-melee":
                return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant);
            // Purify the Soul: every Personality the held action names that it protects (the window's own target first) is marked for the negation GrantKeyword reads.
            case "purify-soul-negate":
            {
                var held = _state.PendingNegatableAbility!;
                foreach (var protectedId in new[] { ReactionAffectedId(held) }.Concat(ActionTargetIds(held)).OfType<string>().Distinct())
                    if (_state.Cards.TryGetValue(protectedId, out var protectedCard) && PurifyTheSoulApplies(source!, held, protectedCard))
                        _state.NegatedEffects.Add(new(PurifyTheSoulKind, protectedId));
                Emit("effectsNegated", "Purify the Soul: the action's effects that give Kolat, Ninja or Shadowlands to your Personalities are negated.", player.Id, source!.Id);
                return false;
            }
            // Set the Fields Aflame: the Spell was destroyed as each action's cost.
            case "set-fields-terrain":
                DiscardTerrain(target!);
                player.TerrainDestroyedThisBattle = true;
                return false;
            case "set-fields-region":
                LockActions(target!, source!.Id);
                return false;
            case "set-fields-holding":
                target!.Bowed = true;
                return false;
            // Touch the Emptiness: -2F, and -- while its Shugenja is Void -- the target's next action this turn is negated (TouchTheEmptinessNegates).
            case "touch-emptiness-weaken":
                ApplyModifier(target!, source!.Id, id, "Force", -2);
                if (WielderOf(source) is { } voidShugenja && Keyword(voidShugenja, "Void"))
                {
                    target!.AbilityUses[TouchTheEmptinessKey] = _state.TurnNumber;
                    Emit("nextActionNegated", $"Touch the Emptiness: the effects of the next action {Definition(target).Name} performs this turn will be negated.", player.Id, target.Id);
                }
                return false;
            // Ward of Air: the Spell was destroyed as the cost; the target's destruction from the held action is negated (Taming the Beast's shape).
            case "ward-of-air-negate":
                NegateEffects(Card(HeldAction().TargetId!), "destroy");
                return false;
            case "ward-of-air-move-home":
                if (WielderOf(source!) is { } wardWielder) MoveHome(wardWielder, false);
                return false;
            // Your Heart's Enemy: -4F; while the Shugenja (the named performer) is Air, the target's actions may not target him (TargetBars.cs, for the turn).
            case "hearts-enemy-weaken":
            {
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                var heartsShugenja = Card(command.PerformerId!);
                if (Keyword(heartsShugenja, "Air")) AddTargetBar(heartsShugenja, target!.Id);
                return false;
            }
            default:
                return null;
        }
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterShadowsEmbraceBatch5_Rung4b2()
    {
        EngineHooks.PersonalityForce.Register("ShadowsEmbraceBatch5Force", 1100, static (engine, context) => engine.ShadowsEmbraceBatch5Force(context.card));
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterShadowsEmbraceBatch5_Rung4b3()
    {
        EngineHooks.ValidateEarly.Register("ValidateShadowsEmbraceBatch5", 200, static (engine, context) => engine.ValidateShadowsEmbraceBatch5(context.id, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunShadowsEmbraceBatch5", 1000, static (engine, context) => engine.RunShadowsEmbraceBatch5(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
    }
}
