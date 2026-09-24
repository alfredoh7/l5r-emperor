using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using L5R.Core;

namespace L5R.Client;

public sealed record SessionSnapshot(MatchView View, ActionOption[] Actions, string Mode, string? RoomCode = null,
    bool[]? PlayersConnected = null);
public sealed record RoomSeat(string RoomCode, int PlayerId, string SeatToken, int ProtocolVersion, string ContentVersion);

/// <summary>The presentation submits intentions here. It never changes engine state.</summary>
public sealed class MatchSession : IDisposable
{
    public event Action? Changed;
    public event Action<string>? Notice;
    public event Action<RoomSeat, string>? SeatCreated;
    public SessionSnapshot? Snapshot { get; private set; }
    // Local lifecycle identity, never serialized or sent as a rules command.
    public long Generation { get; private set; }
    public bool Online => _socket is not null;
    public bool Connected => _socket?.State == WebSocketState.Open;
    public bool Busy { get; private set; }
    public RoomSeat? Seat { get; private set; }
    private MatchEngine? _engine;
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _connection;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private int _localSeat, _practiceNameSeat;
    public string LocalDisplayName { get; set; } = "";
    private MatchView NamedView(MatchView view) => string.IsNullOrWhiteSpace(LocalDisplayName) ? view : view with
    { Players = view.Players.Select(p => p.Id == (Online ? view.ViewerId : _practiceNameSeat) ? p with { Name = LocalDisplayName } : p).ToArray() };

