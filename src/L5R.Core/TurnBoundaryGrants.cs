using System.Text.Json.Serialization;

namespace L5R.Core;

// ======================================================================================================================================================================================
// P4 (issue #690): PLAYER-LEVEL GRANTED ABILITIES -- "you have the ability ...", "each player has the ability ...", "his controller has the ability ...", for a duration.
//
// The ability is an ordinary catalogue ability flagged PlayerAbility (no source card; its uses are the player's), whose id is also registered on EngineHooks.PlayerGrantAbilities. A
// registered id is DORMANT -- not offered, and refused if submitted -- for a player who holds no live grant of it; with a grant it is offered through the normal action list
// (AbilityActions) to that player only, exactly as the keyword action kharmic-discard is. A grant lives in MatchState.PlayerAbilityGrants, keyed by player: it is never an ability of the
// Stronghold card, and it is removed per player when its TurnUntil passes (as that turn begins) or the card it depends on leaves play.
// ======================================================================================================================================================================================

/// <summary>One player's grant of a player ability. <see cref="Source"/> names the granting card (an id or a title); <see cref="WhileInPlay"/>, when set, is a card id the grant ends with.</summary>
public sealed class PlayerAbilityGrant
{
    public int PlayerId { get; set; }
    public string AbilityId { get; set; } = "";
    public string Source { get; set; } = "";
    public TurnUntil Until { get; set; } = TurnUntil.GameEnds();
    public string? WhileInPlay { get; set; }
}

public sealed partial class MatchState
{
    [JsonPropertyOrder(22101)]
    public List<PlayerAbilityGrant> PlayerAbilityGrants { get; set; } = [];
}

internal static partial class EngineHooks
{
    /// <summary>The ids of the player abilities a player has only while granted (each callback returns its feature's ids).</summary>
    internal static readonly OrderedHooks<Func<IEnumerable<string>>> PlayerGrantAbilities = new();
}

public sealed partial class MatchEngine
{
    private static HashSet<string>? _playerGrantAbilityIds;
    private static HashSet<string> PlayerGrantAbilityIds() =>
        _playerGrantAbilityIds ??= new HashSet<string>(EngineHooks.PlayerGrantAbilities.Items.SelectMany(ids => ids()), StringComparer.Ordinal);

    private bool PlayerGrantLive(PlayerAbilityGrant grant) =>
        !UntilPassed(grant.Until) && (grant.WhileInPlay is not { } cardId || _state.Cards.TryGetValue(cardId, out var card) && InPlay(card));

    /// <summary>Whether <paramref name="playerId"/> holds a live grant of the ability.</summary>
    internal bool PlayerAbilityGranted(int playerId, string abilityId) =>
        _state.PlayerAbilityGrants.Count > 0 && _state.PlayerAbilityGrants.Any(g => g.PlayerId == playerId && g.AbilityId == abilityId && PlayerGrantLive(g));

    // AbilityActions' and ValidateAbility's question: a grant-only player ability this player does not hold.
    private bool PlayerGrantDormant(string abilityId, int playerId) => PlayerGrantAbilityIds().Contains(abilityId) && !PlayerAbilityGranted(playerId, abilityId);

    private void ValidatePlayerGrant(string id, AbilityDefinition ability, PlayerState player)
    {
        if (ability.PlayerAbility) Require(!PlayerGrantDormant(id, player.Id), "You do not have that ability.");
    }

    /// <summary>
    /// "<paramref name="playerId"/> has the ability ..." for the duration. A second grant of the same ability from the same source replaces the first (its duration is the new one); grants
    /// from different sources stand side by side, and the ability is held while any of them lives. <paramref name="whileInPlay"/>: a card the grant ends with ("while this card remains in play").
    /// </summary>
    internal void GrantPlayerAbility(int playerId, string abilityId, TurnUntil until, string source, string? whileInPlay = null)
    {
        Require(AbilityCatalog.All.TryGetValue(abilityId, out var ability) && ability.PlayerAbility && PlayerGrantAbilityIds().Contains(abilityId),
            "Only a player ability registered as granted can be granted to a player.");
        _state.PlayerAbilityGrants.RemoveAll(g => g.PlayerId == playerId && g.AbilityId == abilityId && g.Source == source);
        _state.PlayerAbilityGrants.Add(new PlayerAbilityGrant { PlayerId = playerId, AbilityId = abilityId, Source = source, Until = until, WhileInPlay = whileInPlay });
        Emit("playerAbilityGranted", $"{Player(playerId).Name} has the ability \"{ability!.Timing}: {ability.Label}\" {DescribeUntil(until)}.", playerId, whileInPlay);
    }

    /// <summary>"Each player has the ability ...".</summary>
    internal void GrantEachPlayerAbility(string abilityId, TurnUntil until, string source, string? whileInPlay = null)
    {
        foreach (var player in _state.Players) GrantPlayerAbility(player.Id, abilityId, until, source, whileInPlay);
    }

    /// <summary>Ends the grants of the ability to a player (from one source, or from every source); true when any ended.</summary>
    internal bool RevokePlayerAbility(int playerId, string abilityId, string? source = null) =>
        _state.PlayerAbilityGrants.RemoveAll(g => g.PlayerId == playerId && g.AbilityId == abilityId && (source is null || g.Source == source)) > 0;

    // A turn has begun: the grants whose duration has passed, or whose card has left play, are removed.
    private void ExpirePlayerAbilityGrants()
    {
        if (_state.PlayerAbilityGrants.Count == 0) return;
        foreach (var grant in _state.PlayerAbilityGrants.Where(g => !PlayerGrantLive(g)).ToArray())
        {
            _state.PlayerAbilityGrants.Remove(grant);
            if (AbilityCatalog.All.TryGetValue(grant.AbilityId, out var ability))
                Emit("playerAbilityExpired", $"{Player(grant.PlayerId).Name} no longer has the ability \"{ability.Timing}: {ability.Label}\".", grant.PlayerId);
        }
    }
}
