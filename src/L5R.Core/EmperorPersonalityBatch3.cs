namespace L5R.Core;

// Emperor Edition Personality pass (issue #477), batch 3 (personality-wave-70): the execution/validation helpers of the twelve Personalities this batch wires --
// Moto Naleesh, Moto Shigeru, Moto Tetsuo, Shinjo Itao, Utaku Ryoko, Iuchi Shunshi, Tsuruchi Samuru, Tsuruchi Tomaru, Unmei, Yoritomo Hama, Yoritomo Hiromi and Yoritomo Naoto.
// The declarative data (ability ids, targets) is in CardAbilities.cs beside each card's printed text; readings and alternatives are in the batch's report.
//
//   * AN ATTACK COMPARED AGAINST ANOTHER STAT (AttackComparedValue, AttackTargeting.cs): Samuru's "Ranged 6 Attack that you compare against Gold Cost".
//   * A LIVE STATIC KEYWORD MIRROR (UnmeiMirrorsKeyword) and the conditional +3F / granted Ranged 5 Attack that follow from the mirrored keywords (Unmei).
//   * "WILL NOT BE NEGATED OR DELAYED" for one Personality's Battle actions (HiromiUnnegatable): every place a bow, a destruction or a movement from an action
//     may be negated or delayed asks it first (Hiromi).
//   * A BATTLE-LONG ABILITY LOCK (MatchState.BattleAbilityLocks): every ability of each card that was in a unit when the action resolved, until the battle ends (Naoto).
//   * A STATIC STRAIGHTEN IN THE OTHER PLAYER'S STRAIGHTEN PHASE (StraightenNaleeshUnits): Naleesh's unit.

/// <summary>Every ability on card <paramref name="CardId"/> may not be used until the battle ends (Yoritomo Naoto).</summary>
public sealed record BattleAbilityLock(string CardId, string Source);

public sealed partial class MatchEngine
{
    private const string UnmeiMirrorEffect = "unmei-mirror";
    private const string UnmeiAttackAbilityId = "unmei-attack";
    private const string ShigeruItemForceEffect = "shigeru-item-force";
    private const string NaleeshStraightensEffect = "naleesh-straightens";
    private const string HiromiUnnegatableEffect = "hiromi-unnegatable";

    // The keywords Unmei may take from another Mantis Clan Personality of her controller's.
    private static readonly string[] UnmeiMirroredKeywords = ["Earth", "Thunder", "Courtier", "Samurai", "Shugenja", "Cavalry", "Duelist", "Magistrate", "Naval", "Scout", "Tactician"];

    // ---- Unmei: "Unmei has Earth, Thunder, ... and/or Tactician while you control another Mantis Clan Personality with the same keyword" --------------------------------------
    // Read live by Keyword() like every conditional grant (it is gone the moment the other Personality leaves play, or loses the keyword). Asking Keyword() about the other
    // Personality could loop through a second mirror, so the question being asked is remembered while it is asked (the StaticKeywordRules.cs convention).
    private bool UnmeiMirrorsKeyword(CardInstance card, string keyword)
    {
        if (Definition(card).Effect != UnmeiMirrorEffect || !InPlay(card) || !UnmeiMirroredKeywords.Contains(keyword, StringComparer.Ordinal)) return false;
        var key = (card.Id, "unmei:" + keyword);
        if (!_staticKeywordQuestions.Add(key)) return false;
        try
        {
            return _state.Cards.Values.Any(c => c.Id != card.Id && c.OwnerId == card.OwnerId && InPlay(c) && Definition(c).Type == CardType.Personality
                && HasClanAffiliation(c, "Mantis") && Keyword(c, keyword));
        }
        finally { _staticKeywordQuestions.Remove(key); }
    }

    // ---- static Force: Shigeru "+1F while she has an Item", Unmei "+3F while she has Samurai or Shugenja" --------------------------------------------------------------------
    private int EmperorPersonalityBatch3Force(CardInstance card)
    {
        switch (Definition(card).Effect)
        {
            case ShigeruItemForceEffect:
                return Attachments(card.Id).Any(a => Definition(a).Type == CardType.Item) ? 1 : 0;
            case UnmeiMirrorEffect:
                return InPlay(card) && (Keyword(card, "Samurai") || Keyword(card, "Shugenja")) ? 3 : 0;
            default:
                return 0;
        }
    }

