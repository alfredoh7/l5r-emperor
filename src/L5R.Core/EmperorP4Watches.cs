namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, GROUP P4 (continued): ONE-SHOT WATCHES AND SHIELDS, AND THE LOCKS. Every card read against its scan.
//
//   * A GAME-LONG ONE-SHOT STRAIGHTEN NEGATION (Drawing on the Mountain: "Negate his next straightening this game"): P4State.NextStraightenNegated, spent by the first straightening that would really
//     happen (a bowed card that nothing else stops straightening), whenever it comes -- an effect's or the Straighten Phase's.
//   * A NEGATION OF A FUTURE ACTION (Firm Censure: "Negate the effects of the next action performed by the targeted card (this turn)"): Paralyzing Touch's watch (MatchState.ParalyzingTouchWatches),
//     which negates the next action whose performers include the watched card; a Follower performs its own actions.
//   * A GAME-LONG WATCH ON A HOLDING (Scars of War: "Until the game ends, after each time it bows or an ability on it resolves, its controller loses 1 Honor"): P4State.ScarsMarks. A bow is noticed at the
//     end of every command (the Holding was unbowed when last seen and is bowed now: bowed to pay Gold, by a cost or by an effect); an ability on it resolving is FinishActionEffectTracking's record.
//     Each copy of Scars of War on the same Holding is a mark of its own.
//   * A ONE-SHOT SHIELD AGAINST A KIND OF EFFECT (Show of Restraint: "negate the next card effect that moves him home or bows him"): P4State.RestraintShields, for the turn (an ongoing effect lasts the
//     turn: core rules, Duration), spent by the first effect that would bow him (StalwartNegatesBow, the one gate every effect's bow passes) or move him home from a battlefield (MoveHome).
//   * AN ASSIGN-OR-MOVE WATCH (The Menacing Sky: "After the next time (this turn) he assigns or an action resolves which moved him to a battlefield, bow him"): P4State.MenacingSkyWatches, fired by
//     his assignment (either side, every assignment path) or by an action whose record shows it moved him and that leaves him at a battlefield.
//   * A GLOBAL MOVEMENT LOCK (Too Close to Home: "Until your next turn begins, negate all movement to and from battlefields"): the Event stays in play until its owner's next turn begins, and while it
//     is in play MovementLocked refuses every movement home and to a battlefield (assignment is not movement, core rules; nor is the return home after a battle).
//   * A BATTLE-LONG CEILING, A ONE-SHOT MOVE SHIELD FOR A PLAYER'S PERSONALITIES AND A TERRAIN WATCH (The Snow Has Teeth).
//   * AN ATTACK OBLIGATION (Hunger: "You must declare an attack each turn and assign this Personality to attack if possible"): Spiritual Manipulation's owed attack (ForcedAttacksOf) for every
//     Hunger in play, each turn its controller may declare an attack.
// ======================================================================================================================================================================================
public static class EmperorP4WatchAbilities
{
    public const string DrawingOnTheMountain = "drawing-mountain-negate";
    public const string DrawingOnTheMountainEffect = "drawing-mountain-provinces";
    public const string FirmCensure = "firm-censure-negate";
    public const string ScarsOfWar = "scars-of-war-mark";
    public const string ShowOfRestraint = "show-of-restraint";
    public const string MenacingSky = "menacing-sky-watch";
    public const string SnowHasTeeth = "snow-has-teeth";
    public const string HungerNegateMove = "hunger-negate-move";
    public const string HungerEffect = "hunger-must-attack";
    public const string TooCloseToHomeEffect = "too-close-to-home";

    public const string BowedPersonality = "p4-bowed-personality";
    public const string EnemyPersonalityOrFollower = "p4-enemy-personality-or-follower";

