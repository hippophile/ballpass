using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Feed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-feed/specs/feed/spec.md
// Each test uses its own personas (rate limit is 1 post/minute per author) and its own posts, so test order does not matter.
public class FeedHarness(BallpassAppFactory app)
{
    public BallpassAppFactory App => app;

    public T Db<T>(Func<BallpassDbContext, T> query)
    {
        using var scope = app.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<BallpassDbContext>());
    }

    public static async Task<string> Html(HttpClient client, string path) =>
        WebUtility.HtmlDecode(await (await client.GetAsync(path)).Content.ReadAsStringAsync());

    public static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    /// <summary>Signs in by posting the login form directly (any demo id, not only those listed on /demo).</summary>
    public async Task<HttpClient> LoginAsync(string type, int id, string? culture = null)
    {
        var client = app.Client(culture);
        var token = Token(await Html(client, "/demo"));
        var response = await client.PostAsync("/demo/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["personaType"] = type, ["personaId"] = id.ToString(), ["returnUrl"] = "", ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    public int CoachId(int skip) => Db(db => db.Coaches.Where(c => c.IsDemo && c.Role == CoachRole.Head).OrderBy(c => c.Id).Skip(skip).Select(c => c.Id).First());

    /// <summary>Athletes taken from the end of the id range: not seed post authors (those are shuffled across the range, so check anyway).</summary>
    public int AthleteId(int skip) => Db(db =>
    {
        var authors = db.Set<Post>().Where(p => p.AuthorKind == AuthorKind.Athlete).Select(p => p.AuthorId).Distinct().ToList();
        return db.Athletes.Where(a => a.IsDemo && !authors.Contains(a.Id)).OrderByDescending(a => a.Id).Skip(skip).Select(a => a.Id).First();
    });

    public async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string> fields, bool withToken = true, string tokenPage = "/feed")
    {
        if (withToken) fields["__RequestVerificationToken"] = Token(await Html(client, tokenPage));
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    public static IEnumerable<(int Id, string Type, string AuthorKey, string Html)> Cards(string html) =>
        Regex.Matches(html, "<article[^>]*data-post-id=\"(\\d+)\"[^>]*data-post-type=\"(\\w+)\"[^>]*data-author-key=\"([^\"]+)\"[^>]*>(.*?)</article>", RegexOptions.Singleline)
            .Select(m => (int.Parse(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value));

    /// <summary>Follows "show more" links; returns every card of every page.</summary>
    public static async Task<List<(int Id, string Type, string AuthorKey, string Html)>> AllPages(HttpClient client, string first, int maxPages = 12)
    {
        var all = new List<(int, string, string, string)>();
        string? url = first;
        for (var i = 0; i < maxPages && url is not null; i++)
        {
            var html = await Html(client, url);
            var cards = Cards(html).ToList();
            Assert.InRange(cards.Count, 1, FeedLimits.PageSize);
            all.AddRange(cards);
            var more = Regex.Match(html, "<a[^>]*data-feed-more[^>]*href=\"([^\"]+)\"");
            if (!more.Success) more = Regex.Match(html, "<a[^>]*href=\"([^\"]+)\"[^>]*data-feed-more");
            url = more.Success ? more.Groups[1].Value : null;
        }
        return all;
    }
}

[Collection("db")]
public class FeedTests(BallpassAppFactory app)
{
    private readonly FeedHarness h = new(app);

    private static string Days(int n) => DateTime.UtcNow.AddDays(n).ToString("yyyy-MM-dd");

    // ---- seed ----

    private static DemoDataSet WithIds()
    {
        var data = DemoDataGenerator.Generate(42);
        var n = 0;
        foreach (var c in data.Clubs) c.Id = ++n;
        n = 0;
        foreach (var c in data.Coaches) c.Id = ++n;
        n = 0;
        foreach (var a in data.Athletes) a.Id = ++n;
        return data;
    }

    private static readonly DateTimeOffset Ref = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Seed_has_40_posts_with_the_planned_mix_authors_and_timing()
    {
        var data = WithIds();
        var seed = FeedSeeder.Generate(data, Ref);

        Assert.Equal(40, seed.Posts.Count);
        var mix = seed.Posts.GroupBy(p => p.Type).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(8, mix[PostType.LookingForTeam]);
        Assert.Equal(6, mix[PostType.TryoutCall]);
        Assert.Equal(10, mix[PostType.StatUpdate]);
        Assert.Equal(6, mix[PostType.Highlight]);
        Assert.Equal(4, mix[PostType.Badge]);
        Assert.Equal(6, mix[PostType.FreeText]);

        var authors = seed.Posts.Select(p => (p.AuthorKind, p.AuthorId)).Distinct().ToList();
        Assert.Equal(25, authors.Count);
        Assert.Equal(18, authors.Count(a => a.AuthorKind == AuthorKind.Athlete));
        Assert.Equal(5, authors.Count(a => a.AuthorKind == AuthorKind.Coach));
        Assert.Equal(2, authors.Count(a => a.AuthorKind == AuthorKind.Club));
        Assert.All(seed.Posts.Where(p => p.AuthorKind == AuthorKind.Club), p => Assert.NotNull(p.PostedByCoachId));

        var ages = seed.Posts.Select(p => Ref - p.CreatedAt).ToList();
        Assert.Equal(6, ages.Count(a => a < TimeSpan.FromHours(24)));
        Assert.Equal(14, ages.Count(a => a >= TimeSpan.FromDays(2) && a <= TimeSpan.FromDays(7)));
        Assert.Equal(20, ages.Count(a => a >= TimeSpan.FromDays(8) && a <= TimeSpan.FromDays(30)));
        Assert.All(ages, a => Assert.True(a >= TimeSpan.FromMinutes(30), "newest seed post must be older than the rate-limit window"));
        Assert.All(seed.Posts, p => Assert.True(p.IsDemo));
    }

    [Fact]
    public void Seed_authors_are_adults_and_posts_follow_type_rules()
    {
        var data = WithIds();
        var seed = FeedSeeder.Generate(data, Ref);
        var athletes = data.Athletes.ToDictionary(a => a.Id);

        foreach (var p in seed.Posts.Where(p => p.AuthorKind == AuthorKind.Athlete))
            Assert.True(Safeguards.IsAdult(athletes[p.AuthorId].BirthYear, Ref));
        Assert.All(seed.Posts.Where(p => p.Type is PostType.StatUpdate or PostType.Badge), p => Assert.True(p.IsSystem));
        Assert.All(seed.Posts.Where(p => p.Type == PostType.StatUpdate), p => { Assert.NotNull(p.Source); Assert.NotNull(p.Points); });
        Assert.All(seed.Posts.Where(p => p.Type == PostType.TryoutCall), p => { Assert.NotNull(p.ClubId); Assert.NotNull(p.EventDate); Assert.NotNull(p.Deadline); });
        Assert.All(seed.Posts.Where(p => p.Type == PostType.LookingForTeam), p => Assert.True(athletes[p.AuthorId].LookingForTeam));
        Assert.All(seed.Posts, p => Assert.True((p.Text?.Length ?? 0) <= FeedLimits.TextMax));
        Assert.All(seed.Posts.Where(p => p.Text is not null), p => Assert.DoesNotContain("http", p.Text!));
    }

    [Fact]
    public void Seed_has_45_kudos_on_25_posts_max_5_each_and_follows_for_head_coaches()
    {
        var data = WithIds();
        var seed = FeedSeeder.Generate(data, Ref);

        Assert.Equal(45, seed.Kudos.Count);
        var perPost = seed.Kudos.GroupBy(k => k.Post!).Select(g => g.Count()).ToList();
        Assert.Equal(25, perPost.Count); // 15 of the 40 posts have none
        Assert.InRange(perPost.Max(), 1, 5);
        Assert.DoesNotContain(seed.Kudos, k => k.ActorKey == k.Post!.AuthorKey); // nobody gives kudos to their own post
        Assert.Equal(seed.Kudos.Count, seed.Kudos.Select(k => (k.Post, k.ActorKey)).Distinct().Count());

        foreach (var s in data.Shortlist)
            Assert.Contains(seed.Follows, f => f.FollowerKey == $"coach:{s.Coach!.Id}" && f.TargetKey == $"athlete:{s.Athlete!.Id}");
        Assert.Equal(seed.Follows.Count, seed.Follows.Select(f => (f.FollowerKey, f.TargetKey)).Distinct().Count());
        Assert.DoesNotContain(seed.Follows, f => f.FollowerKey == f.TargetKey);
    }

    [Fact]
    public void Seed_is_deterministic_and_shifts_with_the_reference_time()
    {
        var a = FeedSeeder.Generate(WithIds(), Ref);
        var b = FeedSeeder.Generate(WithIds(), Ref);
        var c = FeedSeeder.Generate(WithIds(), Ref.AddDays(10));

        Assert.Equal(a.Posts.Select(p => (p.AuthorKey, p.Type, p.Text, p.CreatedAt)), b.Posts.Select(p => (p.AuthorKey, p.Type, p.Text, p.CreatedAt)));
        Assert.Equal(a.Posts.Select(p => p.CreatedAt.AddDays(10)), c.Posts.Select(p => p.CreatedAt));
    }

    [Fact]
    public void Reset_persists_the_seed()
    {
        Assert.True(h.Db(db => db.Set<Post>().Count(p => p.IsDemo)) >= 40);
        Assert.True(h.Db(db => db.Set<Kudos>().Count()) >= 45);
        Assert.True(h.Db(db => db.Set<Follow>().Count()) > 0);
    }

    // ---- rendering ----

    [Fact]
    public async Task Anonymous_discover_renders_every_post_type_with_author_chip_time_and_no_demo_label()
    {
        var cards = await FeedHarness.AllPages(app.Client(), "/feed");

        foreach (var type in Enum.GetNames<PostType>())
            Assert.Contains(cards, c => c.Type == type);
        Assert.All(cards, c =>
        {
            Assert.Contains("bp-feed-author", c.Html);
            Assert.Contains("bp-feed-avatar", c.Html);
            Assert.Contains("<time", c.Html);
            Assert.DoesNotContain("data-chip=\"demo\"", c.Html);   // flow-release O1
            Assert.DoesNotMatch("(?i)\\(demo\\)|\\(δείγμα\\)|δείγματος", c.Html);
            Assert.DoesNotContain("data-feed-action=\"kudos\"", c.Html); // anonymous is read-only
            Assert.DoesNotContain("bp-feed-menu", c.Html);
        });
    }

    [Fact]
    public async Task Cards_show_type_specific_fields()
    {
        var cards = await FeedHarness.AllPages(app.Client(), "/feed");

        var lft = cards.First(c => c.Type == "LookingForTeam").Html;
        Assert.Contains("data-field=\"position\"", lft);
        Assert.Contains("data-field=\"region\"", lft);
        Assert.Contains("data-field=\"available\"", lft);

        var tryout = cards.First(c => c.Type == "TryoutCall").Html;
        foreach (var f in new[] { "date", "place", "positions", "deadline" })
            Assert.Contains($"data-field=\"{f}\"", tryout);

        var stat = cards.First(c => c.Type == "StatUpdate").Html;
        Assert.Contains("data-source=", stat); // SourceBadge
        foreach (var f in new[] { "season", "games", "points", "rebounds", "assists" })
            Assert.Contains($"data-field=\"{f}\"", stat);

        // flow-release §4.3: a highlight always shows a real picture (its photos or the feed-pool stock photo), never generated art.
        var highlight = cards.First(c => c.Type == "Highlight").Html;
        Assert.Matches("<img\\b[^>]*src=\"/media/[^\"]+\"[^>]*alt=\"[^\"]{5,}\"", highlight);
        Assert.DoesNotContain("bp-feed-art", highlight);
        Assert.DoesNotContain("video/gfx-", highlight);

        Assert.Contains("data-field=\"badge\"", cards.First(c => c.Type == "Badge").Html);
        Assert.Contains("data-field=\"text\"", cards.First(c => c.Type == "FreeText").Html);
    }

    [Fact]
    public async Task Relative_time_is_localized()
    {
        var el = await FeedHarness.Html(app.Client("el"), "/feed");
        var en = await FeedHarness.Html(app.Client("en"), "/feed");

        Assert.Contains("πριν", el);
        Assert.Contains(" ago", en);
        Assert.Contains("Για εσάς", el);
        Assert.Contains("For you", en);
    }

    [Fact]
    public async Task Nav_link_is_in_both_menus_and_home_shows_a_three_post_preview()
    {
        var html = await FeedHarness.Html(app.Client(), "/");

        Assert.True(Regex.Matches(html, "<a[^>]*href=\"/feed\"").Count >= 2);
        Assert.Contains("Τι γίνεται τώρα", html);
        var preview = Regex.Match(html, "<section[^>]*bp-feed-preview.*?</section>", RegexOptions.Singleline).Value;
        Assert.Equal(3, Regex.Matches(preview, "data-post-id=\"").Count);
        Assert.DoesNotContain("<form", preview);
    }

    // ---- pagination ----

    [Fact]
    public async Task Show_more_pages_have_no_duplicates_and_stop_at_the_end()
    {
        var cards = await FeedHarness.AllPages(app.Client(), "/feed");

        Assert.Equal(cards.Count, cards.Select(c => c.Id).Distinct().Count());
        Assert.InRange(cards.Count, 40, FeedLimits.MaxItems);
        // Newest first overall: SortAt descending is reflected in strictly unique ids; spot-check the first page leads with a fresh post.
    }

    [Fact]
    public async Task Cursor_is_capped_at_60_posts()
    {
        var far = new DateTimeOffset(9000, 1, 1, 0, 0, 0, TimeSpan.Zero).UtcTicks;
        var html = await FeedHarness.Html(app.Client(), $"/feed?before={far}.{int.MaxValue}.55");

        Assert.Equal(5, FeedHarness.Cards(html).Count());
        Assert.DoesNotContain("data-feed-more", html);
    }

    [Fact]
    public async Task Garbage_cursor_does_not_break_the_page()
    {
        var response = await app.Client().GetAsync("/feed?before=not-a-cursor");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- composer permissions ----

    [Fact]
    public async Task Composer_shows_role_specific_forms_only_when_signed_in()
    {
        var athlete = await h.LoginAsync("athlete", h.AthleteId(0));
        var coach = await h.LoginAsync("coach", h.CoachId(0));

        var a = await FeedHarness.Html(athlete, "/feed");
        Assert.Contains("data-feed-compose=\"freetext\"", a);
        Assert.Contains("data-feed-compose=\"lft\"", a);
        Assert.Contains("data-feed-compose=\"highlight\"", a);
        Assert.DoesNotContain("data-feed-compose=\"tryout\"", a);

        var c = await FeedHarness.Html(coach, "/feed");
        Assert.Contains("data-feed-compose=\"freetext\"", c);
        Assert.Contains("data-feed-compose=\"tryout\"", c);
        Assert.DoesNotContain("data-feed-compose=\"lft\"", c);
        Assert.DoesNotContain("data-feed-compose=\"highlight\"", c);

        Assert.DoesNotContain("data-feed-compose", await FeedHarness.Html(app.Client(), "/feed"));
    }

    [Fact]
    public async Task Wrong_role_or_system_type_is_403_and_stores_nothing()
    {
        var athlete = await h.LoginAsync("athlete", h.AthleteId(1));
        var coach = await h.LoginAsync("coach", h.CoachId(1));
        var before = h.Db(db => db.Set<Post>().Count());

        Assert.Equal(HttpStatusCode.Forbidden, (await h.PostAsync(athlete, "/feed/post", new() { ["type"] = "TryoutCall", ["place"] = "Γήπεδο", ["eventDate"] = Days(10), ["deadline"] = Days(6), ["positions"] = "PG" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.PostAsync(coach, "/feed/post", new() { ["type"] = "Highlight", ["title"] = "Καλάθι" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.PostAsync(coach, "/feed/post", new() { ["type"] = "LookingForTeam", ["lft"] = "on" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.PostAsync(athlete, "/feed/post", new() { ["type"] = "Badge", ["text"] = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.PostAsync(coach, "/feed/post", new() { ["type"] = "StatUpdate", ["text"] = "x" })).StatusCode);
        Assert.Equal(before, h.Db(db => db.Set<Post>().Count()));
    }

    [Fact]
    public async Task Anonymous_post_is_403()
    {
        var anon = app.Client();
        var token = FeedHarness.Token(await FeedHarness.Html(anon, "/demo"));

        var response = await anon.PostAsync("/feed/post", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["type"] = "FreeText", ["text"] = "hello", ["__RequestVerificationToken"] = token }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Athlete_free_text_is_posted_with_urls_stripped_and_appears_on_page_one()
    {
        var id = h.AthleteId(2);
        var athlete = await h.LoginAsync("athlete", id);

        var response = await h.PostAsync(athlete, "/feed/post", new() { ["type"] = "FreeText", ["text"] = "Σήμερα προπόνηση! Δείτε https://evil.example/x και www.spam.gr τώρα" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var post = h.Db(db => db.Set<Post>().Single(p => p.AuthorKey == $"athlete:{id}"));
        Assert.Equal(PostType.FreeText, post.Type);
        Assert.DoesNotContain("evil.example", post.Text);
        Assert.DoesNotContain("www.", post.Text);
        Assert.Contains("Σήμερα προπόνηση!", post.Text);
        var cards = FeedHarness.Cards(await FeedHarness.Html(athlete, "/feed")).ToList();
        var mine = cards.First(c => c.Id == post.Id); // on page 1 (a boosted looking-for-team post may rank above it)
        Assert.DoesNotContain("evil.example", mine.Html);
    }

    [Fact]
    public async Task Free_text_over_280_chars_is_rejected()
    {
        var id = h.AthleteId(3);
        var athlete = await h.LoginAsync("athlete", id);

        var response = await h.PostAsync(athlete, "/feed/post", new() { ["type"] = "FreeText", ["text"] = new string('α', 281) });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("status=too-long", response.Headers.Location!.ToString());
        Assert.Equal(0, h.Db(db => db.Set<Post>().Count(p => p.AuthorKey == $"athlete:{id}")));
    }

    [Fact]
    public async Task Coach_posts_a_tryout_call_for_the_own_club()
    {
        var coachId = h.CoachId(2);
        var coach = await h.LoginAsync("coach", coachId);

        var response = await h.PostAsync(coach, "/feed/post", new()
        {
            ["type"] = "TryoutCall", ["eventDate"] = Days(20), ["place"] = "Κλειστό Γυμναστήριο Δείγμα", ["positions"] = "PG, SG", ["deadline"] = Days(16),
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var post = h.Db(db => db.Set<Post>().Single(p => p.AuthorKey == $"coach:{coachId}"));
        Assert.Equal(PostType.TryoutCall, post.Type);
        Assert.Equal(h.Db(db => db.Coaches.Where(c => c.Id == coachId).Select(c => c.ClubId).Single()), post.ClubId);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)), post.EventDate);
        Assert.Contains("Κλειστό Γυμναστήριο Δείγμα", await FeedHarness.Html(coach, "/feed"));
    }

    [Fact]
    public async Task Tryout_without_required_fields_is_rejected()
    {
        var coachId = h.CoachId(3);
        var coach = await h.LoginAsync("coach", coachId);

        var response = await h.PostAsync(coach, "/feed/post", new() { ["type"] = "TryoutCall", ["place"] = "" });

        Assert.Contains("status=invalid", response.Headers.Location!.ToString());
        Assert.Equal(0, h.Db(db => db.Set<Post>().Count(p => p.AuthorKey == $"coach:{coachId}")));
    }

    [Fact]
    public async Task Looking_for_team_toggle_posts_and_switches_the_profile_flag()
    {
        var id = h.AthleteId(4);
        var athlete = await h.LoginAsync("athlete", id);

        await h.PostAsync(athlete, "/feed/post", new() { ["type"] = "LookingForTeam", ["lft"] = "on", ["position"] = "PG", ["region"] = "Αττική", ["availableFrom"] = Days(10), ["text"] = "Ψάχνω ομάδα για την επόμενη περίοδο" });

        var post = h.Db(db => db.Set<Post>().Single(p => p.AuthorKey == $"athlete:{id}"));
        Assert.Equal(PostType.LookingForTeam, post.Type);
        Assert.Equal(Position.PG, post.Position);
        Assert.True(h.Db(db => db.Athletes.Single(a => a.Id == id).LookingForTeam));

        await h.PostAsync(athlete, "/feed/post", new() { ["type"] = "LookingForTeam", ["lft"] = "off" });

        Assert.False(h.Db(db => db.Athletes.Single(a => a.Id == id).LookingForTeam));
        Assert.Equal(1, h.Db(db => db.Set<Post>().Count(p => p.AuthorKey == $"athlete:{id}")));
    }

    [Fact]
    public async Task Highlight_post_is_stored_with_title()
    {
        var id = h.AthleteId(5);
        var athlete = await h.LoginAsync("athlete", id);

        await h.PostAsync(athlete, "/feed/post", new() { ["type"] = "Highlight", ["title"] = "Buzzer beater από τη γωνία" });

        var post = h.Db(db => db.Set<Post>().Single(p => p.AuthorKey == $"athlete:{id}"));
        Assert.Equal(PostType.Highlight, post.Type);
        Assert.Equal("Buzzer beater από τη γωνία", post.Title);
        Assert.Contains("Buzzer beater από τη γωνία", await FeedHarness.Html(athlete, "/feed"));
    }

    [Fact]
    public async Task Second_post_within_a_minute_is_rate_limited()
    {
        var id = h.AthleteId(6);
        var athlete = await h.LoginAsync("athlete", id);

        var first = await h.PostAsync(athlete, "/feed/post", new() { ["type"] = "FreeText", ["text"] = "Πρώτη ανάρτηση" });
        var second = await h.PostAsync(athlete, "/feed/post", new() { ["type"] = "FreeText", ["text"] = "Δεύτερη ανάρτηση" });

        Assert.Contains("status=posted", first.Headers.Location!.ToString());
        Assert.Contains("status=rate", second.Headers.Location!.ToString());
        Assert.Equal(1, h.Db(db => db.Set<Post>().Count(p => p.AuthorKey == $"athlete:{id}")));
    }

    [Theory]
    [InlineData("/feed/post")]
    [InlineData("/feed/kudos")]
    [InlineData("/feed/follow")]
    [InlineData("/feed/unfollow")]
    [InlineData("/feed/report")]
    [InlineData("/feed/hide")]
    [InlineData("/feed/block")]
    public async Task Post_without_antiforgery_token_is_400(string path)
    {
        var athlete = await h.LoginAsync("athlete", h.AthleteId(7));

        var response = await h.PostAsync(athlete, path, new() { ["postId"] = "1", ["on"] = "1", ["type"] = "FreeText", ["text"] = "x", ["targetKey"] = "coach:1" }, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- minors ----

    [Theory]
    [InlineData(2007, 2026, true)]
    [InlineData(2008, 2026, false)]
    [InlineData(2012, 2026, false)]
    [InlineData(1995, 2026, true)]
    public void IsAdult_is_conservative(int birthYear, int year, bool expected) =>
        Assert.Equal(expected, Safeguards.IsAdult(birthYear, new DateTimeOffset(year, 6, 1, 0, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Service_rejects_an_author_under_18_and_stores_nothing()
    {
        await using var db = await UnitDb.CreateAsync();
        var club = new Club { Name = "Test (demo)", City = "Αθήνα", Region = "Αττική", Gender = Gender.Female, League = League.A1, IsDemo = true };
        var minor = new Athlete { Slug = "minor-x", FirstName = "Α", LastName = "Β", FirstNameLatin = "A", LastNameLatin = "B", BirthYear = 2012, City = "Αθήνα", Region = "Αττική", IsDemo = true };
        var adult = new Athlete { Slug = "adult-x", FirstName = "Γ", LastName = "Δ", FirstNameLatin = "G", LastNameLatin = "D", BirthYear = 1998, City = "Αθήνα", Region = "Αττική", IsDemo = true };
        db.AddRange(club, minor, adult);
        await db.SaveChangesAsync();
        var service = new FeedService(db, new UnitDb.FixedTime(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero)), Options.Create(new FeedOptions()));

        var rejected = await service.CreateAsync(new FeedViewer($"athlete:{minor.Id}", "Athlete", minor.Id, null), new NewPost(PostType.FreeText) { Text = "Γεια σε όλους" });
        var accepted = await service.CreateAsync(new FeedViewer($"athlete:{adult.Id}", "Athlete", adult.Id, null), new NewPost(PostType.FreeText) { Text = "Γεια σε όλους" });

        Assert.Equal(PostOutcome.Minor, rejected);
        Assert.Equal(PostOutcome.Posted, accepted);
        Assert.Equal(0, await db.Set<Post>().CountAsync(p => p.AuthorKey == $"athlete:{minor.Id}"));
        Assert.Equal(1, await db.Set<Post>().CountAsync(p => p.AuthorKey == $"athlete:{adult.Id}"));
    }

    // ---- kudos ----

    private (int PostId, string AuthorKey, int Count) SeedPostWithKudos(int skip) => h.Db(db =>
    {
        var row = db.Set<Kudos>().GroupBy(k => k.PostId).Select(g => new { PostId = g.Key, Count = g.Count() })
            .OrderBy(x => x.PostId).Skip(skip).First();
        return (row.PostId, db.Set<Post>().Where(p => p.Id == row.PostId).Select(p => p.AuthorKey).First(), row.Count);
    });

    [Fact]
    public async Task Kudos_is_idempotent_and_toggles_off()
    {
        var (postId, _, _) = SeedPostWithKudos(0);
        var actorId = h.AthleteId(8);
        var actor = await h.LoginAsync("athlete", actorId);
        int Count() => h.Db(db => db.Set<Kudos>().Count(k => k.PostId == postId && k.ActorKey == $"athlete:{actorId}"));

        await h.PostAsync(actor, "/feed/kudos", new() { ["postId"] = postId.ToString(), ["on"] = "1" });
        await h.PostAsync(actor, "/feed/kudos", new() { ["postId"] = postId.ToString(), ["on"] = "1" });
        Assert.Equal(1, Count());
        Assert.Contains("aria-pressed=\"true\"", FeedHarness.Cards(await FindOnPages(actor, postId)).First(c => c.Id == postId).Html);

        await h.PostAsync(actor, "/feed/kudos", new() { ["postId"] = postId.ToString(), ["on"] = "0" });
        await h.PostAsync(actor, "/feed/kudos", new() { ["postId"] = postId.ToString(), ["on"] = "0" });
        Assert.Equal(0, Count());
    }

    [Fact]
    public async Task Kudos_count_is_public_but_only_others_can_give_kudos()  // REVERSED by wow-engage: it used to be visible to the author only
    {
        var (postId, authorKey, count) = SeedPostWithKudos(1);
        Assert.True(FeedKeys.TryParse(authorKey, out var kind, out var authorId));
        var author = await h.LoginAsync(kind == AuthorKind.Coach ? "coach" : "athlete", kind == AuthorKind.Club ? h.Db(db => db.Coaches.First(c => c.ClubId == authorId && c.Role == CoachRole.Head).Id) : authorId);
        var other = await h.LoginAsync("coach", h.CoachId(4));

        var own = FeedHarness.Cards(await FindOnPages(author, postId)).First(c => c.Id == postId).Html;
        var foreign = FeedHarness.Cards(await FindOnPages(other, postId)).First(c => c.Id == postId).Html;

        Assert.Contains($"data-kudos-count=\"{count}\"", own);
        Assert.Contains($"data-kudos-count=\"{count}\"", foreign);
        Assert.DoesNotContain("data-feed-action=\"kudos\"", own); // no kudos on your own post
    }

    private static async Task<string> FindOnPages(HttpClient client, int postId)
    {
        string? url = "/feed";
        for (var i = 0; i < 8 && url is not null; i++)
        {
            var html = await FeedHarness.Html(client, url);
            if (FeedHarness.Cards(html).Any(c => c.Id == postId)) return html;
            url = Regex.Match(html, "<a[^>]*data-feed-more[^>]*href=\"([^\"]+)\"").Groups[1].Value is { Length: > 0 } u ? u : null;
        }
        throw new Xunit.Sdk.XunitException($"post {postId} not found on any page");
    }

    // ---- follow ----

    [Fact]
    public async Task Follow_and_unfollow_are_idempotent_and_show_on_cards()
    {
        var coachId = h.CoachId(5);
        var coach = await h.LoginAsync("coach", coachId);
        var target = h.Db(db => db.Set<Post>().Where(p => p.AuthorKind == AuthorKind.Athlete).OrderBy(p => p.Id).Select(p => p.AuthorKey).First());
        int Count() => h.Db(db => db.Set<Follow>().Count(f => f.FollowerKey == $"coach:{coachId}" && f.TargetKey == target));
        h.Db(db => db.Set<Follow>().Where(f => f.FollowerKey == $"coach:{coachId}" && f.TargetKey == target).ExecuteDelete());

        await h.PostAsync(coach, "/feed/follow", new() { ["targetKey"] = target });
        await h.PostAsync(coach, "/feed/follow", new() { ["targetKey"] = target });
        Assert.Equal(1, Count());
        var targetPost = h.Db(db => db.Set<Post>().Where(p => p.AuthorKey == target).OrderBy(p => p.Id).Select(p => p.Id).First());
        Assert.Contains("data-feed-action=\"unfollow\"", FeedHarness.Cards(await FindOnPages(coach, targetPost)).First(c => c.Id == targetPost).Html);

        await h.PostAsync(coach, "/feed/unfollow", new() { ["targetKey"] = target });
        await h.PostAsync(coach, "/feed/unfollow", new() { ["targetKey"] = target });
        Assert.Equal(0, Count());
    }

    [Fact]
    public async Task Athlete_profile_has_a_follow_button_for_signed_in_personas_only()
    {
        var slug = h.Db(db => db.Athletes.Where(a => a.IsDemo).OrderBy(a => a.Id).Select(a => a.Slug).First());
        var coach = await h.LoginAsync("coach", h.CoachId(6));

        Assert.Contains("data-feed-action=\"follow\"", await FeedHarness.Html(coach, $"/p/{slug}"));
        Assert.DoesNotContain("data-feed-action", await FeedHarness.Html(app.Client(), $"/p/{slug}"));
    }

    [Fact]
    public async Task Shortlisting_an_athlete_creates_a_follow()
    {
        var coachId = h.CoachId(7);
        var athleteId = h.Db(db => db.Athletes.Where(a => a.IsDemo && !db.ShortlistEntries.Any(s => s.AthleteId == a.Id)).OrderByDescending(a => a.Id).Select(a => a.Id).First());
        var slug = h.Db(db => db.Athletes.Where(a => a.Id == athleteId).Select(a => a.Slug).Single());
        var coach = await h.LoginAsync("coach", coachId);

        var response = await h.PostAsync(coach, $"/p/{slug}/shortlist", new(), tokenPage: $"/p/{slug}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, h.Db(db => db.Set<Follow>().Count(f => f.FollowerKey == $"coach:{coachId}" && f.TargetKey == $"athlete:{athleteId}")));
    }

    [Fact]
    public async Task Following_tab_shows_only_followed_authors()
    {
        var coachId = h.Db(db => db.Set<Follow>().Where(f => f.FollowerKey.StartsWith("coach:") && db.Set<Post>().Any(p => p.AuthorKey == f.TargetKey))
            .Select(f => f.FollowerKey).First()).Split(':')[1];
        var followed = h.Db(db => db.Set<Follow>().Where(f => f.FollowerKey == $"coach:{coachId}").Select(f => f.TargetKey).ToList());
        var coach = await h.LoginAsync("coach", int.Parse(coachId));

        var cards = await FeedHarness.AllPages(coach, "/feed?tab=following");

        Assert.NotEmpty(cards);
        Assert.All(cards, c => Assert.Contains(c.AuthorKey, followed));
    }

    [Fact]
    public async Task Empty_following_tab_shows_a_hint_instead_of_cards()
    {
        var athlete = await h.LoginAsync("athlete", h.AthleteId(9));

        var html = await FeedHarness.Html(athlete, "/feed?tab=following");

        Assert.Empty(FeedHarness.Cards(html));
        Assert.Contains("data-feed-empty", html);
    }

    // ---- safeguards ----

    private (int PostId, string AuthorKey) AthletePost(int skip) => h.Db(db =>
    {
        var p = db.Set<Post>().Where(x => x.AuthorKind == AuthorKind.Athlete).OrderByDescending(x => x.Id).Skip(skip).First(); // from the end: kudos tests read the lowest ids
        return (p.Id, p.AuthorKey);
    });

    [Fact]
    public async Task Hide_removes_the_post_for_that_persona_only()
    {
        var (postId, _) = AthletePost(0);
        var coach = await h.LoginAsync("coach", h.CoachId(8));
        Assert.Contains(postId, FeedHarness.Cards(await FindOnPages(coach, postId)).Select(c => c.Id));

        await h.PostAsync(coach, "/feed/hide", new() { ["postId"] = postId.ToString() });

        var all = await FeedHarness.AllPages(coach, "/feed");
        Assert.DoesNotContain(all, c => c.Id == postId);
        Assert.Contains(await FeedHarness.AllPages(app.Client(), "/feed"), c => c.Id == postId);
    }

    [Fact]
    public async Task Report_hides_for_the_reporter_at_once_and_three_reports_hide_for_everyone()
    {
        var (postId, _) = AthletePost(1);
        var reporters = new[] { 9, 10, 11 };

        for (var i = 0; i < reporters.Length; i++)
        {
            var coach = await h.LoginAsync("coach", h.CoachId(reporters[i]));
            await h.PostAsync(coach, "/feed/report", new() { ["postId"] = postId.ToString(), ["reason"] = "spam" });

            Assert.DoesNotContain(await FeedHarness.AllPages(coach, "/feed"), c => c.Id == postId); // reporter: at once
            var visibleToAnon = (await FeedHarness.AllPages(app.Client(), "/feed")).Any(c => c.Id == postId);
            Assert.Equal(i < 2, visibleToAnon); // after the 3rd distinct report: hidden for everyone
        }
        Assert.Equal(3, h.Db(db => db.Set<Report>().Count(r => r.PostId == postId)));
        Assert.True(h.Db(db => db.Set<Post>().Single(p => p.Id == postId).IsHidden));
    }

    [Fact]
    public async Task Same_persona_reporting_twice_counts_once_and_unknown_reason_is_rejected()
    {
        var (postId, _) = AthletePost(2);
        var coach = await h.LoginAsync("coach", h.CoachId(12));

        await h.PostAsync(coach, "/feed/report", new() { ["postId"] = postId.ToString(), ["reason"] = "nonsense" });
        Assert.Equal(0, h.Db(db => db.Set<Report>().Count(r => r.PostId == postId)));
        await h.PostAsync(coach, "/feed/report", new() { ["postId"] = postId.ToString(), ["reason"] = "fake" });
        await h.PostAsync(coach, "/feed/report", new() { ["postId"] = postId.ToString(), ["reason"] = "fake" });

        Assert.Equal(1, h.Db(db => db.Set<Report>().Count(r => r.PostId == postId)));
        Assert.False(h.Db(db => db.Set<Post>().Single(p => p.Id == postId).IsHidden));
    }

    [Fact]
    public async Task Block_removes_both_from_each_others_feeds_and_follows()
    {
        var (postId, authorKey) = AthletePost(3);
        Assert.True(FeedKeys.TryParse(authorKey, out _, out var authorId));
        var coachId = h.CoachId(13);
        var coachKey = $"coach:{coachId}";
        var coach = await h.LoginAsync("coach", coachId);
        var author = await h.LoginAsync("athlete", authorId);
        // The coach posts something the author could see, and follows the author; the author follows back.
        await h.PostAsync(coach, "/feed/post", new() { ["type"] = "FreeText", ["text"] = "Μήνυμα του προπονητή πριν το μπλοκ" });
        var coachPostId = h.Db(db => db.Set<Post>().Single(p => p.AuthorKey == coachKey).Id);
        await h.PostAsync(coach, "/feed/follow", new() { ["targetKey"] = authorKey });
        await h.PostAsync(author, "/feed/follow", new() { ["targetKey"] = coachKey });
        Assert.Contains(await FeedHarness.AllPages(author, "/feed"), c => c.Id == coachPostId);

        await h.PostAsync(coach, "/feed/block", new() { ["postId"] = postId.ToString() });

        Assert.DoesNotContain(await FeedHarness.AllPages(coach, "/feed"), c => c.AuthorKey == authorKey);
        Assert.DoesNotContain(await FeedHarness.AllPages(author, "/feed"), c => c.AuthorKey == coachKey);
        Assert.Equal(0, h.Db(db => db.Set<Follow>().Count(f => (f.FollowerKey == coachKey && f.TargetKey == authorKey) || (f.FollowerKey == authorKey && f.TargetKey == coachKey))));
        Assert.Contains(await FeedHarness.AllPages(app.Client(), "/feed"), c => c.Id == postId); // others still see the author
        // A blocked pair cannot re-follow.
        await h.PostAsync(coach, "/feed/follow", new() { ["targetKey"] = authorKey });
        Assert.Equal(0, h.Db(db => db.Set<Follow>().Count(f => f.FollowerKey == coachKey && f.TargetKey == authorKey)));
    }

    // ---- express interest ----

    [Fact]
    public async Task Coach_sees_the_interest_form_and_profile_link_on_looking_for_team_cards_only()
    {
        var coach = await h.LoginAsync("coach", h.CoachId(14));
        var athlete = await h.LoginAsync("athlete", h.AthleteId(10));
        var anon = app.Client();

        var coachCards = await FeedHarness.AllPages(coach, "/feed");
        foreach (var c in coachCards.Where(c => c.Type == "LookingForTeam"))
        {
            Assert.Matches("<form[^>]*action=\"/p/[^\"/]+/interest\"[^>]*data-feed-action=\"interest\"|<form[^>]*data-feed-action=\"interest\"[^>]*action=\"/p/[^\"/]+/interest\"", c.Html);
            Assert.Contains("__RequestVerificationToken", c.Html);
            Assert.Matches("href=\"/p/[^\"/]+\"", c.Html);
        }
        Assert.DoesNotContain(coachCards.Where(c => c.Type != "LookingForTeam"), c => c.Html.Contains("data-feed-action=\"interest\""));
        foreach (var other in new[] { athlete, anon })
            Assert.DoesNotContain("data-feed-action=\"interest\"", string.Concat((await FeedHarness.AllPages(other, "/feed")).Select(c => c.Html)));
    }

    [Fact]
    public async Task Interest_form_on_the_card_posts_to_the_existing_endpoint()
    {
        var coach = await h.LoginAsync("coach", h.CoachId(15));
        var page = await FeedHarness.Html(coach, "/feed");
        var card = FeedHarness.Cards(page).First(c => c.Type == "LookingForTeam");
        var form = Regex.Match(card.Html, "<form[^>]*data-feed-action=\"interest\".*?</form>", RegexOptions.Singleline).Value;
        var action = Regex.Match(form, "action=\"([^\"]+)\"").Groups[1].Value;
        var token = FeedHarness.Token(form);

        var response = await coach.PostAsync(action, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("status=interest", response.Headers.Location!.ToString());
    }

    // ---- isolation ----

    [Fact]
    public async Task Moderation_and_actions_on_unknown_posts_are_404_or_noop()
    {
        var coach = await h.LoginAsync("coach", h.CoachId(16));

        var report = await h.PostAsync(coach, "/feed/report", new() { ["postId"] = "999999", ["reason"] = "spam" });
        var kudos = await h.PostAsync(coach, "/feed/kudos", new() { ["postId"] = "999999", ["on"] = "1" });

        Assert.Equal(HttpStatusCode.NotFound, report.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, kudos.StatusCode);
    }

    [Fact]
    public async Task Return_url_must_be_local()
    {
        var coach = await h.LoginAsync("coach", h.CoachId(17));

        var response = await h.PostAsync(coach, "/feed/follow", new() { ["targetKey"] = "athlete:1", ["returnUrl"] = "https://evil.example/" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/", response.Headers.Location!.OriginalString);
        Assert.DoesNotContain("evil.example", response.Headers.Location!.OriginalString);
    }
}

/// <summary>A separate database and app, so a feed that throws on every call is tested without disturbing the shared one.</summary>
public class ThrowingFeedFactory : BallpassAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Ballpass", TestDb.ConnectionString("ballpass_feedthrow"));
        builder.ConfigureServices(services =>
        {
            services.AddScoped<IFeedService, ThrowingFeedService>();
            services.AddScoped<IFollowService, ThrowingFeedService>();
        });
    }
}

public class ThrowingFeedService : IFeedService, IFollowService
{
    private static T Boom<T>() => throw new InvalidOperationException("feed is down (test)");
    public Task<FeedPageResult> GetFeedAsync(FeedViewer viewer, FeedTab tab, string? before, string? tag = null, CancellationToken ct = default) => Boom<Task<FeedPageResult>>();
    public Task<IReadOnlyList<FeedItem>> GetPreviewAsync(FeedViewer viewer, int count, CancellationToken ct = default) => Boom<Task<IReadOnlyList<FeedItem>>>();
    public Task<PostOutcome> CreateAsync(FeedViewer viewer, NewPost input, CancellationToken ct = default) => Boom<Task<PostOutcome>>();
    public Task<bool> SetKudosAsync(FeedViewer viewer, int postId, bool on, CancellationToken ct = default) => Boom<Task<bool>>();
    public Task<ModOutcome> ReportAsync(FeedViewer viewer, int postId, string? reason, CancellationToken ct = default) => Boom<Task<ModOutcome>>();
    public Task<ModOutcome> HideAsync(FeedViewer viewer, int postId, CancellationToken ct = default) => Boom<Task<ModOutcome>>();
    public Task<ModOutcome> BlockAuthorAsync(FeedViewer viewer, int postId, CancellationToken ct = default) => Boom<Task<ModOutcome>>();
    public Task<bool> FollowAsync(string followerKey, string targetKey, CancellationToken ct = default) => Boom<Task<bool>>();
    public Task UnfollowAsync(string followerKey, string targetKey, CancellationToken ct = default) => Boom<Task>();
    public Task<bool> IsFollowingAsync(string followerKey, string targetKey, CancellationToken ct = default) => Boom<Task<bool>>();
}

public class FeedFailureTests(ThrowingFeedFactory factory) : IClassFixture<ThrowingFeedFactory>
{
    private async Task<HttpClient> CoachAsync()
    {
        var client = factory.Client();
        var token = FeedHarness.Token(await FeedHarness.Html(client, "/demo"));
        var id = Regex.Match(await FeedHarness.Html(client, "/demo"), "data-persona=\"coach\"[^>]*>\\s*<input[^>]*name=\"personaType\"[^>]*>\\s*<input[^>]*name=\"personaId\"[^>]*value=\"(\\d+)\"").Groups[1].Value;
        var r = await client.PostAsync("/demo/login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["personaType"] = "coach", ["personaId"] = id, ["returnUrl"] = "", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        return client;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/search")]
    public async Task Pages_still_render_when_the_feed_throws(string path)
    {
        var client = await CoachAsync();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Profile_and_shortlist_still_work_when_the_feed_throws()
    {
        var client = await CoachAsync();
        var slug = Regex.Match(await FeedHarness.Html(client, "/search"), "href=\"/p/([^\"/]+)\"").Groups[1].Value;
        Assert.NotEmpty(slug);

        var profile = await client.GetAsync($"/p/{slug}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);

        var token = FeedHarness.Token(await FeedHarness.Html(client, $"/p/{slug}"));
        var shortlist = await client.PostAsync($"/p/{slug}/shortlist", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, shortlist.StatusCode);
        Assert.Contains("status=shortlist", shortlist.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Feed_page_itself_degrades_to_a_message_not_a_crash()
    {
        var response = await factory.Client().GetAsync("/feed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-feed-unavailable", await response.Content.ReadAsStringAsync());
    }
}

/// <summary>Own database for service-level tests that need rows (a minor) the shared demo data must never contain.</summary>
public static class UnitDb
{
    public static async Task<BallpassDbContext> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<BallpassDbContext>().UseNpgsql(TestDb.ConnectionString("ballpass_feedunit")).Options;
        var db = new BallpassDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        return db;
    }

    public sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

// Spec: openspec/changes/flow-release/specs/feed/spec.md (SPEC §4.3): three columns, rails.
[Collection("db")]
public class FeedLayoutTests(BallpassAppFactory app)
{
    private readonly FeedHarness h = new(app);

    private static string Css(string file) =>
        Regex.Replace(File.ReadAllText(Path.Combine(Repo(), "src", "Ballpass.Web", "wwwroot", "css", file)), "\\s+", " ");

    private static string Repo()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Ballpass.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    [Fact]
    public void Feed_grid_is_240_600_320_and_the_old_width_override_is_gone()
    {
        Assert.Contains("grid-template-columns: 240px minmax(0, 600px) 320px", Css("bp-feed.css"));
        Assert.Contains("column-gap: 28px", Css("bp-feed.css"));
        var engage = Css("bp-engage.css");
        Assert.DoesNotContain(":has(.bp-engage-side)", engage);
        Assert.DoesNotContain("minmax(0, 720px) 340px", engage);
    }

    [Fact]
    public async Task Feed_has_a_left_rail_a_centre_column_and_a_right_rail()
    {
        var html = await FeedHarness.Html(app.Client(), "/feed");

        var left = html.IndexOf("data-feed-rail=\"left\"", StringComparison.Ordinal);
        var centre = html.IndexOf("data-feed-main", StringComparison.Ordinal);
        var right = html.IndexOf("data-engage=\"sidebar\"", StringComparison.Ordinal);
        Assert.True(left > 0 && centre > left && right > centre, "rails must be left, centre, right");
        var rail = Regex.Match(html, "<aside[^>]*data-feed-rail=\"left\".*?</aside>", RegexOptions.Singleline).Value;
        Assert.Contains("href=\"/demo", rail);   // anonymous: one way in
    }

    [Fact]
    public async Task Coach_left_rail_has_the_me_card_and_the_coach_links()
    {
        var coachId = h.CoachId(2);
        var name = h.Db(db => db.Coaches.Where(c => c.Id == coachId).Select(c => c.FirstName + " " + c.LastName).Single());
        var html = await FeedHarness.Html(await h.LoginAsync("coach", coachId), "/feed");

        var rail = Regex.Match(html, "<aside[^>]*data-feed-rail=\"left\".*?</aside>", RegexOptions.Singleline).Value;
        Assert.Contains(name, rail);
        Assert.Matches("<svg[^>]*class=\"[^\"]*bp-jersey[^>]*width=\"64\"", rail);
        var links = Regex.Matches(rail, "<a[^>]*href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["/match", "/shortlist", "/me/sent"], links);
    }

    [Fact]
    public async Task Athlete_left_rail_links_to_the_inbox_and_the_own_profile()
    {
        var athleteId = h.AthleteId(3);
        var slug = h.Db(db => db.Athletes.Where(a => a.Id == athleteId).Select(a => a.Slug).Single());
        var html = await FeedHarness.Html(await h.LoginAsync("athlete", athleteId), "/feed");

        var rail = Regex.Match(html, "<aside[^>]*data-feed-rail=\"left\".*?</aside>", RegexOptions.Singleline).Value;
        var links = Regex.Matches(rail, "<a[^>]*href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["/me/inbox", $"/p/{slug}"], links);
    }
}
