using System.Text.Json;
using System.Text.Json.Serialization;
using L5R.Core;

namespace L5R.Server;

public static class Protocol
{
    public const int Version = 1;
    public const string ContentVersion = MatchEngine.ContentVersion;
    public const int MaximumMessageBytes = 32 * 1024;

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };
}

public sealed record SeatReservation(string RoomCode, int PlayerId, string SeatToken,
    int ProtocolVersion = Protocol.Version, string ContentVersion = Protocol.ContentVersion);

public sealed record ClientHello(string Type, int ProtocolVersion, string ContentVersion);

public sealed record CommandEnvelope(string Type, string CommandId, long ExpectedVersion, WireGameCommand Command);

// Actor and expected version deliberately do not exist inside the command payload.
// Every other GameCommand field must be listed here AND in MatchSession's outgoing mapping, or it is silently
// dropped online (DeckPosition and TargetPlayerId were, until issue #303 added GoldAmount and a test that
// compares this record against GameCommand by reflection).
public sealed record WireGameCommand(string Type, string? CardId = null, string[]? SourceIds = null,
    int? Province = null, bool ClanDiscount = false, bool ClanAllegiance = false,
    Dictionary<string, int>? Assignments = null, string[]? CardIds = null, string? TargetId = null, string? AbilityId = null, string? PerformerId = null, string? Choice = null,
    string? DeckPosition = null, int? TargetPlayerId = null, int? GoldAmount = null, string? CostOption = null, SlotSelection[]? Selections = null, string? CreatedCard = null)
{
    public GameCommand BindToSeat(int playerId, long expectedVersion) => new(
        Type, playerId, expectedVersion, CardId, SourceIds, Province,
        ClanDiscount, ClanAllegiance, Assignments, CardIds, TargetId, AbilityId, PerformerId, Choice,
        DeckPosition, TargetPlayerId, GoldAmount, CostOption, Selections, CreatedCard);

    public static WireGameCommand FromCore(GameCommand command) => new(command.Type,
        command.CardId, command.SourceIds, command.Province, command.ClanDiscount,
        command.ClanAllegiance, command.Assignments, command.CardIds, command.TargetId, command.AbilityId, command.PerformerId, command.Choice,
        command.DeckPosition, command.TargetPlayerId, command.GoldAmount, command.CostOption, command.Selections, command.CreatedCard);
}

// Gold/TargetGold (issue #303): the engine's Gold quotes for the option, so an online client renders the same
// payment panel from the server's authoritative numbers and never derives a cost.
public sealed record AvailableAction(string Type, string Label, bool Enabled, string? Reason,
    WireGameCommand? Command, string? CardId, string[]? TargetIds, int[]? Provinces, PurchaseQuote? Quote,
    GoldQuote? Gold = null, Dictionary<string, GoldQuote>? TargetGold = null, TargetSlotOption[]? Slots = null, bool NoTarget = false,
    AlternativeCostView? AlternativeCost = null, MoveDestinationView? Movement = null, AttackCueView? Attack = null)
{
    public static AvailableAction FromCore(ActionOption action) => new(action.Type, action.Label,
        action.Enabled, action.Reason, action.Command is null ? null : WireGameCommand.FromCore(action.Command),
        action.CardId, action.TargetIds, action.Provinces, action.Quote, action.Gold, action.TargetGold, action.Slots, action.NoTarget, action.AlternativeCost, action.Movement, action.Attack);
}

public sealed record ServerError(string Code, string Message);

public sealed record StateMessage(string Type, string RoomCode, int PlayerId, long Sequence,
    MatchView View, AvailableAction[] Actions, bool[] PlayersConnected,
    int ProtocolVersion = Protocol.Version, string ContentVersion = Protocol.ContentVersion);

public sealed record CommandReply(string Type, string CommandId, bool Accepted, bool Duplicate,
    long Sequence, ServerError? Error = null);

public sealed record ErrorMessage(string Type, ServerError Error,
    int ProtocolVersion = Protocol.Version, string ContentVersion = Protocol.ContentVersion);

public sealed record SeatCredential(int PlayerId, string TokenHash);
public sealed record CommandReceipt(int PlayerId, string CommandId, string PayloadHash, long Version);

// Entirely server-private: contains secret deck order/seed and token hashes.
// Only StateMessage, CommandReply and ErrorMessage may cross the wire.
public sealed record PersistedRoom(int StorageVersion, string RoomCode, DateTimeOffset CreatedAt,
    SeatCredential[] Seats, ReplayRecord Replay, CommandReceipt[] Receipts);

public sealed class RoomException(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
