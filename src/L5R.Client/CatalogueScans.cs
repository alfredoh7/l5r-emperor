using Godot;
using System.Security.Cryptography;
using System.Text.Json;

namespace L5R.Client;

/// <summary>Local WebP artwork only. The complete pack ships with the game; no runtime HTTP.</summary>
public sealed class CatalogueScans
{
    public const string PackPath = "res://Content/catalogue-art.zip";
    private sealed class Receipt
    {
        public string File { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public int Bytes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
    private sealed class Manifest
    {
        public int SchemaVersion { get; set; }
        public int Quality { get; set; }
        public Dictionary<string, Receipt> Scans { get; set; } = [];
        public Dictionary<string, string> Resources { get; set; } = [];
    }
    private readonly SemaphoreSlim _slots = new(3);
    private readonly Dictionary<string, Texture2D> _textures = [];
    private readonly LinkedList<string> _lru = [];
    private readonly HashSet<string> _pending = [], _failed = [];
    private readonly CancellationTokenSource _life = new();
    private readonly Manifest _manifest;
    private readonly string _packPath;
    private readonly string _legacyCacheDirectory;
    public event Action? Changed;
    public int TextureCount => _textures.Count;
    public int LoadedFromPackCount { get; private set; }
    public bool PackAvailable => Godot.FileAccess.FileExists(_packPath);

    public CatalogueScans(string packPath = PackPath, string? legacyCacheDirectory = null)
    {
        _packPath = packPath;
        _legacyCacheDirectory = legacyCacheDirectory ?? ProjectSettings.GlobalizePath("user://catalogue-scans");
        using var file = Godot.FileAccess.Open("res://Content/catalogue-art.json", Godot.FileAccess.ModeFlags.Read);
        _manifest = file is null ? new() : JsonSerializer.Deserialize<Manifest>(file.GetAsText(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }) ?? new();
        if (_manifest.SchemaVersion != 1 || _manifest.Quality != 85) throw new InvalidOperationException("Unsupported catalogue artwork package.");
    }
    private Receipt? Artwork(CatalogueData.Printing p)
    {
        var key = string.IsNullOrEmpty(p.ResourcePath) ? p.Sha256 : _manifest.Resources.GetValueOrDefault(p.ResourcePath, "");
        return _manifest.Scans.GetValueOrDefault(key);
    }
    public bool HasArtwork(CatalogueData.Printing p) => Artwork(p) is not null;
    public string State(CatalogueData.Printing? p) => p is null ? "No scan indexed" : !PackAvailable ? "Artwork package missing · restart using the game launcher" : _failed.Contains(p.Id) ? "Scan unavailable · repair the artwork package" : _pending.Contains(p.Id) ? "Loading local artwork…" : "Artwork not loaded";
    public Texture2D? Get(CatalogueData.Printing? p)
    {
        if (p is null || _life.IsCancellationRequested) return null;
        if (_textures.TryGetValue(p.Id, out var found)) { _lru.Remove(p.Id); _lru.AddLast(p.Id); return found; }
        if (_pending.Count < 36 && !_pending.Contains(p.Id) && !_failed.Contains(p.Id)) { _pending.Add(p.Id); _ = Load(p); }
        return null;
    }
    private async Task Load(CatalogueData.Printing p)
    {
        var entered = false;
        try
        {
            var receipt = Artwork(p) ?? throw new InvalidDataException("No optimized printing receipt.");
            await _slots.WaitAsync(_life.Token); entered = true;
            // Decode off the UI thread. Each worker owns its ZIPReader and Image; only
            // the final GPU texture is created on Godot's synchronization context.
            using var decoded = await Task.Run(() =>
            {
                if (receipt.File.Contains('/') || receipt.File.Contains('\\') || !receipt.File.EndsWith(".webp", StringComparison.Ordinal) || receipt.Bytes is <= 0 or > 8 * 1024 * 1024)
                    throw new InvalidDataException("Invalid optimized printing receipt.");
                using var zip = new ZipReader();
                if (zip.Open(_packPath) != Error.Ok || !zip.FileExists(receipt.File)) throw new IOException("Artwork package missing or incomplete.");
                var bytes = zip.ReadFile(receipt.File);
                if (bytes.Length != receipt.Bytes || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(receipt.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Artwork differs from its optimized receipt.");
                var image = new Image();
                if (image.LoadWebpFromBuffer(bytes) != Error.Ok || image.GetWidth() != receipt.Width || image.GetHeight() != receipt.Height || receipt.Width > 4096 || receipt.Height > 4096)
                { image.Dispose(); throw new InvalidDataException("Invalid WebP artwork."); }
                image.GenerateMipmaps();
                // Retire only the exact old viewer cache entry once its WebP replacement
                // has been verified and decoded. Archival/published originals are untouched.
                var sourceHash = string.IsNullOrEmpty(p.ResourcePath) ? p.Sha256 : _manifest.Resources.GetValueOrDefault(p.ResourcePath, "");
                if (sourceHash.Length == 64 && sourceHash.All(Uri.IsHexDigit))
                {
                    var legacy = Path.Combine(_legacyCacheDirectory, sourceHash.ToLowerInvariant() + ".jpg");
                    try
                    {
                        if (File.Exists(legacy) && new FileInfo(legacy).Length <= 8 * 1024 * 1024 && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(legacy))).Equals(sourceHash, StringComparison.OrdinalIgnoreCase)) File.Delete(legacy);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                return image;
            }, _life.Token);
            if (_life.IsCancellationRequested) return;
            Remember(p.Id, ImageTexture.CreateFromImage(decoded));
            LoadedFromPackCount++;
        }
        catch (Exception e) when (e is not OutOfMemoryException) { if (!_life.IsCancellationRequested) _failed.Add(p.Id); }
        finally { if (entered) _slots.Release(); _pending.Remove(p.Id); if (!_life.IsCancellationRequested) Changed?.Invoke(); }
    }
    private void Remember(string id, Texture2D texture)
    {
        _textures[id] = texture; _lru.Remove(id); _lru.AddLast(id);
        while (_textures.Count > 80 && _lru.First is { } first) { _textures.Remove(first.Value); _lru.RemoveFirst(); }
    }
    public void Retry() { _failed.Clear(); Changed?.Invoke(); }
    public void Close() { _life.Cancel(); Changed = null; _textures.Clear(); _lru.Clear(); }
}
