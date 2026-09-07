using System.Net.WebSockets;
using System.Text.Json;

namespace L5R.Server;

public sealed class ClientConnection(WebSocket socket, int playerId) : IDisposable
{
    private readonly SemaphoreSlim sendGate = new(1, 1);
    public int PlayerId { get; } = playerId;
    public WebSocket Socket { get; } = socket;

    public async Task SendAsync<T>(T message)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await sendGate.WaitAsync(timeout.Token);
            try
            {
                if (Socket.State != WebSocketState.Open) return;
                var bytes = JsonSerializer.SerializeToUtf8Bytes(message, Protocol.Json);
                await Socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
            }
            finally { sendGate.Release(); }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            Socket.Abort();
        }
    }

    public void Abort() => Socket.Abort();
    public void Dispose() => Socket.Dispose();

    public static async Task<byte[]?> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.MessageType != WebSocketMessageType.Text)
                throw new RoomException("text_required", "Only text JSON messages are supported.");
            if (stream.Length + result.Count > Protocol.MaximumMessageBytes)
                throw new RoomException("message_too_large", "The maximum message size is 32 KiB.");
            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) return stream.ToArray();
        }
    }
}