    public void StartPractice(bool economy, bool published = false, int seat = 0)
    {
        Generation++;Disconnect();
        // A fixed seed shuffled every deck identically, game after game — Practice looked unshuffled
        // even though the shuffle itself was correct. Match the server's per-match randomness (RoomManager.cs).
        var seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));
        _engine = published ? MatchEngine.CreatePublishedMatch(seed) : economy ? MatchEngine.CreateEconomyScenario(seed) : MatchEngine.CreateStarterMatch(seed);
        _localSeat = published ? seat : _engine.State.ActivePlayerId;
        _practiceNameSeat = _localSeat;
        RefreshLocal();
    }

    /// <summary>Demo hosts own their own MatchSession; this never replaces the main practice session.</summary>
    public void StartDuelDemo(string scenario, ulong seed, int seat = 0)
    {
        if (seat is <0 or >1) throw new ArgumentOutOfRangeException(nameof(seat));
        Generation++;Disconnect();
        _engine = MatchEngine.CreateDuelDemo(scenario, seed);
        _localSeat = _practiceNameSeat = seat;
        RefreshLocal();
    }

    public Task BeginDuelDemo()
    {
        if (_engine is null || !_engine.GetView(0).Scenario.StartsWith("duel-demo/", StringComparison.Ordinal) || _engine.GetView(0).LastDuel is not null)
            return Task.CompletedTask;
        var scenario = DuelDemoScenarios.Get(_engine.GetView(0).Scenario[10..]);
        var command = _engine.QueryActions(0).FirstOrDefault(a => a.Enabled && a.Command?.AbilityId == scenario.ChallengeAbility)?.Command;
        if (command is null) { Notice?.Invoke("The prepared challenge is unavailable."); return Task.CompletedTask; }
        return Submit(command);
    }

    public void LeavePrivateTable()
    {
        if (!Online) return;
        Generation++;Disconnect(); Seat = null; Snapshot = null; _engine = null; Changed?.Invoke();
    }

    /// <summary>Scripted UI verification only (issue #303): a real engine state built to put one kind of Gold cost
    /// on screen. Reached only through the --capture-mode=gold-* harness, never from a menu or a room.</summary>
    public void StartShowcase(string kind)
    {
        Disconnect();
        _engine = MatchEngine.CreateGoldPaymentShowcase(kind);
        _localSeat = 0; _practiceNameSeat = 0;
        RefreshLocal();
    }

    public void SwitchPracticeSeat()
    {
        if (_engine is null || Online) return;
        _localSeat = 1 - _localSeat;
        RefreshLocal();
    }

    public void StartCustomPractice(Deck yourDeck, Deck opponentDeck)
    {
        // Construct and validate before disconnecting/replacing a running session.
        var seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));
        var engine = MatchEngine.CreateCustomMatch(seed, yourDeck, opponentDeck);
        Generation++;Disconnect(); _engine = engine; _localSeat = 0; _practiceNameSeat = 0; RefreshLocal();
    }

    public void FollowPracticePriority()
    {
        if (_engine is null || Online) return;
        _localSeat = _engine.State.PriorityPlayerId;
        RefreshLocal();
    }

    private void RefreshLocal()
    {
        if (_engine is null) return;
        Snapshot = new(NamedView(_engine.GetView(_localSeat)), _engine.QueryActions(_localSeat), _engine.GetView(_localSeat).Scenario.StartsWith("duel-demo/", StringComparison.Ordinal) ? "Duel Demo" : "Practice");
        Changed?.Invoke();
    }

    public string? ExportPracticeReplay() => _engine is null ? null : JsonSerializer.Serialize(_engine.ExportReplay(), new JsonSerializerOptions(Json) { WriteIndented = true });

    public bool StepPracticeOpponent(Func<MatchView, ActionOption[], GameCommand?> choose)
    {
        if (_engine is null || Online || _engine.State.WinnerId is not null || _engine.State.PriorityPlayerId == _localSeat) return false;
        var opponent = 1 - _localSeat;
        var command = choose(_engine.GetView(opponent), _engine.QueryActions(opponent));
        if (command is null) return false;
        var result = _engine.Execute(command);
        if (!result.Accepted) Notice?.Invoke(result.Error ?? "The practice opponent could not act.");
        RefreshLocal();
        return result.Accepted;
    }

    /// <summary>Against a real opponent, a reaction window always waits for an explicit pass — bluffing is
    /// part of the game, and silently skipping it would leak whether the passer held anything. Solo against
    /// the practice bot there is no opponent to bluff, so silently pass on the player's behalf whenever
    /// declining is their only legal option (a real reaction, e.g. an actual Proclaim decision, still prompts).</summary>
    public bool StepPracticeReaction()
    {
        if (_engine is null || Online || _engine.State.WinnerId is not null) return false;
        if (_engine.State.PendingChoice is null || _engine.State.PriorityPlayerId != _localSeat) return false;
        var enabled = _engine.QueryActions(_localSeat).Where(a => a.Enabled).ToArray();
        if (enabled is not [{ Type: "declineReaction" } only]) return false;
        var result = _engine.Execute(only.Command!);
        RefreshLocal();
        return result.Accepted;
    }

    /// <summary>The outgoing command payload: every GameCommand field except the acting seat and the expected
    /// version (the server binds both from the authenticated connection). A field missing here is silently
    /// dropped online, so the transport tests compare this against GameCommand by reflection (issue #303). Deck search
    /// (issue #312) adds no field: its commands ride Choice (a deck or a card definition id), Province, SourceIds and
    /// TargetId. The search's view fields (MatchView.PendingSearch, MatchView.ShownCards) travel inside MatchView itself,
    /// which this session deserializes as the core record, so they need no mapping here; the transport test
    /// SearchViewsCrossTheWire fails if a MatchView property is ever missing from the wire.</summary>
    public static object ToWire(GameCommand command) => new
    {
        type = command.Type, cardId = command.CardId, sourceIds = command.SourceIds,
        province = command.Province, clanDiscount = command.ClanDiscount, clanAllegiance = command.ClanAllegiance,
        assignments = command.Assignments, cardIds = command.CardIds, targetId = command.TargetId,
        abilityId = command.AbilityId, performerId = command.PerformerId, choice = command.Choice,
        deckPosition = command.DeckPosition, targetPlayerId = command.TargetPlayerId, goldAmount = command.GoldAmount, costOption = command.CostOption, selections = command.Selections, createdCard = command.CreatedCard, paymentDiscards = command.PaymentDiscards
    };

    public async Task Submit(GameCommand command)
    {
        if (Busy) return;
        if (_engine is not null && !Online)
        {
            var result = _engine.Execute(command);
            if (!result.Accepted) Notice?.Invoke(result.Error ?? "That action is unavailable.");
            RefreshLocal();
            return;
        }
        if (!Connected || Snapshot is null)
        {
            Notice?.Invoke("Connection lost. Reconnect to continue this match.");
            return;
        }
        Busy = true;
        Changed?.Invoke();
        var commandSocket = _socket;
        try
        {
            await Send(new { type = "command", commandId = Guid.NewGuid().ToString(), expectedVersion = command.ExpectedVersion, command = ToWire(command) });
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or InvalidOperationException)
        {
            if (ReferenceEquals(_socket, commandSocket))
            {
                Busy = false;
                Notice?.Invoke("Could not send the action. Reconnect before continuing.");
                Changed?.Invoke();
            }
        }
    }

    public async Task CreateOrJoin(string baseUrl, string? roomCode, bool published = false)
    {
        var uri = ValidateServer(baseUrl);
        using var client = new HttpClient { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(12) };
        var path = string.IsNullOrWhiteSpace(roomCode) ? (published ? "rooms?scenario=" + PublishedContent.Scenario : "rooms") : $"rooms/{Uri.EscapeDataString(roomCode.Trim().ToUpperInvariant())}/join";
        using var response = await client.PostAsync(path, null);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Could not enter the room. Check the server address and room code.");
        var seat = await response.Content.ReadFromJsonAsync<RoomSeat>(Json) ?? throw new InvalidOperationException("The server sent an invalid room response.");
        SeatCreated?.Invoke(seat, uri.ToString());
        await Connect(uri, seat);
    }

    public Task Resume(string baseUrl, RoomSeat seat) => Connect(ValidateServer(baseUrl), seat);

    private static Uri ValidateServer(string value)
    {
        if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Enter an HTTPS server address, or http://localhost:5210 for local play.");
        if (uri.Scheme == "http" && !uri.IsLoopback)
            throw new InvalidOperationException("Remote servers must use HTTPS to protect your player seat.");
        return uri;
    }

    private async Task Connect(Uri server, RoomSeat seat)
    {
        if(_engine is not null||Seat?.RoomCode!=seat.RoomCode||Seat.PlayerId!=seat.PlayerId)Generation++;
        Disconnect();
        _engine = null;
        Snapshot = null;
        Seat = seat;
        var connection = new CancellationTokenSource();
        var token = connection.Token;
        var socket = new ClientWebSocket();
        _connection = connection;
        _socket = socket;
        socket.Options.SetRequestHeader("Authorization", "Bearer " + seat.SeatToken);
        var builder = new UriBuilder(new Uri(server, $"rooms/{Uri.EscapeDataString(seat.RoomCode)}/play")) { Scheme = server.Scheme == "https" ? "wss" : "ws" };
        Busy = true;
        Changed?.Invoke();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            await socket.ConnectAsync(builder.Uri, timeout.Token);
            token.ThrowIfCancellationRequested();
            await Send(new { type = "hello", protocolVersion = 1, contentVersion = MatchEngine.ContentVersion }, socket, token);
            _ = Receive(socket, token);
        }
        catch
        {
            if (ReferenceEquals(_socket, socket)) Disconnect();
            else { socket.Dispose(); connection.Dispose(); }
            throw;
        }
    }

    private Task Send(object message)
    {
        var socket = _socket;
        var connection = _connection;
        if (socket is null || connection is null) throw new InvalidOperationException("Not connected.");
        return Send(message, socket, connection.Token);
    }

    private async Task Send(object message, ClientWebSocket socket, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        await _sendLock.WaitAsync(token);
        try { await socket.SendAsync(bytes, WebSocketMessageType.Text, true, token); }
        finally { _sendLock.Release(); }
    }

    private async Task Receive(ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[16384];
        try
        {
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult part;
                do
                {
                    part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (part.MessageType == WebSocketMessageType.Close) throw new WebSocketException("Closed by server.");
                    message.Write(buffer, 0, part.Count);
                    if (message.Length > 4 * 1024 * 1024) throw new InvalidOperationException("Server snapshot exceeds the supported size.");
                } while (!part.EndOfMessage);
                if (token.IsCancellationRequested || !ReferenceEquals(_socket, socket)) return;
                using var doc = JsonDocument.Parse(message.ToArray());
                var body = doc.RootElement;
                switch (body.GetProperty("type").GetString())
                {
                    case "state":
                        var view = body.GetProperty("view").Deserialize<MatchView>(Json)!;
                        var actions = body.GetProperty("actions").Deserialize<ActionOption[]>(Json)!;
                        // Network action templates deliberately omit actor/version. Bind from our authenticated view.
                        actions = actions.Select(a => a with { Command = a.Command is null ? null : a.Command with { PlayerId = view.ViewerId, ExpectedVersion = view.Version } }).ToArray();
                        var playersConnected = body.TryGetProperty("playersConnected", out var presence)
                            ? presence.Deserialize<bool[]>(Json) : null;
                        Snapshot = new(NamedView(view), actions, "Private room", body.GetProperty("roomCode").GetString(), playersConnected);
                        Busy = false;
                        Changed?.Invoke();
                        break;
                    case "result":
                    case "commandResult":
                    case "ack":
                        // An accepted acknowledgment precedes its state; keep controls locked until that state arrives.
                        Busy = body.TryGetProperty("accepted", out var accepted) && accepted.ValueKind == JsonValueKind.True;
                        if (body.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                            Notice?.Invoke(error.GetProperty("message").GetString() ?? "Action rejected.");
                        Changed?.Invoke();
                        break;
                    case "error":
                        Busy = false;
                        Notice?.Invoke(body.GetProperty("error").GetProperty("message").GetString() ?? "The server rejected the request.");
                        Changed?.Invoke();
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is WebSocketException or JsonException or InvalidOperationException)
        {
            if (!token.IsCancellationRequested && ReferenceEquals(_socket, socket)) Notice?.Invoke("Connection interrupted. Your seat is saved; use Reconnect to resume.");
        }
        finally
        {
            if (!token.IsCancellationRequested && ReferenceEquals(_socket, socket))
            {
                Busy = false;
                Changed?.Invoke();
            }
        }
    }

    public void Disconnect()
    {
        _connection?.Cancel();
        _socket?.Dispose();
        _connection?.Dispose();
        _socket = null;
        _connection = null;
        Busy = false;
    }

    // Pending asynchronous sends may still release this managed semaphore after Disconnect.
    public void Dispose() => Disconnect();
}