    public static Dictionary<string, AbilityDefinition> AddEmperorP4Watches(this Dictionary<string, AbilityDefinition> all)
    {
        // Drawing on the Mountain (oracle-l5r-2236): "While this Shugenja is Earth, your provinces have +1 strength." (the Spell's Effect) and "Battle/Open: Bow this Shugenja, destroy this card, and
        // target a bowed Personality: Negate his next straightening this game."
        all[DrawingOnTheMountain] = new("BattleOrOpen", "Bow this Shugenja, destroy this card, and target a bowed Personality: Negate his next straightening this game", BowedPersonality,
            Costs: [new(CostVerb.Bow, CostSubject.Wielder), new(CostVerbs.Sacrifice, CostSubject.Source)]);
        // Firm Censure (oracle-l5r-2580): "Open: Choose your performing Samurai and target another player's Personality or Follower: Negate the effects of the next action performed by the targeted
        // card (this turn). Bow it if it has no attachments and is not a Samurai."
        all[FirmCensure] = new("Open", "Choose your performing Samurai and target another player's Personality or Follower: Negate the effects of the next action performed by the targeted card (this turn). Bow it if it has no attachments and is not a Samurai",
            EnemyPersonalityOrFollower, Performer: "own-personality-with-keyword", RequiredKeywords: ["Samurai"]);
        // Scars of War (oracle-l5r-6490): "Limited: Bow your performing Courtier and target a Holding: Until the game ends, after each time it bows or an ability on it resolves, its controller loses
        // 1 Honor."
        all[ScarsOfWar] = new("Limited", "Bow your performing Courtier and target a Holding: Until the game ends, after each time it bows or an ability on it resolves, its controller loses 1 Honor", "any-holding",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Courtier"], Costs: ActionCost.BowPerformer);
        // Show of Restraint (oracle-l5r-7197): "Battle: Choose your performing Monk: Negate all current Force penalties on him from actions. If he has a Weapon, negate the next card effect that moves
        // him home or bows him."
        all[ShowOfRestraint] = new("Battle", "Choose your performing Monk: Negate all current Force penalties on him from actions. If he has a Weapon, negate the next card effect that moves him home or bows him",
            "none", Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Monk"]);
        // The Menacing Sky (oracle-l5r-8219): "Limited: Bow this Shugenja unless he is Thunder, and target a Personality: After the next time (this turn) he assigns or an action resolves which moved
        // him to a battlefield, bow him."
        all[MenacingSky] = new("Limited", "Bow this Shugenja unless he is Thunder, and target a Personality: After the next time (this turn) he assigns or an action resolves which moved him to a battlefield, bow him",
            "personality", Costs: ActionCost.BowWielderUnless("Thunder"));
        // The Snow Has Teeth (oracle-l5r-8342): "Reaction: After engaging: The current battlefield's province has a maximum strength equal to its base strength until the end of the battle. Negate the
        // next effect that would move one of your Personalities from its battlefield. After the next time this battle a Terrain enters play, destroy it."
        all[SnowHasTeeth] = new("Reaction", "After engaging: The current battlefield's province has a maximum strength equal to its base strength until the end of the battle. Negate the next effect that would move one of your Personalities from its battlefield. After the next time this battle a Terrain enters play, destroy it",
            "none", Trigger: "after-engaging");
        // Hunger (oracle-l5r-3514): "Reaction: After an action targets this Personality: Negate this Personality's movement from the action's effects." (Its obligation is the Item's Effect.)
        all[HungerNegateMove] = new("Reaction", "After an action targets this Personality: Negate this Personality's movement from the action's effects", "none", Trigger: ReactionTriggers.TargetedByAction);
        return all;
    }
}

/// <summary>A watch or shield of group P4: the card it is on (null for a player-wide one), the player it serves, the turn and battle it was set in, and the card that set it.</summary>
public sealed record P4Watch(string? CardId, int PlayerId, int Turn, int Battle, string Source);

/// <summary>A province's ceiling for one battle (The Snow Has Teeth).</summary>
public sealed record P4ProvinceCap(int OwnerId, int Index, int Battle);

public sealed partial class MatchEngine
{
    // ---- hooks ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool P4WatchBowsTarget(string abilityId) => false;

    private bool P4WatchTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target) => category switch
    {
        // Drawing on the Mountain: "a bowed Personality" (either player's).
        EmperorP4WatchAbilities.BowedPersonality => Definition(target).Type == CardType.Personality && InPlay(target) && target.Bowed,
        // Firm Censure: "another player's Personality or Follower".
        EmperorP4WatchAbilities.EnemyPersonalityOrFollower => target.OwnerId != player.Id && InPlay(target) && Definition(target).Type is CardType.Personality or CardType.Follower,
        _ => P4UnitTargetAllows(category, command, player, source, target)
    };

    private bool? RunEmperorP4Watches(string id, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target)
    {
        var name = source is null ? "" : Definition(source).Name;
        switch (id)
        {
            case EmperorP4WatchAbilities.DrawingOnTheMountain:
                if (target is null || !InPlay(target)) return false;
                if (!P4.NextStraightenNegated.Contains(target.Id)) P4.NextStraightenNegated.Add(target.Id);
                Emit("watchSet", $"{name}: {Definition(target).Name}'s next straightening this game is negated.", player.Id, target.Id);
                return false;
            case EmperorP4WatchAbilities.FirmCensure:
            {
                if (target is null || !InPlay(target)) return false;
                _state.ParalyzingTouchWatches.RemoveAll(w => w.Turn != _state.TurnNumber);
                _state.ParalyzingTouchWatches.Add(new(target.Id, _state.TurnNumber, name));
                Emit("watchSet", $"{name}: the effects of the next action {Definition(target).Name} performs this turn are negated.", player.Id, target.Id);
                if (!HasAttachments(target) && !Keyword(target, "Samurai")) BowByEffect(target, player.Id);
                return false;
            }
            case EmperorP4WatchAbilities.ScarsOfWar:
                if (target is null || !InPlay(target)) return false;
                P4.ScarsMarks.Add(new(target.Id, player.Id, _state.TurnNumber, _state.BattleNumber, name));
                P4.ScarsLastBowed[target.Id] = target.Bowed;
                Emit("watchSet", $"{name}: until the game ends, after each time {Definition(target).Name} bows or an ability on it resolves, its controller loses 1 Honor.", player.Id, target.Id);
                return false;
            case EmperorP4WatchAbilities.ShowOfRestraint:
            {
                var monk = Card(command.PerformerId!);
                var removed = monk.Modifiers.RemoveAll(m => m.Stat == "Force" && m.Amount < 0);
                Emit("forcePenaltiesNegated", $"{name}: every current Force penalty on {Definition(monk).Name} is negated ({removed} removed).", player.Id, monk.Id);
                if (Attachments(monk.Id).Any(a => Keyword(a, "Weapon")))
                {
                    P4.RestraintShields[monk.Id] = _state.TurnNumber;
                    Emit("shieldSet", $"{name}: {Definition(monk).Name} has a Weapon -- the next card effect that moves him home or bows him is negated.", player.Id, monk.Id);
                }
                return false;
            }
            case EmperorP4WatchAbilities.MenacingSky:
                if (target is null || !InPlay(target)) return false;
                P4.MenacingSkyWatches.Add(new(target.Id, player.Id, _state.TurnNumber, _state.BattleNumber, name));
                Emit("watchSet", $"{name}: after the next time this turn {Definition(target).Name} assigns or an action resolves which moved him to a battlefield, he is bowed.", player.Id, target.Id);
                return false;
            case EmperorP4WatchAbilities.SnowHasTeeth:
            {
                if (_state.CurrentBattleProvince is int battlefield && CurrentBattlefieldProvince() is { } province)
                {
                    var owner = _state.Players.First(p => p.Provinces.Contains(province)).Id;
                    P4.ProvinceCaps.Add(new(owner, province.Index, _state.BattleNumber));
                    Emit("provinceCapped", $"{name}: the current battlefield's province has a maximum strength of its base strength ({province.PrintedStrength}) until the end of the battle.", player.Id, source?.Id);
                }
                P4.SnowMoveShields.Add(new(null, player.Id, _state.TurnNumber, _state.BattleNumber, name));
                P4.SnowTerrainWatches.Add(new(null, player.Id, _state.TurnNumber, _state.BattleNumber, name));
                Emit("watchSet", $"{name}: the next effect that would move one of {player.Name}'s Personalities from its battlefield is negated; the next Terrain to enter play this battle is destroyed.", player.Id, source?.Id);
                return false;
            }
            case EmperorP4WatchAbilities.HungerNegateMove:
            {
                if (source?.AttachedTo is not { } wielderId) return false;
                NegateEffects(Card(wielderId), "move");
                return false;
            }
            default:
                return null;
        }
    }

    // ---- Drawing on the Mountain -------------------------------------------------------------------------------------------------------------------------------------------------------

    // Straighten's hook, reached only when nothing else stops the straightening: the one-shot negation is spent by a bowed card's first real straightening.
    private bool P4NegatesNextStraightening(CardInstance card)
    {
        if (!card.Bowed || !P4.NextStraightenNegated.Remove(card.Id)) return false;
        Emit("straightenNegated", $"{Definition(card).Name}'s straightening was negated (Drawing on the Mountain: his next straightening this game).", card.OwnerId, card.Id);
        return true;
    }

    // StaticProvinceBonus' hook: "While this Shugenja is Earth, your provinces have +1 strength."
    private int P4StaticProvinceBonus(CardInstance card, bool mine) =>
        mine && Definition(card).Effect == EmperorP4WatchAbilities.DrawingOnTheMountainEffect && card.Zone == Zone.Attached && WielderOf(card) is { } shugenja && Keyword(shugenja, "Earth") ? 1 : 0;

    // ---- Scars of War -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ScarsHonorLoss(CardInstance holding, P4Watch mark, string what)
    {
        Emit("watchFired", $"{mark.Source}: {Definition(holding).Name} {what} -- its controller loses 1 Honor.", holding.OwnerId, holding.Id);
        GainHonor(holding.OwnerId, -1, mark.Source);
    }

    // After every command: a marked Holding that was unbowed when last seen and is bowed now has bowed.
    private void P4CommandTail()
    {
        if (P4.ScarsMarks.Count == 0) return;
        foreach (var holdingId in P4.ScarsMarks.Select(m => m.CardId!).Distinct().ToArray())
        {
            var holding = Card(holdingId);
            if (!InPlay(holding)) { P4.ScarsMarks.RemoveAll(m => m.CardId == holdingId); P4.ScarsLastBowed.Remove(holdingId); continue; }
            var wasBowed = P4.ScarsLastBowed.TryGetValue(holdingId, out var before) && before;
            P4.ScarsLastBowed[holdingId] = holding.Bowed;
            if (holding.Bowed && !wasBowed)
                foreach (var mark in P4.ScarsMarks.Where(m => m.CardId == holdingId).ToArray()) ScarsHonorLoss(holding, mark, "bowed");
        }
    }

    // FinishActionEffectTracking's hook: an ability on a marked Holding resolved; an action that moved a watched Personality to a battlefield resolved.
    private void P4AfterActionResolved(List<AffectedCard> affected, string abilityId, int playerId, string? sourceId)
    {
        if (sourceId is not null && P4.ScarsMarks.Any(m => m.CardId == sourceId) && _state.Cards.TryGetValue(sourceId, out var holding) && Definition(holding).Type == CardType.Holding)
        {
            P4CommandTail(); // a bow the ability's own cost made is noticed first, in order
            foreach (var mark in P4.ScarsMarks.Where(m => m.CardId == sourceId).ToArray()) ScarsHonorLoss(holding, mark, "had an ability resolve");
        }
        if (P4.MenacingSkyWatches.Count == 0) return;
        foreach (var moved in affected.Where(a => a.Kind == MovedKind).Select(a => a.CardId).Distinct().ToArray())
            if (_state.Cards.TryGetValue(moved, out var card) && card.Zone == Zone.Battlefield) FireMenacingSky(card, "was moved to a battlefield by an action that resolved");
    }

    // ---- Show of Restraint and The Snow Has Teeth: one-shot shields ------------------------------------------------------------------------------------------------------------------------------

    // StalwartNegatesBow's hook: an effect is about to bow him.
    private bool P4ShieldNegatesBow(CardInstance card)
    {
        if (card.Bowed || !(P4.RestraintShields.TryGetValue(card.Id, out var turn) && turn == _state.TurnNumber)) return false;
        P4.RestraintShields.Remove(card.Id);
        Emit("bowNegated", $"{Definition(card).Name}'s bowing was negated (Show of Restraint).", card.OwnerId, card.Id);
        return true;
    }

    // MoveHome's hook: an effect is about to move him home from a battlefield.
    private bool P4ShieldNegatesMoveHome(CardInstance card)
    {
        if (card.Zone != Zone.Battlefield) return false;
        if (P4.RestraintShields.TryGetValue(card.Id, out var turn) && turn == _state.TurnNumber)
        {
            P4.RestraintShields.Remove(card.Id);
            Emit("moveNegated", $"{Definition(card).Name}'s movement home was negated (Show of Restraint).", card.OwnerId, card.Id);
            return true;
        }
        return SnowNegatesMoveFromBattlefield(card);
    }

    // MoveToBattlefield's hook: a move from one battlefield to another is a move from its battlefield.
    private bool P4NegatesMoveToBattlefield(CardInstance card, int province) =>
        card.Zone == Zone.Battlefield && card.ProvinceIndex != province && SnowNegatesMoveFromBattlefield(card);

    private bool SnowNegatesMoveFromBattlefield(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality) return false;
        var shield = P4.SnowMoveShields.FirstOrDefault(w => w.PlayerId == card.OwnerId && w.Turn == _state.TurnNumber);
        if (shield is null) return false;
        P4.SnowMoveShields.Remove(shield);
        Emit("moveNegated", $"{Definition(card).Name}'s movement from its battlefield was negated ({shield.Source}).", card.OwnerId, card.Id);
        return true;
    }

