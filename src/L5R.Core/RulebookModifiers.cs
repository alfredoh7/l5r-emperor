using System.Text.Json.Serialization;

namespace L5R.Core;

// Primitive P19 (issue #717, docs/decisions/2026-10-07-p19-rulebook-modifiers.md): rulebook-action modifiers. ENGINE ONLY: no card is wired here; a later wave registers the modifiers.
//
// The rulebook actions (Tactical Advantage, Equip (Limited), Equip (Battle), Favor (Battle)) are catalogue ids and commands with fixed amounts, timings, source piles and performer rules. A card that
// changes one of them ("+2 to Tactical Advantage's Force bonus when he performs", "Equip may take this Follower from your discard pile", "Equip (Battle) even if bowed and unopposed") had no place to say so.
//   THE CHANNEL  EngineHooks.RulebookModifier: Func<MatchEngine, RulebookContext, RulebookModifier?>. Consulted with (abilityId, player, performer, command); a registrant returns null when it has nothing to say.
//                Results merge: amounts add, the first timing override wins, every flag ORs. Order is priority then ordinal id like every other channel; with nothing registered the question is a length check.
//   THE STORE    MatchState.PlayerTimingGrants: card-free timing grants (once per turn, one turn or game-long) beside the card-keyed EffectTimingGrants of TimingGrants.cs.
// Ability ids the channel is asked about: "tactical-advantage", "equip-battle", "favor-battle" (any PlayerAbility id) and the Equip (Limited) command, whose id is "equip".

/// <summary>Where a rulebook action may take its card from besides the hand.</summary>
public enum RulebookSourcePile { Hand, Discard }

/// <summary>Performer conditions a modifier waives: Equip (Battle) needs an unbowed Shugenja in an opposed army.</summary>
[Flags]
public enum RulebookExemptions { None = 0, Bowed = 1, Unopposed = 2 }

/// <summary>What a registrant changes about one rulebook action. <see cref="AmountDelta"/> adds to the action's amount (Tactical Advantage's Force bonus); <see cref="TimingOverride"/> replaces its timing
/// ("BattleOrOpen"); <see cref="SourcePileAllowed"/> Discard lets Equip take the attachment from its owner's discard pile; <see cref="Exemptions"/> waives performer conditions; <see cref="Absent"/> lets it be taken
/// without a unit at the battlefield; <see cref="AnyTarget"/> lets an "enemy-attacking" target be any Personality at the battlefield.</summary>
public sealed record RulebookModifier(int AmountDelta = 0, string? TimingOverride = null, RulebookSourcePile SourcePileAllowed = RulebookSourcePile.Hand,
    RulebookExemptions Exemptions = RulebookExemptions.None, bool Absent = false, bool AnyTarget = false);

/// <summary>A timing grant that belongs to the player, not to a card in play (Practicing Kata's once-per-turn Equip, an Event's game-long one). Stored in MatchState.PlayerTimingGrants.</summary>
public sealed class PlayerTimingGrant
{
    public string Id { get; set; } = "";
    public int PlayerId { get; set; }
    /// <summary>An <see cref="ActionClass"/> constant.</summary>
    public string ActionClass { get; set; } = "";
    /// <summary>"Battle", "Open" or "BattleOrOpen", as <see cref="TimingGrant.Timing"/>.</summary>
    public string Timing { get; set; } = "BattleOrOpen";
    public bool OncePerTurn { get; set; }
    /// <summary>Narrows the grant to some of the attachments of its class (Harbingers of War's "Followers and Items with a base Gold Cost of 1 to 4"): a key a feature answers in TimingGrantFilterAllows. Null: every one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Filter { get; set; }
    /// <summary>The turn it lapses after; null for game-long.</summary>
    public int? ExpiresAfterTurn { get; set; }
    /// <summary>The turn number it was last spent on (OncePerTurn only).</summary>
    public int? UsedOnTurn { get; set; }
    public bool GrantsOpen => Timing is "Open" or "BattleOrOpen";
    public bool GrantsBattle => Timing is "Battle" or "BattleOrOpen";
}

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Func<MatchEngine, MatchEngine.RulebookContext, RulebookModifier?>> RulebookModifier = new();
}

