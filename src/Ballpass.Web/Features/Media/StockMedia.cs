namespace Ballpass.Web.Features.Media;

/// <summary>One stock image under /media/stock/ with its intrinsic size (width/height attributes, no layout shift).</summary>
public sealed record StockImage(string File, int Width, int Height)
{
    public string Url => StockMedia.Root + File;
}

/// <summary>One stock video (H.264, muted) with its poster.</summary>
public sealed record StockClip(string File, StockImage Poster)
{
    public string Url => StockMedia.Root + File;
}

/// <summary>
/// The only place that knows stock file names (flow-release SPEC §5); pages use these slots and pools.
/// Every file is credited in wwwroot/media/stock/credits.json and docs/ASSETS.md. Rule M2: slots shown next to a named
/// fictional athlete (profile cover pool, highlights, match top card) use only files without faces (tested against credits.json).
/// Rule M1: stock is never an avatar or a portrait; avatars stay JerseyAvatar.
/// </summary>
public static class StockMedia
{
    public const string Root = "/media/stock/";

    private static StockImage Wide(string file) => new(file, 1600, 1067);
    private static StockImage Tall(string file) => new(file, 854, 1280);
    private static StockImage Hd(string file) => new(file, 1280, 720);

    // ---- home, /demo ----
    public static readonly StockClip Hero = new("hero-loop.mp4", Hd("hero-loop-poster.webp"));
    public static readonly StockImage DoorCoach = Wide("door-coach.webp");
    public static readonly StockImage DoorAthlete = Wide("door-athlete.webp");
    public static readonly StockImage ConnectCoachPlayer = Tall("connect-coach-player.webp");
    public static readonly StockImage PersonaCoach = Tall("persona-coach.webp");
    public static readonly StockImage PersonaAthlete = Hero.Poster;

    // ---- named-athlete slots: no faces (M2) ----
    public static readonly StockClip HighlightMain = new("hl-freethrow.mp4", Hd("hl-freethrow-poster.webp"));
    public static readonly StockClip HighlightThumb1 = new("hl-dribble.mp4", Hd("hl-dribble-poster.webp"));
    public static readonly StockClip HighlightThumb2 = new("hl-net.mp4", Hd("hl-net-poster.webp"));
    public static readonly StockImage MatchTop = HighlightMain.Poster;

    public static readonly IReadOnlyList<StockImage> CoverPool =
    [
        new("cover-hall-red.webp", 1600, 978),
        Wide("cover-hall-orange.webp"),
        new("cover-hall-bright.webp", 1600, 1068),
        Wide("empty-court-balls.webp"),
    ];

    // ---- empty states ----
    public static readonly StockImage ShortlistEmpty = CoverPool[3];
    public static readonly StockImage NotFound = CoverPool[1];
    public static readonly StockImage SentEmpty = Wide("team-with-coach.webp");

    // ---- pools ----
    public static readonly IReadOnlyList<StockImage> CoachPool =
    [
        Tall("coach-clipboard.webp"),
        Tall("coach-talk.webp"),
        Hd("coach-board.webp"),
    ];

    public static readonly IReadOnlyList<StockImage> FeedPool =
    [
        new("post-01.webp", 961, 1280),
        new("post-02.webp", 961, 1280),
        new("post-03.webp", 1032, 1280),
        new("post-04.webp", 1024, 1280),
        new("post-05.webp", 1024, 1280),
        new("post-06.webp", 1280, 853),
        Tall("post-07.webp"),
        Tall("post-08.webp"),
        Tall("post-09.webp"),
        Tall("post-10.webp"),
    ];

    /// <summary>Profile cover for an athlete: stable per slug (FNV-1a, not string.GetHashCode, which changes per process).</summary>
    public static StockImage Cover(string slug) => Pick(CoverPool, Fnv1a(slug));

    /// <summary>Inbox coach scene, by coach id.</summary>
    public static StockImage Coach(int coachId) => Pick(CoachPool, coachId);

    /// <summary>Feed fallback picture: postId % pool size (SPEC §4.3).</summary>
    public static StockImage Feed(int postId) => Pick(FeedPool, postId);

    public static IEnumerable<StockClip> Clips => [Hero, HighlightMain, HighlightThumb1, HighlightThumb2];

    public static IEnumerable<StockImage> Images =>
        new[] { DoorCoach, DoorAthlete, ConnectCoachPlayer, PersonaCoach, PersonaAthlete, MatchTop, ShortlistEmpty, NotFound, SentEmpty }
            .Concat(Clips.Select(c => c.Poster)).Concat(CoverPool).Concat(CoachPool).Concat(FeedPool).Distinct();

    /// <summary>Every published file name (images, posters, videos).</summary>
    public static IEnumerable<string> Files => Images.Select(i => i.File).Concat(Clips.Select(c => c.File)).Distinct();

    /// <summary>Files shown next to a named fictional athlete; credits.json must say faces: false for each (M2).</summary>
    public static IEnumerable<string> NoFaceFiles =>
        CoverPool.Select(c => c.File)
            .Concat(new[] { HighlightMain, HighlightThumb1, HighlightThumb2 }.SelectMany(c => new[] { c.File, c.Poster.File }))
            .Append(MatchTop.File).Distinct();

    private static StockImage Pick(IReadOnlyList<StockImage> pool, long key) => pool[(int)(((key % pool.Count) + pool.Count) % pool.Count)];

    private static uint Fnv1a(string text)
    {
        var hash = 2166136261u;
        foreach (var ch in text)
        {
            hash ^= ch;
            hash *= 16777619u;
        }
        return hash;
    }
}
