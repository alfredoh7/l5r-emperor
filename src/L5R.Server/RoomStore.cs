using System.Text.Json;

namespace L5R.Server;

public sealed class RoomStore
{
    private readonly string directory;

    public RoomStore(string directory)
    {
        this.directory = Path.GetFullPath(directory);
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(this.directory);
        else Directory.CreateDirectory(this.directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    public async Task<PersistedRoom[]> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<PersistedRoom>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.room.json"))
        {
            await using var stream = File.OpenRead(path);
            var room = await JsonSerializer.DeserializeAsync<PersistedRoom>(stream, Protocol.Json, cancellationToken)
                ?? throw new InvalidDataException($"Empty room journal: {Path.GetFileName(path)}");
            if (room.StorageVersion != 1 || !RoomManager.IsValidCode(room.RoomCode)
                || Path.GetFileName(path) != room.RoomCode + ".room.json")
                throw new InvalidDataException($"Unsupported or invalid room journal: {Path.GetFileName(path)}");
            result.Add(room);
        }
        return result.ToArray();
    }

    public async Task SaveAsync(PersistedRoom room, CancellationToken cancellationToken = default)
    {
        if (!RoomManager.IsValidCode(room.RoomCode)) throw new InvalidDataException("Invalid room code.");
        var destination = Path.Combine(directory, room.RoomCode + ".room.json");
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough
            };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var stream = new FileStream(temporary, options))
            {
                await JsonSerializer.SerializeAsync(stream, room, Protocol.Json, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
