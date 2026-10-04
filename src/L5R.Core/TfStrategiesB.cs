using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// TWENTY FESTIVALS STRATEGIES, BATCH B (strategy-wave-110) -- issue #622. Every card is read from its EARLIEST EMPEROR-ERA scan, not from Oracle's later template.
//
//   * KNIFE IN THE DARKNESS (Emperor Edition, Ninja): "Battle: Choose your performing Ninja Personality and target an enemy Personality: Move both Personalities home; this movement will not be
//     negated. Bow them." The generic performer-and-target move (MoveSpec.PerformerAndTarget), unnegatable (MovementCannotBeNegated), then both are bowed.
//   * LAKESIDE RETREAT (Gates of Chaos, Terrain): a Terrain whose "Battle: Destroy a Terrain (if able). Put this Terrain into play." replaces the Terrain at the battlefield, and whose trait
//     "Before this battle resolves, straighten a Personality or Follower" joins the before-resolution set. Its controller must straighten one bowed Personality or Follower of either player's
//     (any location): the choice reuses the optional before-resolution window (beforeBattleOptional), as Hard Pressed does.
//   * MERCANTILE CONFLICT (Aftermath, Discipline 1): "Political Open: Bow your target unbowed Courtier. The next two Honor losses from your cards (this turn) are increased by 1 each." A loss is
//     "from your cards" when its source card -- the named one, else the card of the action being resolved -- is yours (the Forgotten Temple's reading); the rulebook's own dishonorable-death loss is
//     not from a card. Each loss counts once it really happens (a negated loss spends nothing), and a player who controls The Forgotten Temple has a loss that "may not be increased".
//   * MUTUAL SUPPORT (Torn Asunder): "Battle: Choose two or more of your performing opposed Personalities, each with a different Clan alignment: Give each performer +2F." The performers are one
//     performing slot; no two share a Clan alignment (a multi-Clan Personality shares if any one of his alignments is shared) and each has one (an Unaligned Personality has none to differ by).
//   * STEAL AN ADVANTAGE (Aftermath): "This Strategy's ability is Battle/Engage if you control a Scout at this battlefield. Battle: A target enemy card may not use one of its abilities (you
//     choose)." Doji Hakuseki's lock (AbilityLockOptions, the turn): the acting player chooses one of the target's action abilities. Two entries: the Battle action, and the same effect as an
//     "After engaging" Reaction (an Engage action) taken only with a Scout of yours at the battlefield.
//   * STRATEGIC WITHDRAWAL (Aftermath): "As a Focus Effect, if you win this duel, draw a card. Battle: Move your target Personality home. You may target an enemy Personality and give him -3F."
//     The Focus Effect draws for the focuser when his side's Personality won (a tie has no winner); the Battle action's two targets are two slots, the enemy one optional.
//   * THE ETERNAL RAINBOWS (Gates of Chaos, Terrain): "Battle: Destroy a Terrain (if able). Put this Terrain into play. Your target opposed Personality has +2F (while this card is in play)." Played
//     through its own ability (it names a target), so never through the generic playStrategy Terrain rule; the +2F is read live while the Terrain stays in play (MatchState.TfStrategiesBRainbows).
//   * UNHOLY STRIKE (Aftermath): "Battle: Melee 2 Attack. Interrupt: Give a Melee or Ranged Attack +2 strength." An Interrupt plays exactly like a Reaction to "before an action's effects resolve" (core
//     rules): for an Attack that is the before-attack-resolves window every Attack Reaction uses (Precision, Sniping).
//
// Preserving Forces and Unsafe Passage (Embers of War) are in TfStrategiesB2.cs.
// ======================================================================================================================================================================================

public static class TfStrategiesBAbilities
{
    public const string KnifeMove = "knife-darkness-move";
    public const string LakesideStraighten = "lakeside-retreat-straighten";
    public const string MercantileBow = "mercantile-conflict-bow";
    public const string MutualSupportBoost = "mutual-support-boost";
    public const string StealBattle = "steal-advantage-battle", StealEngage = "steal-advantage-engage";
    public const string WithdrawalFocus = "strategic-withdrawal-focus", WithdrawalMove = "strategic-withdrawal-move";
    public const string RainbowsPlay = "eternal-rainbows-play";
    public const string UnholyAttack = "unholy-strike-attack", UnholyBoost = "unholy-strike-boost";
    public const string PerformersSlot = "performers", MoverSlot = "mover", WeakenedSlot = "weakened";
    private const int MostPerformers = 12;