    // ProvinceStrengthMax' hook: The Snow Has Teeth's ceiling, while its battle lasts.
    private bool P4ProvinceCapped(ProvinceState province) =>
        P4.ProvinceCaps.Any(c => c.Battle == _state.BattleNumber && c.Index == province.Index && _state.Players[c.OwnerId].Provinces.Contains(province) && ProvinceIsBattlefieldOfCurrentBattle(province));

    // A Terrain entered play (PlayStrategy, PutTerrainIntoPlay): the next one this battle is destroyed.
    private void P4TerrainEnteredPlay(CardInstance terrain)
    {
        var watch = P4.SnowTerrainWatches.FirstOrDefault(w => w.Battle == _state.BattleNumber && w.Turn == _state.TurnNumber);
        P4.SnowTerrainWatches.RemoveAll(w => w.Battle != _state.BattleNumber || w.Turn != _state.TurnNumber);
        if (watch is null || terrain.Zone != Zone.Battlefield) return;
        P4.SnowTerrainWatches.Remove(watch);
        Emit("watchFired", $"{watch.Source}: {Definition(terrain).Name} entered play -- it is destroyed.", watch.PlayerId, terrain.Id);
        DiscardTerrain(terrain);
        AfterCardDestroyed(terrain);
    }

    // ---- The Menacing Sky --------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Every assignment path's hook (the Maneuvers Segments and a created battlefield's): he assigned.
    private void P4Assigned(CardInstance card) => FireMenacingSky(card, "assigned");