public sealed partial class MatchState
{
    // 3200-3299 is reserved for P19. Both properties are omitted while unused, so a match that never had a card-free grant serializes exactly as before.
    [JsonPropertyOrder(3200)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PlayerTimingGrant>? PlayerTimingGrants { get; set; }
    [JsonPropertyOrder(3201)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int PlayerTimingGrantSequence { get; set; }
}

public sealed partial class MatchEngine
{
    internal readonly record struct RulebookContext(string AbilityId, int PlayerId, string? PerformerId, GameCommand? Command);

    /// <summary>Test seam (L5R.Core.Tests has InternalsVisibleTo): modifiers a test supplies, read after the registered ones. Always null outside a test.</summary>
    internal static List<Func<MatchEngine, RulebookContext, RulebookModifier?>>? TestRulebookModifiers;

    // The merged answer of every registrant, or null when none has anything to say.
    private RulebookModifier? RulebookModifierFor(string abilityId, int playerId, string? performerId, GameCommand? command)
    {
        var hooks = EngineHooks.RulebookModifier.Items;
        var extra = TestRulebookModifiers;
        if (hooks.Length == 0 && extra is not { Count: > 0 }) return null;
        var context = new RulebookContext(abilityId, playerId, performerId, command);
        RulebookModifier? merged = null;
        foreach (var hook in hooks) merged = MergeRulebookModifiers(merged, hook(this, context));
        if (extra is not null) foreach (var hook in extra) merged = MergeRulebookModifiers(merged, hook(this, context));
        return merged;
    }

    private static RulebookModifier? MergeRulebookModifiers(RulebookModifier? first, RulebookModifier? next) =>
        next is null ? first : first is null ? next : new RulebookModifier(first.AmountDelta + next.AmountDelta, first.TimingOverride ?? next.TimingOverride,
            first.SourcePileAllowed == RulebookSourcePile.Discard || next.SourcePileAllowed == RulebookSourcePile.Discard ? RulebookSourcePile.Discard : RulebookSourcePile.Hand,
            first.Exemptions | next.Exemptions, first.Absent || next.Absent, first.AnyTarget || next.AnyTarget);

    // The ability as this action is taken: timing and Absent as the performer's modifiers leave them. Only a rulebook (player) ability is asked.
    private AbilityDefinition RulebookAbility(AbilityDefinition ability, string id, GameCommand command)
    {
        if (!ability.PlayerAbility || RulebookModifierFor(id, command.PlayerId, command.PerformerId, command) is not { } modifier) return ability;
        return ability with { Timing = modifier.TimingOverride ?? ability.Timing, Absent = ability.Absent || modifier.Absent };
    }

    // Tactical Advantage's Force bonus is its Focus Value plus this; never below zero.
    private int RulebookAmountDelta(string abilityId, int playerId, GameCommand command) => RulebookModifierFor(abilityId, playerId, command.PerformerId, command)?.AmountDelta ?? 0;

    // "Target any Personality": "enemy-attacking" targets are widened for a player ability (Hantei's Guidance's Favor (Battle)).
    private bool RulebookAllowsAnyTarget(string abilityId, int playerId, GameCommand command) => RulebookModifierFor(abilityId, playerId, command.PerformerId, command)?.AnyTarget == true;

    // Whether the owner may take this card from his discard pile for the rulebook action (Equip's own command id "equip", or "equip-battle"). Private to the owner: only he is ever asked.
    private bool RulebookDiscardSource(string abilityId, int playerId, string? performerId, CardInstance card)
    {
        if (card.OwnerId != playerId || card.Zone != Zone.FateDiscard) return false;
        var command = abilityId == "equip" ? new GameCommand("equip", playerId, _state.Version, card.Id, TargetId: performerId)
            : new GameCommand("useAbility", playerId, _state.Version, AbilityId: abilityId, PerformerId: performerId, TargetId: card.Id);
        return RulebookModifierFor(abilityId, playerId, performerId, command)?.SourcePileAllowed == RulebookSourcePile.Discard;
    }

    // Equip (Battle)'s performer when a modifier waives a condition: an own Shugenja at the battlefield, bowed and/or unopposed only as far as the exemptions say.
    private bool RulebookEquipPerformerLegal(int playerId, CardInstance performer, GameCommand? command)
    {
        if (performer.OwnerId != playerId || Definition(performer).Type != CardType.Personality || !AtBattle(performer) || !Keyword(performer, "Shugenja")) return false;
        if (RulebookModifierFor("equip-battle", playerId, performer.Id, command) is not { Exemptions: not RulebookExemptions.None } modifier) return false;
        var opposed = _state.CurrentBattleProvince is int province && Army(1 - playerId, province).Any();
        return (!performer.Bowed || modifier.Exemptions.HasFlag(RulebookExemptions.Bowed)) && (opposed || modifier.Exemptions.HasFlag(RulebookExemptions.Unopposed));
    }

    private IEnumerable<string?> RulebookEquipPerformers(int playerId) =>
        EngineHooks.RulebookModifier.Items.Length == 0 && TestRulebookModifiers is not { Count: > 0 } || _state.CurrentBattleProvince is null ? []
            : _state.Cards.Values.Where(c => RulebookEquipPerformerLegal(playerId, c, null)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id);

    // The discard-pile attachments the owner may equip with the Equip command, in id order. Empty (and free) while no modifier is registered.
    private IEnumerable<CardInstance> RulebookEquipDiscardCandidates(int playerId, string abilityId)
    {
        if (EngineHooks.RulebookModifier.Items.Length == 0 && TestRulebookModifiers is not { Count: > 0 }) return [];
        return _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.FateDiscard
                && (abilityId == "equip" ? Definition(c).Type is CardType.Item or CardType.Follower or CardType.Spell : Definition(c).Type == CardType.Spell || Keyword(c, "Reserve"))
                && RulebookDiscardSource(abilityId, playerId, null, c))
            .OrderBy(c => c.Id, StringComparer.Ordinal);
    }

    // QueryActions' view of a widened rulebook ability: the ability with the timing/Absent any of this player's performers would give it, so the offer loop reaches it in the phase the override names.
    // The performers are filtered afterwards (RulebookKeepPerformers) so a timing for one Personality is not offered for another.
    private AbilityDefinition RulebookListingAbility(AbilityDefinition ability, string id, int playerId)
    {
        if (!ability.PlayerAbility || EngineHooks.RulebookModifier.Items.Length == 0 && TestRulebookModifiers is not { Count: > 0 }) return ability;
        var timing = RulebookModifierFor(id, playerId, null, null)?.TimingOverride;
        var absent = ability.Absent || RulebookModifierFor(id, playerId, null, null)?.Absent == true;
        timing ??= _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => RulebookModifierFor(id, playerId, c.Id, null)?.TimingOverride).FirstOrDefault(t => t is not null);
        return timing is null && absent == ability.Absent ? ability : ability with { Timing = timing ?? ability.Timing, Absent = absent };
    }

