using Godot;
using System.Net.Http;
using System.Security.Cryptography;

namespace L5R.Client;

/// <summary>Bounded, verified on-demand scan cache. Only receipt-pinned Oracle URLs are fetched.</summary>
public sealed class CatalogueScans
{
    private static readonly System.Net.Http.HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(25) };
    private readonly SemaphoreSlim _slots = new(3);
    private readonly Dictionary<string, Texture2D> _textures = [];
    private readonly LinkedList<string> _lru = [];
    private readonly HashSet<string> _pending = [], _failed = [];
    private readonly CancellationTokenSource _life = new();
    public event Action? Changed;
    public bool Offline { get; set; }
    public string CacheDirectory { get; } = ProjectSettings.GlobalizePath("user://catalogue-scans");
    public int TextureCount => _textures.Count;
    public string State(CatalogueData.Printing? p) => p is null ? "No scan indexed" : _failed.Contains(p.Id) ? "Scan unavailable · inspect for details" : _pending.Contains(p.Id) ? "Loading scan…" : "Scan not cached";
    public Texture2D? Get(CatalogueData.Printing? p)
    {
        if (p is null) return null;
        if (_textures.TryGetValue(p.Id, out var found)) { _lru.Remove(p.Id); _lru.AddLast(p.Id); return found; }
        if (!string.IsNullOrEmpty(p.ResourcePath) && ResourceLoader.Exists(p.ResourcePath))
        { found = GD.Load<Texture2D>(p.ResourcePath); Remember(p.Id, found); return found; }
        if (_pending.Count < 36 && !_pending.Contains(p.Id) && !_failed.Contains(p.Id) && !_life.IsCancellationRequested) { _pending.Add(p.Id); _ = Load(p); }
        return null;
    }
    private async Task Load(CatalogueData.Printing p)
    {
        var entered = false;
        try
        {
            await _slots.WaitAsync(_life.Token); entered = true;
            if (!Uri.TryCreate(p.SourceUrl, UriKind.Absolute, out var url) || url.Scheme != "https" || url.Host != "images.oracleofthevoid.com" || !url.AbsolutePath.StartsWith("/l5r/", StringComparison.Ordinal)
                || p.Sha256.Length != 64 || p.Sha256.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid scan receipt.");
            Directory.CreateDirectory(CacheDirectory);
            var path = Path.Combine(CacheDirectory, p.Sha256.ToLowerInvariant()+".jpg");
            byte[]? bytes = File.Exists(path) ? await File.ReadAllBytesAsync(path, _life.Token) : null;
            bool Valid(byte[] b) => b.Length <= 8 * 1024 * 1024 && Convert.ToHexString(SHA256.HashData(b)).Equals(p.Sha256, StringComparison.OrdinalIgnoreCase);
            if (bytes is not null && !Valid(bytes)) bytes = null;
            if (bytes is null)
            {
                if (Offline) throw new IOException("Offline scan not cached.");
                using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _life.Token);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > 8 * 1024 * 1024) throw new InvalidDataException("Scan exceeds size limit.");
                using var stream = await response.Content.ReadAsStreamAsync(_life.Token);
                using var buffer = new MemoryStream(); var chunk = new byte[65536]; int count;
                while ((count = await stream.ReadAsync(chunk, _life.Token)) > 0) { buffer.Write(chunk, 0, count); if (buffer.Length > 8 * 1024 * 1024) throw new InvalidDataException("Scan exceeds size limit."); }
                bytes = buffer.ToArray();
                if (!Valid(bytes)) throw new InvalidDataException("Scan differs from recorded printing.");
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { await File.WriteAllBytesAsync(temporary, bytes, _life.Token); File.Move(temporary, path, true); }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            if (_life.IsCancellationRequested) return;
            using var image = new Image();
            if (image.LoadJpgFromBuffer(bytes) != Error.Ok || image.GetWidth() > 4096 || image.GetHeight() > 4096) throw new InvalidDataException("Invalid scan image.");
            image.GenerateMipmaps();
            Remember(p.Id, ImageTexture.CreateFromImage(image));
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
