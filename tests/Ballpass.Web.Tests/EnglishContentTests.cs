using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Components.Shared;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Engage;
using Ballpass.Web.Features.Find.Ai;
using Ballpass.Web.Features.Loop;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/english-content/specs/localization/spec.md

/// <summary>Shipped configuration (English default) on its own database: the pages are scanned for Greek letters, so no other test's
/// runtime Greek content may land here.</summary>
public class EnglishContentAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Ballpass", TestDb.ConnectionString("ballpass_encontent"));

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().ResetAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}

public class EnglishContentTests(EnglishContentAppFactory app) : IClassFixture<EnglishContentAppFactory>
{
    private static readonly Regex Greek = new(@"[Ͱ-Ͽἀ-῿]");

    private string Config(string key) => app.Services.GetRequiredService<IConfiguration>()[key] ?? "";

    private T Db<T>(Func<BallpassDbContext, T> query)
    {
        using var scope = app.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<BallpassDbContext>());
    }

    private HttpClient Client(string culture)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Culture={Uri.EscapeDataString($"c={culture}|uic={culture}")}");
        return client;
    }

    private int FeaturedCoachId => Db(db => db.Coaches.Where(c => c.IsDemo && c.FirstName == Config("Demo:Featured:CoachFirstName")
        && c.LastName == Config("Demo:Featured:CoachLastName")).Select(c => c.Id).Single());

    private int FeaturedAthleteId => Db(db => db.Athletes.Where(a => a.Slug == Config("Demo:Featured:AthleteSlug")).Select(a => a.Id).Single());

    private async Task<HttpClient> SignedIn(string type, string culture)
    {
        var id = type == "coach" ? FeaturedCoachId : FeaturedAthleteId;
        var client = Client(culture);
        var token = FeedHarness.Token(await FeedHarness.Html(client, "/demo"));
        var response = await client.PostAsync("/demo/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["personaType"] = type, ["personaId"] = id.ToString(), ["returnUrl"] = "", ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    /// <summary>What a reader sees: text nodes (scripts, styles, comments and meta tags removed) plus alt / title / aria-label / placeholder.</summary>
    public static string VisibleText(string rawHtml)
    {
        var html = Regex.Replace(rawHtml, "<(script|style)[^>]*>.*?</\\1>|<!--.*?-->|<meta[^>]*>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var attrs = Regex.Matches(html, "\\s(?:alt|title|aria-label|placeholder)=\"([^\"]*)\"").Select(m => m.Groups[1].Value);
        var text = Regex.Replace(html, "<[^>]+>", "\n");
        return WebUtility.HtmlDecode(text + "\n" + string.Join("\n", attrs));
    }

    /// <summary>Lines of visible text with a Greek letter, minus the allowed runtime texts.</summary>
    private static List<string> GreekLines(string rawHtml, params string[] allowed)
    {
        var text = VisibleText(rawHtml);
        foreach (var a in allowed) text = text.Replace(a, " ");
        return text.Split('\n').Select(l => l.Trim()).Where(l => Greek.IsMatch(l)).Distinct().ToList();
    }

    private async Task<string> Html(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private int FeaturedCoachThread => Db(db => db.Set<Conversation>().Where(c => c.CoachId == FeaturedCoachId)
        .OrderByDescending(c => c.Messages.Count).ThenBy(c => c.Id).Select(c => c.Id).First());

    public static TheoryData<string, string> EnglishPages() => new()
    {
        { "/feed", "coach" },
        { "/messages/{thread}", "coach" },
        { "/messages", "coach" },
        { "/me/inbox", "athlete" },
        { "/p/{featured}", "" },
        { "/match?need=featured", "coach" },
        { "/demo", "" },
        { "/", "" },
        { "/why", "" },
        { "/feed", "" },
        { "/messages", "athlete" },
        { "/feed", "athlete" },
        { "/me/sent", "coach" },
        { "/shortlist", "coach" },
        { "/search", "coach" },
        { "/p/{featured}/season", "" },
    };

    // ---------- pages ----------

    [Theory]
    [MemberData(nameof(EnglishPages))]
    public async Task English_pages_show_no_greek_letters(string path, string persona)
    {
        var client = persona == "" ? Client("en") : await SignedIn(persona, "en");
        var url = path.Replace("{featured}", Config("Demo:Featured:AthleteSlug")).Replace("{thread}", FeaturedCoachThread.ToString());

        var greek = GreekLines(await Html(client, url));

        Assert.True(greek.Count == 0, $"{url}: Greek in English page:\n" + string.Join("\n", greek.Take(40)));
    }

    [Fact]
    public async Task Runtime_content_shows_as_typed_next_to_english_seed()
    {
        var coach = await SignedIn("coach", "en");
        var thread = Db(db => db.Set<Conversation>().Where(c => c.CoachId == FeaturedCoachId && c.BlockedAt == null)
            .OrderBy(c => c.Messages.Count).ThenBy(c => c.Id).Select(c => c.Id).First());
        const string typed = "Γεια σου από το τεστ";
        var token = FeedHarness.Token(await FeedHarness.Html(coach, "/messages"));
        await coach.PostAsync($"/messages/{thread}/send", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["body"] = typed, ["__RequestVerificationToken"] = token,
        }));

        var html = await Html(coach, $"/messages/{thread}");

        Assert.Contains(typed, WebUtility.HtmlDecode(html));
        var greek = GreekLines(html, typed);
        Assert.True(greek.Count == 0, "Greek besides the typed message:\n" + string.Join("\n", greek));
        var seeded = Db(db => db.Set<Message>().Where(m => m.ConversationId == thread && m.BodyEn != null).Select(m => m.BodyEn!).ToList());
        Assert.NotEmpty(seeded);
        Assert.All(seeded, b => Assert.Contains(b, WebUtility.HtmlDecode(html)));
    }

    [Fact]
    public async Task Greek_pages_keep_the_greek_seed()
    {
        var thread = FeaturedCoachThread;
        var coach = await SignedIn("coach", "el");
        var messages = WebUtility.HtmlDecode(await Html(coach, $"/messages/{thread}"));
        var bodies = Db(db => db.Set<Message>().Where(m => m.ConversationId == thread).Select(m => new { m.Body, m.BodyEn }).ToList());
        Assert.All(bodies, b => Assert.Contains(b.Body, messages));
        Assert.All(bodies.Where(b => b.BodyEn != null), b => Assert.DoesNotContain(b.BodyEn!, messages));

        var slug = Config("Demo:Featured:AthleteSlug");
        var athlete = Db(db => db.Athletes.Include(a => a.Seasons).ThenInclude(s => s.Club).Single(a => a.Slug == slug));
        var greek = WebUtility.HtmlDecode(await Html(Client("el"), $"/p/{slug}"));
        var english = WebUtility.HtmlDecode(await Html(Client("en"), $"/p/{slug}"));
        Assert.Contains(athlete.Bio!, greek);
        Assert.Contains(athlete.BioEn!, english);
        var club = athlete.Seasons.First().Club!;   // club history
        Assert.Contains(club.Name, greek);
        Assert.Contains(club.NameEn!, english);
        Assert.Matches(@"\d+ο εκατοστημόριο", greek);
        Assert.Matches(@"\d+(st|nd|rd|th) percentile", english);
        Assert.DoesNotMatch(@"(?i)percentile \d", english);
    }

    [Fact]
    public async Task English_dates_use_one_format()
    {
        var coach = await SignedIn("coach", "en");
        var thread = VisibleText(await Html(coach, $"/messages/{FeaturedCoachThread}"));
        var separators = Regex.Matches(await Html(coach, $"/messages/{FeaturedCoachThread}"), "class=\"bp-msg-day\"[^>]*><span>([^<]+)</span>")
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
        Assert.NotEmpty(separators);
        Assert.All(separators, s => Assert.Matches(@"^(Today|Yesterday|[A-Z][a-z]+day, [A-Z][a-z]{2} \d{1,2}(, \d{4})?)$", s));
        Assert.Matches(@"Connected since [A-Z][a-z]{2} \d{1,2}\b", thread);

        var inbox = VisibleText(await Html(await SignedIn("athlete", "en"), "/me/inbox"));
        Assert.DoesNotMatch(@"\b\d{1,2}/\d{1,2}/\d{4}\b", inbox);
        Assert.Matches(@"\b[A-Z][a-z]{2} \d{1,2}, \d{4}\b", inbox);
    }

    // ---------- hashtags ----------

    private static List<(string Key, string Count, string Label)> Trends(string html)
    {
        var box = Regex.Match(html, "data-engage=\"trends\".*?</ol>", RegexOptions.Singleline).Value;
        return Regex.Matches(box, "data-tag-key=\"([^\"]+)\" data-tag-count=\"(\\d+)\">\\s*<a[^>]*>#([^<]+)</a>")
            .Select(m => (WebUtility.HtmlDecode(m.Groups[1].Value), m.Groups[2].Value, WebUtility.HtmlDecode(m.Groups[3].Value))).ToList();
    }

    [Fact]
    public async Task Trending_tags_have_the_same_keys_and_counts_in_both_languages()
    {
        var en = Trends(await Html(await SignedIn("coach", "en"), "/feed"));
        var el = Trends(await Html(await SignedIn("coach", "el"), "/feed"));

        Assert.NotEmpty(en);
        Assert.Equal(el.Select(t => (t.Key, t.Count)), en.Select(t => (t.Key, t.Count)));
        Assert.All(en, t => Assert.DoesNotMatch(Greek, t.Label));
        Assert.Contains(el, t => Greek.IsMatch(t.Label));
    }

    [Fact]
    public async Task English_and_greek_tag_filter_the_same_posts()
    {
        var client = await SignedIn("coach", "en");
        var english = await FeedHarness.AllPages(client, "/feed?tag=lookingforteam");
        var greek = await FeedHarness.AllPages(client, "/feed?tag=" + Uri.EscapeDataString("ψάχνωομάδα"));

        Assert.NotEmpty(greek);
        Assert.Equal(greek.Select(c => c.Id), english.Select(c => c.Id));
    }

    [Theory]
    [InlineData("#ψάχνωομάδα", "ψαχνωομαδα")]
    [InlineData("#lookingforteam", "ψαχνωομαδα")]
    [InlineData("#Stats", "στατιστικα")]
    [InlineData("#tryouts", "tryouts")]
    [InlineData("#κάτιάλλο", "κατιαλλο")]
    public void Tag_aliases_resolve_to_one_key(string typed, string key)
    {
        Assert.Equal(key, HashtagCatalog.Key(typed));
        Assert.Equal(key, PostTagParser.Parse(typed).Single().Tag);
    }

    [Fact]
    public void Tag_labels_follow_the_language()
    {
        using (new CultureScope("en"))
        {
            Assert.Equal("lookingforteam", HashtagCatalog.Label("ψαχνωομαδα", "ψάχνωομάδα"));
            Assert.Equal("stats", HashtagCatalog.Label("στατιστικα", "στατιστικά"));
            Assert.Equal("δικόμου", HashtagCatalog.Label("δικομου", "δικόμου"));
        }
        using (new CultureScope("el"))
        {
            Assert.Equal("ψάχνωομάδα", HashtagCatalog.Label("ψαχνωομαδα", "lookingforteam"));
            Assert.Equal("tryouts", HashtagCatalog.Label("tryouts", "tryouts"));
        }
    }

    // ---------- seed ----------

    private static readonly DateTimeOffset Ref = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private static void English(string? el, string? en, string what)
    {
        if (string.IsNullOrEmpty(el)) return;
        Assert.False(string.IsNullOrWhiteSpace(en), $"{what}: no English for «{el}»");
        Assert.False(Greek.IsMatch(en!), $"{what}: Greek in English «{en}»");
    }

    [Fact]
    public void Generator_writes_english_clubs_bios_and_notes()
    {
        var data = DemoDataGenerator.Generate(42);

        Assert.All(data.Clubs, c => English(c.Name, c.NameEn, "club"));
        Assert.All(data.Clubs, c => Assert.StartsWith(GreekNames.Cities.Single(x => x.Name == c.City).NameLatin + " ", c.NameEn));
        Assert.Equal(data.Clubs.Count, data.Clubs.Select(c => c.NameEn).Distinct().Count());
        Assert.All(data.Athletes, a => English(a.Bio, a.BioEn, "bio"));
        Assert.All(data.Interests, i => English(i.Note, i.NoteEn, "note"));
    }

    [Fact]
    public void Feed_and_engage_seeds_write_english()
    {
        var data = DemoDataGenerator.Generate(42);
        var n = 0;
        foreach (var c in data.Clubs) c.Id = ++n;
        n = 0;
        foreach (var c in data.Coaches) c.Id = ++n;
        n = 0;
        foreach (var a in data.Athletes) a.Id = ++n;
        var feed = FeedSeeder.Generate(data, Ref);
        var engage = EngageSeeder.Generate(data, feed.Posts, feed.Kudos, Ref);

        Assert.All(feed.Posts, p =>
        {
            English(p.Text, p.TextEn, "post text");
            English(p.Title, p.TitleEn, "title");
            English(p.Place, p.PlaceEn, "place");
            English(p.Positions, p.PositionsEn, "positions");
        });
        Assert.All(engage.Comments, c => English(c.Text, c.TextEn, "comment"));
        // Hashtag edits: the English text carries the English tags, which parse to the same keys as the Greek text.
        Assert.Equal(engage.TextEdits.Count, engage.TextEditsEn.Count);
        foreach (var ((post, text), (postEn, textEn)) in engage.TextEdits.Zip(engage.TextEditsEn))
        {
            Assert.Same(post, postEn);
            English(text, textEn, "tagged text");
            Assert.InRange(textEn.Length, 1, 280);
            Assert.Equal(PostTagParser.Parse(text).Select(t => t.Tag), PostTagParser.Parse(textEn).Select(t => t.Tag));
        }
    }

    [Fact]
    public void Message_seed_writes_english_bodies()
    {
        var featured = app.Services.GetRequiredService<IOptions<DemoOptions>>().Value.Featured;
        var data = DemoDataGenerator.Generate(42);
        InterestLifecycleSeeder.Apply(data.Interests, Ref, 42);
        var seed = MessagesSeeder.Build(data, Ref, featured, 42);
        var forbidden = new Regex("(?i)demo|seeded|fictional|@|https?://|\\{|\\}");

        Assert.All(seed.NewInterests, i => English(i.Note, i.NoteEn, "note"));
        Assert.All(seed.Conversations.SelectMany(c => c.Messages), m =>
        {
            English(m.Body, m.BodyEn, "message");
            Assert.False(forbidden.IsMatch(m.BodyEn!), m.BodyEn);
        });
        // Addressed by the athlete's Latin first name.
        Assert.Contains(seed.Conversations, c => c.Messages.Any(m => m.BodyEn!.Contains(c.Athlete!.FirstNameLatin + "!")));
    }

    // ---------- places ----------

    [Fact]
    public void Cities_use_english_exonyms()
    {
        Assert.Equal(
            ["Athens", "Piraeus", "Peristeri", "Thessaloniki", "Serres", "Patras", "Agrinio", "Heraklion", "Chania", "Larissa", "Volos", "Ioannina", "Komotini", "Rhodes"],
            GreekNames.Cities.Select(c => c.NameLatin));
    }

    public static TheoryData<string, string> CityNames()
    {
        var data = new TheoryData<string, string>();
        foreach (var c in GreekNames.Cities)
        {
            data.Add($"σέντερ από {c.Name}", c.Region);
            data.Add($"center from {c.NameLatin}", c.Region);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(CityNames))]
    public void Rule_parser_maps_greek_and_english_city_names(string query, string region)
    {
        var filters = new RuleParser().Parse(query);
        Assert.Equal(region, filters.Region);
    }

    // ---------- helpers ----------

    [Theory]
    [InlineData(1, "1st")] [InlineData(2, "2nd")] [InlineData(3, "3rd")] [InlineData(4, "4th")]
    [InlineData(11, "11th")] [InlineData(12, "12th")] [InlineData(13, "13th")]
    [InlineData(21, "21st")] [InlineData(22, "22nd")] [InlineData(23, "23rd")] [InlineData(82, "82nd")]
    [InlineData(100, "100th")] [InlineData(101, "101st")] [InlineData(111, "111th")] [InlineData(112, "112th")]
    public void Ordinals_in_english(int n, string expected)
    {
        using var _ = new CultureScope("en");
        Assert.Equal(expected, Ordinal.Format(n));
    }

    [Fact]
    public void Ordinals_in_greek_are_the_bare_number()
    {
        using var _ = new CultureScope("el");
        Assert.Equal("82", Ordinal.Format(82));
    }

    [Fact]
    public void Dates_in_english()
    {
        using var _ = new CultureScope("en");
        Assert.Equal("Nov 22, 2026", DisplayDate.Date(new DateTime(2026, 11, 22)));
        Assert.Equal("Nov 22, 2026", DisplayDate.Date(new DateTime(2026, 11, 22), greekFormat: "d"));
        Assert.Equal("Nov 22, 2026", DisplayDate.Date(new DateOnly(2026, 11, 22)));
        Assert.Equal("Sunday, Nov 1", DisplayDate.Day(new DateTime(2026, 11, 1), 2026));
        Assert.Equal("Saturday, Nov 1, 2025", DisplayDate.Day(new DateTime(2025, 11, 1), 2026));
        Assert.Equal("Nov 11", DisplayDate.MonthDay(new DateTime(2026, 11, 11)));
    }

    [Fact]
    public void Dates_in_greek_are_unchanged()
    {
        using var _ = new CultureScope("el");
        var d = new DateTime(2026, 11, 22);
        Assert.Equal(d.ToString("d MMM yyyy", CultureInfo.CurrentCulture), DisplayDate.Date(d));
        Assert.Equal(d.ToString("d", CultureInfo.CurrentCulture), DisplayDate.Date(d, greekFormat: "d"));
        Assert.Equal("Κυριακή 1 Νοεμβρίου", DisplayDate.Day(new DateTime(2026, 11, 1), 2026));
        Assert.Equal(new DateTime(2026, 11, 11).ToString("d MMM", CultureInfo.CurrentCulture), DisplayDate.MonthDay(new DateTime(2026, 11, 11)));
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo culture = CultureInfo.CurrentCulture, ui = CultureInfo.CurrentUICulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(name);

        public void Dispose() => (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, ui);
    }
}