    // Whether the printed timing already allows the action in this phase for this player (the base of RulebookListingAbility's widening).
    private bool RulebookBaseTimingOpenNow(AbilityDefinition baseAbility, int playerId) => baseAbility.Timing switch
    {
        "Battle" => _state.Phase == Phase.Battle,
        "BattleOrOpen" => _state.Phase is Phase.Action or Phase.Battle,
        "Open" => _state.Phase == Phase.Action,
        "Limited" => _state.Phase == Phase.Action && playerId == _state.ActivePlayerId,
        _ => true
    };

    // A performer is offered when the printed timing is open now, or his own modifier overrides the timing.
    private string?[] RulebookKeepPerformers(AbilityDefinition baseAbility, AbilityDefinition widened, string id, int playerId, string?[] performers) =>
        ReferenceEquals(baseAbility, widened) || RulebookBaseTimingOpenNow(baseAbility, playerId) ? performers
            : performers.Where(p => p is not null && RulebookModifierFor(id, playerId, p, null)?.TimingOverride is not null).ToArray();

    // ---- card-free timing grants --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Gives a player a timing for a class of action that no card in play gives (Practicing Kata: Equip as Battle/Open once per turn). With <paramref name="thisTurnOnly"/> it lapses with the turn; otherwise it is game-long. Returns its id.</summary>
    internal string GrantPlayerTiming(int playerId, string actionClass, string timing, bool oncePerTurn = false, bool thisTurnOnly = false, string? filter = null)
    {
        _state.PlayerTimingGrants ??= [];
        var id = $"timing-grant-{++_state.PlayerTimingGrantSequence}";
        _state.PlayerTimingGrants.Add(new PlayerTimingGrant { Id = id, PlayerId = playerId, ActionClass = actionClass, Timing = timing, OncePerTurn = oncePerTurn, Filter = filter, ExpiresAfterTurn = thisTurnOnly ? _state.TurnNumber : null });
        return id;
    }

    internal void RemovePlayerTimingGrant(string id) => _state.PlayerTimingGrants?.RemoveAll(g => g.Id == id);

    // The grants this player holds now for this class (or for every equip), unspent, in id order.
    private PlayerTimingGrant[] FreeTimingGrantsOf(int playerId, string? actionClass, CardInstance? attachment = null)
    {
        PruneWave740KataGrants();
        return _state.PlayerTimingGrants is not { Count: > 0 } grants ? []
            : grants.Where(g => g.PlayerId == playerId && (g.Filter is null || attachment is not null && TimingGrantFilterAllows(g.Filter, attachment)) && (g.ExpiresAfterTurn is null || g.ExpiresAfterTurn >= _state.TurnNumber) && (!g.OncePerTurn || g.UsedOnTurn != _state.TurnNumber)
                && (g.ActionClass == ActionClass.EquipAny || g.ActionClass == actionClass)).OrderBy(g => g.Id, StringComparer.Ordinal).ToArray();
    }

    private void SpendPlayerTimingGrant(PlayerTimingGrant? grant) { if (grant is { OncePerTurn: true }) grant.UsedOnTurn = _state.TurnNumber; }

    internal int PlayerTimingGrantCountForTest() => _state.PlayerTimingGrants?.Count ?? 0;

    // Registration only: no feature registers a modifier here (the later card wave does); the channel is sealed at startup by EngineHookStartup.Build.
}
