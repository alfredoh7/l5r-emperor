using System.Text.Json.Serialization;

namespace L5R.Core;

// Primitive P12 (issue #696; docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md): the immunity ledger.
//
// "Other players' cards will not bow this Shugenja", "negate his destruction from battle resolution", "negate other players' effects that prevent him assigning, moving or performing
// actions", "his movement cannot be negated", "negate Force penalties", "other players' cards do not affect your provinces' strengths", "negate Personalities' movement to or from this
// battlefield": before this file each of these was a one-off (NegatesEffect, a per-turn stamp, a per-command flag...). One ledger and one question now cover them:
//
//     IsImmune(card, kind, sourcePlayer)        -- asked once, at the choke point where the effect would happen, and ONLY there.
//
// An immunity is either
//   * STORED (dynamic) -- an Immunity record an action creates (GrantImmunity / GrantPlayerImmunity / GrantBattlefieldImmunity): on one card, on a player (his provinces, his cards), or on a
//     battlefield, until the end of the turn / phase / a TurnUntil boundary / the game / while the card (and the card that granted it) is in play; or
//   * STANDING -- a StandingImmunity rule a card's feature file registers once on EngineHooks.StandingImmunities: it covers the card itself, its controller's cards that fit a keyword, the
//     Followers attached to it, its controller (province strength), or the battlefield it stands at; it may be conditional ("while defending") and is read live while the carrier is in play.
//
// WHICH EFFECTS: an immunity stops a CARD EFFECT whose source is another player's (Source OtherPlayers), any player's (Any) or another Terrain's (OtherTerrains: the effect is an action of a
// Terrain card other than the immune one). A cost, the rulebook and a battle's own resolution are not card effects and have no source player, so they are never stopped -- except
// DestroyInResolution, which is exactly the battle resolution's own destruction and ignores the source. An action whose bowing, destruction or movement "will not be negated" (Hiromi,
// Makito, Suitengu's Torch: HiromiUnnegatable / CurrentActionUnnegatable) is not stopped by an immunity either, as it is not stopped by a watch.
//
// ORDER at every choke point: the delay store, the protections and shields already there, THEN the immunity, THEN the P7 watch, then the effect. An immune effect is NEGATED, never delayed,
// and a negated effect does not consume a watch (the immunity check sits before TryConsumeWatch and returns).
//
// KINDS and where each is asked (one `if` each):
//   Bow                 StalwartNegatesBow (per card: inside a unit bow every card asks for itself, R-017).
//   Destroy             DestroyUnits, DestroyAttachment.
//   DestroyInResolution TfResolutionDestroyable (the one filter the battle resolution's three destroy sites share).
//   MoveNegation        MovementNegationBarred (the predicate every movement negation already asks: Winter Sled's, PerformersSled.cs) and the two sled-only guards beside it.
//   Prevention          HasActionLock (action locks) and MovementLocked (movement locks, which also refuse assigning): the lock is ignored when it is another player's.
//   ForcePenalty        ApplyModifier.
//   ProvinceStrength    the player's provinces: GiveProvinceStrengthThisTurn / GiveProvinceStrengthUntilResolution (the writers) and ProvinceSourceBonuses / StaticProvinceBonus (the readers).
//   TerrainDestroy      DiscardTerrain.
//   MovementAtBattlefield MoveHome, MovementToBattlefieldNegated and the location switch: a movement by an effect from or to the battlefield.

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImmunityKind { Bow, Destroy, DestroyInResolution, MoveNegation, Prevention, ForcePenalty, ProvinceStrength, TerrainDestroy, MovementAtBattlefield }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImmunitySource
{
    /// <summary>Only an effect of a card another player controls than the immune card's controller (the usual "other players' cards").</summary>
    OtherPlayers,
    /// <summary>Any card effect, the controller's own included.</summary>
    Any,
    /// <summary>An effect of a Terrain card other than the immune card ("Terrains cannot be destroyed by other Terrains").</summary>
    OtherTerrains,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImmunityUntil
{
    /// <summary>The turn whose number is UntilTurn.</summary>
    Turn,
    /// <summary>The phase UntilPhase of the turn UntilTurn.</summary>
    Phase,
    /// <summary>A TurnUntil boundary (Boundary): "until your next turn begins".</summary>
    TurnBoundary,
    /// <summary>For the rest of the game.</summary>
    Permanent,
    /// <summary>As long as the immune card (when it names one) and OwnerCardId (the card that granted it, when set) are in play.</summary>
    WhileInPlay,
}

/// <summary>One stored immunity. It names a card (CardId), a player (PlayerId: his provinces, or with TargetKeyword/TargetType his cards that fit) or a battlefield (Battlefield).</summary>
public sealed class Immunity
{
    public string Id { get; set; } = "";
    public ImmunityKind Kind { get; set; }
    public string? CardId { get; set; }
    public int? PlayerId { get; set; }
    public int? Battlefield { get; set; }
    public string? TargetKeyword { get; set; }
    public CardType? TargetType { get; set; }
    public ImmunitySource Source { get; set; } = ImmunitySource.OtherPlayers;
    public ImmunityUntil Until { get; set; } = ImmunityUntil.Turn;
    public int UntilTurn { get; set; }
    public Phase? UntilPhase { get; set; }
    public TurnUntil? Boundary { get; set; }
    /// <summary>The card whose effect granted it; the immunity ends when that card leaves play (WhileInPlay).</summary>
    public string? OwnerCardId { get; set; }
}

public sealed partial class MatchState
{
    // 2200-2299 is reserved for P12. Both properties are omitted while unused, so a match that never had an immunity serializes exactly as before.
    [JsonPropertyOrder(2200)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Immunity>? Immunities { get; set; }
    [JsonPropertyOrder(2201)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ImmunitySequence { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImmunityScope
{
    /// <summary>The card that carries the rule.</summary>
    Self,
    /// <summary>Cards its controller controls that fit TargetKeyword (and TargetType, when set), the carrier included when it fits.</summary>
    OwnersCards,
    /// <summary>The Followers attached to the carrier.</summary>
    AttachedFollowers,
    /// <summary>The controller himself (province strength): his provinces.</summary>
    Player,
    /// <summary>The battlefield the carrier stands at (a Terrain there, a Region on that province): movement from or to it.</summary>
    Battlefield,
    /// <summary>The card the carrier is attached to: "this Shugenja" of a Spell, "this Personality" of an Item (issue #736).</summary>
    AttachedHost,
}

/// <summary>What a choke point tells a rule about the effect: the subject (a card, a player or a battlefield) and the effect's source.</summary>
internal sealed record ImmunityContext(ImmunityKind Kind, CardInstance? Card, int? PlayerId, int? Battlefield, int? SourcePlayerId, CardInstance? SourceCard);

/// <summary>An immunity a card carries (registered once, on EngineHooks.StandingImmunities). OwnerIs selects the carrier cards in play; Scope and TargetKeyword pick what is immune.</summary>
internal sealed record StandingImmunity(
    string Id,
    Func<CardDefinition, bool> OwnerIs,
    ImmunityKind Kind,
    ImmunityScope Scope = ImmunityScope.Self,
    string? TargetKeyword = null,
    CardType? TargetType = null,
    ImmunitySource Source = ImmunitySource.OtherPlayers,
    Func<MatchEngine, ImmunityContext, CardInstance, bool>? Condition = null,
    bool EventCarrier = false)
{
    /// <summary>The rule for the cards whose catalogue Effect is `effect` (how a card names its own traits).</summary>
    public static Func<CardDefinition, bool> OnEffect(string effect) => d => d.Effect == effect;
}

internal static partial class EngineHooks
{
    /// <summary>Standing immunities (ImmunityLedger.cs). Register with a unique id and a priority of 10000 or more; the rules are read at every choke point.</summary>
    internal static readonly OrderedHooks<Func<StandingImmunity>> StandingImmunities = new();
}

public sealed partial class MatchEngine
{
    // ---- registering ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private string NextImmunityId() => "immunity-" + (++_state.ImmunitySequence);

    private Immunity AddImmunity(Immunity immunity)
    {
        _state.Immunities ??= [];
        _state.Immunities.RemoveAll(i => !ImmunityLive(i)); // keep the ledger short: what has lapsed is gone
        if (string.IsNullOrEmpty(immunity.Id)) immunity.Id = NextImmunityId();
        _state.Immunities.Add(immunity);
        return immunity;
    }

    private Immunity Stamp(Immunity immunity, ImmunityUntil until, int? untilTurn, TurnUntil? boundary, string? ownerCardId)
    {
        immunity.Until = until; immunity.UntilTurn = untilTurn ?? _state.TurnNumber; immunity.Boundary = boundary; immunity.OwnerCardId = ownerCardId;
        if (until == ImmunityUntil.Phase) immunity.UntilPhase = _state.Phase;
        return immunity;
    }

    /// <summary>A card is immune to `kind` ("A Cleansing Breath: negate other players' effects that prevent him assigning ...", "negate his next ... "), until the end of the turn unless `until` says otherwise.</summary>
    internal Immunity GrantImmunity(CardInstance card, ImmunityKind kind, ImmunityUntil until = ImmunityUntil.Turn, ImmunitySource source = ImmunitySource.OtherPlayers, string? ownerCardId = null, TurnUntil? boundary = null) =>
        AddImmunity(Stamp(new Immunity { Kind = kind, CardId = card.Id, Source = source }, until, null, boundary, ownerCardId));

    /// <summary>A player is immune to `kind` (ProvinceStrength: "your provinces"), or with a keyword the cards of his that fit it.</summary>
    internal Immunity GrantPlayerImmunity(int playerId, ImmunityKind kind, ImmunityUntil until = ImmunityUntil.Turn, ImmunitySource source = ImmunitySource.OtherPlayers, string? ownerCardId = null, TurnUntil? boundary = null,
        string? targetKeyword = null, CardType? targetType = null) =>
        AddImmunity(Stamp(new Immunity { Kind = kind, PlayerId = playerId, Source = source, TargetKeyword = targetKeyword, TargetType = targetType }, until, null, boundary, ownerCardId));

    /// <summary>A battlefield is immune to `kind` (MovementAtBattlefield: Personalities' movement to or from it is negated).</summary>
    internal Immunity GrantBattlefieldImmunity(int battlefield, ImmunityKind kind, ImmunityUntil until = ImmunityUntil.Turn, ImmunitySource source = ImmunitySource.OtherPlayers, string? ownerCardId = null, TurnUntil? boundary = null) =>
        AddImmunity(Stamp(new Immunity { Kind = kind, Battlefield = battlefield, Source = source }, until, null, boundary, ownerCardId));

    internal void RemoveImmunity(string id) => _state.Immunities?.RemoveAll(i => i.Id == id);

    // ---- reading -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool ImmunityLive(Immunity immunity)
    {
        if (immunity.CardId is not null && !(_state.Cards.TryGetValue(immunity.CardId, out var subject) && InPlay(subject))) return false; // an immunity leaves with the card
        return immunity.Until switch
        {
            ImmunityUntil.Turn => immunity.UntilTurn == _state.TurnNumber,
            ImmunityUntil.Phase => immunity.UntilTurn == _state.TurnNumber && immunity.UntilPhase == _state.Phase,
            ImmunityUntil.TurnBoundary => immunity.Boundary is null || !UntilPassed(immunity.Boundary),
            ImmunityUntil.WhileInPlay => immunity.OwnerCardId is null || _state.Cards.TryGetValue(immunity.OwnerCardId, out var owner) && InPlay(owner),
            _ => true
        };
    }

    private static StandingImmunity[]? _standingImmunityRules;
    /// <summary>Test seam (L5R.Core.Tests has InternalsVisibleTo): extra rules a test supplies, read beside the registered ones. Always null outside a test.</summary>
    internal static List<StandingImmunity>? TestStandingImmunities;

    private static IEnumerable<StandingImmunity> StandingImmunityRules()
    {
        _standingImmunityRules ??= EngineHooks.StandingImmunities.Items.Select(item => item()).ToArray();
        return TestStandingImmunities is { Count: > 0 } extra ? _standingImmunityRules.Concat(extra) : _standingImmunityRules;
    }

    /// <summary>The battlefield a carrier stands at: a card on the battlefield, or a Region attached to the province of that index.</summary>
    private static int? CarrierBattlefield(CardInstance carrier) => carrier.Zone is Zone.Battlefield or Zone.Region ? carrier.ProvinceIndex : null;

    private bool ImmunitySourceMatches(ImmunitySource source, ImmunityKind kind, int subjectController, CardInstance? subjectCard, int? sourcePlayerId, CardInstance? sourceCard)
    {
        if (kind == ImmunityKind.DestroyInResolution) return true; // the battle's own resolution has no source player; it is what this kind is about
        if (sourcePlayerId is null) return false; // a cost, the rulebook: not a card effect
        return source switch
        {
            ImmunitySource.Any => true,
            ImmunitySource.OtherTerrains => sourceCard is not null && IsTerrainCard(sourceCard) && sourceCard.Id != subjectCard?.Id,
            _ => sourcePlayerId != subjectController
        };
    }

    private bool StoredImmunityCovers(Immunity i, CardInstance? card, int? playerId, int? battlefield) =>
        i.CardId is not null ? card is not null && i.CardId == card.Id
        : i.Battlefield is not null ? battlefield == i.Battlefield
        : i.PlayerId is not null && (card is null ? playerId == i.PlayerId
            : card.OwnerId == i.PlayerId && (i.TargetKeyword is not null || i.TargetType is not null) && InPlay(card) && (i.TargetKeyword is null || Keyword(card, i.TargetKeyword)) && (i.TargetType is null || Definition(card).Type == i.TargetType)); // a type alone names the cards too (issue #735: The Inevitable Grasp of Conquest)

    private bool StandingImmunityCovers(StandingImmunity rule, CardInstance carrier, CardInstance? card, int? playerId, int? battlefield) => rule.Scope switch
    {
        ImmunityScope.Self => card is not null && card.Id == carrier.Id,
        ImmunityScope.OwnersCards => card is not null && card.OwnerId == carrier.OwnerId && InPlay(card) && (rule.TargetKeyword is null || Keyword(card, rule.TargetKeyword)) && (rule.TargetType is null || Definition(card).Type == rule.TargetType),
        ImmunityScope.AttachedFollowers => card is not null && card.Zone == Zone.Attached && card.AttachedTo == carrier.Id && Definition(card).Type == CardType.Follower,
        ImmunityScope.Player => card is null && playerId == carrier.OwnerId,
        ImmunityScope.AttachedHost => card is not null && carrier.Zone == Zone.Attached && carrier.AttachedTo == card.Id,
        _ => card is null && battlefield is not null && CarrierBattlefield(carrier) == battlefield // ImmunityScope.Battlefield
    };

    /// <summary>
    /// THE question: is this subject immune to `kind` from this source? The subject is a card, or (card null) the player `playerId` or the battlefield `battlefield`. `sourcePlayerId` is the player whose
    /// card's effect it is (null: not a card effect, nothing is stopped), `sourceCard` the card whose action it is when known (OtherTerrains). Pure: nothing is spent, nothing is logged.
    /// </summary>
    internal bool IsImmune(CardInstance? card, ImmunityKind kind, int? sourcePlayerId, CardInstance? sourceCard = null, int? playerId = null, int? battlefield = null)
    {
        var stored = _state.Immunities;
        var rules = StandingImmunityRules();
        if ((stored is null || stored.Count == 0) && !rules.Any()) return false;
        if (sourcePlayerId is not null && kind is ImmunityKind.Bow or ImmunityKind.Destroy or ImmunityKind.TerrainDestroy or ImmunityKind.ForcePenalty or ImmunityKind.MovementAtBattlefield
            && (HiromiUnnegatable() || CurrentActionUnnegatable())) return false; // an action whose bowing, destruction or movement will not be negated
        var controller = card?.OwnerId ?? playerId;
        var context = new ImmunityContext(kind, card, playerId, battlefield, sourcePlayerId, sourceCard);
        if (stored is not null)
            foreach (var i in stored)
                if (i.Kind == kind && StoredImmunityCovers(i, card, playerId, battlefield) && ImmunityLive(i)
                    && ImmunitySourceMatches(i.Source, kind, controller ?? (i.PlayerId ?? -1), card, sourcePlayerId, sourceCard)) return true;
        foreach (var rule in rules)
        {
            if (rule.Kind != kind) continue;
            foreach (var carrier in _state.Cards.Values.Where(c => (InPlay(c) || rule.EventCarrier && c.Zone == Zone.EventInPlay) && rule.OwnerIs(Definition(c))))
            {
                if (!StandingImmunityCovers(rule, carrier, card, playerId, battlefield)) continue;
                if (!ImmunitySourceMatches(rule.Source, kind, controller ?? carrier.OwnerId, card, sourcePlayerId, sourceCard)) continue;
                if (rule.Condition is { } condition && !condition(this, context, carrier)) continue;
                return true;
            }
        }
        return false;
    }

    /// <summary>The card whose action is running right now (a resolving command), or null when none is.</summary>
    private CardInstance? EffectSourceCard() =>
        (_runningCommand ?? _attackCommandInResolution)?.CardId is { } id && _state.Cards.TryGetValue(id, out var source) ? source : null;

    /// <summary>The choke points' one line: when the card is immune, logs the negation and says so (the effect must not happen).</summary>
    private bool ImmuneNegates(CardInstance card, ImmunityKind kind, int? sourcePlayerId, string what)
    {
        if (!IsImmune(card, kind, sourcePlayerId, EffectSourceCard())) return false;
        Emit("immunityNegated", $"{Definition(card).Name}'s {what} was negated: it is immune.", card.OwnerId, card.Id);
        return true;
    }

    // ---- the kinds that are not one card's ---------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"Other players' cards do not affect this player's provinces' strengths" for a change an effect of `sourcePlayerId` would make now.</summary>
    private bool ProvinceStrengthImmune(int provinceOwnerId, int? sourcePlayerId) => IsImmune(null, ImmunityKind.ProvinceStrength, sourcePlayerId, EffectSourceCard(), playerId: provinceOwnerId);

    /// <summary>The player's provinces ignore every contribution from another player's card in play (read side of ProvinceStrength).</summary>
    private bool ProvinceIgnoresOthersCards(int provinceOwnerId) =>
        IsImmune(null, ImmunityKind.ProvinceStrength, 1 - provinceOwnerId, null, playerId: provinceOwnerId);

    /// <summary>Movement by an effect from or to a battlefield with a MovementAtBattlefield immunity is negated. `from` is where the card stands, `to` where it would go; either may be null.</summary>
    private bool BattlefieldMovementNegated(CardInstance card, int? from, int? to)
    {
        if (EffectSourcePlayer() is not int source) return false;
        foreach (var battlefield in new[] { from, to }.OfType<int>().Distinct())
        {
            if (!IsImmune(null, ImmunityKind.MovementAtBattlefield, source, EffectSourceCard(), battlefield: battlefield, playerId: card.OwnerId)) continue;
            Emit("moveNegated", $"{Definition(card).Name}'s movement {(battlefield == from ? "from" : "to")} {BattlefieldLabel(battlefield)} was negated.", card.OwnerId, card.Id);
            return true;
        }
        return false;
    }

    /// <summary>The player whose card set an action lock (the card that is its source), or null when unknown (never ignored by a Prevention immunity).</summary>
    private int? LockSourcePlayer(string? sourceId) => sourceId is not null && _state.Cards.TryGetValue(sourceId, out var source) ? source.OwnerId : null;

    /// <summary>The player whose card's effect applies a modifier: the running action's, else the owner of the source card the modifier names (null for a label with no card).</summary>
    private int? ModifierSourcePlayer(string source) => EffectSourcePlayer() ?? (_state.Cards.TryGetValue(source, out var card) ? card.OwnerId : null);

    /// <summary>A province's strength change an effect would make: negated (logged) when the province's owner is immune to that source.</summary>
    private bool ProvinceStrengthChangeNegated(int provinceOwnerId, int provinceIndex, string source)
    {
        if (!ProvinceStrengthImmune(provinceOwnerId, EffectSourcePlayer())) return false;
        Emit("provinceStrengthNegated", $"{source}: {Player(provinceOwnerId).Name}'s province {provinceIndex + 1} is unaffected: it is immune to other players' cards.", provinceOwnerId);
        return true;
    }

    /// <summary>The battlefield a unit stands at, for BattlefieldMovementNegated.</summary>
    private static int? StandingBattlefield(CardInstance card) => card.Zone == Zone.Battlefield ? card.ProvinceIndex : null;

    // Test seams (L5R.Core.Tests has InternalsVisibleTo).
    internal bool IsImmuneForTest(string cardId, ImmunityKind kind, int? sourcePlayerId, string? sourceCardId = null) =>
        IsImmune(Card(cardId), kind, sourcePlayerId, sourceCardId is null ? null : Card(sourceCardId));
    internal bool ProvinceStrengthImmuneForTest(int owner, int? sourcePlayerId) => ProvinceStrengthImmune(owner, sourcePlayerId);
    internal void GiveProvinceStrengthForTest(int owner, int province, int amount, int? sourcePlayer, string label) =>
        AsEffect<bool>(sourcePlayer, null, () => { GiveProvinceStrengthThisTurn(owner, province, amount, label); return true; });
    internal int EffectiveProvinceStrengthForTest(int owner, int province) => EffectiveProvinceStrength(Player(owner).Provinces.Single(p => p.Index == province));
    private T AsEffect<T>(int? sourcePlayer, string? sourceCardId, Func<T> body)
    {
        var saved = _runningCommand;
        _runningCommand = sourcePlayer is int player ? new GameCommand("pass", player, _state.Version, CardId: sourceCardId) : null;
        try { return body(); } finally { _runningCommand = saved; }
    }
    internal bool MoveHomeByEffectForTest(string cardId, int? sourcePlayer)
    {
        var card = Card(cardId); var before = card.Zone;
        AsEffect<bool>(sourcePlayer, null, () => { MoveHome(card, false); return true; });
        return card.Zone != before;
    }
    internal bool MoveToBattlefieldByEffectForTest(string cardId, int province, int? sourcePlayer) => AsEffect(sourcePlayer, null, () => TryMoveToBattlefield(Card(cardId), province));
    internal bool IsDefendingUnitForTest(string cardId) => IsDefendingUnit(Card(cardId));
    internal bool ActionLockedForTest(string cardId) => ActionLocked(Card(cardId));
    internal bool MovementLockedForTest(string cardId, MovementKinds kind) => MovementLocked(Card(cardId), kind);
    internal bool MovementLockNegatesForTest(string cardId, MovementKinds kind) => MovementLockNegates(Card(cardId), kind);
    internal bool DiscardTerrainByEffectForTest(string terrainId, int? sourcePlayer, string? sourceCardId)
    {
        var terrain = Card(terrainId);
        AsEffect<bool>(sourcePlayer, sourceCardId, () => { DiscardTerrain(terrain); return true; });
        return terrain.Zone != Zone.Battlefield;
    }
    internal CardInstance[] ResolutionDestroyableForTest(CardInstance[] army, bool defending) => TfResolutionDestroyable(army, defending);
    internal int DestroyByEffectForTest(string[] cardIds, int? sourcePlayer) => DestroyUnits(cardIds.Select(Card).ToArray(), false, sourcePlayer);
    internal bool BowByEffectForTest(string cardId, int sourcePlayer) => !StalwartNegatesBow(Card(cardId), sourcePlayer);
}
