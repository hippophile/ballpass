using System.Globalization;
using System.Net;
using System.Resources;
using System.Text.RegularExpressions;
using Ballpass.Web.Components.Shared;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Evidence;
using Ballpass.Web.Features.Feed;
using Ballpass.Web.Features.Share;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-audit-fixes/specs/{feed,share,localization,demo-data,loop}/spec.md
[Collection("db")]
public class AuditMiscTests(BallpassAppFactory app)
{
    private readonly FeedHarness h = new(app);

    // ---------- 5. home strip honours hide / block ----------

    private static List<(int Id, string Type, string AuthorKey, string Html)> Strip(string html) => FeedHarness.Cards(html).ToList();

    // Orchestrator review: with MinPeers hardcoded at 20, NO female center had a percentile (19 qualify), which removed the
    // core demo moment. The threshold is now config (Evidence:MinPeers, default 15, an estimate) and shown on the page.
    [Fact]
    public async Task Female_center_profile_shows_percentiles_with_default_config_and_the_threshold_text()
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Ballpass.Web.Data.BallpassDbContext>();
        var centers = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include(
                db.Athletes.Where(a => a.IsDemo && a.Gender == Ballpass.Web.Data.Gender.Female && a.Position == Ballpass.Web.Data.Position.C),
                a => a.Stats)
            .ToList();
        var slug = centers
            .Where(a => a.Stats.OrderByDescending(s => s.Season).First() is var l && l.Games >= 10 && l.Minutes >= 15)
            .Select(a => a.Slug).First();

        var html = System.Net.WebUtility.HtmlDecode(await (await app.Client().GetAsync($"/p/{slug}")).Content.ReadAsStringAsync());

