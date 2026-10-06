namespace L5R.Core;

/// <summary>Paralyzing Touch (oracle-l5r-9919): the effects of the next action the Personality <paramref name="CardId"/> performs on turn <paramref name="Turn"/>
/// are negated.</summary>
public sealed record ParalyzingTouchWatch(string CardId, int Turn, string Source);

/// <summary>Spiritual Manipulation (oracle-l5r-9929): <paramref name="PlayerId"/> must declare an attack on turn <paramref name="Turn"/> and assign
/// <paramref name="TargetId"/> to attack if that is legal.</summary>
public sealed record ForcedAttack(int PlayerId, string TargetId, int Turn, string Source);

/// <summary>Preserving Beauty (oracle-l5r-9921): <paramref name="PlayerId"/> gains 3 Honor before turn <paramref name="Turn"/> ends if the province of
/// <paramref name="ProvinceOwnerId"/> at <paramref name="ProvinceIndex"/> (the battlefield the action was taken at) has not been destroyed.</summary>
public sealed record PreservingBeautyCheck(int PlayerId, int ProvinceOwnerId, int ProvinceIndex, int Turn, string Source);

// The Shadow's Embrace pass (issue #476), batch 4: the helpers behind strategy-wave-57 -- Overcome Adversity, Preserving Beauty, Running Battle, Serenity in Air,
// Social Grace, Subtle Games, Sudden Movement, Tactical Assault, The Lesson of Earth, Theological Indecision, Thuggish Techniques, Unstable Ground, Void Tattoo,
// Watchers in the Dark, Paralyzing Touch, Spiritual Manipulation and The Game Has Changed. Each card's ability definitions and reading are documented at its
// entry in CardAbilities.cs; only the execution/validation helpers and the small primitives live here. The shared files gained only additive arms:
//
//   * A DELAYED PROVINCE-HELD HONOR CHECK (MatchState.PreservingBeautyChecks): "before this turn ends, gain 3 Honor if that battlefield's province has not been
//     destroyed", drained in EndTurn beside Asahina Shigemitsu's.
//   * A SWITCH (SwitchPlan/SwitchBlocked, Sudden Movement; core rules Glossary, Switch): two units simultaneously swap locations, and negating one side's movement negates the other's too --
//     every negation that would stop either movement is asked first, without consuming anything, and only then does either move.
//   * A "DISHONOR" NEGATION KIND (MatchState.NegatedEffects, read by Dishonor): "negate his dishonoring from the action's effects" (Social Grace), offered only against
//     an action that dishonors its target (DishonorsTarget, curated like DestroysTarget).
//   * A STANDING ASSIGNMENT WATCH (AssignmentWatch kind WatchSocialGrace): "after EACH time he is assigned to attack this turn" -- not spent when it fires.
//   * A WHOLE-CARD ABILITY LOCK (DisabledAbility with AllAbilitiesUsageKey): "abilities on cards (now) in the target's unit may not be used" (Watchers in the Dark),
//     Doji Hakuseki's single-ability lock widened to every ability of the card, for the turn.
//   * A TERRAIN PLAYED THROUGH ITS OWN ABILITY (Unstable Ground): its play prints a cost and a target, so the generic playStrategy Terrain rule neither lists nor
//     accepts it (TerrainPlaysThroughOwnAbility), and its ability puts it into play at the battlefield (PutTerrainIntoPlay).
//   * A DELAYED NEGATION OF A FUTURE ACTION (MatchState.ParalyzingTouchWatches): "after this duel ends, negate the effects of the next action performed by its
//     winner" -- asked as every action begins to resolve (ExecuteAbilityEffects, beside Burning Dreams): an action the watched Personality performs (as its named
//     performer, its source, the wielder of its source, a performing slot card, or the Personality a Lobby bows) is taken, its costs paid, its effects negated
//     (ResolveNegatedAction), unless it cannot be negated (ActionProtected). The duration is the turn, none being printed. An Equip, a Strategy's playStrategy Terrain
//     play and a Reserve recruit are the controller acting, not a Personality performing (War Weariness's own reading), so they neither spend nor meet it.
//   * A FORCED ATTACK (MatchState.ForcedAttacks): "his controller must declare a normal attack this turn and assign the target (if legal)" -- the Attack
//     Declaration's decline is refused while it is owed, and an attackers' assignment in the Maneuvers segment the target could be assigned in must include him.
//     "May not declare an attack" (Utaku Chikako) wins over it, as "may not" forbids the choice outright.
//   * A REPEATED RESOLUTION-TIME CHOICE (The Game Has Changed): an engine-listed effect choice that opens again after each answer until the player is done
//     (ContinueShadowsEmbraceBatch4Choice, called from ResolveEffectChoice), with options that straighten one attachment, or transfer one attachment to another
//     target (AttachmentTransfers.cs), each attachment at most once.
//   * The follow-up choice "you may move your Scout home" (BranchCondition.Always, BranchEffect.MovePerformerHome), the Focus Effect "give its winner +3F", and a
//     cost "remove this card from the game" paid by a Strategy still in its player's hand (PayAbilityCosts now takes it out of the hand as well).
public sealed partial class MatchEngine
{
    /// <summary>Social Grace: "After each time he is assigned to attack this turn, gain 2 Honor" -- an assignment watch that stays for the turn.</summary>
    public const string WatchSocialGrace = "social-grace";
    /// <summary>A DisabledAbility usage key that locks every ability of its card (Watchers in the Dark).</summary>

