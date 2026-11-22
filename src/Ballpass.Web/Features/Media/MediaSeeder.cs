using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Bogus;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Features.Media;

/// <summary>Pure function of (posts with ids, athletes with ids): one seeded Randomizer, no clock. Stock photos are decoration only
/// (the UI labels them); they are never matched to the named author.</summary>
public static class MediaSeeder
{
    private const int ReelAthletes = 40;

    public static IReadOnlyList<MediaItem> Generate(IReadOnlyList<Post> posts, IReadOnlyList<Athlete> athletes)
    {
        var r = new Randomizer(DemoDataSeeder.Seed + 202);
        var photos = MediaCatalog.All.Where(a => a.Kind == MediaKind.Photo).ToList();
        var videos = MediaCatalog.All.Where(a => a.Kind == MediaKind.Video).ToList();
        if (photos.Count == 0) return [];

        // A shuffled deck that refills when empty: repeated picks stay spread out and deterministic.
        Func<IReadOnlyList<MediaAsset>> Deck(IReadOnlyList<MediaAsset> pool)
        {
            var queue = new Queue<MediaAsset>();
            return () =>
            {
                if (queue.Count == 0) foreach (var a in r.Shuffle(pool)) queue.Enqueue(a);
                return [queue.Dequeue()];
            };
        }
        IReadOnlyList<MediaAsset> Themed(params string[] themes) => photos.Where(p => themes.Contains(p.Theme)).ToList();
        var tryoutDeck = Deck(Themed("court", "gym"));
        var carouselDeck = Deck(Themed("action", "net", "ball", "court"));
        var lftDeck = Deck(Themed("action", "ball", "shoes", "court"));
        var freeDeck = Deck(Themed("gym", "net", "shoes", "ball"));
        var videoDeck = Deck(videos);

        MediaItem Make(MediaAsset a, int position) => new()
        {
            File = a.File, Poster = a.Poster, Kind = a.Kind, Width = a.Width, Height = a.Height,
            AltEl = a.AltEl, AltEn = a.AltEn, IsGraphic = a.Graphic, Position = position, IsDemo = true,
        };

        var items = new List<MediaItem>();
        void ForPost(Post p, IEnumerable<MediaAsset> assets)
        {
            var i = 0;
            foreach (var a in assets) { var m = Make(a, i++); m.PostId = p.Id; items.Add(m); }
        }

        var ordered = posts.OrderBy(p => p.Id).ToList();
        var highlightNo = 0;
        foreach (var p in ordered.Where(p => p.Type == PostType.Highlight))
        {
            if (highlightNo++ % 2 == 0 && videos.Count > 0) ForPost(p, videoDeck());
            else
            {
                var n = r.Int(2, 4);
                var picked = new List<MediaAsset>();
                while (picked.Count < n) { var a = carouselDeck()[0]; if (!picked.Contains(a)) picked.Add(a); }
                ForPost(p, picked);
            }
        }
        foreach (var p in ordered.Where(p => p.Type == PostType.TryoutCall)) ForPost(p, tryoutDeck());

        // Looking-for-team and free-text posts: about 40 % get a photo (a few with a second one), so the overall share is about 60 %.
        var rest = ordered.Where(p => p.Type is PostType.LookingForTeam or PostType.FreeText).ToList();
        var chosen = r.Shuffle(rest).Take((int)Math.Round(rest.Count * 0.4)).Select(p => p.Id).ToHashSet();
        foreach (var p in rest.Where(p => chosen.Contains(p.Id)))
        {
            var deck = p.Type == PostType.FreeText ? freeDeck : lftDeck;
            var first = deck()[0];
            var second = r.Bool(0.25f) ? deck()[0] : null;
            ForPost(p, second is null || second == first ? [first] : [first, second]);
        }

        // Highlight reels: athletes who author posts first (so the stories bar and the feed agree), then the rest.
        var postAuthors = ordered.Where(p => p.AuthorKind == AuthorKind.Athlete).Select(p => p.AuthorId).ToHashSet();
        var reelAthletes = r.Shuffle(athletes.Where(a => postAuthors.Contains(a.Id)))
            .Concat(r.Shuffle(athletes.Where(a => !postAuthors.Contains(a.Id)))).Take(ReelAthletes).ToList();
        foreach (var a in reelAthletes)
        {
            var n = r.Int(1, 3);
            var assets = new List<MediaAsset>();
            if (videos.Count > 0 && r.Bool(0.35f)) assets.Add(videoDeck()[0]);
            while (assets.Count < n) { var x = carouselDeck()[0]; if (!assets.Contains(x)) assets.Add(x); }
            var i = 0;
            foreach (var asset in assets) { var m = Make(asset, i++); m.AthleteId = a.Id; items.Add(m); }
        }
        return items;
    }

    /// <summary>Replaces all demo media rows. Runs inside the caller's transaction, after posts and athletes have ids.</summary>
    public static async Task ReplaceAsync(BallpassDbContext db, CancellationToken ct = default)
    {
        await db.Set<MediaItem>().Where(m => m.IsDemo).ExecuteDeleteAsync(ct);
        var posts = await db.Set<Post>().AsNoTracking().Where(p => p.IsDemo).OrderBy(p => p.Id).ToListAsync(ct);
        var athletes = await db.Athletes.AsNoTracking().Where(a => a.IsDemo).OrderBy(a => a.Id).ToListAsync(ct);
        db.Set<MediaItem>().AddRange(Generate(posts, athletes));
        await db.SaveChangesAsync(ct);
    }
}