    // ---- Naleesh: "Naleesh's unit straightens during each player's Straighten Phase" ------------------------------------------------------------------------------------------
    // FinishStraightening's hook for the Straighten Phase of the OTHER player (his own already straightened his unit). "Unit": Naleesh and every attachment on him.
    private void StraightenNaleeshUnits(PlayerState activePlayer)
    {
        foreach (var naleesh in _state.Cards.Values.Where(c => c.OwnerId != activePlayer.Id && InPlay(c) && Definition(c).Type == CardType.Personality && Definition(c).Effect == NaleeshStraightensEffect).ToArray())
            foreach (var member in UnitMembers(naleesh).Where(m => m.Bowed).ToArray()) Straighten(member);
    }

    // ---- Hiromi: "Bowing, destruction, and movement from Battle actions Hiromi performs will not be negated or delayed" -----------------------------------------------------------
    // The command of the Battle action now resolving, if Hiromi performs it: the running command, or the deferred Attack whose resolution is under way.
    private GameCommand? _attackCommandInResolution;

    private bool HiromiPerforms(GameCommand? command)
    {
        if (command?.AbilityId is not { } id || !AbilityCatalog.All.TryGetValue(id, out var ability)) return false;
        // The Shadow's Lair (Emperor Edition Stronghold pass, issue #486 batch 3): "this will not be negated or delayed if you control a Ninja" -- read as the same unnegatable movement.
        if (id == "shadows-lair-move") return ControlsKeyword(command.PlayerId, "Ninja");
        if (MakitoDestruction(command)) return true; // Makito (three-scans pass, issue #496): "this destruction will not be negated or delayed".
        if (!(ability.Timing == "Battle" || ability.Timing == "BattleOrOpen" && _state.Phase == Phase.Battle)) return false;
        return ActionPerformingPersonality(command) is { } performer && Definition(performer).Effect == HiromiUnnegatableEffect;
    }

    // Every negation or delay of a bow, a destruction or a movement asks this: true while such an action of Hiromi's is resolving.
    // Suitengu's Torch (Emperor Edition Stronghold pass, issue #486 batch 2) asks the same questions of one announced Thunder Battle action: nothing of it is negated or delayed.
    private bool HiromiUnnegatable() => HiromiPerforms(_runningCommand ?? _attackCommandInResolution) || CurrentActionUnnegatable();

    // The action as a whole cannot be negated (Protection's shape) when it is one of Hiromi's Battle actions that bows, destroys or moves. Her other effects on the same action
    // (a Force change) are not named by the card; negating the action whole would also negate its bow, destruction or movement, which is what the card forbids.
    private bool HiromiProtects(GameCommand? command) =>
        CommandUnnegatable(command) // Suitengu's Torch's protected action and Temple of Purity's Force penalty (issue #486, batch 2): the whole action.
        || MakitoDestruction(command) // Makito (issue #496): negating his action whole would negate the destruction that will not be negated.
        || command?.AbilityId is { } id && HiromiPerforms(command)
        && (ActionMayBowOrDestroy(id, AttackKind(id) != "") || MoveOrStraightenKind(id) == "move" || AbilityCatalog.All[id].Move is not null);

