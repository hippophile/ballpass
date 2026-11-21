using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Ballpass.Web.Features.Share;

public sealed record CardResult(byte[] Png, string Source, long RenderMs);

/// <summary>
/// Memory + disk cache for share cards, keyed by slug + content hash (so an edit re-renders, a repeat never does).
/// At most <see cref="MaxConcurrentRenders"/> Skia renders run at once; the rest wait.
/// Cache dir: config <c>Share:CacheDir</c>, default a folder under the OS temp path.
/// </summary>
public sealed partial class OgCardService(IOgCardRenderer renderer, IConfiguration config, ILogger<OgCardService> log)
{
    public const int MaxConcurrentRenders = 2;
    private const int MaxMemoryEntries = 400;

    private readonly SemaphoreSlim gate = new(MaxConcurrentRenders);
    private readonly ConcurrentDictionary<string, byte[]> memory = new();
    private readonly string dir = config["Share:CacheDir"] is { Length: > 0 } d ? d : Path.Combine(Path.GetTempPath(), "ballpass-og-cards");

    [GeneratedRegex("[^a-z0-9-]")] private static partial Regex Unsafe();

    public async Task<CardResult> GetAsync(OgCardModel model, CancellationToken ct = default)
    {
        var slug = Unsafe().Replace(model.Slug.ToLowerInvariant(), "_");
        var key = $"{slug}-{model.Hash}";
        if (memory.TryGetValue(key, out var hit)) return new CardResult(hit, "memory", 0);

        await gate.WaitAsync(ct);
        try
        {
            if (memory.TryGetValue(key, out hit)) return new CardResult(hit, "memory", 0);   // rendered while we waited

            var path = Path.Combine(dir, key + ".png");
            if (File.Exists(path))
            {
                var onDisk = await File.ReadAllBytesAsync(path, ct);
                Remember(key, onDisk);
                return new CardResult(onDisk, "disk", 0);
            }

            var sw = Stopwatch.StartNew();
            var png = await Task.Run(() => renderer.Render(model), ct);
            sw.Stop();
            log.LogInformation("Rendered OG card {Key}: {Bytes} bytes in {Ms} ms", key, png.Length, sw.ElapsedMilliseconds);

            Persist(slug, key, png);
            Remember(key, png);
            return new CardResult(png, "render", sw.ElapsedMilliseconds);
        }
        finally { gate.Release(); }
    }

    private void Remember(string key, byte[] png)
    {
        if (memory.Count >= MaxMemoryEntries) memory.Clear();
        memory[key] = png;
    }

    // Best effort: a read-only or full disk must never break the endpoint (memory cache still works).
    private void Persist(string slug, string key, byte[] png)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var tmp = Path.Combine(dir, key + "." + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllBytes(tmp, png);
            File.Move(tmp, Path.Combine(dir, key + ".png"), overwrite: true);
            foreach (var old in Directory.EnumerateFiles(dir, slug + "-*.png"))   // superseded versions of this athlete
                if (!old.EndsWith(key + ".png", StringComparison.Ordinal)
                    && Regex.IsMatch(Path.GetFileName(old), "^" + Regex.Escape(slug) + "-[0-9a-f]{16}\\.png$"))
                    File.Delete(old);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.LogWarning(e, "OG card disk cache write failed for {Key}", key);
        }
    }
}