    // ---- validation (ValidateAbility, after the per-id Requires; also run for each candidate of a slot, with ability.Slots null) ----

    private void ValidateShadowsEmbraceBatch4(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // Preserving Beauty: "If any enemy units are at the current battlefield" -- a condition on taking the action.
            case "preserving-beauty":
                Require(_state.CurrentBattleProvince is int beautyBattle && Army(1 - player.Id, beautyBattle).Any(), "There are no enemy units at the current battlefield.");
                break;
            // Running Battle: "If you have Reconnaissance, you may target one of his attachments."
            case "running-battle" when ability.Slots is not null:
                if (SlotCards(command, "attachment").Any())
                    Require(HasReconnaissanceAtCurrentBattlefield(player.Id), "You may target one of his attachments only if you have Reconnaissance.");
                break;
            // Serenity in Air: a Kata is put into play from the hand, and its Battle/Open action is taken only while it is in play (KataCards.cs, Skipping the Puddle).
            case "serenity-air-enter":
                Require(source!.Zone == Zone.Hand && player.Hand.Contains(source.Id), "A Kata is put into play from your hand.");
                break;
            case "serenity-air-straighten":
            case "serenity-air-straighten-open":
                Require(source!.Zone == Zone.Home, "Serenity in Air must be in play.");
                break;
            // Void Tattoo: "discard a card unless your Personality is Void" -- the discarding entry is taken by a performer who is not Void.
            case "void-tattoo-return":
                if (command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var tattooed))
                    Require(!Keyword(tattooed, "Void"), "Your Personality is Void: no card is discarded.");
                break;
            // The Game Has Changed: "one to three Personalities controlled by the same player".
            case "game-has-changed" when ability.Slots is not null:
                Require(SlotCards(command, "targets").Select(c => c.OwnerId).Distinct().Count() <= 1, "The targets must be controlled by the same player.");
                break;
            // Unstable Ground: "If there is no Terrain in play at the current battlefield" -- and it is played from the hand.
            case "unstable-ground-destroy":
                Require(source!.Zone == Zone.Hand && player.Hand.Contains(source.Id), "Unstable Ground is played from your hand.");
                Require(!TerrainAtCurrentBattlefield() || MarshallingFieldsWaiverAvailable(player.Id), "There is already a Terrain in play at the current battlefield."); // The Marshalling Fields (issue #501)
                break;
        }
        // A Strategy played from the hand is not also the card it discards to pay for itself (Void Tattoo's "discard a card", judged for each "hand" slot candidate).
        if (id is "void-tattoo-return" or "serenity-air-straighten-open" && ability.Target == "hand" && command.TargetId is not null && command.TargetId == command.CardId)
            Require(false, "The card you play is not the card you discard.");
        if (id is "void-tattoo-return" or "serenity-air-straighten-open" && ability.Slots is not null && command.CardId is not null)
            Require(!SlotCards(command, "discard").Any(c => c.Id == command.CardId), "The card you play is not the card you discard.");
    }

    // Sudden Movement's two targets: Personalities of one controller at two different locations (each at home or at an unresolved battlefield). The first is any
    // Personality that has such a partner (so the default offer never picks one that leaves the second slot empty); the second is a partner of the first.
    private bool SwitchPartners(CardInstance first, CardInstance second) =>
        first.Id != second.Id && first.OwnerId == second.OwnerId && Definition(second).Type == CardType.Personality && InPlay(first) && InPlay(second)
        && AtAnyLocation(first) && AtAnyLocation(second) && LocationOf(first) is { } here && LocationOf(second) is { } there && here != there;

    private bool HasSwitchPartner(CardInstance card) => _state.Cards.Values.Any(other => SwitchPartners(card, other));

    private bool SwitchPartnerAllowed(GameCommand command, CardInstance target) =>
        SlotCards(command, "first").FirstOrDefault() is { } first ? SwitchPartners(first, target) : HasSwitchPartner(target);

    private bool TerrainAtCurrentBattlefield() =>
        _state.CurrentBattleProvince is int province && _state.Cards.Values.Any(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == province && Keyword(c, "Terrain"));

    // ---- execution (RunAbilityEffects' default): null when the id is not this batch's; false when its effects are done; true when a window opened ----

    private bool? RunShadowsEmbraceBatch4(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant,
        bool playedFromDiscard, int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            // Overcome Adversity: "Reduce his Force by your Paragon's Force. Draw a card. You may take an additional Battle action. Lose 5 Honor."
            case "overcome-adversity":
                {
                    var paragon = Card(command.PerformerId!);
                    ApplyModifier(target!, source!.Id, id, "Force", -CardForce(paragon));
                    Draw(player);
                    GrantAdditionalAction(player.Id, null);
                    GainHonor(player.Id, -5, Definition(source).Name, source);
                    return false;
                }
            // Preserving Beauty: the check names the battlefield's province now; it is judged as the turn ends.
            case "preserving-beauty":
                // A battlefield without a province has none to keep (issue #492, rulings pass R1): nothing is queued.
                if (CurrentProvinceIndex() is not int beautyProvince) return false;
                _state.PreservingBeautyChecks.Add(new(player.Id, 1 - _state.ActivePlayerId, beautyProvince, _state.TurnNumber, Definition(source!).Name));
                Emit("delayedHonorQueued", $"{Definition(source!).Name}: {player.Name} gains 3 Honor before this turn ends if this battlefield's province has not been destroyed.", player.Id, source!.Id);
                return false;
            // Running Battle: -4F; the chosen attachment (only with Reconnaissance) goes to its owner's hand; then the Scout may move home.
            case "running-battle":
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                foreach (var attachment in SlotCards(command, "attachment").ToArray())
                    if (attachment.Zone == Zone.Attached) ReturnToHand(attachment);
                return BeginConditionalChoice(command, ability, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            // Serenity in Air.
            case "serenity-air-enter":
                EnterPlayAsKata(player, source!);
                return false;
            case "serenity-air-straighten":
            case "serenity-air-straighten-open":
                Straighten(SlotCards(command, "target").FirstOrDefault() ?? target!);
                return false;
            // Social Grace's Reaction: the held action's dishonoring of the targeted Courtier is negated.
            case "social-grace-negate":
                NegateEffects(Card(HeldAction().TargetId!), "dishonor");
                return false;
            // Social Grace's Open action: the penalty is the Courtier's Personal Honor now; the watch pays 2 Honor each time he is assigned to attack this turn.
            case "social-grace-penalty":
                {
                    var courtier = Card(command.PerformerId!);
                    ApplyModifier(target!, source!.Id, id, "Force", -CardPersonalHonor(courtier));
                    _state.AssignmentWatches.Add(new(target!.Id, WatchSocialGrace, player.Id, _state.TurnNumber, Definition(source).Name));
                    return false;
                }
            // Subtle Games: the Courtier was bowed as the cost.
            case "subtle-games-dishonor":
                Dishonor(target!);
                Draw(player);
                return false;
            // Tactical Assault.
            case "tactical-assault-destroy":
                DestroyTargetCard(target!, player.Id);
                return false;
            // The Lesson of Earth: the move, then the straighten unless the Monk is Earth (read now).
            case "lesson-of-earth-move":
                ExecuteMove(command, ability, source, target);
                if (!Keyword(Card(command.PerformerId!), "Earth") && InPlay(target!)) StraightenUnit(target!);
                return false;
            // Theological Indecision.
            case "theological-indecision-discard":
                target!.Zone = Zone.DynastyDiscard; target.FaceUp = true;
                Emit("celestialDiscarded", $"{Definition(target).Name} was discarded.", target.OwnerId, target.Id);
                // Ryoshun's Guidance's +1C leaves with it: a Personality it alone kept above 0 Chi is destroyed (its own discard path's rule).
                if (Definition(target).Effect == RyoshunsGuidanceEffect) CheckChiAfterRyoshunLeaves(target.OwnerId);
                return false;
            case "theological-indecision-chi":
                ApplyModifier(target!, source!.Id, id, "Chi", -2);
                return false;
            // Thuggish Techniques: the performer was bowed as the cost.
            case "thuggish-techniques-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            // Unstable Ground: the Scout was bowed as the cost; the target is destroyed and the Terrain enters play at the battlefield.
            case "unstable-ground-destroy":
                DestroyTargetCard(target!, player.Id);
                if (TerrainAtCurrentBattlefield() && source!.Zone == Zone.Hand) SpendMarshallingFieldsWaiver(player.Id, source); // The Marshalling Fields (issue #501): the restriction was ignored
                PutTerrainIntoPlay(player, source!);
                return false;
            // Void Tattoo: the discard (unless Void) and the removal were the costs.
            case "void-tattoo-return":
            case "void-tattoo-return-void":
                {
                    var tattoo = SlotCards(command, "target").FirstOrDefault() ?? target!;
                    if (tattoo.Zone == Zone.FateDiscard) ReturnToHand(tattoo);
                    GrantAdditionalAction(player.Id, null);
                    return false;
                }
            // Paralyzing Touch's Battle action: to another unresolved battlefield (the command names it); bowed if he moved.
            case "paralyzing-touch-move":
                if (ExecuteMove(command, ability, source, target) && !StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            // Spiritual Manipulation: the forced attack is owed by the target's controller -- the active player -- for this turn.
            case "spiritual-manipulation-force":
                _state.ForcedAttacks.Add(new(target!.OwnerId, target.Id, _state.TurnNumber, Definition(source!).Name));
                Emit("attackForced", $"{Definition(source!).Name}: {Player(target.OwnerId).Name} must declare an attack this turn and assign {Definition(target).Name} to it, if legal.", player.Id, target.Id);
                return false;
            // The Game Has Changed: the straighten steps, then the transfer steps, each answered as it resolves.
            case "game-has-changed":
                _state.GameHasChangedTransferred.Clear();
                return BeginGameHasChangedChoice(command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            // Watchers in the Dark.
            case "watchers-dark-recon":
                {
                    GrantReconnaissanceAtCurrentBattlefield(player.Id);
                    var host = Card(UnitHostId(target!));
                    foreach (var card in Attachments(host.Id).Prepend(host).ToArray()) DisableAllAbilities(card, Definition(source!).Name);
                    return false;
                }
            default:
                return null;
        }
    }

    // ---- Preserving Beauty ----

    // Drained as the turn ends (EndTurn): every check of this turn whose province still stands pays its 3 Honor.
    private void ResolvePreservingBeautyChecks()
    {
        foreach (var check in _state.PreservingBeautyChecks.Where(c => c.Turn == _state.TurnNumber).ToArray())
        {
            var province = Player(check.ProvinceOwnerId).Provinces.FirstOrDefault(p => p.Index == check.ProvinceIndex);
            if (province is null || province.Destroyed) continue;
            GainHonor(check.PlayerId, 3, check.Source);
        }
        _state.PreservingBeautyChecks.Clear();
    }

    // ---- Sudden Movement: the switch ----

    // Where a card stands, as a destination: a battlefield's province index, or null for its controller's home.
    private static int? SwitchDestination(CardInstance card) => card.Zone == Zone.Battlefield ? card.ProvinceIndex : null;

    // Would this card's movement to `destination` (a battlefield, or home when null) be negated? Asked of every negation the two movers pass through (the held
    // action's negations, Hida Kaiji's trait, a movement lock, Kyuden Kitsune) WITHOUT consuming any of them.
    private bool SwitchMovementWouldBeNegated(CardInstance card, int? destination)
    {
        if (_state.NegatedEffects.Any(n => n.CardId == card.Id && n.Kind is "move" or "all")) return true;
        if (Definition(card).Effect == NegateFirstEnemyMoveEachTurnEffect && InPlay(card) && _runningCommand is { } acting && acting.PlayerId != card.OwnerId
            && !MovementCannotBeNegated(acting) && !(card.AbilityUses.TryGetValue(EnemyMoveNegatedTurnKey, out var turn) && turn == _state.TurnNumber)) return true;
        if (destination is null) return MovementLocked(card, MovementKinds.Home);
        if (MovementLocked(card, MovementKinds.ToBattlefield)) return true;
        return _state.Phase == Phase.Battle && card.OwnerId == _state.ActivePlayerId && _state.PriorityPlayerId != 1 - _state.ActivePlayerId
            && _state.Cards.Values.Any(c => c.OwnerId == 1 - _state.ActivePlayerId && InPlay(c) && Definition(c).Effect == KitsuneNegationEffect)
            && !_state.KitsuneNegatedBattles.Contains($"{_state.TurnNumber}:{destination}");
    }

    // The two movements of a switch, each to where the other stands now (core rules Glossary, Switch).
    private (CardInstance Card, int? To)[] SwitchPlan(GameCommand command)
    {
        var first = SlotCards(command, "first").Single(); var second = SlotCards(command, "second").Single();
        return [(first, SwitchDestination(second)), (second, SwitchDestination(first))];
    }

    // True when neither moves: a mover is no longer at a location, or either movement would be negated -- then the negated side still spends the negation that
    // stopped it (its movement is attempted through the ordinary path, which refuses it) and the other side does not move at all.
    private bool SwitchBlocked((CardInstance Card, int? To)[] plan, string source)
    {
        if (plan.Any(m => !InPlay(m.Card) || m.Card.Zone is not (Zone.Home or Zone.Battlefield)))
        {
            Emit("switchFailed", $"{source}: a Personality is no longer where he stood, so neither moves.", plan[0].Card.OwnerId);
            return true;
        }
        var negated = plan.Where(m => SwitchMovementWouldBeNegated(m.Card, m.To)).ToArray();
        if (negated.Length == 0) return false;
        foreach (var (card, to) in negated) { if (to is int province) TryMoveToBattlefield(card, province); else MoveHome(card, false); }
        Emit("switchNegated", $"{source}: {Definition(negated[0].Card).Name}'s movement is negated, so the switch -- the other movement too -- is negated.", plan[0].Card.OwnerId);
        return true;
    }

    // ---- Social Grace ----

    // "After another player's action targets your Courtier" -- and the action dishonors its target (a Reaction is offered only when it could do something).
    private bool SocialGraceApplies(CardInstance card, ReactionSubject subject, CardInstance affected) =>
        subject.Command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality && Keyword(affected, "Courtier")
        && !subject.IsAttack && DishonorsTarget(subject.Command.AbilityId);

    // The actions whose effects dishonor their target as the action resolves -- curated, extendable by id like DestroysTarget and BowsTarget. A dishonor that
    // waits on another player's later decision (an Either/Or, a decider's choice) is outside the held action's own resolution, so it is not listed.
    private static bool DishonorsTarget(string? abilityId) => abilityId is "training-dishonor-target" or "slanderer-dishonor" or "justice-of-crane-dishonor"
        or "discovering-conspiracy-expose" or "insulting-gesture-shame" or "izuna-kolat" or "izuna-ninja" or "izuna-shadowlands" or "accidental-confession-dishonor"
        or "saibankan-dishonor" or "yoshihara-dishonor" or "testimony-dishonor" or "tsujiken-exp-dishonor" or "ingenuity-dishonor" or "duel-of-haiku"
        or "subtle-games-dishonor" or WlPersAftermathAbilities.JiroDishonor; // issue #674: Yasuki Jiro

    // ---- Watchers in the Dark ----

    private void DisableAllAbilities(CardInstance card, string source)
    {
        _state.DisabledAbilities.RemoveAll(d => d.Turn != _state.TurnNumber);
        _state.DisabledAbilities.Add(new(card.Id, AllAbilitiesUsageKey, _state.TurnNumber, source));
        Emit("abilitiesDisabled", $"{source}: abilities on {Definition(card).Name} may not be used this turn.", card.OwnerId, card.Id);
    }

    // ---- Unstable Ground ----

    // The Terrains whose own ability puts them into play: never played through the generic playStrategy Terrain rule (MatchQueries, PlayStrategy).
    // Deep Snows (issue #503) is put into play by its Reaction; The Eternal Rainbows (issue #622, TfStrategiesB.cs) names a target.
    private static bool TerrainPlaysThroughOwnAbility(CardInstance card) => (Definition(card).Abilities ?? []).Any(a => a is "unstable-ground-destroy" or EmperorP3Abilities.DeepSnowsEnter) || TfStrategiesBTerrainThroughAbility(card);

    // "Put this card into play there": PlayStrategy's own Terrain placement, from the hand to the current battlefield.
    private void PutTerrainIntoPlay(PlayerState player, CardInstance terrain)
    {
        if (terrain.Zone != Zone.Hand || _state.CurrentBattleProvince is not int province) return;
        player.Hand.Remove(terrain.Id);
        terrain.Zone = Zone.Battlefield; terrain.ProvinceIndex = province; terrain.FaceUp = true;
        player.TerrainPlayedThisBattle = true; // Ring of Water's own recruitment condition
        Emit("terrainPlayed", $"{player.Name} put {Definition(terrain).Name} into play as Terrain at this battlefield.", player.Id, terrain.Id);
        P4TerrainEnteredPlay(terrain); // The Snow Has Teeth (issue #503)
    }

    // ---- Paralyzing Touch ----

    private void WatchParalyzingTouch(CardInstance winner, string source)
    {
        _state.ParalyzingTouchWatches.RemoveAll(w => w.Turn != _state.TurnNumber);
        _state.ParalyzingTouchWatches.Add(new(winner.Id, _state.TurnNumber, source));
        Emit("actionNegationQueued", $"{source}: the effects of the next action {Definition(winner).Name} performs this turn will be negated.", winner.OwnerId, winner.Id);
    }

    // The Personalities that perform the command's action: its named performer or its source (an Item's or Spell's wielder), its performing slot cards, and the
    // Personality a Lobby bows.
    private IEnumerable<CardInstance> ActionPerformers(GameCommand command)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || !IsAction(ability)) yield break;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var card) ? card : null;
        var named = ability.Performer is not null && command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var chosen) ? chosen : null;
        var actor = named is not null && Definition(named).Type is CardType.Personality or CardType.Follower ? named : source;
        if (actor is not null) yield return ActingPersonality(actor);
        if (ability.Slots is not null) foreach (var performing in PerformingSlotCards(command, ability)) yield return performing;
        if (command.AbilityId is "lobby" && command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var lobbying)) yield return lobbying;
    }

    // ExecuteAbilityEffects' hook: the action's effects are negated when a watched Personality performs it. Every watch on him is spent by it.
    private bool ParalyzingTouchNegates(GameCommand command)
    {
        if (_state.ParalyzingTouchWatches.Count == 0) return false;
        var performers = ActionPerformers(command).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var due = _state.ParalyzingTouchWatches.Where(w => w.Turn == _state.TurnNumber && performers.Contains(w.CardId)).ToArray();
        _state.ParalyzingTouchWatches.RemoveAll(w => w.Turn != _state.TurnNumber || due.Contains(w));
        if (due.Length == 0) return false;
        var watched = Card(due[0].CardId);
        if (ActionProtected(command))
        {
            Emit("actionProtected", $"{due[0].Source}: {Definition(watched).Name}'s action cannot be negated.", watched.OwnerId, watched.Id);
            return false;
        }
        Emit("actionNegated", $"{due[0].Source}: the effects of {Definition(watched).Name}'s next action are negated.", watched.OwnerId, watched.Id);
        return true;
    }

    // ---- Spiritual Manipulation ----

    private IEnumerable<ForcedAttack> ForcedAttacksOf(int playerId) =>
        (Player(playerId).MayNotAttackThisTurn ? [] : _state.ForcedAttacks.Where(f => f.PlayerId == playerId && f.Turn == _state.TurnNumber)).Concat(P4ForcedAttacks(playerId)); // Hunger (issue #503)

    // The Attack Declaration: the decline is refused while an attack is owed. Null when the player may decline.
    private string? ForcedAttackDeclineRefusal(int playerId) =>
        ForcedAttacksOf(playerId).FirstOrDefault() is { } forced ? $"{forced.Source}: you must declare an attack this turn." : null;

    // "Assign the target (if legal)": he can be assigned in this Maneuvers segment -- the same rules Assign applies (his controller's, at home, unbowed or allowed
    // to assign bowed, his unit's Infantry/Cavalry kind matching the segment, no movement lock on assignment).
    private IEnumerable<CardInstance> ForcedAssigneesNow(int playerId, bool cavalry) =>
        ForcedAttacksOf(playerId).Select(f => _state.Cards.TryGetValue(f.TargetId, out var c) ? c : null).OfType<CardInstance>().Distinct()
            .Where(c => c.OwnerId == playerId && c.Zone == Zone.Home && Definition(c).Type == CardType.Personality && (!c.Bowed || MayAssignBowed(c))
                && (!cavalry || IsCavalryUnit(c)) && !(cavalry && CavalryAssignmentBarredP6()) && !MovementLocked(c, MovementKinds.Assign) && !MovementLocked(c, MovementKinds.AssignAttack));

    // The attackers' assignment (AssignAttackers): every forced target assignable now is in it. Null when the assignment is acceptable.
    private string? ForcedAssignmentRefusal(int playerId, IReadOnlyDictionary<string, int>? assignments, bool cavalry) =>
        ForcedAssigneesNow(playerId, cavalry).FirstOrDefault(c => assignments?.ContainsKey(c.Id) != true) is { } missing
            ? $"{ForcedAttacksOf(playerId).First(f => f.TargetId == missing.Id).Source}: {Definition(missing).Name} must be assigned to attack." : null; // issue #503: Hunger's owed attacks are listed there too

    // ---- The Game Has Changed ----

    private const string GameHasChangedStraighten = "tghc-straighten:", GameHasChangedTransfer = "tghc-transfer:", GameHasChangedDoneStraightening = "tghc-done-straightening", GameHasChangedDone = "tghc-done";

    // The options of the current step: while any target has a bowed attachment and the player has not moved on, "straighten it" (and "done straightening");
    // then each transfer of an attachment not yet transferred to another target it may legally attach to (and "done").
    private EffectOption[] GameHasChangedOptions(GameCommand command, bool straightening)
    {
        var targets = SlotCards(command, "targets").Where(InPlay).ToArray();
        var options = new List<EffectOption>();
        if (straightening)
        {
            foreach (var host in targets)
                foreach (var attachment in Attachments(host.Id).Where(a => a.Bowed).OrderBy(a => a.Id, StringComparer.Ordinal))
                    options.Add(new(GameHasChangedStraighten + attachment.Id, $"Straighten {Definition(attachment).Name} ({Definition(host).Name})", [GameHasChangedStraighten + attachment.Id]));
            if (options.Count > 0) { options.Add(new(GameHasChangedDoneStraightening, "Straighten no more attachments", [GameHasChangedDoneStraightening])); return options.ToArray(); }
        }
        foreach (var from in targets)
            foreach (var attachment in Attachments(from.Id).Where(a => !_state.GameHasChangedTransferred.Contains(a.Id)).OrderBy(a => a.Id, StringComparer.Ordinal))
                foreach (var to in targets.Where(t => t.Id != from.Id && CanTransferTo(attachment, t)))
                    options.Add(new($"{GameHasChangedTransfer}{attachment.Id}:{to.Id}", $"Transfer {Definition(attachment).Name} from {Definition(from).Name} to {Definition(to).Name}",
                        [$"{GameHasChangedTransfer}{attachment.Id}:{to.Id}"]));
        if (options.Count > 0) options.Add(new(GameHasChangedDone, "Transfer no more attachments", [GameHasChangedDone]));
        return options.ToArray();
    }

    // Opens the first step that has anything to choose; false when there is nothing to straighten or transfer (the action simply ends).
    private bool BeginGameHasChangedChoice(GameCommand command, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore, HashSet<string> bowedBefore,
        List<AdditionalActionGrant> preExistingGrants)
    {
        var options = GameHasChangedOptions(command, straightening: true);
        if (options.Length == 0) return false;
        OpenGameHasChangedChoice(command, options, new()
        {
            Command = command,
            PlayerId = command.PlayerId,
            Negated = _state.NegatedEffects.ToList(),
            ConsumedGrant = consumedGrant,
            PlayedFromDiscard = playedFromDiscard,
            OpponentHonorBefore = opponentHonorBefore,
            BowedBefore = bowedBefore.ToList(),
            PreExistingGrants = preExistingGrants.ToList()
        });
        return true;
    }

    private void OpenGameHasChangedChoice(GameCommand command, EffectOption[] options, PendingEffectChoice context)
    {
        context.Options = options.ToList();
        _state.PendingEffectChoice = context;
        var straightening = options.Any(o => o.Id.StartsWith(GameHasChangedStraighten, StringComparison.Ordinal) || o.Id == GameHasChangedDoneStraightening);
        _state.PendingChoice = new("effectChoice", command.PlayerId, command.CardId,
            straightening ? "The Game Has Changed: straighten one of the targets' attachments, or move on to transferring." : "The Game Has Changed: transfer an attachment among the targets, or finish.");
        _state.PriorityPlayerId = command.PlayerId; _state.ConsecutivePasses = 0;
    }

    // ApplyBranchEffect's arm for The Game Has Changed's listed options; false for any other effect.
    private bool ApplyShadowsEmbraceBatch4BranchEffect(string effect, GameCommand command)
    {
        if (effect.StartsWith(GameHasChangedStraighten, StringComparison.Ordinal))
        {
            var attachment = Card(effect[GameHasChangedStraighten.Length..]);
            if (InPlay(attachment)) Straighten(attachment);
            return true;
        }
        if (effect.StartsWith(GameHasChangedTransfer, StringComparison.Ordinal))
        {
            var ids = effect[GameHasChangedTransfer.Length..].Split(':');
            var attachment = Card(ids[0]); var host = Card(ids[1]);
            if (CanTransferTo(attachment, host)) TransferAttachment(attachment, host, command.CardId is { } sourceId ? Definition(Card(sourceId)).Name : "The Game Has Changed");
            _state.GameHasChangedTransferred.Add(attachment.Id);
            return true;
        }
        return effect is GameHasChangedDoneStraightening or GameHasChangedDone;
    }

    // ResolveEffectChoice's hook, after the chosen option applied: true when the action goes on to another step of its choice (it re-opens and waits).
    private bool ContinueShadowsEmbraceBatch4Choice(PendingEffectChoice pending, EffectOption chosen)
    {
        if (pending.Command.AbilityId != "game-has-changed" || chosen.Id == GameHasChangedDone) return false;
        var straightening = chosen.Id.StartsWith(GameHasChangedStraighten, StringComparison.Ordinal);
        var options = GameHasChangedOptions(pending.Command, straightening);
        if (options.Length == 0) return false;
        OpenGameHasChangedChoice(pending.Command, options, new()
        {
            Command = pending.Command,
            PlayerId = pending.PlayerId,
            Negated = pending.Negated,
            ConsumedGrant = pending.ConsumedGrant,
            PlayedFromDiscard = pending.PlayedFromDiscard,
            OpponentHonorBefore = pending.OpponentHonorBefore,
            BowedBefore = pending.BowedBefore,
            PreExistingGrants = pending.PreExistingGrants
        });
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterShadowsEmbraceBatch4Hooks()
    {
        EngineHooks.ValidateMiddle.Register("ValidateShadowsEmbraceBatch4", 200, static (engine, context) => engine.ValidateShadowsEmbraceBatch4(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunShadowsEmbraceBatch4", 800, static (engine, context) => engine.RunShadowsEmbraceBatch4(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
    }
}