    // ---- validation --------------------------------------------------------------------------------------------------------------------------------------------------------------
    private void ValidateEmperorPersonalityBatch3(string id, GameCommand command, CardInstance? source)
    {
        // Yoritomo Hama: "a card with lower Gold Cost" -- lower than Hama's own.
        if (id is "hama-bow" or "hama-straighten" && command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var hamaTarget))
            Require(CardGoldCost(hamaTarget) < CardGoldCost(source!), "The target's Gold Cost must be lower than Yoritomo Hama's.");
        // Moto Tetsuo: "If you control any Cavalry units at the current battlefield:" -- a condition before the colon gates the action.
        if (id == "tetsuo-move-attack")
            Require(_state.CurrentBattleProvince is int tetsuoBattle && Army(source!.OwnerId, tetsuoBattle).Any(IsCavalryUnit), "You must control a Cavalry unit at the current battlefield.");
        // Utaku Ryoko: "a Personality or Follower opposing her" -- an enemy card in the army at the current battlefield (she is at it: the after-engaging-at-own-battlefield trigger).
        if (id == "ryoko-penalty" && command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var ryokoTarget))
            Require(AtBattle(ryokoTarget) && source is not null && AtBattle(source), "The target must oppose Utaku Ryoko at the current battlefield.");
    }

    // ---- ability locks -----------------------------------------------------------------------------------------------------------------------------------------------------------
    private bool BattleAbilityLocked(CardInstance card) => _state.BattleAbilityLocks.Count > 0 && _state.BattleAbilityLocks.Any(l => l.CardId == card.Id);

    // ---- effects -----------------------------------------------------------------------------------------------------------------------------------------------------------------
    // True when the action paused (an Attack's reaction window, an attach's entry-into-play window) and finishes from there.
    private bool RunEmperorPersonalityBatch3Effect(string id, GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        switch (id)
        {
            // Moto Naleesh: bow the unit, then destroy every card in it that had no attachments and no Cavalry when the action resolved (read before anything is destroyed).
            case "naleesh-overrun":
            {
                var unit = UnitMembers(target!).ToArray();
                var doomed = unit.Where(c => !HasAttachments(c) && !Keyword(c, "Cavalry")).ToArray();
                BowUnitByEffect(target!, player.Id);
                foreach (var card in doomed.Where(c => c.Zone == Zone.Attached)) if (InPlay(card)) DestroyAttachment(card);
                foreach (var personality in doomed.Where(c => c.Zone != Zone.Attached && Definition(c).Type == CardType.Personality)) if (InPlay(personality)) DestroyUnits([personality], false, player.Id);
                return false;
            }
            // Moto Shigeru: negate the action, permanently reduce her Personal Honor by the number of other Personalities her controller controls, and if it is now 0 she commits seppuku.
            case "shigeru-negate":
            {
                NegateHeldAction(source!);
                var others = _state.Cards.Values.Count(c => c.OwnerId == player.Id && c.Id != source!.Id && InPlay(c) && Definition(c).Type == CardType.Personality);
                if (others > 0) AddPermanentModifier(source!, source!.Id, id, "PersonalHonor", -others);
                if (InPlay(source!) && CardPersonalHonor(source!) <= 0) CommitSeppuku(source!, player.Id);
                return false;
            }
            case "itao-attach":
                return ExecuteAttach(command, ability.Attach!, player, source, target);
            // Utaku Ryoko: a Force penalty equal to her Personal Honor as the action resolves.
            case "ryoko-penalty":
                ApplyModifier(target!, source!.Id, id, "Force", -CardPersonalHonor(source!));
                return false;
            case "shunshi-lock":
                LockMovement(target!, Definition(source!).Name, MovementKinds.ToBattlefield | MovementKinds.Assign);
                return false;
            case "samuru-attack": case "tomaru-attack-3": case "unmei-attack":
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            case "hama-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "hama-straighten":
                Straighten(target!);
                return false;
            // Yoritomo Hiromi: destroy the target -- a unit's Personality takes his unit with him, an attachment alone is destroyed alone.
            case "hiromi-thunder":
                if (target!.Zone == Zone.Attached) DestroyAttachment(target); else DestroyUnits([target], false, player.Id);
                return false;
            // Yoritomo Naoto: every card in the unit now (the Personality and each attachment) has its abilities locked until the battle ends.
            case "naoto-lock":
                foreach (var member in UnitMembers(target!).ToArray()) if (!BattleAbilityLocked(member)) _state.BattleAbilityLocks.Add(new(member.Id, source!.Id));
                Emit("abilitiesLocked", $"{Definition(source!).Name}: abilities on the cards in {Definition(target!).Name}'s unit may not be used until this battle ends.", target!.OwnerId, target.Id);
                return false;
            default:
                throw new InvalidOperationException($"Unknown Emperor Edition personality batch 3 ability '{id}'.");
        }
    }

    // "Commits seppuku" (core rules: rehonor the Personality, then destroy him; these effects will not be negated).
    private void CommitSeppuku(CardInstance card, int playerId)
    {
        Rehonor(card);
        DestroyUnits([card], actingPlayerId: playerId);
        Emit("seppuku", $"{Definition(card).Name} committed seppuku, dying honorably.", card.OwnerId, card.Id);
        RecordFact(FactKinds.Seppuku, playerId, card.Id); // P6 (AfterFacts.cs)
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorPersonalityBatch3Hooks()
    {
        EngineHooks.PersonalityForce.Register("EmperorPersonalityBatch3Force", 1000, static (engine, context) => engine.EmperorPersonalityBatch3Force(context.card));
        EngineHooks.ValidateLate.Register("ValidateEmperorPersonalityBatch3", 600, static (engine, context) => engine.ValidateEmperorPersonalityBatch3(context.id, context.command, context.source));
    }
}
