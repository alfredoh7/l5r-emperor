namespace L5R.Core;

// The Shadow's Embrace pass (issue #476), batch 3: the helpers and effects behind strategy-wave-56's Strategies. Each card's reading is on its ability
// definition (CardAbilities.cs); what needed new engine state is here:
//
//   * Cowed and Defeated's watch (CowedAndDefeatedWatch): "After the next time (this turn) another player's action resolves that targeted your performer or a battle
//     resolves which destroyed him" -- fired once, by whichever comes first, from the points an action is truly done (Grappling Snake's own FinishGrapplingWatches
//     points: CompleteAbilityResolution, a deferred Attack's ResolveAttack, a duel's FinishDuelResolution) and from a battle's resolution.
//   * Deep Roots: a turn-long "contributes Force even while bowed, and nothing prevents him from contributing" (CardInstance.DeepRootsTurn), beside Tireless Efforts'
//     own penalty negation.
//   * Duel to First Blood's Focus Effect ("+1 Focus Value to each of your focused cards") is read into the duel totals; its refusal draws the challenger two cards.
//   * Forged Documents: the target's controller orders his province cards at the bottom of his Dynasty deck -- a listed effect choice naming provinces, never a
//     face-down card -- and the provinces refill face-up.
//   * Kitsuki Methods: two chained Either/Or decisions (EitherOr.cs), the target's controller's then the acting player's.
//   * Lessons Never Forgotten: the optional attachment target chosen after its Melee Attack resolves (at once, or after the Attack's reaction window).
//   * Methodical Questioning: "put this card into your hand from your discard pile before this turn ends" (ReturnToHandBeforeTurnEnds), run where the turn ends (EndTurn).
//   * Fields of Victory: a before-resolution Terrain trait.
//   * Crowded Streets: a Terrain trait at the end of every resolved action its controller's Personality at the battlefield performed (an Either/Or whose finish carries on
//     with the action's own tail), and the Defender's additional Battle action when it is played.
//   * Forthrightness: a turn-long watch counting its player's announced actions on Strategies, judged when an action from a card with the shown Strategy's title resolves.
//   * Forge Your Soul: the top four Fate cards are shown (the public shown-cards record), then two chained Either/Or decisions of the acting player's -- attach one shown
//     Weapon to the performer at 4 less Gold (one option per Weapon, by deck position, never by card id), then shuffle or not -- and a Kensai who showed no Weapon draws.
//     The attached Weapon's entering-play window opens once the action's own effects are done.

/// <summary>Cowed and Defeated: PerformerId is the Courtier or Magistrate who performed it, TargetId the Personality it dishonored, PlayerId whose action it was.</summary>
public sealed record CowedAndDefeatedWatch(string PerformerId, string TargetId, int PlayerId, int Turn, string SourceId);

/// <summary>Methodical Questioning: the card goes from its owner's discard pile to his hand as the turn numbered Turn ends.</summary>
public sealed record ReturnToHandBeforeTurnEnds(string CardId, int PlayerId, int Turn);

/// <summary>Forthrightness: the Samurai to grant, the shown Strategy's title and how many actions on Strategies its player has announced since.</summary>
public sealed class ForthrightnessWatch
{
    public int PlayerId { get; set; }
    public string SamuraiId { get; set; } = "";
    public string Title { get; set; } = "";
    public int Turn { get; set; }
    public string SourceId { get; set; } = "";
    public int StrategyActionsAnnounced { get; set; }
}

public sealed partial class MatchEngine
{
    private const string CrowdedStreetsEffect = "crowded-streets";
    private const string ShadowsEmbraceAfterActionOrigin = "after-action";
    private const string Se3DestroyAttachmentEffect = "se3-destroy-attachment:";
    private const string Se3ForgedOrderEffect = "se3-forged-order:";

    // ---- small readers -------------------------------------------------------------------------------------------------------------------------------------
    // A card's location (core rules Glossary): a battlefield, or its controller's home -- an attachment is where its Personality is, a Stronghold, Holding or Celestial at home.
    private string? ShadowsEmbraceLocation(CardInstance card)
    {
        var at = card.Zone == Zone.Attached && card.AttachedTo is string hostId && _state.Cards.TryGetValue(hostId, out var host) ? host : card;
        return at.Zone switch
        {
            Zone.Battlefield => $"battlefield:{at.ProvinceIndex}",
            Zone.Home or Zone.Stronghold or Zone.Celestial => $"home:{at.OwnerId}",
            _ => null
        };
    }

    // "A Ninja unit" (core rules, Keywords): the Personality and every Follower in his unit carry the keyword.
    private bool InNinjaUnit(CardInstance personality) =>
        Keyword(personality, "Ninja") && Attachments(personality.Id).Where(a => Definition(a).Type == CardType.Follower).All(f => Keyword(f, "Ninja"));

    // Grief: the last resolved action's effects destroyed a Personality of this player's.
    private bool LastActionDestroyedPersonalityOf(int ownerId) =>
        _state.LastResolvedAction is { } facts && facts.Affected.Any(a => a.Kind == DestroyedKind && _state.Cards.TryGetValue(a.CardId, out var dead)
            && dead.OwnerId == ownerId && Definition(dead).Type == CardType.Personality);

    // I Cast Two Shadows: the battlefield the last resolved action moved this card home from, or null.
    private int? LastActionMovedHomeFrom(CardInstance card) =>
        _state.LastResolvedAction?.Affected.LastOrDefault(a => a.CardId == card.Id && a.Kind == MovedKind && a.FromBattlefield is not null)?.FromBattlefield;

    // Deep Roots.
    private bool DeepRooted(CardInstance card) => card.DeepRootsTurn == _state.TurnNumber;

