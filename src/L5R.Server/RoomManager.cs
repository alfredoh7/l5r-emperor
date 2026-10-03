using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using L5R.Core;

namespace L5R.Server;

public sealed class RoomManager(RoomStore store, ILogger<RoomManager> logger)
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly ConcurrentDictionary<string, Room> rooms = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim creationGate = new(1, 1);

    public static bool IsValidCode(string? code) => code is { Length: 8 }
        && code.All(CodeAlphabet.Contains);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        foreach (var record in await store.LoadAllAsync(cancellationToken))
        {
            if (record.Replay.ContentVersion != Protocol.ContentVersion)
            {
                // Saved under other content (e.g. before card ids became opaque, issue #611): leave the file on disk, do not load it,
                // and keep the server starting. There is no migration of saved match semantics.
                logger.LogWarning("Skipping room {RoomCode}: saved under incompatible content {SavedVersion}, this build is {Version}.", record.RoomCode, record.Replay.ContentVersion, Protocol.ContentVersion);
                continue;
            }
            if (record.Seats.Length is < 1 or > 2 || record.Seats[0].PlayerId != 0
                || (record.Seats.Length == 2 && record.Seats[1].PlayerId != 1)
                || record.Seats.Any(s => s.TokenHash.Length != 64 || !s.TokenHash.All(Uri.IsHexDigit)))
                throw new InvalidDataException($"Room {record.RoomCode} has invalid seat credentials.");
            if (!rooms.TryAdd(record.RoomCode, new Room(record, MatchEngine.Replay(record.Replay))))
                throw new InvalidDataException($"Duplicate room: {record.RoomCode}");
        }
        logger.LogInformation("Recovered {RoomCount} private rooms", rooms.Count);
    }

    public async Task<SeatReservation> CreateAsync(CancellationToken cancellationToken = default, string scenario = "starter")
    {
        if (scenario is not ("starter" or PublishedContent.Scenario)) throw new RoomException("unsupported_scenario", "Choose training or the published Lion/Crane profile.");
        await creationGate.WaitAsync(cancellationToken);
        try
        {
            string code;
            do { code = string.Concat(Enumerable.Range(0, 8).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)])); }
            while (rooms.ContainsKey(code));
            var token = NewToken();
            var seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));
            var engine = scenario == PublishedContent.Scenario ? MatchEngine.CreatePublishedMatch(seed) : MatchEngine.CreateStarterMatch(seed);
            var record = new PersistedRoom(1, code, DateTimeOffset.UtcNow,
                [new SeatCredential(0, Hash(token))], engine.ExportReplay(), []);
            await store.SaveAsync(record, cancellationToken);
            rooms[code] = new Room(record, engine);
            return new SeatReservation(code, 0, token);
        }
        finally { creationGate.Release(); }
    }

    public async Task<SeatReservation> JoinAsync(string code, CancellationToken cancellationToken = default)
    {
        var room = GetRoom(code);
        await room.Gate.WaitAsync(cancellationToken);
        try
        {
            if (room.Record.Seats.Length == 2) throw new RoomException("room_full", "Both player seats are reserved.", 409);
            var token = NewToken();
            var updated = room.Record with { Seats = [.. room.Record.Seats, new SeatCredential(1, Hash(token))] };
            await store.SaveAsync(updated, cancellationToken);
            room.Record = updated;
            return new SeatReservation(code, 1, token);
        }
        finally { room.Gate.Release(); }
    }

    public AuthorizedSeat Authorize(string code, string? token)
    {
        var room = GetRoom(code);
        if (string.IsNullOrEmpty(token) || token.Length > 128)
            throw new RoomException("unauthorized", "A valid seat token is required.", 401);
        var candidate = Convert.FromHexString(Hash(token));
        var seat = room.Record.Seats.FirstOrDefault(s => CryptographicOperations.FixedTimeEquals(candidate, Convert.FromHexString(s.TokenHash)));
        if (seat is null) throw new RoomException("unauthorized", "A valid seat token is required.", 401);
        return new AuthorizedSeat(code, seat.PlayerId);
    }

    public async Task AttachAsync(AuthorizedSeat seat, ClientConnection connection, CancellationToken cancellationToken = default)
    {
        var room = GetRoom(seat.RoomCode);
        await room.Gate.WaitAsync(cancellationToken);
        try
        {
            if (room.Connections.Remove(seat.PlayerId, out var previous)) previous.Abort();
            room.Connections[seat.PlayerId] = connection;
            await BroadcastLockedAsync(room);
        }
        finally { room.Gate.Release(); }
    }

    // Cleanup, not the request the connection was serving: called from a `finally` once the socket is closing, so
    // it must run to completion even when the caller's own token (context.RequestAborted) is already canceled --
    // callers should pass CancellationToken.None, never the token that triggered the detach.
    public async Task DetachAsync(AuthorizedSeat seat, ClientConnection connection, CancellationToken cancellationToken = default)
    {
        var room = GetRoom(seat.RoomCode);
        await room.Gate.WaitAsync(cancellationToken);
        try
        {
            if (room.Connections.GetValueOrDefault(seat.PlayerId) == connection)
            {
                room.Connections.Remove(seat.PlayerId);
                await BroadcastLockedAsync(room);
            }
        }
        finally { room.Gate.Release(); }
    }

    public async Task SubmitAsync(AuthorizedSeat seat, ClientConnection connection, CommandEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var room = GetRoom(seat.RoomCode);
        await room.Gate.WaitAsync(cancellationToken);
        try
        {
            var version = room.Engine.Version;
            if (room.Connections.GetValueOrDefault(seat.PlayerId) != connection)
                throw new RoomException("session_replaced", "This seat has reconnected in another session.", 409);
            if (envelope.Type != "command" || !Guid.TryParseExact(envelope.CommandId, "D", out _)
                || envelope.Command is null || string.IsNullOrWhiteSpace(envelope.Command.Type))
            {
                logger.LogDebug("Rejected command from player {PlayerId} in room {RoomCode}: invalid_command", seat.PlayerId, seat.RoomCode);
                await RejectAsync(connection, envelope.CommandId, version, "invalid_command", "A command requires a UUID commandId and a command type.");
                return;
            }

            var fingerprint = Hash(JsonSerializer.Serialize(new { envelope.ExpectedVersion, envelope.Command }, Protocol.Json));
            var previous = room.Record.Receipts.FirstOrDefault(r => r.PlayerId == seat.PlayerId && r.CommandId == envelope.CommandId);
            if (previous is not null)
            {
                if (previous.PayloadHash != fingerprint)
                {
                    logger.LogDebug("Rejected command from player {PlayerId} in room {RoomCode}: command_id_reused", seat.PlayerId, seat.RoomCode);
                    await RejectAsync(connection, envelope.CommandId, version, "command_id_reused", "This commandId was already accepted with a different payload.");
                }
                else
                {
                    await connection.SendAsync(new CommandReply("commandResult", envelope.CommandId, true, true, previous.Version));
                    await connection.SendAsync(CreateState(room, seat.PlayerId));
                }
                return;
            }

            if (envelope.ExpectedVersion != version)
            {
                logger.LogDebug("Rejected command from player {PlayerId} in room {RoomCode}: stale_version", seat.PlayerId, seat.RoomCode);
                await RejectAsync(connection, envelope.CommandId, version, "stale_version", "The match changed; use the latest state before acting.");
                await connection.SendAsync(CreateState(room, seat.PlayerId));
                return;
            }
            if (room.Record.Seats.Length != 2)
            {
                logger.LogDebug("Rejected command from player {PlayerId} in room {RoomCode}: waiting_for_opponent", seat.PlayerId, seat.RoomCode);
                await RejectAsync(connection, envelope.CommandId, version, "waiting_for_opponent", "The second player must join before play begins.");
                return;
            }

            // Apply to an isolated clone so persistence failure cannot leave a half-committed room. Execute()
            // already validates atomically against a detached copy of _state; Clone() (issue #550) gets the
            // same isolation directly from the live engine instead of replaying every prior command from turn one.
            var trial = room.Engine.Clone();
            var result = trial.Execute(envelope.Command.BindToSeat(seat.PlayerId, envelope.ExpectedVersion));
            if (!result.Accepted)
            {
                logger.LogDebug("Rejected command from player {PlayerId} in room {RoomCode}: illegal_action: {Reason}", seat.PlayerId, seat.RoomCode, result.Error);
                await RejectAsync(connection, envelope.CommandId, version, "illegal_action", result.Error ?? "The action is not legal.");
                return;
            }

            var receipt = new CommandReceipt(seat.PlayerId, envelope.CommandId, fingerprint, result.Version);
            var updated = room.Record with
            {
                Replay = trial.ExportReplay(),
                Receipts = room.Record.Receipts.Append(receipt).TakeLast(1024).ToArray()
            };
            try { await store.SaveAsync(updated, cancellationToken); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogError(ex, "Could not persist command for room {RoomCode}; state was not committed", seat.RoomCode);
                await RejectAsync(connection, envelope.CommandId, version, "storage_unavailable", "The server could not save the action. The match is unchanged; retry later.");
                return;
            }
            room.Record = updated;
            room.Engine = trial;
            await connection.SendAsync(new CommandReply("commandResult", envelope.CommandId, true, false, result.Version));
            await BroadcastLockedAsync(room);
        }
        finally { room.Gate.Release(); }
    }

    private static Task RejectAsync(ClientConnection connection, string commandId, long version, string code, string message)
        => connection.SendAsync(new CommandReply("commandResult", commandId, false, false, version, new ServerError(code, message)));

    private static StateMessage CreateState(Room room, int playerId)
    {
        var view = room.Engine.GetView(playerId);
        return new("state", room.Record.RoomCode, playerId, view.Version, view,
            room.Engine.QueryActions(playerId).Select(AvailableAction.FromCore).ToArray(),
            [room.Connections.ContainsKey(0), room.Connections.ContainsKey(1)]);
    }

    private static Task BroadcastLockedAsync(Room room) => Task.WhenAll(room.Connections.Select(pair => pair.Value.SendAsync(CreateState(room, pair.Key))));

    private Room GetRoom(string code)
    {
        code = code.ToUpperInvariant();
        if (!IsValidCode(code) || !rooms.TryGetValue(code, out var room))
            throw new RoomException("room_not_found", "This room does not exist.", 404);
        return room;
    }

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public sealed record AuthorizedSeat(string RoomCode, int PlayerId);

    private sealed class Room(PersistedRoom record, MatchEngine engine)
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public PersistedRoom Record { get; set; } = record;
        public MatchEngine Engine { get; set; } = engine;
        public Dictionary<int, ClientConnection> Connections { get; } = [];
    }
}
