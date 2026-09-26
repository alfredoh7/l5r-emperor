using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace L5R.Server;

public static class ServerApplication
{
    public static async Task<WebApplication> BuildAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // Startup configuration is immutable; avoid recursive file watchers over asset/tool folders.
            Args = [.. args, "--hostBuilder:reloadConfigOnChange=false"],
            ApplicationName = typeof(ServerApplication).Assembly.FullName
        });
        builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://127.0.0.1:5210");
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 4096);
        builder.Services.AddSingleton(new RoomStore(builder.Configuration["L5R:StoragePath"]
            ?? Path.Combine(builder.Environment.ContentRootPath, "data", "rooms")));
        builder.Services.AddSingleton<RoomManager>();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("room-writes", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });

        var app = builder.Build();
        await app.Services.GetRequiredService<RoomManager>().LoadAsync();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try { await next(context); }
            catch (RoomException exception) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = exception.StatusCode;
                await context.Response.WriteAsJsonAsync(new ErrorMessage("error",
                    new ServerError(exception.Code, exception.Message)), Protocol.Json);
            }
        });
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
        app.UseRateLimiter();

        app.MapGet("/health", () => Results.Json(new
        {
            status = "ok", protocolVersion = Protocol.Version, contentVersion = Protocol.ContentVersion
        }, Protocol.Json));
        app.MapPost("/rooms", async (RoomManager manager, string? scenario, CancellationToken cancellationToken)
            => Results.Json(await manager.CreateAsync(cancellationToken, scenario ?? "starter"), Protocol.Json, statusCode: 201))
            .RequireRateLimiting("room-writes");
        app.MapPost("/rooms/{code}/join", async (string code, RoomManager manager, CancellationToken cancellationToken)
            => Results.Json(await manager.JoinAsync(code, cancellationToken), Protocol.Json, statusCode: 201))
            .RequireRateLimiting("room-writes");
        app.MapGet("/rooms/{code}/play", HandleWebSocketAsync);
        return app;
    }

    private static async Task HandleWebSocketAsync(HttpContext context, string code, RoomManager manager,
        ILogger<RoomManager> logger)
    {
        if (!context.WebSockets.IsWebSocketRequest)
            throw new RoomException("websocket_required", "This endpoint requires a WebSocket upgrade.", 400);
        var authorization = context.Request.Headers.Authorization.ToString();
        var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim() : null;
        var seat = manager.Authorize(code, token);
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var connection = new ClientConnection(socket, seat.PlayerId);
        var attached = false;
        try
        {
            using (var helloTimeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted))
            {
                helloTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                var bytes = await ClientConnection.ReceiveAsync(socket, helloTimeout.Token);
                if (bytes is null) return;
                var hello = JsonSerializer.Deserialize<ClientHello>(bytes, Protocol.Json);
                if (hello is null || hello.Type != "hello" || hello.ProtocolVersion != Protocol.Version
                    || hello.ContentVersion != Protocol.ContentVersion)
                {
                    await connection.SendAsync(new ErrorMessage("error", new ServerError("version_mismatch",
                        "Send a hello with the server protocolVersion and contentVersion before playing.")));
                    return;
                }
            }

            await manager.AttachAsync(seat, connection, context.RequestAborted);
            attached = true;
            while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                var bytes = await ClientConnection.ReceiveAsync(socket, context.RequestAborted);
                if (bytes is null) break;
                CommandEnvelope? envelope;
                try { envelope = JsonSerializer.Deserialize<CommandEnvelope>(bytes, Protocol.Json); }
                catch (JsonException)
                {
                    await connection.SendAsync(new ErrorMessage("error", new ServerError("invalid_json",
                        "Expected a command envelope; unknown fields, including playerId, are rejected.")));
                    continue;
                }
                if (envelope is null)
                {
                    await connection.SendAsync(new ErrorMessage("error", new ServerError("invalid_json", "Expected a JSON object.")));
                    continue;
                }
                await manager.SubmitAsync(seat, connection, envelope, context.RequestAborted);
            }
        }
        catch (JsonException)
        {
            await connection.SendAsync(new ErrorMessage("error", new ServerError("invalid_hello", "A valid hello message is required.")));
        }
        catch (RoomException exception)
        {
            await connection.SendAsync(new ErrorMessage("error", new ServerError(exception.Code, exception.Message)));
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            logger.LogDebug("Player {PlayerId} disconnected from room {RoomCode}", seat.PlayerId, seat.RoomCode);
        }
        finally
        {
            // Cleanup must run even though context.RequestAborted is typically already canceled here.
            if (attached) await manager.DetachAsync(seat, connection, CancellationToken.None);
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Session ended", timeout.Token); }
                catch (Exception exception) when (exception is WebSocketException or OperationCanceledException) { }
            }
        }
    }
}