    // Duel to First Blood's Focus Effect: each of its copies among a side's focused cards gives every one of that side's focused cards +1 Focus Value -- applied with the other Focus Value effects, in the
    // duel's Focus Effect order (DuelFocusValues, EmperorFollowupsDuel.cs, issue #498).

    private int RingsControlled(int playerId) => _state.Cards.Values.Count(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Ring);

    // ---- Attack strengths (AttackStrength's hook) ----------------------------------------------------------------------------------------------------------
    // Burning Fury: "3 plus the number of Fire tokens on him and the number of Rings you control" -- sourceId is the performing Personality, read live.
    private int? ShadowsEmbraceBatch3AttackStrength(string abilityId, string? sourceId) => abilityId switch
    {
        "burning-fury-attack" => sourceId is not null && _state.Cards.TryGetValue(sourceId, out var fury) ? 3 + TokenCount(fury, "Fire") + RingsControlled(fury.OwnerId) : 3,
        "lessons-never-forgotten-attack" => 4,
        _ => null
    };

    // ---- per-id conditions (ValidateAbility's hook) --------------------------------------------------------------------------------------------------------
    private void ValidateShadowsEmbraceBatch3(string id, GameCommand command, PlayerState player)
    {
        switch (id)
        {
            // Defensive Grill: "an unresolved battlefield where he would be opposed" -- one holding units of the side opposing him.
            case "defensive-grill-move" when command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var grilled):
                Require(command.Province is int grillTo && Army(1 - grilled.OwnerId, grillTo).Any(), "He would not be opposed at that battlefield.");
                break;
            // I Cast Two Shadows: "a different battlefield than the one from which he was moved home".
            case "cast-two-shadows-move" when command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var shadow):
                Require(command.Province is int shadowTo && shadowTo != LastActionMovedHomeFrom(shadow), "Choose a different battlefield than the one he was moved home from.");
                break;
        }
    }

    // ---- Execution hook (RunAbilityEffects' default) -------------------------------------------------------------------------------------------------------
    // null: not one of this batch's abilities. false: its effects are done, the action finishes as usual. true: a window or a choice opened, the caller returns.
    private bool? RunShadowsEmbraceBatch3(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant,
        bool playedFromDiscard, int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case "burning-fury-attack":
                return ResolveOrDeferAttack(id, player, target?.Id, command.PerformerId, consumedGrant);
            // Captured Convict: "Move him home." Then the conditional player choice, only if he is dishonorable, Kolat, Ninja or Shadowlands.
            case "captured-convict-move":
                MoveHome(target!, false);
                return BeginConditionalChoice(command, ability, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            // Cowed and Defeated: "Dishonor him. His controller loses 1 Honor." and the watch on the performer.
            case "cowed-defeated-dishonor":
                Dishonor(target!, player.Id);
                GainHonor(target!.OwnerId, -1, Definition(source!).Name, source);
                _state.CowedAndDefeatedWatches.Add(new(command.PerformerId!, target.Id, player.Id, _state.TurnNumber, source!.Id));
                Emit("watchSet", $"{Definition(source).Name}: after the next time this turn another player's action resolves that targeted {Definition(Card(command.PerformerId!)).Name}, or a battle resolves which destroyed him, {Definition(target).Name}'s controller loses 2 Honor.", player.Id, source.Id);
                return false;
            // Deep Roots.
            case "deep-roots-negate":
            {
                var rooted = Card(command.PerformerId!);
                NegateForcePenalties(rooted);
                rooted.DeepRootsTurn = _state.TurnNumber;
                Emit("forceContributionGuaranteed", $"{Definition(source!).Name}: {Definition(rooted).Name} contributes Force even while bowed this turn, and nothing prevents him from contributing it.", player.Id, rooted.Id);
                return false;
            }
            case "defensive-grill-move":
            case "cast-two-shadows-move":
                ExecuteMove(command, ability, source, target);
                return false;
            // Direct the Path: "Put it on top of its owner's deck." A created card is removed from the game instead (it never enters a deck).
            case "direct-the-path-return":
                if (!InPlay(target!)) return false;
                if (NegatesEffect(target!, "all")) { Emit("effectsNegated", $"{Definition(target!).Name}: the action's effects were negated before they resolved.", target!.OwnerId, target.Id); return false; }
                if (IsCreatedCard(target!)) RemoveFromGame(target!); else ToOwnerDeck(target!, "Top");
                return false;
            case "forged-documents-order":
                return BeginForgedDocuments(command, player, source!, target!, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            // Forthrightness: show the Strategy (the public shown-cards record) and watch for the next action from a card with its title.
            case "forthrightness-show":
            {
                var shown = target!;
                _state.ShownCards.Add(new ShownCard(_state.ShownCards.Count + 1, player.Id, shown.DefinitionId, source!.Id, source.DefinitionId, "Hand", $"{Definition(source).Name}, shown from hand",
                    _state.TurnNumber, _state.Events.Count + 1L));
                Emit("cardShown", $"{player.Name} shows {Definition(shown).Name} ({Definition(source).Name}).", player.Id);
                _state.ForthrightnessWatches.Add(new() { PlayerId = player.Id, SamuraiId = command.PerformerId!, Title = Definition(shown).Name, Turn = _state.TurnNumber, SourceId = source.Id });
                return false;
            }
            // Forthrightness's granted ability: "Limited: Gain 1 Honor." on the Samurai -- his own action, so a dishonorable Samurai is rehonored instead.
            case "forthrightness-honor":
                GainHonorFromPerformer(source!, 1, "Forthrightness");
                return false;
            // Grief: "Dishonor him. His controller loses Honor equal to his base Personal Honor."
            case "grief-dishonor":
                Dishonor(target!, player.Id);
                GainHonor(target!.OwnerId, -Definition(target).PersonalHonor, Definition(source!).Name, source);
                return false;
            case "kitsuki-methods-interrogate":
            {
                SpendHandPlayedCard(player, source);
                if (!consumedGrant) _state.AdditionalActions.RemoveAll(preExistingGrants.Contains);
                var stay = _state.AdditionalActions.Any(g => g.PlayerId == player.Id && Forfeitable(g));
                if (OpenEitherOr(EitherOrSpecs.KitsukiMethodsDiscard, source!, player.Id, target!.OwnerId, target, "action", bowedBeforeAction, stay, command.PerformerId)) return true;
                // No decision for the target's controller (an empty hand): his refusal handed the acting player his own choice, which is open now.
                return _state.PendingEitherOr is not null;
            }
            // Kuon's Legacy: "Straighten him. Give him +3F. He may not perform actions. You may target and bow a card at his location."
            case "kuons-legacy-rage":
            {
                var kuon = Card(command.PerformerId!);
                Straighten(kuon);
                ApplyModifier(kuon, source!.Id, id, "Force", 3);
                LockActions(kuon, source.Id);
                foreach (var bowed in SlotCards(command, "bow").ToArray())
                    if (InPlay(bowed) && !StalwartNegatesBow(bowed, player.Id)) bowed.Bowed = true;
                return false;
            }
            case "lessons-never-forgotten-attack":
            {
                var yojimbo = Card(command.PerformerId!);
                if (ResolveOrDeferAttack(id, player, target?.Id, yojimbo.Id, consumedGrant)) return true;
                return BeginLessonsFollowUp(command, player.Id, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            }
            // Methodical Questioning: the Strategy is spent as it is played (the look defers the action's tail); the bowing play returns it to hand as the turn ends.
            case "methodical-questioning-look":
            case "methodical-questioning-bow-look":
                SpendHandPlayedCard(player, source);
                if (id == "methodical-questioning-bow-look") _state.ReturnsToHandBeforeTurnEnds.Add(new(source!.Id, player.Id, _state.TurnNumber));
                BeginLook(ability.Look!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            // Forge Your Soul: "Show the top four cards of your Fate deck." Then the attach and shuffle decisions (EitherOr.cs), chained.
            case "forge-your-soul-show":
            {
                SpendHandPlayedCard(player, source);
                if (!consumedGrant) _state.AdditionalActions.RemoveAll(preExistingGrants.Contains);
                var stay = _state.AdditionalActions.Any(g => g.PlayerId == player.Id && Forfeitable(g));
                var smith = Card(command.PerformerId!);
                var shown = ForgeShownCards(player.Id);
                foreach (var card in shown)
                {
                    _state.ShownCards.Add(new ShownCard(_state.ShownCards.Count + 1, player.Id, card.DefinitionId, source!.Id, source.DefinitionId, SearchDeck.Fate, $"{Definition(source).Name}, top of the Fate deck",
                        _state.TurnNumber, _state.Events.Count + 1L));
                    Emit("cardShown", $"{player.Name} shows {Definition(card).Name} ({Definition(source).Name}, top of the Fate deck).", player.Id);
                }
                if (shown.Length == 0) Emit("deckEmpty", $"{player.Name}'s Fate deck is empty: nothing is shown.", player.Id);
                var spec = shown.Any(IsWeapon) ? EitherOrSpecs.ForgeYourSoulAttach : EitherOrSpecs.ForgeYourSoulShuffleNoWeapon;
                if (OpenEitherOr(spec, source!, player.Id, player.Id, null, "action", bowedBeforeAction, stay, smith.Id)) return true;
                return _state.PendingEitherOr is not null;
            }
            // One Shout: "Give him +2F, or +4F if he has an attachment."
            case "one-shout-boost":
            {
                var shouter = Card(command.PerformerId!);
                ApplyModifier(shouter, source!.Id, id, "Force", HasAttachments(shouter) ? 4 : 2);
                return false;
            }
            default:
                return null;
        }
    }

    // ---- listed effect choices (EffectChoice.cs), decided by any player ------------------------------------------------------------------------------------
    // Pauses the action for deciderId to pick one of the listed options; the action finishes once it is answered (ResolveEffectChoice). A single option applies at once.
    private bool BeginShadowsEmbraceListedChoice(GameCommand command, int deciderId, EffectOption[] options, string prompt, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        if (options.Length == 0) return false;
        if (options.Length == 1)
        {
            foreach (var effect in options[0].Effects) ApplyShadowsEmbraceBatch3BranchEffect(effect, command);
            return false;
        }
        _state.PendingEffectChoice = new()
        {
            Command = command, PlayerId = deciderId, Negated = _state.NegatedEffects.ToList(), ConsumedGrant = consumedGrant, PlayedFromDiscard = playedFromDiscard,
            OpponentHonorBefore = opponentHonorBefore, BowedBefore = bowedBefore.ToList(), PreExistingGrants = preExistingGrants.ToList(), Options = options.ToList()
        };
        _state.PendingChoice = new("effectChoice", deciderId, command.CardId, prompt, command.PlayerId);
        _state.PriorityPlayerId = deciderId; _state.ConsecutivePasses = 0;
        return true;
    }

    // ApplyBranchEffect's arm for this batch's listed options; false for any other effect.
    private bool ApplyShadowsEmbraceBatch3BranchEffect(string effect, GameCommand command)
    {
        if (effect.StartsWith(Se3DestroyAttachmentEffect, StringComparison.Ordinal))
        {
            var attachment = Card(effect[Se3DestroyAttachmentEffect.Length..]);
            if (attachment.Zone == Zone.Attached) DestroyTargetCard(attachment, command.PlayerId);
            return true;
        }
        if (effect.StartsWith(Se3ForgedOrderEffect, StringComparison.Ordinal))
        {
            var parts = effect[Se3ForgedOrderEffect.Length..].Split(':');
            ApplyForgedDocuments(int.Parse(parts[0]), parts[1].Split(',').Select(int.Parse).ToArray(), command.CardId is null ? null : Card(command.CardId));
            return true;
        }
        return false;
    }

    // ---- Lessons Never Forgotten -----------------------------------------------------------------------------------------------------------------------------
    // "If you own a dead Courtier or Shugenja, you may target and destroy an attachment." -- independent of the Attack (asked whatever it did), and only when the condition holds.
    // The candidates are the attachments at the current battlefield (a Battle action's target stands there). The played Strategy is spent as the choice opens.
    private bool BeginLessonsFollowUp(GameCommand command, int playerId, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore,
        List<AdditionalActionGrant> preExistingGrants)
    {
        var ownsDeadAdvisor = _state.Cards.Values.Any(c => c.OwnerId == playerId && c.Zone == Zone.Dead && Definition(c).Type == CardType.Personality && (Keyword(c, "Courtier") || Keyword(c, "Shugenja")));
        if (!ownsDeadAdvisor) return false;
        var attachments = _state.Cards.Values.Where(c => c.Zone == Zone.Attached && Definition(c).Type is CardType.Follower or CardType.Item or CardType.Spell && AtBattle(c))
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        if (attachments.Length == 0) return false;
        var source = command.CardId is null ? null : Card(command.CardId);
        SpendHandPlayedCard(Player(playerId), source);
        var options = attachments.Select(a => new EffectOption($"destroy-{a.Id}", $"Destroy {Definition(a).Name} (on {Definition(Card(a.AttachedTo)).Name})", [Se3DestroyAttachmentEffect + a.Id]))
            .Append(new EffectOption("none", "Destroy no attachment", [])).ToArray();
        return BeginShadowsEmbraceListedChoice(command, playerId, options, $"{(source is null ? "Lessons Never Forgotten" : Definition(source).Name)}: you may target and destroy an attachment.",
            consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
    }

    // The deferred Attack's own completion (ResolveAttack), as Thunder's Wrath's follow-up is handed over there: every card bowed now counts as bowed before (an Attack bows nothing).
    private bool BeginLessonsFollowUpAfterDeferredAttack(PendingAttack attack, List<AdditionalActionGrant> grantsBeforeAttack)
    {
        if (attack.AbilityId != "lessons-never-forgotten-attack" || attack.Command is null) return false;
        var bowedNow = _state.Cards.Values.Where(c => c.Bowed).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        return BeginLessonsFollowUp(attack.Command, attack.PlayerId, attack.Additional, false, null, bowedNow, grantsBeforeAttack);
    }

    // ---- Forged Documents --------------------------------------------------------------------------------------------------------------------------------------
    // "His controller puts all cards in his Provinces at the bottom of his Dynasty deck in any order, refilling them face-up." The order is his: one option per order of his
    // occupied provinces, named by province (a face-down card is never named); with one card or none there is nothing to order.
    private bool BeginForgedDocuments(GameCommand command, PlayerState player, CardInstance source, CardInstance target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        var controller = Player(target.OwnerId);
        var occupied = controller.Provinces.Where(p => !p.Destroyed && p.CardId is not null).Select(p => p.Index).OrderBy(i => i).ToArray();
        if (occupied.Length <= 1) { ApplyForgedDocuments(controller.Id, occupied, source); return false; }
        SpendHandPlayedCard(player, source);
        string Describe(int index)
        {
            var card = Card(controller.Provinces.First(p => p.Index == index).CardId);
            return $"province {index + 1} ({(card.FaceUp ? Definition(card).Name : "face-down")})";
        }
        var options = Permutations(occupied).Select(order => new EffectOption($"order-{string.Join("-", order)}",
            $"Bottom of the Dynasty deck in this order, the last lowest: {string.Join(", ", order.Select(Describe))}",
            [$"{Se3ForgedOrderEffect}{controller.Id}:{string.Join(",", order)}"])).ToArray();
        return BeginShadowsEmbraceListedChoice(command, controller.Id, options, $"{Definition(source).Name}: put the cards in your provinces at the bottom of your Dynasty deck in any order.",
            consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
    }

    private static IEnumerable<int[]> Permutations(int[] items)
    {
        if (items.Length <= 1) { yield return items; yield break; }
        for (var i = 0; i < items.Length; i++)
            foreach (var rest in Permutations(items.Where((_, j) => j != i).ToArray()))
                yield return [items[i], .. rest];
    }

    private void ApplyForgedDocuments(int ownerId, int[] order, CardInstance? source)
    {
        var owner = Player(ownerId);
        var name = source is null ? "Forged Documents" : Definition(source).Name;
        var emptied = new List<ProvinceState>();
        foreach (var index in order)
        {
            var province = owner.Provinces.FirstOrDefault(p => p.Index == index && !p.Destroyed);
            if (province?.CardId is not { } cardId) continue;
            var card = Card(cardId);
            var wasFaceUp = card.FaceUp;
            province.CardId = null;
            card.Zone = Zone.DynastyDeck; card.ProvinceIndex = null; card.FaceUp = false; card.AbilityUses.Clear();
            owner.DynastyDeck.Add(card.Id);
            emptied.Add(province);
            // A face-down card stays hidden: neither its name nor its id is announced.
            if (wasFaceUp) Emit("toDeck", $"{name}: {Definition(card).Name} from {owner.Name}'s province {index + 1} was put on the bottom of the Dynasty deck.", owner.Id);
            else Emit("toDeck", $"{name}: the face-down card in {owner.Name}'s province {index + 1} was put on the bottom of the Dynasty deck.", owner.Id);
        }
        foreach (var province in emptied)
        {
            if (province.Destroyed || owner.DynastyDeck.Count == 0) continue;
            var refillId = owner.DynastyDeck[0]; owner.DynastyDeck.RemoveAt(0);
            var refill = Card(refillId);
            province.CardId = refillId; refill.Zone = Zone.Province; refill.ProvinceIndex = province.Index; refill.FaceUp = true;
            Emit("provinceRefilled", $"{owner.Name} refilled province {province.Index + 1} face up with {Definition(refill).Name} ({name}).", owner.Id, refillId);
        }
    }

    // ---- Cowed and Defeated ------------------------------------------------------------------------------------------------------------------------------------
    // "After the next time (this turn) another player's action resolves that targeted your performer": called where an action is truly done, with its targets.
    private void FireCowedAndDefeatedActionWatches(int actingPlayerId, GameCommand? command, string? extraTargetId = null)
    {
        if (_state.CowedAndDefeatedWatches.Count == 0) return;
        var targeted = (command is null ? [] : ActionTargetIds(command)).Append(extraTargetId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var watch in _state.CowedAndDefeatedWatches.ToArray())
        {
            if (watch.Turn != _state.TurnNumber) { _state.CowedAndDefeatedWatches.Remove(watch); continue; }
            if (actingPlayerId == watch.PlayerId || !targeted.Contains(watch.PerformerId)) continue;
            FireCowedAndDefeatedWatch(watch, "another player's action that targeted him resolved");
        }
    }

    // "... or a battle resolves which destroyed him": the armies of the battle that just resolved, read after its destruction.
    private void FireCowedAndDefeatedBattleWatches(CardInstance[] attackers, CardInstance[] defenders)
    {
        if (_state.CowedAndDefeatedWatches.Count == 0) return;
        foreach (var watch in _state.CowedAndDefeatedWatches.ToArray())
        {
            if (watch.Turn != _state.TurnNumber) { _state.CowedAndDefeatedWatches.Remove(watch); continue; }
            if (!attackers.Concat(defenders).Any(c => c.Id == watch.PerformerId) || InPlay(Card(watch.PerformerId))) continue;
            FireCowedAndDefeatedWatch(watch, "the battle's resolution destroyed him");
        }
    }

    private void FireCowedAndDefeatedWatch(CowedAndDefeatedWatch watch, string what)
    {
        _state.CowedAndDefeatedWatches.Remove(watch);
        var target = Card(watch.TargetId); var source = Card(watch.SourceId);
        Emit("watchFired", $"{Definition(source).Name}: {Definition(Card(watch.PerformerId)).Name} -- {what}. {Player(target.OwnerId).Name} loses 2 Honor.", watch.PlayerId, source.Id);
        GainHonor(target.OwnerId, -2, Definition(source).Name, source);
    }

    // ---- Methodical Questioning ----------------------------------------------------------------------------------------------------------------------------
    // "Put this card into your hand from your discard pile before this turn ends" -- where the turn ends (EndTurn), if the card is in its owner's discard pile then.
    private void ResolveReturnsToHandBeforeTurnEnds()
    {
        var due = _state.ReturnsToHandBeforeTurnEnds.ToArray();
        _state.ReturnsToHandBeforeTurnEnds.Clear();
        foreach (var entry in due)
            if (entry.Turn == _state.TurnNumber && _state.Cards.TryGetValue(entry.CardId, out var card) && card.Zone == Zone.FateDiscard && card.OwnerId == entry.PlayerId)
                ReturnToHand(card);
    }

    // ---- Fields of Victory ---------------------------------------------------------------------------------------------------------------------------------
    // "If your army has more total Force than the enemy army, reduce the Force of each Personality and Follower in the enemy army to 0." -- the Terrain's controller's army,
    // totalled exactly as the resolution will total it (ArmyForces), read when its turn in the before-resolution set comes.
    private void FieldsOfVictory(CardInstance terrain, PendingBeforeBattleResolve pending)
    {
        var attackers = Army(pending.Attacker, pending.ProvinceIndex).ToArray(); var defenders = Army(pending.Defender, pending.ProvinceIndex).ToArray();
        var (attackerForce, defenderForce) = ArmyForces(attackers, defenders, pending.Attacker, pending.Defender);
        var attacking = terrain.OwnerId == pending.Attacker;
        var (mine, theirs) = attacking ? (attackerForce, defenderForce) : (defenderForce, attackerForce);
        if (mine <= theirs)
        {
            Emit("beforeBattleEffect", $"{Definition(terrain).Name}: {Player(terrain.OwnerId).Name}'s army does not have more total Force ({mine} to {theirs}); nothing happens.", terrain.OwnerId, terrain.Id);
            return;
        }
        foreach (var enemy in (attacking ? defenders : attackers).ToArray())
        {
            if (!InPlay(enemy)) continue;
            foreach (var follower in Attachments(enemy.Id).Where(a => Definition(a).Type == CardType.Follower).ToArray())
                if (CardForce(follower) > 0) ApplyModifier(follower, terrain.Id, "fields-of-victory-zero", "Force", -CardForce(follower));
            if (CardForce(enemy) > 0) ApplyModifier(enemy, terrain.Id, "fields-of-victory-zero", "Force", -CardForce(enemy));
        }
        Emit("beforeBattleEffect", $"{Definition(terrain).Name}: the Force of each Personality and Follower in the enemy army is reduced to 0.", terrain.OwnerId, terrain.Id);
    }

    // ---- Kitsuki Methods (EitherOr.cs) ---------------------------------------------------------------------------------------------------------------------
    private string ShadowsEmbraceEitherOrPrompt(PendingEitherOr p, string source, string affected) => p.SpecId switch
    {
        EitherOrSpecs.KitsukiMethodsDiscard => $"{source}: discard a card at random from your hand, or your opponent chooses to gain 2 Honor or make you lose Honor equal to {affected}'s base Personal Honor.",
        EitherOrSpecs.KitsukiMethodsReward => $"{source}: gain 2 Honor, or {affected}'s controller loses Honor equal to his base Personal Honor.",
        EitherOrSpecs.ForgeYourSoulAttach => $"{source}: you may attach one of the shown Weapons to your performer, paying 4 less Gold.",
        EitherOrSpecs.ForgeYourSoulShuffle or EitherOrSpecs.ForgeYourSoulShuffleNoWeapon => $"{source}: you may shuffle your Fate deck.",
        _ => $"{source}: move {affected} home, or leave him at the battlefield."
    };

    private EitherOrOption[] ShadowsEmbraceEitherOrOptions(PendingEitherOr p, CardInstance? affected)
    {
        var name = affected is null ? "the target" : Definition(affected).Name;
        var honor = affected is null ? 0 : Definition(affected).PersonalHonor;
        return p.SpecId switch
        {
            EitherOrSpecs.KitsukiMethodsDiscard =>
            [
                new("discard", "Discard a card at random", "A random card of your hand is discarded.", EitherOrKind.Avoid, 0, Player(p.DeciderPlayerId).Hand.Count > 0, "You have no card to discard."),
                new("refuse", "Do not discard", $"Your opponent gains 2 Honor, or you lose {honor} Honor -- his choice.", EitherOrKind.Accept, 0, true, null)
            ],
            EitherOrSpecs.ForgeYourSoulAttach => ForgeAttachOptions(p),
            EitherOrSpecs.ForgeYourSoulShuffle or EitherOrSpecs.ForgeYourSoulShuffleNoWeapon =>
            [
                new("shuffle", "Shuffle your Fate deck", "Your Fate deck is shuffled.", EitherOrKind.Choose, 0, true, null),
                new("keep", "Do not shuffle", "The shown cards stay where they are.", EitherOrKind.Choose, 0, true, null)
            ],
            EitherOrSpecs.KitsukiMethodsReward =>
            [
                new("honor", "Gain 2 Honor", "You gain 2 Honor.", EitherOrKind.Choose, 0, true, null),
                new("shame", $"{name}'s controller loses {honor} Honor", $"Your opponent loses Honor equal to {name}'s base Personal Honor.", EitherOrKind.Choose, 0, true, null)
            ],
            _ =>
            [
                new("home", $"Move {name} home", $"{name} moves home.", EitherOrKind.Choose, 0, affected is not null && affected.Zone == Zone.Battlefield, $"{name} is no longer at the battlefield."),
                new("stay", $"Leave {name} there", "Nothing happens.", EitherOrKind.Choose, 0, true, null)
            ]
        };
    }

    private void ApplyShadowsEmbraceEitherOr(PendingEitherOr p, EitherOrOption option, PlayerState decider, CardInstance? affected, string[]? sourceIds)
    {
        var source = Card(p.SourceCardId); var name = Definition(source).Name;
        switch (p.SpecId, option.Key)
        {
            // Forge Your Soul: attach the chosen Weapon (or none), then the shuffle decision.
            case (EitherOrSpecs.ForgeYourSoulAttach, _):
            {
                CardInstance? attached = null;
                if (option.Key.StartsWith("attach-", StringComparison.Ordinal) && p.PerformerId is not null)
                {
                    var weapon = ForgeShownCards(decider.Id)[int.Parse(option.Key["attach-".Length..])];
                    var smith = Card(p.PerformerId);
                    Pay(decider.Id, weapon, option.GoldCost, sourceIds, paying: new PaymentSubject(weapon, PaymentKind.Equip));
                    decider.FateDeck.Remove(weapon.Id);
                    weapon.AbilityUses.Clear(); weapon.Bowed = false;
                    weapon.Zone = Zone.Attached; weapon.AttachedTo = smith.Id; weapon.FaceUp = true; weapon.ProvinceIndex = smith.ProvinceIndex;
                    NoteAttached(decider.Id, weapon, "deck"); // Issue #501 (EmperorMechD.cs): the attach log.
                    if (Definition(weapon).AttachGrantsKeyword is string granted) GrantKeyword(smith, granted, weapon.Id, "Attachment");
                    Emit("equipped", $"{Definition(weapon).Name} attached to {Definition(smith).Name} from the top of the Fate deck ({name}).", decider.Id, weapon.Id);
                    attached = weapon;
                }
                else Emit("eitherOrChosen", $"{decider.Name} attached no Weapon ({name}).", decider.Id, source.Id);
                OpenEitherOr(EitherOrSpecs.ForgeYourSoulShuffle, source, p.ActingPlayerId, decider.Id, attached, p.Origin, p.BowedBefore, p.StayWithActor, p.PerformerId);
                break;
            }
            case (EitherOrSpecs.ForgeYourSoulShuffle or EitherOrSpecs.ForgeYourSoulShuffleNoWeapon, "shuffle"):
                ShuffleDeck(decider, SearchDeck.Fate);
                break;
            case (EitherOrSpecs.ForgeYourSoulShuffle or EitherOrSpecs.ForgeYourSoulShuffleNoWeapon, _):
                Emit("eitherOrChosen", $"{decider.Name} did not shuffle ({name}).", decider.Id, source.Id);
                break;
            case (EitherOrSpecs.KitsukiMethodsDiscard, "discard"):
            {
                var hand = decider.Hand.ToList();
                var random = new StableRandom(_state.RngState);
                random.Shuffle(hand);
                _state.RngState = random.State;
                Emit("eitherOrChosen", $"{decider.Name} chose to discard a card at random ({name}).", decider.Id, source.Id);
                DiscardFate(decider, Card(hand[0]));
                break;
            }
            case (EitherOrSpecs.KitsukiMethodsDiscard, _):
                Emit("eitherOrChosen", $"{decider.Name} did not discard ({name}): {Player(p.ActingPlayerId).Name} chooses.", decider.Id, source.Id);
                OpenEitherOr(EitherOrSpecs.KitsukiMethodsReward, source, p.ActingPlayerId, p.ActingPlayerId, affected, p.Origin, p.BowedBefore, p.StayWithActor, p.PerformerId);
                break;
            case (EitherOrSpecs.KitsukiMethodsReward, "honor"):
            {
                Emit("eitherOrChosen", $"{decider.Name} chose to gain 2 Honor ({name}).", decider.Id, source.Id);
                // The performing Courtier's action: a dishonorable one is rehonored instead of the gain.
                if (p.PerformerId is not null && _state.Cards.TryGetValue(p.PerformerId, out var courtier) && courtier.OwnerId == decider.Id) GainHonorFromPerformer(courtier, 2, name);
                else GainHonor(decider.Id, 2, name, source);
                break;
            }
            case (EitherOrSpecs.KitsukiMethodsReward, _):
                Emit("eitherOrChosen", $"{decider.Name} chose: {Definition(affected!).Name}'s controller loses Honor equal to his base Personal Honor ({name}).", decider.Id, source.Id);
                GainHonor(affected!.OwnerId, -Definition(affected).PersonalHonor, name, source);
                break;
            case (EitherOrSpecs.CrowdedStreetsMoveHome, "home"):
            {
                Emit("eitherOrChosen", $"{decider.Name} moves {Definition(affected!).Name} home ({name}).", decider.Id, affected!.Id);
                // A trait's move, not an action's effect: it is not recorded as something the next action did.
                var recorded = _state.CurrentActionAffected.Count;
                MoveHome(affected!, false);
                if (_state.CurrentActionAffected.Count > recorded) _state.CurrentActionAffected.RemoveRange(recorded, _state.CurrentActionAffected.Count - recorded);
                break;
            }
            default:
                Emit("eitherOrChosen", $"{decider.Name} leaves {Definition(affected!).Name} at the battlefield ({name}).", decider.Id, source.Id);
                break;
        }
    }

    // ---- Forge Your Soul -----------------------------------------------------------------------------------------------------------------------------------
    // The shown cards: the top four of the acting player's Fate deck (fewer when it is short) -- nothing changes the deck while its decisions are open, so they are read again.
    private CardInstance[] ForgeShownCards(int playerId) => Player(playerId).FateDeck.Take(4).Select(Card).ToArray();

    private bool IsWeapon(CardInstance card) => Definition(card).Type == CardType.Item && Keyword(card, "Weapon");

    // One option per shown Weapon (named by its place in the deck, never by its instance id), each at its Equip cost less 4 and legal only if the performer may attach it and
    // its player can pay; and "none".
    private EitherOrOption[] ForgeAttachOptions(PendingEitherOr p)
    {
        var smith = p.PerformerId is not null && _state.Cards.TryGetValue(p.PerformerId, out var found) ? found : null;
        var options = new List<EitherOrOption>();
        var shown = ForgeShownCards(p.DeciderPlayerId);
        for (var position = 0; position < shown.Length; position++)
        {
            var weapon = shown[position];
            if (!IsWeapon(weapon)) continue;
            var def = Definition(weapon);
            var attachable = smith is not null && InPlay(smith) && Definition(smith).Type == CardType.Personality && AttachmentLegalTarget(def, smith) && AttachmentAllowed(weapon, smith)
                && !((Keyword(weapon, "Unique") || Keyword(weapon, "Singular")) && ControlsCopyOf(p.DeciderPlayerId, def.Name));
            var cost = smith is null ? 0 : Math.Max(0, EquipGoldCost(p.DeciderPlayerId, weapon, smith, fromHand: false).Cost - 4);
            var payable = cost == 0 || PaymentOptions(p.DeciderPlayerId, cost).Length > 0;
            options.Add(new($"attach-{position}", $"Attach {def.Name} ({cost} Gold)", $"{def.Name} attaches to {(smith is null ? "your performer" : Definition(smith).Name)}.", EitherOrKind.Choose, cost,
                attachable && payable, attachable ? $"You cannot produce {cost} Gold." : "He may not attach that Weapon."));
        }
        options.Add(new("none", "Attach no Weapon", "No card is attached.", EitherOrKind.Choose, 0, true, null));
        return options.ToArray();
    }

    // FinishEitherOr's hook. Crowded Streets: the resolved action's own tail carries on. Forge Your Soul's shuffle decision: "If you did not show a Weapon and he is a Kensai, draw a
    // card", then the attached Weapon's entering-play window. True when it took the finish over.
    private bool FinishShadowsEmbraceEitherOr(PendingEitherOr p)
    {
        if (p.Origin == ShadowsEmbraceAfterActionOrigin)
        {
            _state.PendingChoice = null;
            ContinueAfterActionTail(p.ActingPlayerId, p.ResumePlayerId);
            return true;
        }
        if (p.SpecId is not (EitherOrSpecs.ForgeYourSoulShuffle or EitherOrSpecs.ForgeYourSoulShuffleNoWeapon)) return false;
        _state.PendingChoice = null;
        var actor = Player(p.ActingPlayerId);
        if (p.SpecId == EitherOrSpecs.ForgeYourSoulShuffleNoWeapon && p.PerformerId is not null && _state.Cards.TryGetValue(p.PerformerId, out var smith) && InPlay(smith) && Keyword(smith, "Kensai"))
            Draw(actor);
        if (p.AffectedCardId is not null && _state.Cards.TryGetValue(p.AffectedCardId, out var weapon) && weapon.Zone == Zone.Attached && OpenEntryReactionWindow(actor.Id, weapon, 1 - actor.Id)) return true;
        if (p.BowedBefore is not null && OpenBowReactionWindow(actor.Id, p.BowedBefore.ToHashSet(StringComparer.Ordinal), 1 - actor.Id)) return true;
        if (OpenOverconfidentDrawWindow(1 - actor.Id)) return true;
        NextAction(actor.Id, p.StayWithActor);
        return true;
    }

    // ---- Crowded Streets -----------------------------------------------------------------------------------------------------------------------------------
    // "If you are the Defender, you may take an additional Battle action." -- put into play by its controller (PlayStrategy). True when a grant was made (priority stays).
    private bool CrowdedStreetsDefenderGrant(int playerId, CardInstance terrain)
    {
        if (Definition(terrain).Effect != CrowdedStreetsEffect || playerId == _state.ActivePlayerId) return false;
        GrantAdditionalAction(playerId, null);
        Emit("additionalActionGranted", $"{Definition(terrain).Name}: {Player(playerId).Name}, the Defender, may take an additional Battle action.", playerId, terrain.Id);
        return true;
    }

    // "After an action resolves that one or more of your Personalities at the current battlefield performed: You may move any of those performers home." A trait of the Terrain
    // in play at the current battlefield, asked of its controller where the action is truly done (FinishActionEffectTracking), before any Reaction to the same moment. The
    // performer is the resolved action's (its named performer, else the Personality it is on). True when the decision is open: its finish carries on with the action's tail.
    private bool OpenCrowdedStreetsTrait(int closesTo)
    {
        if (_state.Phase != Phase.Battle || _state.CurrentBattleProvince is not int battle || _state.LastResolvedAction is not { } facts) return false;
        var streets = TerrainsAtBattlefield(battle).FirstOrDefault(t => Definition(t).Effect == CrowdedStreetsEffect && t.OwnerId == facts.PlayerId);
        if (streets is null) return false;
        var performer = facts.PerformerId is not null && _state.Cards.TryGetValue(facts.PerformerId, out var named) ? named : PerformerOfResolvedAction(facts);
        if (performer is null || Definition(performer).Type != CardType.Personality || performer.OwnerId != streets.OwnerId || !AtBattle(performer)) return false;
        SuspendOpenWindow();
        return OpenEitherOr(EitherOrSpecs.CrowdedStreetsMoveHome, streets, facts.PlayerId, streets.OwnerId, performer, ShadowsEmbraceAfterActionOrigin, resumePlayerId: closesTo);
    }

    // The rest of a resolved action's tail once a trait's decision is answered (FinishActionEffectTracking's own order): the after-action Reactions, Moshi Ino's storm, then priority on.
    private void ContinueAfterActionTail(int actingPlayerId, int closesTo)
    {
        if (OpenAfterActionResolvedWindow(closesTo)) return;
        if (OpenInoStorm(closesTo)) return;
        NextAction(actingPlayerId, closesTo == actingPlayerId);
    }

    // ---- Forthrightness ------------------------------------------------------------------------------------------------------------------------------------
    // Every action its player announces on a Strategy (in hand, in play as a Terrain, or played from a discard pile) counts against the watch.
    private void NoteForthrightnessStrategyAction(int playerId, CardInstance card)
    {
        if (_state.ForthrightnessWatches.Count == 0 || Definition(card).Type != CardType.Strategy) return;
        foreach (var watch in _state.ForthrightnessWatches.Where(w => w.PlayerId == playerId && w.Turn == _state.TurnNumber)) watch.StrategyActionsAnnounced++;
    }

    // An action of this player's from this card resolves: a watch naming its title is judged -- granted when no action on a Strategy other than this one was announced since.
    private void ResolveForthrightnessWatches(int playerId, CardInstance card)
    {
        if (_state.ForthrightnessWatches.Count == 0) return;
        _state.ForthrightnessWatches.RemoveAll(w => w.Turn != _state.TurnNumber);
        foreach (var watch in _state.ForthrightnessWatches.Where(w => w.PlayerId == playerId && w.Title == Definition(card).Name).ToArray())
        {
            _state.ForthrightnessWatches.Remove(watch);
            var others = watch.StrategyActionsAnnounced - (Definition(card).Type == CardType.Strategy ? 1 : 0);
            var samurai = _state.Cards.TryGetValue(watch.SamuraiId, out var found) ? found : null;
            if (others > 0 || samurai is null || !InPlay(samurai))
            {
                Emit("watchLapsed", $"Forthrightness: {Player(playerId).Name} resolved {Definition(card).Name}, but {(others > 0 ? "announced other actions on Strategies first" : "the Samurai is no longer in play")}.", playerId, card.Id);
                continue;
            }
            GrantAbility(samurai, "forthrightness-honor", watch.SourceId, GrantDuration.Permanent);
            Emit("abilityGranted", $"Forthrightness: {Definition(samurai).Name} permanently gains \"Limited: Gain 1 Honor.\"", playerId, samurai.Id);
        }
    }
}
