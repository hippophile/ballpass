using Ballpass.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Features.Media;

/// <summary>An athlete with a highlight reel (stories bar, story page).</summary>
public record StoryRing(int AthleteId, string Slug, string NameEl, string NameLatin, string? ClubName, int Count);

public interface IMediaService
{
    Task<IReadOnlyList<MediaItem>> ForPostAsync(int postId, CancellationToken ct = default);
    Task<IReadOnlyList<MediaItem>> ReelAsync(int athleteId, CancellationToken ct = default);
    /// <summary>Athletes that have a reel, in a stable order.</summary>
    Task<IReadOnlyList<StoryRing>> StoriesAsync(int max, CancellationToken ct = default);
}

public class MediaService(BallpassDbContext db) : IMediaService
{
    public async Task<IReadOnlyList<MediaItem>> ForPostAsync(int postId, CancellationToken ct = default) =>
        await db.Set<MediaItem>().AsNoTracking().Where(m => m.PostId == postId).OrderBy(m => m.Position).ToListAsync(ct);

    public async Task<IReadOnlyList<MediaItem>> ReelAsync(int athleteId, CancellationToken ct = default) =>
        await db.Set<MediaItem>().AsNoTracking().Where(m => m.AthleteId == athleteId).OrderBy(m => m.Position).ToListAsync(ct);

    public async Task<IReadOnlyList<StoryRing>> StoriesAsync(int max, CancellationToken ct = default)
    {
        var rows = await db.Set<MediaItem>().AsNoTracking()
            .Where(m => m.AthleteId != null && m.Athlete!.IsDemo)
            .GroupBy(m => m.AthleteId!.Value)
            .Select(g => new { Id = g.Key, First = g.Min(m => m.Id), Count = g.Count() })
            .OrderBy(g => g.First).Take(max).ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToList();
        var athletes = await db.Athletes.AsNoTracking().Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.Slug, a.FirstName, a.LastName, a.FirstNameLatin, a.LastNameLatin, Club = a.CurrentClub != null ? a.CurrentClub.Name : null })
            .ToDictionaryAsync(a => a.Id, ct);
        return rows.Select(r => athletes[r.Id] is var a
            ? new StoryRing(a.Id, a.Slug, $"{a.FirstName} {a.LastName}", $"{a.FirstNameLatin} {a.LastNameLatin}", a.Club, r.Count) : null!).ToList();
    }
}