    private void FireMenacingSky(CardInstance card, string what)
    {
        var due = P4.MenacingSkyWatches.Where(w => w.CardId == card.Id && w.Turn == _state.TurnNumber).ToArray();
        P4.MenacingSkyWatches.RemoveAll(w => w.Turn != _state.TurnNumber || due.Contains(w));
        foreach (var watch in due)
        {
            Emit("watchFired", $"{watch.Source}: {Definition(card).Name} {what} -- he is bowed.", watch.PlayerId, card.Id);
            if (InPlay(card)) BowByEffect(card, watch.PlayerId);
        }
    }

    // ---- Too Close to Home --------------------------------------------------------------------------------------------------------------------------------------------------------------

    // MovementLocked's hook: while the Event is in play, every movement home and to a battlefield is negated (a move between battlefields is both).
    private bool P4GlobalMovementLock(MovementKinds kind) =>
        (kind & (MovementKinds.Home | MovementKinds.ToBattlefield)) != 0 && EventsInPlay(EmperorP4WatchAbilities.TooCloseToHomeEffect).Any();

    // The reveal: "You may choose and destroy a Region." -- a choice of the Event's owner among the Regions in play (or none); then the Event stays in play until his next turn begins.
    private bool ResolveTooCloseToHome(PlayerState player, ProvinceState province, CardInstance card)
    {
        var regions = _state.Cards.Values.Where(c => c.Zone == Zone.Region && Definition(c).Type == CardType.Region).Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
        if (regions.Count == 0)
        {
            KeepEventInPlay(player, province, card, $"{Definition(card).Name} stays in play until {player.Name}'s next turn begins: all movement to and from battlefields is negated.", turnBeginsIn: 1);
            return false;
        }
        OpenEventChoice(new PendingEventChoice { Kind = "p4-too-close", EventCardId = card.Id, PlayerId = player.Id, Origin = "reveal", Candidates = regions,
            Prompt = $"{Definition(card).Name}: you may choose and destroy a Region." });
        return true;
    }