    public static Dictionary<string, AbilityDefinition> AddTfStrategiesB(this Dictionary<string, AbilityDefinition> all)
    {
        all[KnifeMove] = new("Battle", "Choose your performing Ninja Personality and target an enemy Personality: Move both Personalities home; this movement will not be negated. Bow them", "enemy-personality",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Ninja"], ActionKeywords: ["Ninja"], Move: new(MoveSpec.PerformerAndTarget, MoveSpec.Home));
        all[LakesideStraighten] = new("BeforeResolution", "Before this battle resolves, straighten a Personality or Follower", "none", Trigger: "before-battle-resolves");
        all[MercantileBow] = new("Open", "Discipline (1). Political Open: Bow your target unbowed Courtier. The next two Honor losses from your cards (this turn) are increased by 1 each", "own-unbowed-card-with-keyword",
            RequiredKeywords: ["Courtier"], ActionKeywords: ["Political"], Discipline: true);
        all[MutualSupportBoost] = new("Battle", "Choose two or more of your performing opposed Personalities, each with a different Clan alignment: Give each performer +2F", "slots",
            Slots: [new(PerformersSlot, "own-opposed", "Two or more of your opposed Personalities, each with a different Clan alignment", 2, MostPerformers, Performing: true)]);
        all[StealBattle] = new("Battle", "A target enemy card may not use one of its abilities (you choose)", "enemy-card");
        all[StealEngage] = new("Reaction", "Engage: If you control a Scout at this battlefield, a target enemy card may not use one of its abilities (you choose)", "enemy-card-explicitly-at-battle", Trigger: "after-engaging");
        all[WithdrawalFocus] = new("FocusEffect", "As a Focus Effect, if you win this duel, draw a card", "none");
        all[WithdrawalMove] = new("Battle", "Move your target Personality home. You may target an enemy Personality and give him -3F", "slots",
            Slots: [new(MoverSlot, "own-personality", "Your Personality to move home"), new(WeakenedSlot, "enemy-personality", "An enemy Personality to give -3F (optional)", 0, 1)]);
        all[RainbowsPlay] = new("Battle", "Destroy a Terrain (if able). Put this Terrain into play. Your target opposed Personality has +2F (while this card is in play)", "own-opposed");
        all[UnholyAttack] = new("Battle", "Melee 2 Attack", "attack");
        all[UnholyBoost] = new("Reaction", "Interrupt: Give a Melee or Ranged Attack +2 strength", "none", Trigger: "before-attack-resolves");
        return all;
    }
}

/// <summary>The Eternal Rainbows' +2F: the Terrain card and the Personality its play named (read live while the Terrain is in play).</summary>
public sealed record TfStrategiesBRainbow(string TerrainId, string TargetId);

/// <summary>Mercantile Conflict's watch: <paramref name="Remaining"/> more Honor losses from <paramref name="PlayerId"/>'s cards are increased by 1, until the turn <paramref name="Turn"/> ends.</summary>
public sealed record TfStrategiesBHonorWatch(int PlayerId, int Remaining, int Turn, string Source);

public sealed partial class MatchState
{
    [JsonPropertyOrder(1360)]
    public List<TfStrategiesBRainbow> TfStrategiesBRainbows { get; set; } = [];
    [JsonPropertyOrder(1361)]
    public List<TfStrategiesBHonorWatch> TfStrategiesBHonorWatches { get; set; } = [];
}

public sealed partial class MatchEngine
{
    // ---- validation (ValidateMiddle) --------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidateTfStrategiesB(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // Mutual Support: the whole selection (a candidate's own legality is the "own-opposed" category's).
            case TfStrategiesBAbilities.MutualSupportBoost when ability.Slots is not null:
            {
                var performers = SlotCards(command, TfStrategiesBAbilities.PerformersSlot).ToArray();
                Require(performers.All(p => ClanAlignments(p).Length > 0), "Each performer needs a Clan alignment.");
                Require(TfStrategiesBClansDiffer(performers), "Each performer needs a different Clan alignment.");
                break;
            }
            // Steal an Advantage's Engage action: "if you control a Scout at this battlefield".
            case TfStrategiesBAbilities.StealEngage:
                Require(ControlsScoutAtBattlefield(player.Id), "You control no Scout at this battlefield.");
                break;
            // Unholy Strike's Interrupt: "a Melee or Ranged Attack".
            case TfStrategiesBAbilities.UnholyBoost:
                Require(_state.PendingAttack is { Kind: "Melee" or "Ranged" }, "Unholy Strike only gives a Melee or Ranged Attack +2 strength.");
                break;
        }
    }

    // No two of these share a Clan alignment.
    private bool TfStrategiesBClansDiffer(IReadOnlyList<CardInstance> cards)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in cards)
            foreach (var clan in ClanAlignments(card))
                if (!seen.Add(clan)) return false;
        return true;
    }

    // "A Scout at this battlefield" that the player controls: a Personality (or Follower in his unit) with the keyword at the battle's battlefield.
    private bool ControlsScoutAtBattlefield(int playerId)
    {
        var battlefield = _state.PendingEngage?.ProvinceIndex ?? _state.CurrentBattleProvince;
        return battlefield is int province && _state.Cards.Values.Any(c => c.OwnerId == playerId && Keyword(c, "Scout") && InPlay(c) && (c.Zone == Zone.Battlefield ? c.ProvinceIndex == province
            : c.Zone == Zone.Attached && c.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) && host.Zone == Zone.Battlefield && host.ProvinceIndex == province));
    }

    // TargetSlots' offered default for Mutual Support: the first two candidates whose Clan alignments differ (the plain "first two" may share a Clan while another pair is legal).
    private GameCommand? TfStrategiesBDefaultSelection(GameCommand command, AbilityDefinition ability, TargetSlotOption[] options)
    {
        if (command.AbilityId != TfStrategiesBAbilities.MutualSupportBoost || options.FirstOrDefault(o => o.Slot == TfStrategiesBAbilities.PerformersSlot) is not { } offered) return null;
        var picked = new List<CardInstance>();
        foreach (var card in offered.CardIds.Select(Card))
        {
            if (ClanAlignments(card).Length == 0 || !TfStrategiesBClansDiffer([.. picked, card])) continue;
            picked.Add(card);
            if (picked.Count == 2) break;
        }
        return picked.Count == 2 ? command with { Selections = [new(TfStrategiesBAbilities.PerformersSlot, picked.Select(c => c.Id).ToArray())] } : null;
    }

    // ---- execution (RunEffects) -------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunTfStrategiesB(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            // Knife in the Darkness: both move home (the movement will not be negated), then both are bowed -- each unless its own bowing is prevented.
            case TfStrategiesBAbilities.KnifeMove:
            {
                var performer = Card(command.PerformerId);
                ExecuteMove(command, ability, source, target);
                foreach (var card in new[] { performer, target! })
                    if (InPlay(card) && !StalwartNegatesBow(card, player.Id)) card.Bowed = true;
                return false;
            }
            // Mercantile Conflict: the Courtier is bowed; the next two Honor losses from the player's cards this turn each grow by 1.
            case TfStrategiesBAbilities.MercantileBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                _state.TfStrategiesBHonorWatches.RemoveAll(w => w.Turn != _state.TurnNumber || w.Remaining <= 0);
                _state.TfStrategiesBHonorWatches.Add(new(player.Id, 2, _state.TurnNumber, Definition(source!).Name));
                Emit("honorLossWatch", $"{Definition(source!).Name}: the next two Honor losses from {player.Name}'s cards this turn are increased by 1 each.", player.Id, source!.Id);
                return false;
            // Mutual Support: each performer gets +2F.
            case TfStrategiesBAbilities.MutualSupportBoost:
                foreach (var performer in SlotCards(command, TfStrategiesBAbilities.PerformersSlot).ToArray())
                    if (InPlay(performer)) ApplyModifier(performer, source!.Id, id, "Force", 2);
                return false;
            // Steal an Advantage: the acting player chooses one of the target's abilities, which may not be used this turn (Doji Hakuseki's lock).
            case TfStrategiesBAbilities.StealBattle:
            case TfStrategiesBAbilities.StealEngage:
                return BeginListedEffectChoice(command, AbilityLockOptions(target!), $"{Definition(source!).Name}: choose one of {Definition(target!).Name}'s abilities; it may not be used this turn.",
                    consumedGrant, playedFromDiscard, opponentHonorBefore, bowedBefore, preExistingGrants);
            // Strategic Withdrawal: the Personality goes home; an enemy Personality, if one was targeted, has -3F (separate effects).
            case TfStrategiesBAbilities.WithdrawalMove:
            {
                var mover = SlotCards(command, TfStrategiesBAbilities.MoverSlot).First();
                MoveHome(mover, false);
                foreach (var weakened in SlotCards(command, TfStrategiesBAbilities.WeakenedSlot).ToArray())
                    if (InPlay(weakened)) ApplyModifier(weakened, source!.Id, id, "Force", -3);
                return false;
            }
            // The Eternal Rainbows: any Terrain at the battlefield is destroyed (if able), the Terrain enters play, and the target has +2F while it stays.
            case TfStrategiesBAbilities.RainbowsPlay:
            {
                if (_state.CurrentBattleProvince is int battlefield && source!.Zone == Zone.Hand)
                {
                    foreach (var existing in _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == battlefield && Keyword(c, "Terrain") && c.Id != source.Id).OrderBy(c => c.Id, StringComparer.Ordinal).Take(1).ToArray())
                        DiscardTerrain(existing);
                    PutTerrainIntoPlay(player, source);
                    _state.TfStrategiesBRainbows.RemoveAll(r => r.TerrainId == source.Id);
                    _state.TfStrategiesBRainbows.Add(new(source.Id, target!.Id));
                    Emit("terrainBonus", $"{Definition(target).Name} has +2F while {Definition(source).Name} stays in play.", player.Id, target.Id);
                }
                return false;
            }
            // Unholy Strike's Battle action: a Melee 2 Attack (kind and strength are the AttackKind / AttackStrengthBefore hooks below).
            case TfStrategiesBAbilities.UnholyAttack:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant); // true: the Attack's reaction window is open and the action waits
            // Unholy Strike's Interrupt: the Attack in its before-resolution window gets +2 strength.
            case TfStrategiesBAbilities.UnholyBoost:
                _state.PendingAttack!.Strength += 2;
                return false;
        }
        return null;
    }

    // Strategic Withdrawal's Focus Effect (ApplyDuelFocusEffectCore's default arm): "if you win this duel, draw a card" -- the focuser's side's Personality won (a tie has no winner).
    private void ApplyTfStrategiesBFocusEffect(string abilityId, int ownerId, int? winnerPlayerId)
    {
        if (abilityId == TfStrategiesBAbilities.WithdrawalFocus && winnerPlayerId == ownerId) Draw(Player(ownerId));
    }

    // The Eternal Rainbows' +2F, read live (PersonalityForce hook): each Terrain still in play whose play named this Personality.
    private int TfStrategiesBRainbowForce(CardInstance card)
    {
        if (_state.TfStrategiesBRainbows.Count == 0 || !InPlay(card)) return 0;
        return 2 * _state.TfStrategiesBRainbows.Count(r => r.TargetId == card.Id && _state.Cards.TryGetValue(r.TerrainId, out var terrain) && terrain.Zone == Zone.Battlefield);
    }

    // GainHonor's hook, run just before a loss is applied: Mercantile Conflict raises a loss from its player's cards by 1 for each watch that still has a use this turn.
    private int TfStrategiesBHonorLoss(int playerId, int amount, string reason, CardInstance? source)
    {
        if (amount >= 0 || _state.TfStrategiesBHonorWatches.Count == 0) return amount;
        _state.TfStrategiesBHonorWatches.RemoveAll(w => w.Turn != _state.TurnNumber || w.Remaining <= 0);
        if (reason == "Dishonorable death" || ControlsForgottenTemple(playerId)) return amount; // the rulebook's own loss is from no card; the Temple's loss "may not be increased"
        var from = source ?? (_runningCommand?.CardId is string runningId && _state.Cards.TryGetValue(runningId, out var running) ? running : null);
        if (from is null) return amount;
        var fromPlayer = from.OriginalOwnerId ?? from.OwnerId;
        foreach (var watch in _state.TfStrategiesBHonorWatches.Where(w => w.PlayerId == fromPlayer).ToArray())
        {
            _state.TfStrategiesBHonorWatches[_state.TfStrategiesBHonorWatches.IndexOf(watch)] = watch with { Remaining = watch.Remaining - 1 };
            amount -= 1;
            Emit("honorLossIncreased", $"{Player(playerId).Name}'s Honor loss from {reason} is increased by 1 ({watch.Source}).", playerId, from.Id);
        }
        return amount;
    }

    // ---- Lakeside Retreat: its before-resolution trait, answered through "beforeBattleOptional" -----------------------------------------------------------------------------------------

    // The Personalities and Followers in play of either player's that are bowed (a straightened one has nothing to straighten).
    private IEnumerable<CardInstance> LakesideRetreatCandidates() =>
        _state.Cards.Values.Where(c => c.Bowed && InPlay(c) && (Definition(c).Type == CardType.Personality || Definition(c).Type == CardType.Follower && c.Zone == Zone.Attached))
            .OrderBy(c => c.Id, StringComparer.Ordinal);

    private bool IsLakesideRetreatChoice(PendingBeforeBattleResolve pending) =>
        pending.ActiveSourceId is { } itemId && _state.Cards.TryGetValue(itemId, out var source) && BeforeResolutionAbilityId(source) == TfStrategiesBAbilities.LakesideStraighten;

    // Opens the choice, or (nothing is bowed) does nothing. True when the choice is open.
    private bool BeginLakesideRetreat(CardInstance terrain, PendingBeforeBattleResolve pending)
    {
        if (!LakesideRetreatCandidates().Any())
        {
            Emit("beforeBattleEffect", $"{Definition(terrain).Name}: no Personality or Follower is bowed.", terrain.OwnerId, terrain.Id);
            return false;
        }
        _state.PendingChoice = new("beforeBattleOptional", terrain.OwnerId, terrain.Id, $"Before this battle resolves: straighten a Personality or Follower ({Definition(terrain).Name}).");
        _state.PriorityPlayerId = terrain.OwnerId;
        return true;
    }

    // "use" names the card (TargetId). There is no declining: the straightening is mandatory when there is a card to straighten.
    private void ResolveLakesideRetreat(GameCommand command, int playerId)
    {
        Require(command.Choice == "use", "Lakeside Retreat must straighten a Personality or Follower.");
        Require(command.TargetId is not null && LakesideRetreatCandidates().Any(c => c.Id == command.TargetId), "Choose a bowed Personality or Follower.");
        var card = Card(command.TargetId);
        Straighten(card);
        Emit("beforeBattleEffect", $"Lakeside Retreat: {Definition(card).Name} was straightened.", playerId, card.Id);
    }

    private IEnumerable<ActionOption> LakesideRetreatOptions(int playerId, string? sourceId)
    {
        if (_state.PendingChoice?.PlayerId != playerId) yield break;
        foreach (var card in LakesideRetreatCandidates())
            yield return new("beforeBattleOptional", $"Straighten {Definition(card).Name}", true, null, new GameCommand("beforeBattleOptional", playerId, _state.Version, TargetId: card.Id, Choice: "use"), card.Id);
    }

    // PlayStrategy's Terrain rule: Lakeside Retreat's "Destroy a Terrain (if able). Put this Terrain into play" replaces the Terrain already at the battlefield.
    private static bool TfStrategiesBTerrainReplaces(CardDefinition definition) => (definition.Abilities ?? []).Contains(TfStrategiesBAbilities.LakesideStraighten);

    // TerrainPlaysThroughOwnAbility: The Eternal Rainbows names a target, so it is played through its own Battle ability.
    private static bool TfStrategiesBTerrainThroughAbility(CardInstance card) => (Definition(card).Abilities ?? []).Contains(TfStrategiesBAbilities.RainbowsPlay);

    // MovementCannotBeNegated: Knife in the Darkness's "this movement will not be negated".
    private static bool TfStrategiesBUnnegatable(GameCommand command) => command.AbilityId == TfStrategiesBAbilities.KnifeMove;

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTfStrategiesBHooks()
    {
        EngineHooks.Abilities.Register("AddTfStrategiesB", 11800, static abilities => abilities.AddTfStrategiesB());
        EngineHooks.ValidateMiddle.Register("ValidateTfStrategiesB", 11800, static (engine, context) => engine.ValidateTfStrategiesB(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunTfStrategiesB", 11800, static (engine, c) =>
            engine.RunTfStrategiesB(c.id, c.ability, c.command, c.player, c.source, c.target, c.consumedGrant, c.playedFromDiscard, c.opponentHonorBeforeBattleAction, c.bowedBeforeAction, c.preExistingGrants));
        EngineHooks.BowsTarget.Register("TfStrategiesBBowsTarget", 11800, static context => context.abilityId is TfStrategiesBAbilities.MercantileBow or TfStrategiesBAbilities.KnifeMove);
        EngineHooks.PersonalityForce.Register("TfStrategiesBRainbowForce", 11800, static (engine, context) => engine.TfStrategiesBRainbowForce(context.card));
        EngineHooks.AttackKind.Register("TfStrategiesBAttackKind", 11800, static context => context.abilityId == TfStrategiesBAbilities.UnholyAttack ? "Melee" : null);
        EngineHooks.AttackStrengthBefore.Register("TfStrategiesBAttackStrength", 11800, static (engine, context) => context.abilityId == TfStrategiesBAbilities.UnholyAttack ? 2 : null);
    }
}
