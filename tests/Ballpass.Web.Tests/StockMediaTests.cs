using System.Text.Json;
using System.Text.RegularExpressions;
using Ballpass.Web.Components.Media;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Loop;
using Ballpass.Web.Features.Media;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/flow-release/specs/{media,demo-login}/spec.md (SPEC §5 media plan, M1-M5, O1, O4)
[Collection("db")]
public class StockMediaTests(BallpassAppFactory app)
{
    private static readonly string Repo = FindRepo();
    private static readonly string WebDir = Path.Combine(Repo, "src", "Ballpass.Web");
    private static readonly string StockDir = Path.Combine(WebDir, "wwwroot", "media", "stock");

    private static string FindRepo()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Ballpass.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    private static List<JsonElement> Credits()
    {
        var path = Path.Combine(StockDir, "credits.json");
        Assert.True(File.Exists(path), "wwwroot/media/stock/credits.json missing");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.EnumerateArray().ToList();
    }

    private static JsonElement CreditFor(string file) =>
        Credits().SingleOrDefault(e => e.TryGetProperty("name", out var n) && n.GetString() == file) is { ValueKind: JsonValueKind.Object } e
            ? e : throw new Xunit.Sdk.XunitException($"{file} has no credits.json entry");

    // ---------- files, credits, folder ----------

    [Fact]
    public void Every_slot_file_exists_and_is_credited()
    {
        var files = StockMedia.Files.ToList();
        Assert.NotEmpty(files);
        foreach (var f in files)
        {
            Assert.True(File.Exists(Path.Combine(StockDir, f)), $"missing wwwroot/media/stock/{f}");
            var c = CreditFor(f);
            foreach (var key in new[] { "source_page_url", "author", "licence" })
                Assert.False(string.IsNullOrWhiteSpace(c.GetProperty(key).GetString()), $"{f}: {key} empty");
            Assert.True(c.TryGetProperty("faces", out var faces) && faces.ValueKind is JsonValueKind.True or JsonValueKind.False, $"{f}: faces flag missing");
        }
    }

    [Fact]
    public void Stock_folder_holds_exactly_the_slot_files_and_the_credits()
    {
        var onDisk = Directory.Exists(StockDir) ? Directory.GetFiles(StockDir).Select(f => Path.GetFileName(f)).Order().ToList() : [];
        var expected = StockMedia.Files.Append("credits.json").Distinct().Order().ToList();

        Assert.Equal(expected, onDisk);
        Assert.Equal(StockMedia.Files.Distinct().Count(), Credits().Count);
    }

    [Fact]
    public void Slots_next_to_a_named_athlete_have_no_faces()
    {
        var noFace = StockMedia.NoFaceFiles.ToList();
        Assert.Contains(StockMedia.MatchTop.File, noFace);
        Assert.Contains(StockMedia.HighlightMain.Poster.File, noFace);
        Assert.All(StockMedia.CoverPool, c => Assert.Contains(c.File, noFace));
        foreach (var f in noFace)
            Assert.False(CreditFor(f).GetProperty("faces").GetBoolean(), $"{f} shows faces but sits next to a named athlete (M2)");
    }

    [Fact]
    public void Possible_minor_photo_never_ships_and_the_feed_pool_has_ten()
    {
        Assert.Equal(10, StockMedia.FeedPool.Count);
        Assert.DoesNotContain(Credits(), e => e.GetProperty("source_page_url").GetString()!.Contains("40732461"));
        Assert.Equal(Enumerable.Range(1, 10).Select(i => $"post-{i:00}.webp"), StockMedia.FeedPool.Select(p => p.File));
    }

    [Fact]
    public void Image_sizes_match_the_files()
    {
        foreach (var img in StockMedia.Images)
        {
            using var codec = SKCodec.Create(Path.Combine(StockDir, img.File));
            Assert.NotNull(codec);
            Assert.Equal((codec.Info.Width, codec.Info.Height), (img.Width, img.Height));
        }
    }

    [Fact]
    public void Pools_pick_deterministically()
    {
        Assert.Equal(StockMedia.Cover("danai-bakali"), StockMedia.Cover("danai-bakali"));
        var covers = Enumerable.Range(0, 200).Select(i => StockMedia.Cover($"athlete-{i}")).Distinct().ToList();
        Assert.Equal(StockMedia.CoverPool.Count, covers.Count);
        Assert.Equal(StockMedia.FeedPool[7], StockMedia.Feed(17));
        Assert.Equal(StockMedia.CoachPool[2], StockMedia.Coach(5));
        Assert.Contains(StockMedia.Feed(-3), StockMedia.FeedPool);
    }