    // ResolveEventChoiceCommand's hook: false when the decision is not this group's.
    private bool ResolveP4EventChoice(GameCommand command, PendingEventChoice pending)
    {
        if (pending.Kind != "p4-too-close") return false;
        var card = Card(pending.EventCardId); var player = Player(pending.PlayerId);
        Require(command.Choice == "none" || command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose a Region in play, or none.");
        _state.PendingChoice = null; _state.PendingEventChoice = null;
        if (command.Choice != "none" && command.TargetId is { } regionId && Card(regionId).Zone == Zone.Region)
        {
            Emit("eventChose", $"{Definition(card).Name}: {player.Name} destroys {Definition(Card(regionId)).Name}.", player.Id, regionId);
            DestroyRegion(Card(regionId));
        }
        else Emit("eventChose", $"{Definition(card).Name}: {player.Name} destroys no Region.", player.Id, card.Id);
        FinishRevealedEvent(card, keep: true, turnBeginsIn: 1, message: $"{Definition(card).Name} stays in play until {player.Name}'s next turn begins: all movement to and from battlefields is negated.");
        return true;
    }

    private IEnumerable<ActionOption> P4EventChoiceActions(PendingEventChoice pending, int playerId)
    {
        if (pending.Kind != "p4-too-close") yield break;
        var eventName = Definition(Card(pending.EventCardId)).Name;
        foreach (var id in pending.Candidates)
            yield return new("eventChoice", $"{eventName}: destroy {Definition(Card(id)).Name}", true, null, new GameCommand("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, TargetId: id), id);
        yield return new("eventChoice", $"{eventName}: destroy no Region", true, null, new GameCommand("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, Choice: "none"), pending.EventCardId);
    }

    // ---- Hunger ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // ForcedAttacksOf' hook: an owed attack, with its wielder to be assigned, for each Hunger its controller has in play (every turn he may declare one).
    private IEnumerable<ForcedAttack> P4ForcedAttacks(int playerId)
    {
        if (Player(playerId).MayNotAttackThisTurn || AttackLockedByEvent(playerId)) yield break;
        foreach (var hunger in _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Attached && Definition(c).Effect == EmperorP4WatchAbilities.HungerEffect && WielderOf(c) is not null)
                     .OrderBy(c => c.Id, StringComparer.Ordinal))
            yield return new ForcedAttack(playerId, hunger.AttachedTo!, _state.TurnNumber, Definition(hunger).Name);
    }

    // The targeted-by-action trigger: "After an action targets this Personality" -- the wielder, by any player's action.
    private bool P4TargetedReactionApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected) =>
        abilityId == EmperorP4WatchAbilities.HungerNegateMove && card.Zone == Zone.Attached && affected.Id == card.AttachedTo;
}
