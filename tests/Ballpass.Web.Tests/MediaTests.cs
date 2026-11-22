using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Media;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-media/specs/media/spec.md
[Collection("db")]
public class MediaTests(BallpassAppFactory app)
{
    private readonly FeedHarness h = new(app);

    private static readonly string Repo = FindRepo();
    private static readonly string MediaRoot = Path.Combine(Repo, "src", "Ballpass.Web", "wwwroot", "media");
    private static readonly DateTimeOffset Ref = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private static string FindRepo()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Ballpass.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    private static (List<Post> Posts, List<Athlete> Athletes) SeedInput()
    {
        var data = DemoDataGenerator.Generate(42);
        var n = 0;
        foreach (var c in data.Clubs) c.Id = ++n;
        n = 0;
        foreach (var c in data.Coaches) c.Id = ++n;
        n = 0;
        foreach (var a in data.Athletes) a.Id = ++n;
        var posts = FeedSeeder.Generate(data, Ref).Posts.OrderBy(p => p.CreatedAt).ToList();
        n = 0;
        foreach (var p in posts) p.Id = ++n;
        return (posts, data.Athletes.ToList());
    }

    private static readonly string[] Eligible = ["Highlight", "FreeText", "TryoutCall", "LookingForTeam"];

    private static string Key(MediaItem m) => $"{m.PostId}|{m.AthleteId}|{m.Position}|{m.File}";

    // ---- catalog / assets ----