    [Fact]
    public void Pages_never_type_stock_file_names()
    {
        var names = StockMedia.Files.ToList();
        var sources = Directory.GetFiles(WebDir, "*.razor", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(WebDir, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => Path.GetFileName(f) != "StockMedia.cs");
        foreach (var f in sources)
        {
            var text = File.ReadAllText(f);
            Assert.DoesNotContain("media/stock/", text);
            foreach (var n in names) Assert.DoesNotContain(n, text);
        }
    }

    // ---------- components ----------

    private static async Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
        return System.Net.WebUtility.HtmlDecode(html);   // the renderer encodes Greek as character references
    }

    [Fact]
    public async Task StockPhoto_renders_a_sized_lazy_image_and_a_scrim_without_a_chip()
    {
        var html = await Render<StockPhoto>(new() { ["Image"] = StockMedia.DoorCoach, ["Alt"] = "Προπονήτρια με ομάδα" });

        var img = Regex.Match(html, "<img\\b[^>]*>").Value;
        Assert.Contains($"src=\"{StockMedia.DoorCoach.Url}\"", img);
        Assert.Contains($"width=\"{StockMedia.DoorCoach.Width}\"", img);
        Assert.Contains($"height=\"{StockMedia.DoorCoach.Height}\"", img);
        Assert.Contains("alt=\"Προπονήτρια με ομάδα\"", img);
        Assert.Contains("loading=\"lazy\"", img);
        Assert.Contains("decoding=\"async\"", img);
        Assert.Matches("class=\"bp-photo-scrim[^\"]*\"", html);
        Assert.DoesNotContain("chip", html);
        Assert.DoesNotMatch("(?i)demo|αρχείου", html);
    }

    [Fact]
    public async Task StockPhoto_priority_image_is_eager_with_high_fetch_priority()
    {
        var html = await Render<StockPhoto>(new() { ["Image"] = StockMedia.Hero.Poster, ["Priority"] = true });

        var img = Regex.Match(html, "<img\\b[^>]*>").Value;
        Assert.Contains("fetchpriority=\"high\"", img);
        Assert.DoesNotContain("loading=\"lazy\"", img);
        Assert.Contains("alt=\"\"", img);   // decorative by default
    }

    [Fact]
    public async Task HeroVideo_loads_the_video_only_on_wide_screens_without_reduced_motion()
    {
        var html = await Render<HeroVideo>([]);

        var video = Regex.Match(html, "<video\\b[^>]*>").Value;
        foreach (var attr in new[] { "muted", "autoplay", "loop", "playsinline" })
            Assert.Matches($"\\s{attr}(\\s|=|>)", video);
        Assert.Contains("preload=\"metadata\"", video);
        Assert.Contains($"poster=\"{StockMedia.Hero.Poster.Url}\"", video);
        var sources = Regex.Matches(html, "<source\\b[^>]*>").Select(m => m.Value).ToList();
        Assert.Single(sources);
        Assert.Contains($"src=\"{StockMedia.Hero.Url}\"", sources[0]);
        Assert.Contains("type=\"video/mp4\"", sources[0]);
        Assert.Contains("media=\"(min-width:768px) and (prefers-reduced-motion:no-preference)\"", sources[0]);
        Assert.Matches("class=\"bp-photo-scrim[^\"]*\"", html);
    }

    // ---------- Demo:Featured config ----------

    [Fact]
    public void Featured_defaults_are_empty_in_appsettings_json()
    {
        var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(WebDir, "appsettings.json")),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var featured = json.RootElement.GetProperty("Demo").GetProperty("Featured");
        foreach (var key in new[] { "CoachFirstName", "CoachLastName", "AthleteSlug", "NeedQuery" })
            Assert.Equal("", featured.GetProperty(key).GetString());
    }

    [Fact]
    public void Featured_development_values_name_seeded_personas()
    {
        var featured = app.Services.GetRequiredService<IOptions<DemoOptions>>().Value.Featured;

        Assert.Equal("Ευτυχία", featured.CoachFirstName);
        Assert.Equal("Σωτηρίου", featured.CoachLastName);
        Assert.Equal("danai-bakali", featured.AthleteSlug);
        var data = DemoDataGenerator.Generate(42);
        Assert.Single(data.Coaches, c => c.FirstName == featured.CoachFirstName && c.LastName == featured.CoachLastName);
        Assert.Single(data.Athletes, a => a.Slug == featured.AthleteSlug);
    }
}