        Assert.DoesNotContain("λιγότεροι από 20 συνομήλικοι", html);
        Assert.Contains("15 συνομήλικοι", html); // threshold note renders the configured value
        Assert.Matches("\\d+ο εκατοστημόριο", html);
    }

    [Fact]
    public async Task Home_strip_no_longer_shows_an_author_the_coach_blocked()
    {
        var coachId = h.CoachId(11);
        var coachKey = $"coach:{coachId}";
        var coach = await h.LoginAsync("coach", coachId);
        var before = Strip(await FeedHarness.Html(coach, "/"));
        Assert.NotEmpty(before);
        var target = before.First(c => c.AuthorKey != coachKey);
        try
        {
            var response = await h.PostAsync(coach, "/feed/block", new() { ["postId"] = target.Id.ToString() });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var after = Strip(await FeedHarness.Html(coach, "/"));

            Assert.NotEmpty(after);
            Assert.DoesNotContain(after, c => c.AuthorKey == target.AuthorKey);
            Assert.Contains(Strip(await FeedHarness.Html(app.Client(), "/")), c => c.Id == target.Id);   // anonymous visitors are unaffected
        }
        finally
        {
            h.Db(db => db.Set<Block>().Where(b => b.BlockerKey == coachKey).ExecuteDelete());
        }
    }

    [Fact]
    public async Task Home_strip_no_longer_shows_a_post_the_coach_hid()
    {
        var coachId = h.CoachId(12);
        var coachKey = $"coach:{coachId}";
        var coach = await h.LoginAsync("coach", coachId);
        var before = Strip(await FeedHarness.Html(coach, "/"));
        var target = before.First(c => c.AuthorKey != coachKey);
        try
        {
            await h.PostAsync(coach, "/feed/hide", new() { ["postId"] = target.Id.ToString() });

            Assert.DoesNotContain(Strip(await FeedHarness.Html(coach, "/")), c => c.Id == target.Id);
        }
        finally
        {
            h.Db(db => db.Set<PostHide>().Where(x => x.ActorKey == coachKey).ExecuteDelete());
        }
    }

    // ---------- 11 / 14. one relative-time helper ----------

    [Theory]
    [InlineData(20, "μόλις τώρα")]
    [InlineData(5 * 60, "πριν από 5 λ.")]
    [InlineData(3 * 3600, "πριν από 3 ώ.")]
    [InlineData(2 * 86400, "πριν από 2 ημ.")]
    public void Relative_time_wording_is_one_format(int secondsAgo, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("el");
        try
        {
            var now = new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
            using var scope = app.Services.CreateScope();
            var loc = scope.ServiceProvider.GetRequiredService<IStringLocalizer<SharedResource>>();

            Assert.Equal(expected, RelativeTimeText.Format(loc, now.AddSeconds(-secondsAgo), now));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public async Task Sent_page_uses_the_shared_wording()
    {
        var coachId = h.CoachId(4);
        var coach = await h.LoginAsync("coach", coachId);

        var html = await FeedHarness.Html(coach, "/me/sent");

        Assert.Matches("πριν από \\d+ ημ\\.", html);
        Assert.DoesNotMatch("πριν \\d+ (ημ|ώρ|λ)\\.", html);
    }

    // ---------- 11. formal plural register ----------

    private static readonly Regex Singular = new(
        @"(?<!\p{L})(Δες|Βρες|Διάλεξε|Ακολούθησε|Ακολουθείς|Συνδέσου|Περίμενε|Δοκίμασε|Συμπλήρωσε|Πρόσθεσε|Προσθέσεις|Ξεκίνα|Σταμάτα|Δώσε|Έχεις|έστειλες|Απέρριψες|Αποδέχτηκες|βλέπεις|ψάχνεις|αποσυνδεθείς|γυρίσεις|πρόσθεσες|άλλαξες|Κύλισε|Ακύρωσε|Αφαίρεσε|Πάτα|Επίλεξε|Γράψε|Πες|εσένα|σου|σε\s+εσένα)(?!\p{L})",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    [Theory]
    [InlineData("FeedResource")]
    [InlineData("LoopResource")]
    [InlineData("EvidenceResource")]
    [InlineData("FindResource")]
    [InlineData("ShareResource")]
    public void Greek_resources_use_the_formal_plural_register(string resource)
    {
        var manager = new ResourceManager($"Ballpass.Web.Resources.{resource}", typeof(FeedResource).Assembly);
        var set = manager.GetResourceSet(new CultureInfo("el"), createIfNotExists: true, tryParents: true)!;
        var offenders = new List<string>();
        foreach (System.Collections.DictionaryEntry e in set)
            if (e.Value is string text && Singular.Match(text) is { Success: true } m) offenders.Add($"{e.Key}: «{m.Value}» in «{text}»");

        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    // ---------- 12. share card follows LookingForTeam ----------

    private static Athlete NoClub(bool looking) => new()
    {
        Slug = "share-looking", FirstName = "Ελένη", LastName = "Δοκιμαστική", FirstNameLatin = "Eleni", LastNameLatin = "Dokimastiki",
        BirthYear = 2001, Gender = Gender.Female, Position = Position.PG, HeightCm = 170, City = "Αθήνα", Region = "Attica", IsDemo = true,
        LookingForTeam = looking,
        Stats = [new StatLine { Season = "2025-26", Games = 12, Minutes = 20m, Points = 8m, Rebounds = 3m, Assists = 4m, Source = StatSource.SelfReported }],
    };

    [Fact]
    public void Card_text_and_hash_follow_the_looking_for_team_flag()
    {
        var on = OgCardModelFactory.Build(NoClub(true), 2026);
        var off = OgCardModelFactory.Build(NoClub(false), 2026);

        Assert.Contains("Ψάχνει ομάδα", on.ClubLine);
        Assert.Contains("Χωρίς ομάδα", on.ClubLine);
        Assert.DoesNotContain("Ψάχνει ομάδα", off.ClubLine);
        Assert.Contains("Χωρίς ομάδα", off.ClubLine);
        Assert.NotEqual(on.Hash, off.Hash);   // the cache key includes the flag
    }

    [Fact]
    public async Task Card_etag_changes_when_the_flag_is_toggled()
    {
        var slug = "share-looking" + TestDb.Suffix.Replace("_", "-");
        var athlete = NoClub(true);
        athlete.Slug = slug;
        h.Db(db => { db.Athletes.Where(a => a.Slug == slug).ExecuteDelete(); db.Athletes.Add(athlete); db.SaveChanges(); return 0; });
        try
        {
            var client = app.Client();
            var first = (await client.GetAsync($"/p/{slug}/og.png")).Headers.ETag!.Tag;
            h.Db(db => db.Athletes.Where(a => a.Slug == slug).ExecuteUpdate(s => s.SetProperty(a => a.LookingForTeam, false)));

            var second = (await client.GetAsync($"/p/{slug}/og.png")).Headers.ETag!.Tag;

            Assert.NotEqual(first, second);
        }
        finally { h.Db(db => db.Athletes.Where(a => a.Slug == slug).ExecuteDelete()); }
    }

    // ---------- 10. wrapped note + compare warning ----------

    [Fact]
    public async Task Wrapped_cohort_note_names_the_position_group_and_the_minutes_floor()
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
        var ranks = scope.ServiceProvider.GetRequiredService<ICohortRank>();
        var candidates = await db.Athletes.AsNoTracking().Where(a => a.IsDemo && a.Gender == Gender.Female && a.Position == Position.PF && a.Stats.Any()).ToListAsync();
        Athlete? found = null;
        foreach (var a in candidates)
            if (await WrappedCohort.BestAsync(ranks, a.Id) is not null && SeasonWrapped.From(await db.StatLines.Where(s => s.AthleteId == a.Id).ToListAsync())!.IsFullStory) { found = a; break; }
        Assert.NotNull(found);

        var html = WebUtility.HtmlDecode(await app.Client().GetStringAsync($"/p/{found!.Slug}/season"));

        var note = Regex.Match(html, "data-slide=\"cohort\".*?<p class=\"bp-share-note\">(.*?)</p>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Contains("φόργουορντ", note);                       // the position group
        Assert.Contains("15 λεπτά ανά αγώνα", note);               // the minutes floor
        Assert.Contains("αθλήτριες", note);
        Assert.DoesNotMatch("επίδειξης|φανταστικ", note);           // flow-release O1
    }

    // ---------- 15. surviving mutants N13 / N16 ----------

    private static StatLine Line(int games, string season = "2025-26") => new()
    {
        Season = season, Games = games, Minutes = 20m, Points = 8m, Rebounds = 3m, Assists = 2m, Source = StatSource.SelfReported,
    };

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void Wrapped_full_story_starts_at_exactly_five_games(int games, bool full)
    {
        Assert.Equal(full, SeasonWrapped.From([Line(games)])!.IsFullStory);
        Assert.Equal(5, SeasonWrapped.FullStoryMinGames);
    }

    [Fact]
    public void Wrapped_games_are_summed_over_the_latest_season_before_the_boundary_check() =>
        Assert.True(SeasonWrapped.From([Line(3), Line(2), Line(30, "2024-25")])!.IsFullStory);

    [Fact]
    public async Task Athlete_with_exactly_five_games_gets_the_full_story_page()
    {
        var slug = "share-five-games" + TestDb.Suffix.Replace("_", "-");
        var athlete = NoClub(false);
        athlete.Slug = slug;
        athlete.Stats = [new StatLine { Season = "2025-26", League = League.B, Games = 5, Minutes = 11.5m, Points = 4m, Rebounds = 2m, Assists = 1m, Source = StatSource.SelfReported }];
        h.Db(db => { db.Athletes.Where(a => a.Slug == slug).ExecuteDelete(); db.Athletes.Add(athlete); db.SaveChanges(); return 0; });
        try
        {
            var html = WebUtility.HtmlDecode(await app.Client().GetStringAsync($"/p/{slug}/season"));

            var slides = Regex.Matches(html, "data-slide=\"([a-z]+)\"").Select(m => m.Groups[1].Value).ToList();
            Assert.Contains("line", slides);
        }
        finally { h.Db(db => db.Athletes.Where(a => a.Slug == slug).ExecuteDelete()); }
    }

    [Fact]
    public async Task Self_follow_is_refused_by_the_service_and_the_endpoint()
    {
        var coachId = h.CoachId(10);
        var key = $"coach:{coachId}";
        using (var scope = app.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<IFollowService>().FollowAsync(key, key));
        var coach = await h.LoginAsync("coach", coachId);

        var response = await h.PostAsync(coach, "/feed/follow", new() { ["targetKey"] = key });

        Assert.Contains("status=follow-refused", response.Headers.Location!.OriginalString);
        Assert.Equal(0, h.Db(db => db.Set<Follow>().Count(f => f.FollowerKey == key && f.TargetKey == key)));
    }

    // ---------- 13. IsDemo on kudos / follows / blocks / reports ----------

    [Fact]
    public void Seeded_kudos_and_follows_are_flagged_demo()
    {
        Assert.True(h.Db(db => db.Set<Kudos>().Any()));
        Assert.True(h.Db(db => db.Set<Follow>().Any()));
        Assert.True(h.Db(db => db.Set<Kudos>().All(k => k.IsDemo)));
        Assert.True(h.Db(db => db.Set<Follow>().All(f => f.IsDemo)));
    }

    [Fact]
    public async Task Rows_created_through_the_endpoints_are_flagged_demo_too()
    {
        var coachId = h.CoachId(13);
        var key = $"coach:{coachId}";
        var (postId, authorKey) = h.Db(db =>
        {
            var p = db.Set<Post>().Where(x => x.AuthorKind == AuthorKind.Athlete && !x.IsHidden).OrderBy(x => x.Id).Skip(2).First();
            return (p.Id, p.AuthorKey);
        });
        var coach = await h.LoginAsync("coach", coachId);
        try
        {
            await h.PostAsync(coach, "/feed/kudos", new() { ["postId"] = postId.ToString(), ["on"] = "1" });
            await h.PostAsync(coach, "/feed/follow", new() { ["targetKey"] = authorKey });
            await h.PostAsync(coach, "/feed/report", new() { ["postId"] = postId.ToString(), ["reason"] = "spam" });
            await h.PostAsync(coach, "/feed/block", new() { ["postId"] = postId.ToString() });

            Assert.True(h.Db(db => db.Set<Kudos>().Single(k => k.ActorKey == key && k.PostId == postId).IsDemo));
            Assert.True(h.Db(db => db.Set<Report>().Single(r => r.ReporterKey == key && r.PostId == postId).IsDemo));
            Assert.True(h.Db(db => db.Set<Block>().Single(b => b.BlockerKey == key && b.BlockedKey == authorKey).IsDemo));
        }
        finally
        {
            h.Db(db =>
            {
                db.Set<Kudos>().Where(k => k.ActorKey == key).ExecuteDelete();
                db.Set<Report>().Where(r => r.ReporterKey == key).ExecuteDelete();
                db.Set<Block>().Where(b => b.BlockerKey == key).ExecuteDelete();
                db.Set<PostHide>().Where(x => x.ActorKey == key).ExecuteDelete();
                db.Set<Follow>().Where(f => f.FollowerKey == key).ExecuteDelete();
                return 0;
            });
        }
        // The follow itself is checked through the service (the block above removes it by design).
        var followKey = "coach:" + h.CoachId(14);
        using var scope = app.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<IFollowService>().FollowAsync(followKey, authorKey));
        try { Assert.True(h.Db(db => db.Set<Follow>().Single(f => f.FollowerKey == followKey && f.TargetKey == authorKey).IsDemo)); }
        finally { h.Db(db => db.Set<Follow>().Where(f => f.FollowerKey == followKey).ExecuteDelete()); }
    }

    // ---------- 13. seeder uses the injected clock ----------

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Seeder_stamps_the_feed_relative_to_the_injected_time_provider()
    {
        var fixedNow = new DateTimeOffset(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<BallpassDbContext>().UseNpgsql(TestDb.ConnectionString("ballpass_seedclock")).Options;
        await using var db = new BallpassDbContext(options);
        await db.Database.EnsureDeletedAsync();

        await new DemoDataSeeder(db, new FixedTime(fixedNow)).ResetAsync();

        var created = await db.Set<Post>().Select(p => p.CreatedAt).ToListAsync();
        Assert.NotEmpty(created);
        Assert.All(created, c => Assert.InRange(c, fixedNow.AddDays(-31), fixedNow));
        var kudos = await db.Set<Kudos>().Select(k => k.CreatedAt).ToListAsync();
        Assert.All(kudos, c => Assert.InRange(c, fixedNow.AddDays(-31), fixedNow));
        var seen = await db.Interests.Where(i => i.SeenAt != null).Select(i => i.SeenAt!.Value).ToListAsync();
        Assert.All(seen, c => Assert.True(c <= fixedNow));
        await db.Database.EnsureDeletedAsync();
    }
}
