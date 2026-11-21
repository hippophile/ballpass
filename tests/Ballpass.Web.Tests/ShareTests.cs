using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Share;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-share/specs/share/spec.md
[Collection("db")]
public class ShareTests(BallpassAppFactory app)
{
    private static readonly DemoDataSet Data = DemoDataGenerator.Generate(42);

    private static string Latest(Athlete a) => a.Stats.Max(s => s.Season)!;

    // Two seasons, latest has >= 5 games.
    private static readonly Athlete Full = Data.Athletes.First(a => a.Stats.Count >= 2 && a.Stats.First(s => s.Season == Latest(a)).Games >= 5);

    /// <summary>Records every render; wraps the real renderer. Own cache dir so reruns never hit an old cache.</summary>
    private sealed class SpyRenderer(IOgCardRenderer inner) : IOgCardRenderer
    {
        public List<OgCardModel> Calls { get; } = [];
        public byte[] Render(OgCardModel model) { lock (Calls) Calls.Add(model); return inner.Render(model); }
    }

    private (HttpClient Client, SpyRenderer Spy) SpyClient(string? publicBase = null)
    {
        var spy = new SpyRenderer(new OgCardRenderer());
        var cacheDir = Path.Combine(Path.GetTempPath(), "bp-share-test-" + Guid.NewGuid().ToString("N"));
        var factory = app.WithWebHostBuilder(b =>
        {
            b.UseSetting("Share:CacheDir", cacheDir);
            if (publicBase is not null) b.UseSetting("PublicBaseUrl", publicBase);
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IOgCardRenderer>();
                s.AddSingleton<IOgCardRenderer>(spy);
            });
        });
        return (factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }), spy);
    }

    private static SKBitmap Decode(byte[] png) => SKBitmap.Decode(png) ?? throw new InvalidOperationException("not a PNG");

    // ---- OG meta tags ----

    [Fact]
    public async Task Profile_has_absolute_og_image_tags_from_request_host_by_default()
    {
        var html = WebUtility.HtmlDecode(await app.Client().GetStringAsync($"/p/{Full.Slug}"));

        Assert.Matches($"<meta[^>]*property=\"og:image\"[^>]*content=\"http://localhost/p/{Full.Slug}/og\\.png\"", html);
        Assert.Matches("<meta[^>]*property=\"og:image:width\"[^>]*content=\"1200\"", html);
        Assert.Matches("<meta[^>]*property=\"og:image:height\"[^>]*content=\"630\"", html);
        Assert.Matches("<meta[^>]*property=\"og:image:alt\"[^>]*content=\"[^\"]+\"", html);
        Assert.Matches("<meta[^>]*name=\"twitter:card\"[^>]*content=\"summary_large_image\"", html);
        // A second HeadContent on the page would replace the first: the existing og:title must survive next to the share tags.
        Assert.Matches("<meta[^>]*property=\"og:title\"", html);
    }

    [Fact]
    public async Task Og_image_uses_PublicBaseUrl_when_configured()
    {
        var (client, _) = SpyClient(publicBase: "https://demo.example.test/");
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/p/{Full.Slug}"));

        Assert.Contains($"content=\"https://demo.example.test/p/{Full.Slug}/og.png\"", html);
    }

    // ---- og.png ----

    [Fact]
    public async Task Og_png_is_1200x630_png_under_300KB_without_cookie()
    {
        var (client, _) = SpyClient();
        var response = await client.GetAsync($"/p/{Full.Slug}/og.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length < 300 * 1024, $"{bytes.Length} bytes");
        using var bmp = Decode(bytes);
        Assert.Equal(1200, bmp.Width);
        Assert.Equal(630, bmp.Height);
        Assert.NotNull(response.Headers.ETag);
    }

    [Fact]
    public async Task Second_request_is_served_from_cache_and_etag_gives_304()
    {
        var (client, spy) = SpyClient();
        var first = await client.GetAsync($"/p/{Full.Slug}/og.png");
        var etag = first.Headers.ETag!.ToString();
        var second = await client.GetAsync($"/p/{Full.Slug}/og.png");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsByteArrayAsync(), await second.Content.ReadAsByteArrayAsync());
        Assert.Single(spy.Calls);

        var req = new HttpRequestMessage(HttpMethod.Get, $"/p/{Full.Slug}/og.png");
        req.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var conditional = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotModified, conditional.StatusCode);
        Assert.Single(spy.Calls);
    }

    [Fact]
    public async Task Watermark_bands_are_solid_top_and_bottom_and_text_was_passed_to_the_renderer()
    {
        var (client, spy) = SpyClient();
        var bytes = await client.GetByteArrayAsync($"/p/{Full.Slug}/og.png");

        Assert.Equal("DEMO · ΦΑΝΤΑΣΤΙΚΟ ΠΡΟΣΩΠΟ · FICTIONAL ATHLETE", spy.Calls.Single().WatermarkText);

        using var bmp = Decode(bytes);
        var band = (int)Math.Ceiling(bmp.Height * 0.05);   // >= 5% of the height
        // Sample rows across both bands at the left/right gutters (the text sits in the middle).
        foreach (var y in new[] { 0, band / 2, band, bmp.Height - 1, bmp.Height - 1 - band / 2, bmp.Height - 1 - band })
            foreach (var x in new[] { 0, 12, 40, bmp.Width - 41, bmp.Width - 13, bmp.Width - 1 })
                Assert.Equal(OgCardRenderer.BandColor, bmp.GetPixel(x, y));
        // The centre is not band colour (card body).
        Assert.NotEqual(OgCardRenderer.BandColor, bmp.GetPixel(bmp.Width / 2, bmp.Height / 2));
    }

    [Fact]
    public void Long_greek_name_renders_inside_the_canvas_without_throwing()
    {
        var athlete = new Athlete
        {
            Slug = "long-name", FirstName = "Αικατερίνη-Παναγιώτα", LastName = "Παπαδοπούλου-Χατζηγεωργίου-Καραμανλή",
            FirstNameLatin = "Aikaterini", LastNameLatin = "Papadopoulou", BirthYear = 2002, Gender = Gender.Female,
            Position = Position.SG, HeightCm = 181, City = "Αθήνα", Region = "Αττική", IsDemo = true,
            CurrentClub = new Club { Name = "Αθλητικός Όμιλος Μεγάλης Ελλάδας Πάτρας", City = "Πάτρα", Region = "Δυτ. Ελλάδα", IsDemo = true },
            Stats = [new StatLine { Season = "2025-26", Games = 20, Minutes = 24.5m, Points = 12.3m, Rebounds = 5.1m, Assists = 3.2m, Source = StatSource.CoachConfirmed }],
        };
        var model = OgCardModelFactory.Build(athlete, 2026);

        var png = new OgCardRenderer().Render(model);

        using var bmp = Decode(png);
        Assert.Equal((1200, 630), (bmp.Width, bmp.Height));
        Assert.Contains("Παπαδοπούλου-Χατζηγεωργίου-Καραμανλή", model.Name);
        // Right gutter between the bands stays background-dark: nothing drew past the 1136px content edge.
        for (var y = 80; y < 550; y += 10)
            for (var x = 1170; x < 1200; x += 6)
                Assert.True(bmp.GetPixel(x, y).Red < 0x40, $"pixel ({x},{y}) is bright: name overflowed");
    }

    [Fact]
    public async Task Unknown_slug_is_404_for_card_and_season()
    {
        var client = app.Client();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/p/nobody-here/og.png")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/p/nobody-here/season")).StatusCode);
    }

    // ---- share panel ----

    [Fact]
    public async Task Profile_share_panel_has_preview_download_and_wrapped_link()
    {
        var html = WebUtility.HtmlDecode(await app.Client().GetStringAsync($"/p/{Full.Slug}"));

        Assert.Matches($"<img[^>]*src=\"/p/{Full.Slug}/og\\.png", html);
        Assert.Matches($"<a[^>]*href=\"/p/{Full.Slug}/og\\.png[^\"]*\"[^>]*download[^>]*>[^<]*Λήψη κάρτας", html);
        Assert.Contains($"href=\"/p/{Full.Slug}/season\"", html);
    }

    // ---- Season Wrapped ----

    private static string Metric(string html, string metric)
    {
        var m = Regex.Match(html, $"data-metric=\"{Regex.Escape(metric)}\" data-value=\"([^\"]*)\"");
        Assert.True(m.Success, $"metric {metric} missing");
        return m.Groups[1].Value;
    }

    private static List<string> Slides(string html) =>
        Regex.Matches(html, "<section[^>]*data-slide=\"([a-z]+)\"").Select(m => m.Groups[1].Value).ToList();

    private static string Inv(decimal v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    private static string InvSigned(decimal v) => (v > 0 ? "+" : "") + Inv(v);

    [Fact]
    public async Task Wrapped_numbers_equal_their_linq_aggregates()
    {
        var html = WebUtility.HtmlDecode(await app.Client().GetStringAsync($"/p/{Full.Slug}/season"));
        var latest = Latest(Full);
        var lines = Full.Stats.Where(s => s.Season == latest).ToList();
        var line = lines.OrderByDescending(s => s.Games).First();
        var prev = Full.Stats.Where(s => string.CompareOrdinal(s.Season, latest) < 0)
            .OrderByDescending(s => s.Season, StringComparer.Ordinal).First();

        Assert.Equal(lines.Sum(s => s.Games).ToString(), Metric(html, "games"));
        Assert.Equal(Math.Round(lines.Sum(s => s.Games * s.Minutes)).ToString(CultureInfo.InvariantCulture), Metric(html, "minutes"));
        Assert.Equal(Inv(line.Points), Metric(html, "points"));
        Assert.Equal(Inv(line.Rebounds), Metric(html, "rebounds"));
        Assert.Equal(Inv(line.Assists), Metric(html, "assists"));
        Assert.Equal(InvSigned(line.Points - prev.Points), Metric(html, "delta-points"));
        Assert.Equal(InvSigned(line.Rebounds - prev.Rebounds), Metric(html, "delta-rebounds"));
        Assert.Equal(InvSigned(line.Assists - prev.Assists), Metric(html, "delta-assists"));
        // Visible text is the Greek-formatted value (comma decimal), and the source label is shown.
        Assert.Contains(">" + line.Points.ToString("0.0", new CultureInfo("el")) + "<", html);
        Assert.Matches("(Αυτο-δηλωμένο|Επιβεβαιωμένο από προπονητή|Επαληθευμένο από λίγκα \\(mock\\))", html);
        Assert.Contains($"href=\"/p/{Full.Slug}?src=wrapped\"", html);
        Assert.Contains($"/p/{Full.Slug}/og.png", html);
        var slides = Slides(html);
        Assert.Equal(["intro", "games", "line", "growth"], slides.Take(4));
        Assert.Equal("outro", slides[^1]);
    }

    [Fact]
    public async Task Wrapped_cohort_slide_matches_an_independent_computation_and_is_labelled_fictional()
    {
        // First woman whose own season has >= 20 same-position-group peers.
        string Group(Position p) => p is Position.PG or Position.SG ? "G" : p is Position.SF or Position.PF ? "F" : "C";
        var athlete = Data.Athletes.First(a => a.Gender == Gender.Female && a.Stats.Count > 0 && a.Stats.Max(s => s.Season) == "2025-26"
            && a.Stats.First(s => s.Season == "2025-26").Games >= 5
            && Data.Athletes.Count(o => o.Gender == Gender.Female && Group(o.Position) == Group(a.Position) && o.Stats.Any(s => s.Season == "2025-26")) >= 20);
        var peers = Data.Athletes.Where(o => o.Gender == Gender.Female && Group(o.Position) == Group(athlete.Position))
            .Select(o => o.Stats.FirstOrDefault(s => s.Season == "2025-26")).Where(s => s is not null).Select(s => s!).ToList();
        var mine = athlete.Stats.First(s => s.Season == "2025-26");
        var best = new (string Name, Func<StatLine, decimal> Get)[]
        {
            ("points", s => s.Points), ("rebounds", s => s.Rebounds), ("assists", s => s.Assists), ("steals", s => s.Steals), ("blocks", s => s.Blocks),
        }.Select(m => (m.Name, Pct: 100.0 * (peers.Count(p => m.Get(p) < m.Get(mine)) + 0.5 * peers.Count(p => m.Get(p) == m.Get(mine))) / peers.Count))
         .MaxBy(x => x.Pct);

        var html = WebUtility.HtmlDecode(await app.Client().GetStringAsync($"/p/{athlete.Slug}/season"));

        if (best.Pct < 50) { Assert.DoesNotContain("data-slide=\"cohort\"", html); return; }
        Assert.Contains("data-slide=\"cohort\"", html);
        Assert.Equal(peers.Count.ToString(), Metric(html, "cohort-n"));
        Assert.Equal(Math.Max(1, 100 - (int)Math.Round(best.Pct, MidpointRounding.AwayFromZero)).ToString(), Metric(html, "cohort-top"));
        Assert.Contains($"data-cohort-metric=\"{best.Name}\"", html);
        Assert.Contains($"ανάμεσα σε {peers.Count} αθλήτριες επίδειξης (φανταστικά)", html);
    }

    [Fact]
    public async Task Wrapped_with_fewer_than_5_games_shows_only_intro_games_and_outro()
    {
        var slug = "share-few-games" + TestDb.Suffix.Replace("_", "-");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
            await db.Athletes.Where(a => a.Slug == slug).ExecuteDeleteAsync();
            db.Athletes.Add(new Athlete
            {
                Slug = slug, FirstName = "Ελένη", LastName = "Δοκιμαστική", FirstNameLatin = "Eleni", LastNameLatin = "Dokimastiki",
                BirthYear = 2001, Gender = Gender.Female, Position = Position.PG, HeightCm = 170, City = "Αθήνα", Region = "Αττική", IsDemo = true,
                Stats = [new StatLine { Season = "2025-26", League = League.B, Games = 3, Minutes = 11.5m, Points = 4m, Rebounds = 2m, Assists = 1m, Source = StatSource.SelfReported }],
            });
            await db.SaveChangesAsync();
        }
        try
        {
            var html = WebUtility.HtmlDecode(await app.Client().GetStringAsync($"/p/{slug}/season"));

            Assert.Equal(["intro", "games", "outro"], Slides(html));
            Assert.Equal("3", Metric(html, "games"));
            Assert.Contains("Η σεζόν 2025-26 της Ελένης", Regex.Replace(html, "<[^>]+>", ""));   // season sits in a nowrap span
        }
        finally
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
            await db.Athletes.Where(a => a.Slug == slug).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public void Cohort_below_20_is_skipped_and_at_20_picks_the_highest_midrank_metric()
    {
        CohortRow Row(decimal pts, decimal reb) => new(Position.SG, pts, reb, 1m, 1m, 1m);
        var mine = Row(30m, 3m);
        List<CohortRow> Peers(int n) => [mine, .. Enumerable.Range(0, n - 1).Select(i => Row(i, i))];

        Assert.Null(SeasonCohort.Best(mine, Peers(19)));

        var best = SeasonCohort.Best(mine, Peers(20));
        Assert.NotNull(best);
        Assert.Equal(CohortMetric.Points, best.Metric);
        Assert.Equal(20, best.N);
        Assert.Equal(98, best.Percentile);          // (19 below + 0.5 self) / 20 = 97.5 -> 98
        Assert.Equal(2, best.TopPercent);
    }

    [Fact]
    public void Cohort_uses_only_the_same_position_group()
    {
        var mine = new CohortRow(Position.C, 10m, 10m, 10m, 1m, 1m);
        var others = Enumerable.Range(0, 25).Select(_ => new CohortRow(Position.PG, 1m, 1m, 1m, 1m, 1m)).ToList();

        Assert.Null(SeasonCohort.Best(mine, [mine, .. others]));   // only 1 centre -> n < 20
    }

    [Theory]
    [InlineData("Ελένη", "Ελένης")]
    [InlineData("Μαρία", "Μαρίας")]
    [InlineData("Ζωή", "Ζωής")]
    [InlineData("Δέσποινα", "Δέσποινας")]
    public void Greek_female_first_name_genitive(string name, string expected) =>
        Assert.Equal(expected, GreekGenitive.FemaleFirstName(name));

    [Fact]
    public void Greek_genitive_gives_null_when_no_safe_rule_applies() =>
        Assert.Null(GreekGenitive.FemaleFirstName("Μυρτώ"));
}