    [Fact]
    public void Catalog_has_about_30_photos_and_6_videos_with_alt_text()
    {
        Assert.InRange(MediaCatalog.All.Count(a => a.Kind == MediaKind.Photo), 28, 40);
        Assert.Equal(6, MediaCatalog.All.Count(a => a.Kind == MediaKind.Video));
        foreach (var a in MediaCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(a.AltEl), a.Id);
            Assert.False(string.IsNullOrWhiteSpace(a.AltEn), a.Id);
            Assert.True(a.Width > 0 && a.Height > 0, a.Id);
            if (a.Kind == MediaKind.Video) Assert.False(string.IsNullOrEmpty(a.Poster), a.Id);
        }
    }

    [Fact]
    public void Every_file_referenced_by_the_catalog_and_the_seed_exists_under_wwwroot_media()
    {
        Assert.NotEmpty(MediaCatalog.All);
        var (posts, athletes) = SeedInput();
        var files = MediaCatalog.All.SelectMany(a => new[] { a.File, a.Poster }).Where(f => f is not null)
            .Concat(MediaSeeder.Generate(posts, athletes).SelectMany(m => new[] { m.File, m.Poster }).Where(f => f is not null))
            .Distinct().ToList();
        Assert.NotEmpty(files);
        foreach (var f in files) Assert.True(File.Exists(Path.Combine(MediaRoot, f!)), $"missing wwwroot/media/{f}");
    }

    [Fact]
    public void Every_file_under_wwwroot_media_is_listed_in_ASSETS_md()
    {
        var doc = File.ReadAllText(Path.Combine(Repo, "docs", "ASSETS.md"));
        var files = Directory.GetFiles(MediaRoot, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(MediaRoot, f).Replace('\\', '/')).ToList();
        Assert.NotEmpty(files);
        foreach (var f in files) Assert.True(doc.Contains($"`{f}`"), $"{f} is not logged in docs/ASSETS.md");
    }

    [Fact]
    public void Total_media_size_is_at_most_40_MB()
    {
        var bytes = Directory.Exists(MediaRoot) ? Directory.GetFiles(MediaRoot, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) : 0;
        Assert.InRange(bytes, 1, 40L * 1024 * 1024);
    }

    [Fact]
    public void Catalog_files_respect_the_compression_limits()
    {
        Assert.NotEmpty(MediaCatalog.All);
        foreach (var a in MediaCatalog.All)
        {
            var size = new FileInfo(Path.Combine(MediaRoot, a.File)).Length;
            if (a.Kind == MediaKind.Video) Assert.True(size <= 2_500_000, $"{a.File} {size} B");
            else
            {
                Assert.EndsWith(".webp", a.File);
                Assert.True(size <= 200_000, $"{a.File} {size} B");
                Assert.True(Math.Max(a.Width, a.Height) <= 1280, a.File);
            }
        }
    }

    // ---- seed ----

    [Fact]
    public void Seed_is_deterministic_and_follows_the_plan()
    {
        var (posts, athletes) = SeedInput();
        var a = MediaSeeder.Generate(posts, athletes);
        var b = MediaSeeder.Generate(posts, athletes);
        Assert.NotEmpty(a);
        Assert.Equal(a.Select(Key), b.Select(Key));

        var byPost = a.Where(m => m.PostId is not null).GroupBy(m => m.PostId!.Value).ToDictionary(g => g.Key, g => g.OrderBy(m => m.Position).ToList());
        Assert.All(byPost.Values, items => Assert.InRange(items.Count, 1, 4));
        var postType = posts.ToDictionary(p => p.Id, p => p.Type);
        var types = byPost.Keys.Select(id => postType[id]).Distinct();
        Assert.All(types, t => Assert.Contains(t.ToString(), Eligible));

        var eligible = posts.Where(p => Eligible.Contains(p.Type.ToString())).ToList();
        var share = (double)eligible.Count(p => byPost.ContainsKey(p.Id)) / eligible.Count;
        Assert.InRange(share, 0.5, 0.75);

        var catalog = MediaCatalog.All.ToDictionary(c => c.File);
        foreach (var p in posts.Where(p => p.Type == PostType.TryoutCall))
        {
            Assert.True(byPost.ContainsKey(p.Id), "tryout call without media");
            Assert.Single(byPost[p.Id]);
            Assert.Contains(catalog[byPost[p.Id][0].File].Theme, new[] { "court", "gym" });
        }
        foreach (var p in posts.Where(p => p.Type == PostType.Highlight))
        {
            var items = byPost[p.Id];
            Assert.True(items.Any(m => m.Kind == MediaKind.Video) ? items.Count == 1 : items.Count is >= 2 and <= 4, "highlight = a video or a 2-4 photo carousel");
        }
        Assert.Contains(byPost.Values, items => items.Any(m => m.Kind == MediaKind.Video));
        Assert.Contains(byPost.Values, items => items.Count(m => m.Kind == MediaKind.Photo) >= 2);

        var reels = a.Where(m => m.AthleteId is not null).GroupBy(m => m.AthleteId!.Value).ToList();
        Assert.InRange(reels.Count, 38, 42);
        Assert.All(reels, g => Assert.InRange(g.Count(), 1, 3));
        Assert.All(a, m => Assert.True((m.PostId is null) ^ (m.AthleteId is null), "post xor athlete"));
        Assert.All(a, m => Assert.True(m.IsDemo));
    }

    [Fact]
    public void Seeded_rows_are_in_the_database()
    {
        var rows = h.Db(db => db.Set<MediaItem>().Count());
        Assert.InRange(rows, 40, 400);
    }

    // ---- UI: post media ----

    private async Task<(int PostId, string Card)> CardWith(HttpClient client, Func<List<MediaItem>, bool> pick)
    {
        var groups = h.Db(db => db.Set<MediaItem>().Where(m => m.PostId != null).AsNoTracking().ToList()).GroupBy(m => m.PostId!.Value)
            .Where(g => pick(g.OrderBy(m => m.Position).ToList())).Select(g => g.Key).ToHashSet();
        Assert.NotEmpty(groups);
        var cards = await FeedHarness.AllPages(client, "/feed");
        var hit = cards.FirstOrDefault(c => groups.Contains(c.Id));
        Assert.True(hit.Id != 0, "no feed page shows a post with the wanted media");
        return (hit.Id, hit.Html);
    }

    [Fact]
    public async Task Photo_renders_with_size_alt_lazy_loading_and_the_stock_chip()
    {
        var (_, card) = await CardWith(app.Client(), items => items.Count == 1 && items[0].Kind == MediaKind.Photo);
        var img = Regex.Match(card, "<img\\b[^>]*src=\"/media/[^\"]+\"[^>]*>").Value;
        Assert.NotEmpty(img);
        Assert.Matches("width=\"\\d+\"", img);
        Assert.Matches("height=\"\\d+\"", img);
        Assert.Matches("alt=\"[^\"]{5,}\"", img);
        Assert.Contains("loading=\"lazy\"", img);
        Assert.Contains("decoding=\"async\"", img);
        Assert.Contains("Εικόνα αρχείου · demo", card);
    }

    [Fact]
    public async Task Video_has_muted_playsinline_preload_none_poster_and_no_autoplay()
    {
        var (_, card) = await CardWith(app.Client(), items => items.Any(m => m.Kind == MediaKind.Video));
        var video = Regex.Match(card, "<video\\b[^>]*>").Value;
        Assert.NotEmpty(video);
        Assert.Matches("\\smuted(\\s|=|>)", video);
        Assert.Matches("\\splaysinline(\\s|=|>)", video);
        Assert.Contains("preload=\"none\"", video);
        Assert.Matches("poster=\"/media/[^\"]+\\.webp\"", video);
        Assert.Matches("\\sloop(\\s|=|>)", video);
        Assert.DoesNotContain("autoplay", video);
        Assert.Matches("\\scontrols(\\s|=|>)", video);
        Assert.Contains("Γραφικό · demo", card);
        Assert.Matches("<source\\b[^>]*src=\"/media/[^\"]+\\.mp4\"[^>]*type=\"video/mp4\"", card);
    }

    [Fact]
    public async Task Carousel_has_scroll_snap_slides_and_one_dot_per_photo()
    {
        var (_, card) = await CardWith(app.Client(), items => items.Count >= 2 && items.All(m => m.Kind == MediaKind.Photo));
        var slides = Regex.Matches(card, "<li\\b[^>]*data-media-slide").Count;
        var dots = Regex.Matches(card, "<a\\b[^>]*data-media-dot").Count;
        Assert.InRange(slides, 2, 4);
        Assert.Equal(slides, dots);
        Assert.Contains("data-media-carousel", card);
    }

    [Fact]
    public async Task Single_photo_has_no_dots_and_opens_a_css_only_lightbox()
    {
        var (_, card) = await CardWith(app.Client(), items => items.Count == 1 && items[0].Kind == MediaKind.Photo);
        Assert.DoesNotContain("data-media-dot", card);
        var href = Regex.Match(card, "<a\\b[^>]*data-media-open[^>]*href=\"#(lb-[^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(href);
        Assert.Matches($"<div[^>]*id=\"{Regex.Escape(href)}\"[^>]*class=\"bp-media-lightbox", card);
        Assert.Matches($"<a[^>]*class=\"bp-media-close\"[^>]*href=\"#!\"", card);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("athlete")]
    [InlineData("coach")]
    public async Task Everyone_sees_media_and_it_is_self_hosted(string who)
    {
        var client = who switch
        {
            "athlete" => await h.LoginAsync("athlete", h.AthleteId(0)),
            "coach" => await h.LoginAsync("coach", h.CoachId(0)),
            _ => app.Client(),
        };
        var (_, card) = await CardWith(client, items => items.Count >= 1);
        Assert.Contains("class=\"bp-media", card);
        foreach (Match m in Regex.Matches(card, "\\b(src|poster)=\"([^\"]*)\""))
            Assert.StartsWith("/media/", m.Groups[2].Value);
        Assert.DoesNotMatch("<(img|video|source)\\b[^>]*(src|poster)=\"https?:", card);
    }

    // ---- stories bar + profile highlights ----

    [Fact]
    public async Task Stories_bar_lists_only_athletes_with_a_reel()
    {
        var html = await FeedHarness.Html(app.Client(), "/feed");
        Assert.Contains("Στιγμιότυπα", html);
        var slugs = Regex.Matches(html, "data-story-slug=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.InRange(slugs.Count, 8, 40);
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
        var withReel = h.Db(db => db.Set<MediaItem>().Where(m => m.AthleteId != null).Select(m => m.Athlete!.Slug).Distinct().ToList()).ToHashSet();
        Assert.All(slugs, s => Assert.Contains(s, withReel));
        Assert.All(slugs, s => Assert.Contains($"href=\"/p/{s}#highlights\"", html));
        Assert.Matches("<svg[^>]*class=\"[^\"]*bp-jersey", html);
    }

    [Fact]
    public async Task Profile_highlights_replace_the_no_video_placeholder()
    {
        var (withSlug, withoutSlug) = h.Db(db =>
        {
            var reel = db.Set<MediaItem>().Where(m => m.AthleteId != null).Select(m => m.AthleteId!.Value).Distinct().ToList();
            return (db.Athletes.Where(a => a.IsDemo && reel.Contains(a.Id)).OrderBy(a => a.Id).Select(a => a.Slug).First(),
                    db.Athletes.Where(a => a.IsDemo && !reel.Contains(a.Id)).OrderBy(a => a.Id).Select(a => a.Slug).First());
        });

        var withHtml = await FeedHarness.Html(app.Client(), $"/p/{withSlug}");
        Assert.Matches("id=\"highlights\"", withHtml);
        Assert.DoesNotContain("Δεν έχει προστεθεί βίντεο ακόμη", withHtml);
        var section = withHtml[withHtml.IndexOf("id=\"highlights\"", StringComparison.Ordinal)..];
        Assert.Contains("class=\"bp-media", section);
        Assert.Contains("/media/", section);

        var withoutHtml = await FeedHarness.Html(app.Client(), $"/p/{withoutSlug}");
        Assert.Contains("Δεν έχει προστεθεί βίντεο ακόμη", withoutHtml);
        Assert.DoesNotContain("id=\"highlights\"", withoutHtml);
    }

    [Fact]
    public async Task Story_page_shows_the_reel_with_previous_and_next_links()
    {
        var slug = h.Db(db => db.Set<MediaItem>().Where(m => m.AthleteId != null).OrderBy(m => m.Id).Select(m => m.Athlete!.Slug).First());
        var response = await app.Client().GetAsync($"/stories/{slug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("class=\"bp-media", html);
        Assert.Contains("data-story-next", html);
        Assert.Contains("data-story-prev", html);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client().GetAsync("/stories/no-such-athlete")).StatusCode);
    }

    // ---- jersey avatar ----

    [Fact]
    public void Jersey_number_is_deterministic_in_range_and_spread()
    {
        Assert.Equal(JerseyStyle.Number(17), JerseyStyle.Number(17));
        var numbers = Enumerable.Range(1, 400).Select(JerseyStyle.Number).ToList();
        Assert.All(numbers, n => Assert.InRange(n, 0, 99));
        Assert.True(numbers.Distinct().Count() >= 60, "numbers should spread over 0-99");
    }

    [Fact]
    public void Jersey_colour_follows_the_club_is_neutral_without_one_and_readable()
    {
        Assert.Equal(JerseyStyle.Palette("Αστέρας Δείγμα"), JerseyStyle.Palette("Αστέρας Δείγμα"));
        var clubs = Enumerable.Range(1, 40).Select(i => $"Σύλλογος {i}").ToList();
        Assert.True(clubs.Select(c => JerseyStyle.Palette(c).Fill).Distinct().Count() >= 5, "clubs should get different colours");
        var neutral = JerseyStyle.Palette(null);
        Assert.Matches("^#[0-9A-Fa-f]{6}$", neutral.Fill);
        Assert.DoesNotContain(clubs, c => JerseyStyle.Palette(c).Fill == neutral.Fill);
        foreach (var p in clubs.Select(JerseyStyle.Palette).Append(neutral))
            Assert.True(JerseyStyle.Contrast(p.Fill, p.Text) >= 4.5, $"{p.Fill} vs {p.Text}");
    }

    [Fact]
    public async Task Feed_cards_use_the_jersey_avatar_and_keep_the_initials_and_the_old_class()
    {
        var cards = await FeedHarness.AllPages(app.Client(), "/feed");
        var athleteCard = cards.First(c => c.AuthorKey.StartsWith("athlete:")).Html;
        Assert.Matches("<svg[^>]*class=\"[^\"]*bp-jersey[^\"]*bp-feed-avatar|<[a-z]+[^>]*class=\"[^\"]*bp-feed-avatar[^\"]*\"[^>]*>\\s*<svg[^>]*bp-jersey", athleteCard);
        Assert.Matches("<text[^>]*class=\"bp-jersey-initials\"[^>]*>[^<]{1,3}</text>", athleteCard);
        Assert.Matches("<text[^>]*class=\"bp-jersey-number\"[^>]*>\\d{1,2}</text>", athleteCard);
        Assert.DoesNotContain("data-jersey-role=\"coach\"", athleteCard);
        var coachCard = cards.First(c => c.AuthorKey.StartsWith("coach:")).Html;
        Assert.Contains("data-jersey-role=\"coach\"", coachCard);
    }

    [Fact]
    public async Task Search_results_and_profile_header_use_the_jersey_avatar()
    {
        var coach = await h.LoginAsync("coach", h.CoachId(0));
        var search = await FeedHarness.Html(coach, "/search");
        Assert.Contains("bp-jersey", search);
        Assert.Matches("<text[^>]*class=\"bp-jersey-initials\"", search);
        Assert.DoesNotContain("class=\"bp-initials\"", search);

        var slug = h.Db(db => db.Athletes.Where(a => a.IsDemo).OrderBy(a => a.Id).Select(a => a.Slug).First());
        var profile = await FeedHarness.Html(app.Client(), $"/p/{slug}");
        Assert.Matches("<svg[^>]*class=\"[^\"]*bp-jersey", profile);
        Assert.Matches("<text[^>]*class=\"bp-jersey-initials\"", profile);
    }

    // ---- assets served ----

    [Fact]
    public async Task Media_files_are_served_with_the_right_content_type()
    {
        Assert.NotEmpty(MediaCatalog.All);
        var photo = MediaCatalog.All.First(a => a.Kind == MediaKind.Photo);
        var video = MediaCatalog.All.First(a => a.Kind == MediaKind.Video);
        var c = app.Client();
        var r1 = await c.GetAsync($"/media/{photo.File}");
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal("image/webp", r1.Content.Headers.ContentType?.MediaType);
        var r2 = await c.GetAsync($"/media/{video.File}");
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal("video/mp4", r2.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Css_and_js_are_linked_and_the_script_is_tiny()
    {
        var html = await FeedHarness.Html(app.Client(), "/feed");
        Assert.Matches("<link[^>]*href=\"[^\"]*bp-media[^\"]*\\.css\"", html);
        Assert.Matches("<script[^>]*src=\"[^\"]*bp-media[^\"]*\\.js\"", html);
        var js = File.ReadAllText(Path.Combine(Repo, "src", "Ballpass.Web", "wwwroot", "js", "bp-media.js"));
        Assert.Contains("IntersectionObserver", js);
        Assert.Contains("prefers-reduced-motion", js);
        Assert.Contains("0.6", js);
        Assert.InRange(js.Length, 100, 4000);
    }
}
