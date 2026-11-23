using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Evidence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-loop/specs/loop/spec.md
[Collection("db")]
public class LoopTests(BallpassAppFactory app)
{
    private readonly FeedHarness h = new(app);
    private static readonly DemoDataSet Data = DemoDataGenerator.Generate(42);

    // ---------- helpers ----------

    private static Task<string> Html(HttpClient c, string path) => FeedHarness.Html(c, path);

    /// <summary>Interest rows inserted by a test and removed again, so tests never depend on seed ids or order.</summary>
    private sealed class Fx(BallpassAppFactory app) : IDisposable
    {
        public List<int> Ids { get; } = [];

        public (int InterestId, int CoachId, int AthleteId, string Slug) Add(int coachId, int athleteSkip,
            DateTimeOffset createdAt, DateTimeOffset? seenAt = null, InterestReply reply = InterestReply.None, DateTimeOffset? repliedAt = null,
            string? note = "Δοκιμή")
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
            var gender = db.Coaches.Where(c => c.Id == coachId).Select(c => c.Club!.Gender).Single();
            var athlete = db.Athletes.Where(a => a.IsDemo && a.Gender == gender && a.Stats.Any()
                    && !db.Interests.Any(i => i.CoachId == coachId && i.AthleteId == a.Id))
                .OrderBy(a => a.Id).Skip(athleteSkip).First();
            var interest = new Interest
            {
                CoachId = coachId, AthleteId = athlete.Id, CreatedAt = createdAt, SeenAt = seenAt, Reply = reply,
                RepliedAt = repliedAt, Note = note, IsDemo = true,
            };
            db.Interests.Add(interest);
            db.SaveChanges();
            Ids.Add(interest.Id);
            return (interest.Id, coachId, athlete.Id, athlete.Slug);
        }

        public void Dispose()
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
            db.Interests.Where(i => Ids.Contains(i.Id)).ExecuteDelete();
        }
    }

    private Interest Row(int id) => h.Db(db => db.Interests.AsNoTracking().Single(i => i.Id == id));

    private static int Unread(string html)
    {
        var m = Regex.Match(html, "<a[^>]*href=\"/me/sent\"[^>]*data-unread=\"(\\d+)\"");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    private static readonly DateTimeOffset Past = DateTimeOffset.UtcNow.AddDays(-3);

    // ---------- 1. Evidence minutes floor ----------

    private static StatLine Line(double minutes = 20, int games = 20, double points = 10) => new()
    {
        Season = "2025-26", Games = games, Minutes = (decimal)minutes, Points = (decimal)points, Rebounds = 4m, Assists = 3m,
        Steals = 1m, FgPct = 45m, Source = StatSource.SelfReported,
    };

    private static List<CohortMember> Members(int count, int firstId, double minutes) =>
        Enumerable.Range(0, count).Select(i => new CohortMember(firstId + i, Line(minutes, points: 5 + i))).ToList();

    private static CohortEvidence Eval(StatLine subject, IReadOnlyList<CohortMember> cohort, decimal? floor = null) =>
        floor is null
            ? CohortCalculator.Evaluate(1, Gender.Female, PositionGroup.Guard, subject, cohort, false, new StatAggregator())
            : CohortCalculator.Evaluate(1, Gender.Female, PositionGroup.Guard, subject, cohort, false, new StatAggregator(), floor.Value);

    [Fact]
    public void Minutes_floor_excludes_a_low_minute_peer()
    {
        var withLow = Members(20, 100, 20).Concat(Members(1, 500, 14.9)).ToList();

        var result = Eval(Line(), withLow);

        Assert.Equal(20, result.PeerCount);
        Assert.Equal(InsufficientReason.None, result.Get(EvidenceMetric.Points)!.Reason);
    }

    [Fact]
    public void Minutes_floor_can_push_the_cohort_below_the_peer_minimum()
    {
        var justBelow = CohortCalculator.DefaultMinPeers - 1;
        var cohort = Members(justBelow, 100, 20).Concat(Members(5, 500, 9)).ToList();

        var result = Eval(Line(), cohort);

        Assert.Equal(justBelow, result.PeerCount);
        Assert.All(result.Metrics, m => Assert.Equal(InsufficientReason.TooFewPeers, m.Reason));
    }

    [Fact]
    public void Peer_at_exactly_the_floor_counts()
    {
        Assert.Equal(20, Eval(Line(), Members(20, 100, 15)).PeerCount);
    }

    [Fact]
    public void Athlete_below_the_floor_gets_no_percentile_and_the_minutes_reason()
    {
        var result = Eval(Line(minutes: 13), Members(40, 100, 20));

        Assert.All(result.Metrics, m =>
        {
            Assert.Null(m.Percentile);
            Assert.Equal(InsufficientReason.TooFewMinutes, m.Reason);
        });
    }

    [Fact]
    public void Floor_is_a_parameter()
    {
        Assert.Equal(0, Eval(Line(), Members(30, 100, 20), floor: 25m).PeerCount);
    }

    [Fact]
    public async Task Config_floor_is_honoured_and_the_note_mentions_minutes()
    {
        var slug = h.Db(db => db.Athletes.Where(a => a.IsDemo && a.Stats.Any(s => s.Games >= 10)).OrderBy(a => a.Id).Select(a => a.Slug).First());

        var normal = await Html(app.Client(), $"/p/{slug}");
        Assert.Contains("15 λεπτά ανά αγώνα", normal);

        var strict = app.WithWebHostBuilder(b => b.UseSetting("Evidence:MinMinutesPerGame", "60"));
        var html = await Html(strict.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false }), $"/p/{slug}");
        Assert.DoesNotMatch("data-pct=\"\\d+\"", html);
        Assert.Contains("λιγότερα από 60 λεπτά ανά αγώνα", html);
    }

    [Fact]
    public async Task Low_minute_athlete_profile_shows_the_reason_and_rank_is_null()
    {
        var low = Data.Athletes.First(a => a.Stats.Count > 0 && a.Stats.OrderByDescending(s => s.Season).First() is { Games: >= 10, Minutes: < 15 });
        var html = await Html(app.Client(), $"/p/{low.Slug}");

        Assert.Contains("Δεν υπάρχουν αρκετά δεδομένα", html);
        Assert.Contains("λιγότερα από 15 λεπτά ανά αγώνα", html);
        Assert.DoesNotMatch("data-pct=\"\\d+\"", html);

        using var scope = app.Services.CreateScope();
        var id = scope.ServiceProvider.GetRequiredService<BallpassDbContext>().Athletes.Where(a => a.Slug == low.Slug).Select(a => a.Id).Single();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<ICohortRank>().RankInCohort(id, EvidenceMetric.Points));
    }

    // ---------- 2. Wrapped uses ICohortRank ----------

    private sealed class StubRank(Func<EvidenceMetric, (int, int)?> rank) : ICohortRank
    {
        public Task<(int Rank, int N)?> RankInCohort(int athleteId, EvidenceMetric metric) => Task.FromResult(rank(metric));
    }

    private static string Latest(Athlete a) => a.Stats.Max(s => s.Season)!;

    private static readonly Athlete Full = Data.Athletes.First(a => a.Stats.Count >= 2 && a.Stats.First(s => s.Season == Latest(a)).Games >= 5);

    private async Task<string> Wrapped(Func<EvidenceMetric, (int, int)?> rank)
    {
        var factory = app.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<ICohortRank>();
            s.AddScoped<ICohortRank>(_ => new StubRank(rank));
        }));
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        return WebUtility.HtmlDecode(await client.GetStringAsync($"/p/{Full.Slug}/season"));
    }

    [Fact]
    public async Task Wrapped_skips_the_cohort_slide_when_ICohortRank_returns_null()
    {
        Assert.DoesNotContain("data-slide=\"cohort\"", await Wrapped(_ => null));
    }

    [Fact]
    public async Task Wrapped_skips_the_slide_when_the_best_percentile_is_below_50()
    {
        Assert.DoesNotContain("data-slide=\"cohort\"", await Wrapped(_ => (30, 40)));
    }

    [Fact]
    public async Task Wrapped_shows_the_best_metric_from_ICohortRank()
    {
        // Points rank 2 of 40: percentile round(38/39*100) = 97, top 3%. Every other metric ranks lower.
        var html = await Wrapped(m => m == EvidenceMetric.Points ? (2, 40) : (20, 40));

        Assert.Contains("data-slide=\"cohort\"", html);
        Assert.Contains("data-cohort-metric=\"points\"", html);
        Assert.Matches("data-metric=\"cohort-n\" data-value=\"40\"", html);
        Assert.Matches("data-metric=\"cohort-top\" data-value=\"3\"", html);
    }

    [Fact]
    public void The_duplicate_SeasonCohort_helper_is_gone()
    {
        Assert.Null(typeof(Program).Assembly.GetType("Ballpass.Web.Features.Share.SeasonCohort"));
    }

    // ---------- 3. Profile action bar ----------

    [Fact]
    public async Task Coach_profile_has_one_compact_action_bar_with_note_inside_details_inside_the_interest_form()
    {
        var coach = h.CoachId(6);
        var slug = h.Db(db =>
        {
            var gender = db.Coaches.Where(c => c.Id == coach).Select(c => c.Club!.Gender).Single();
            return db.Athletes.Where(a => a.IsDemo && a.Gender == gender && a.Stats.Any() && !db.Interests.Any(i => i.CoachId == coach && i.AthleteId == a.Id))
                .OrderBy(a => a.Id).Select(a => a.Slug).First();
        });
        var client = await h.LoginAsync("coach", coach);

        var html = await Html(client, $"/p/{slug}");

        var start = html.IndexOf("class=\"bp-loop-actionbar\"", StringComparison.Ordinal);
        Assert.True(start > 0, "action bar container");
        var bar = html[start..html.IndexOf("</section>", start, StringComparison.Ordinal)];
        Assert.Contains("Εκδήλωση ενδιαφέροντος", bar);
        Assert.Contains("data-action=\"shortlist\"", bar);
        Assert.Contains("Λίστα", bar);
        Assert.Contains("data-feed-action=", bar);
        Assert.Contains($"/compare?a={slug}", bar);
        Assert.Contains("Σύγκριση με", bar);

        var form = Regex.Match(bar, "<form[^>]*data-action=\"interest\"[^>]*>.*?</form>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(form);
        Assert.Matches("<details[^>]*>.*<summary[^>]*>Προσθήκη σημείωσης \\(προαιρετικό\\)</summary>.*<textarea[^>]*name=\"note\"", form.Replace("\n", " "));
        Assert.Contains("class=\"bp-btn", form);
    }

    [Fact]
    public void Action_bar_wraps_on_mobile()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Ballpass.Web", "wwwroot", "css", "bp-loop.css"));
        Assert.Matches("\\.bp-loop-actionbar\\s*\\{[^}]*flex-wrap:\\s*wrap", css);
    }

    // Orchestrator review: the pill said "αθλήτρια" even for a men's club, and "στον προπονητή" for a female coach.
    [Fact]
    public async Task Switch_pill_wording_follows_the_gender_of_who_you_switch_to()
    {
        var h = new FeedHarness(app);
        int HeadCoach(Gender clubGender, bool femaleCoach) => h.Db(db => db.Coaches
            .Where(c => c.IsDemo && c.Role == CoachRole.Head && c.Club!.Gender == clubGender)
            .OrderBy(c => c.Id).AsEnumerable()
            .First(c => GreekNames.IsFemaleFirstName(c.FirstName) == femaleCoach).Id);

        var menClubCoach = await h.LoginAsync("coach", HeadCoach(Gender.Male, femaleCoach: false));
        Assert.Contains("Δείτε το ως αθλητής", await Html(menClubCoach, "/search"));

        var womanCoach = await h.LoginAsync("coach", HeadCoach(Gender.Female, femaleCoach: true));
        Assert.Contains("Δείτε το ως αθλήτρια", await Html(womanCoach, "/search"));
        var switched = await h.PostAsync(womanCoach, "/demo/switch?to=athlete", [], tokenPage: "/search");
        Assert.Equal(HttpStatusCode.Redirect, switched.StatusCode);
        Assert.Contains("Πίσω στην προπονήτρια", await Html(womanCoach, "/me/inbox"));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ballpass.slnx"))) dir = dir.Parent;
        return dir!.FullName;
    }

    // ---------- 4. Composer hint once ----------

    [Fact]
    public async Task Composer_hint_is_shown_once_and_the_textarea_has_no_placeholder()
    {
        var client = await h.LoginAsync("coach", h.CoachId(5));

        var html = await Html(client, "/feed");

        Assert.Single(Regex.Matches(html, "Έως 280 χαρακτήρες"));
        var textarea = Regex.Match(html, "<textarea[^>]*id=\"bp-feed-text\"[^>]*>").Value;
        Assert.NotEmpty(textarea);
        Assert.DoesNotContain("placeholder", textarea);
    }

    // ---------- 5. Pager keeps AI params ----------

    [Fact]
    public async Task Search_pager_keeps_parsed_nlq_and_unsupported()
    {
        var client = await h.LoginAsync("coach", h.CoachId(4));
        var nlq = "ψηλές γκαρντ";

        var html = await Html(client, $"/search?gender=Female&parsed=rules&nlq={Uri.EscapeDataString(nlq)}&unsupported=minors");

        var href = Regex.Match(html, "<a[^>]*rel=\"next\"[^>]*href=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(href);
        var query = HttpUtility.ParseQueryString(href[(href.IndexOf('?') + 1)..]);
        Assert.Equal("rules", query["parsed"]);
        Assert.Equal(nlq, query["nlq"]);
        Assert.Equal("minors", query["unsupported"]);
        Assert.Equal("2", query["page"]);
    }

    // ---------- 6. Interest lifecycle ----------

    [Fact]
    public async Task Opening_the_inbox_marks_unseen_interests_seen_once()
    {
        using var fx = new Fx(app);
        var a = fx.Add(h.CoachId(9), 0, Past);
        var other = fx.Add(h.CoachId(9), 1, Past);
        var client = await h.LoginAsync("athlete", a.AthleteId);
        Assert.Null(Row(a.InterestId).SeenAt);

        await Html(client, "/me/inbox");
        var first = Row(a.InterestId).SeenAt;
        Assert.NotNull(first);
        Assert.InRange(first!.Value, DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(1));

        await Html(client, "/me/inbox");
        Assert.Equal(first, Row(a.InterestId).SeenAt);
        Assert.Null(Row(other.InterestId).SeenAt);   // someone else's interest is untouched
    }

    [Fact]
    public async Task Owner_accepts_and_sees_the_fictional_club_contact()
    {
        using var fx = new Fx(app);
        var a = fx.Add(h.CoachId(10), 0, Past);
        var client = await h.LoginAsync("athlete", a.AthleteId);
        var inbox = await Html(client, "/me/inbox");
        Assert.Contains($"action=\"/me/inbox/{a.InterestId}/reply\"", inbox);
        // flow-release §4.8: the athlete's move is teal, the card carries a coach scene photo from the pool.
        var pending = Regex.Match(inbox, $"<li[^>]*class=\"interest\"[^>]*data-interest-id=\"{a.InterestId}\".*?</li>", RegexOptions.Singleline).Value;
        Assert.Matches("<button[^>]*name=\"reply\"[^>]*value=\"accept\"[^>]*class=\"bp-btn-athlete\"", pending);
        Assert.Matches("<button[^>]*name=\"reply\"[^>]*value=\"decline\"[^>]*class=\"bp-btn-ghost\"", pending);
        var female = GreekNames.IsFemaleFirstName(h.Db(db => db.Coaches.Single(c => c.Id == a.CoachId).FirstName));
        Assert.Contains($"src=\"{Ballpass.Web.Features.Flow.FlowMedia.CoachScene(a.CoachId, female).Url}\"", pending);
        Assert.Contains("<blockquote class=\"bp-interest-note\">Δοκιμή</blockquote>", pending);

        var response = await h.PostAsync(client, $"/me/inbox/{a.InterestId}/reply", new() { ["reply"] = "accept" }, tokenPage: "/me/inbox");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/me/inbox", response.Headers.Location!.OriginalString);
        var row = Row(a.InterestId);
        Assert.Equal(InterestReply.Accepted, row.Reply);
        Assert.NotNull(row.RepliedAt);

        var html = await Html(client, "/me/inbox");
        var li = Regex.Match(html, $"<li[^>]*class=\"interest\"[^>]*data-interest-id=\"{a.InterestId}\".*?</li>", RegexOptions.Singleline).Value;
        Assert.Contains("επικοινωνία μέσω συλλόγου", li);
        Assert.Matches("contact@[a-z0-9-]+\\.invalid", li);   // reserved TLD: never a real mailbox
        Assert.DoesNotMatch("(?i)demo|φανταστικ|επίδειξ", li);   // flow-release O1: no "(demo)" tag, no "fictional address" note
        Assert.DoesNotContain($"action=\"/me/inbox/{a.InterestId}/reply\"", li);
        Assert.Contains("Συνδεθήκατε", li);
        var addr = Regex.Match(li, "contact@[a-z0-9-]+\\.invalid").Value;
        // messages (2026-11-23): the teal action opens the conversation; the mailto stays as a secondary ghost link.
        Assert.Matches($"<a[^>]*class=\"bp-btn-athlete\"[^>]*href=\"/messages/with/{a.InterestId}\"", li);
        Assert.Matches($"<a[^>]*class=\"bp-btn-ghost\"[^>]*href=\"mailto:{Regex.Escape(addr)}\"", li);
        Assert.DoesNotContain(".invalid", Regex.Replace(li, "<[^>]+>", " "));   // the reserved address lives only in the mailto, never as visible text
        foreach (Match m in Regex.Matches(html, "[\\w.+-]+@[\\w.-]+"))
            Assert.EndsWith(".invalid", m.Value);
    }

    [Fact]
    public async Task Interest_without_a_note_renders_no_empty_note_box()
    {
        using var fx = new Fx(app);
        var a = fx.Add(h.CoachId(12), 0, Past, note: null);
        var client = await h.LoginAsync("athlete", a.AthleteId);
        var inbox = await Html(client, "/me/inbox");
        var li = Regex.Match(inbox, $"<li[^>]*class=\"interest\"[^>]*data-interest-id=\"{a.InterestId}\".*?</li>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(li);
        Assert.DoesNotContain("bp-interest-note", li);
        Assert.DoesNotContain("Χωρίς σημείωση", li);
    }

    [Fact]
    public async Task Owner_declines_and_a_second_reply_is_ignored()
    {
        using var fx = new Fx(app);
        var a = fx.Add(h.CoachId(11), 0, Past);
        var client = await h.LoginAsync("athlete", a.AthleteId);

        await h.PostAsync(client, $"/me/inbox/{a.InterestId}/reply", new() { ["reply"] = "decline" }, tokenPage: "/me/inbox");
        var repliedAt = Row(a.InterestId).RepliedAt;
        await h.PostAsync(client, $"/me/inbox/{a.InterestId}/reply", new() { ["reply"] = "accept" }, tokenPage: "/me/inbox");

        var row = Row(a.InterestId);
        Assert.Equal(InterestReply.Declined, row.Reply);
        Assert.Equal(repliedAt, row.RepliedAt);
        Assert.DoesNotContain(".invalid", await Html(client, "/me/inbox"));
    }

    [Fact]
    public async Task Reply_access_control_and_validation()
    {
        using var fx = new Fx(app);
        var a = fx.Add(h.CoachId(12), 0, Past);
        var path = $"/me/inbox/{a.InterestId}/reply";
        var owner = await h.LoginAsync("athlete", a.AthleteId);
        var otherAthlete = await h.LoginAsync("athlete", h.Db(db => db.Athletes.Where(x => x.IsDemo && x.Id != a.AthleteId).OrderBy(x => x.Id).Select(x => x.Id).First()));
        var coach = await h.LoginAsync("coach", a.CoachId);

        Assert.Equal(HttpStatusCode.BadRequest, (await h.PostAsync(owner, path, new() { ["reply"] = "accept" }, withToken: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.PostAsync(otherAthlete, path, new() { ["reply"] = "accept" }, tokenPage: "/me/inbox")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.PostAsync(coach, path, new() { ["reply"] = "accept" }, tokenPage: "/shortlist")).StatusCode);
        var anon = app.Client();
        var anonResponse = await h.PostAsync(anon, path, new() { ["reply"] = "accept" }, tokenPage: "/demo");
        Assert.Equal(HttpStatusCode.Redirect, anonResponse.StatusCode);
        Assert.StartsWith("/demo", anonResponse.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.PostAsync(owner, path, new() { ["reply"] = "maybe" }, tokenPage: "/me/inbox")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await h.PostAsync(owner, "/me/inbox/999999999/reply", new() { ["reply"] = "accept" }, tokenPage: "/me/inbox")).StatusCode);
        Assert.Equal(InterestReply.None, Row(a.InterestId).Reply);
    }

    [Fact]
    public async Task Sent_page_is_coach_only()
    {
        var anon = await app.Client().GetAsync("/me/sent");
        Assert.Equal(HttpStatusCode.Redirect, anon.StatusCode);
        Assert.StartsWith("/demo", anon.Headers.Location!.OriginalString);

        var athlete = await h.LoginAsync("athlete", h.AthleteId(2));
        var denied = await athlete.GetAsync("/me/sent");
        Assert.True(denied.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect);
        if (denied.StatusCode == HttpStatusCode.Redirect) Assert.Contains("/denied", denied.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Sent_page_shows_a_stepper_per_interest_with_states_and_times()
    {
        using var fx = new Fx(app);
        var coachId = h.CoachId(13);
        var now = DateTimeOffset.UtcNow;
        var unseen = fx.Add(coachId, 0, now.AddDays(-5));
        var seen = fx.Add(coachId, 1, now.AddDays(-4), seenAt: now.AddDays(-3));
        var accepted = fx.Add(coachId, 2, now.AddDays(-3), seenAt: now.AddDays(-2), reply: InterestReply.Accepted, repliedAt: now.AddDays(-1));
        var declined = fx.Add(coachId, 3, now.AddDays(-2), seenAt: now.AddDays(-1), reply: InterestReply.Declined, repliedAt: now.AddHours(-3));
        var client = await h.LoginAsync("coach", coachId);

        var html = await Html(client, "/me/sent");

        string Item(int id) => Regex.Match(html, $"<article[^>]*class=\"bp-loop-sent\"[^>]*data-interest-id=\"{id}\"[^>]*>.*?</article>", RegexOptions.Singleline).Value;
        string State(string item, string step) => Regex.Match(item, $"<li[^>]*data-step=\"{step}\"[^>]*data-state=\"(\\w+)\"").Groups[1].Value;

        Assert.Contains("data-state=\"sent\"", Item(unseen.InterestId));
        Assert.Equal(("done", "pending", "pending"), (State(Item(unseen.InterestId), "sent"), State(Item(unseen.InterestId), "seen"), State(Item(unseen.InterestId), "replied")));
        Assert.Equal(("done", "done", "pending"), (State(Item(seen.InterestId), "sent"), State(Item(seen.InterestId), "seen"), State(Item(seen.InterestId), "replied")));
        Assert.Contains("data-state=\"seen\"", Item(seen.InterestId));
        var acc = Item(accepted.InterestId);
        Assert.Equal(("done", "done", "done"), (State(acc, "sent"), State(acc, "seen"), State(acc, "replied")));
        Assert.Contains("data-state=\"accepted\"", acc);
        Assert.Contains("Αποδοχή", acc);
        var dec = Item(declined.InterestId);
        Assert.Contains("data-state=\"declined\"", dec);
        Assert.Contains("Απόρριψη", dec);

        // Done steps carry the exact time as a title and a machine-readable datetime; the chip links to the profile.
        Assert.Equal(3, Regex.Matches(acc, "<time[^>]*datetime=\"[^\"]+\"[^>]*title=\"[^\"]+\"").Count);
        Assert.Single(Regex.Matches(Item(unseen.InterestId), "<time[^>]*title="));
        Assert.Matches($"<a[^>]*class=\"bp-loop-chip\"[^>]*href=\"/p/{accepted.Slug}\"", acc);

        // flow-release §4.7: dots orange (sent), orange (seen), teal (reply); done steps carry bp-step-done.
        string Classes(string item, string step) => Regex.Match(item, $"<li[^>]*class=\"([^\"]*)\"[^>]*data-step=\"{step}\"").Groups[1].Value;
        Assert.Contains("bp-step-coach", Classes(acc, "sent"));
        Assert.Contains("bp-step-coach", Classes(acc, "seen"));
        Assert.Contains("bp-step-athlete", Classes(acc, "replied"));
        Assert.All(new[] { "sent", "seen", "replied" }, s => Assert.Contains("bp-step-done", Classes(acc, s)));
        Assert.DoesNotContain("bp-step-done", Classes(Item(unseen.InterestId), "seen"));
        Assert.Matches("<ol[^>]*class=\"bp-stepper", acc);
        // One primary action on the page: the next match.
        Assert.Single(Regex.Matches(html, "class=\"bp-btn-primary\""));
        Assert.Matches("<a[^>]*class=\"bp-btn-primary\"[^>]*href=\"/match\"", html);
    }

    [Fact]
    public async Task Sent_page_empty_state_has_a_photo_and_leads_to_match()
    {
        var coachId = h.Db(db => db.Coaches.Where(c => c.IsDemo && !db.Interests.Any(i => i.CoachId == c.Id)).OrderBy(c => c.Id).Select(c => c.Id).First());
        var client = await h.LoginAsync("coach", coachId);

        var html = await Html(client, "/me/sent");

        Assert.Contains($"src=\"{Ballpass.Web.Features.Media.StockMedia.SentEmpty.Url}\"", html);
        Assert.Matches("<a[^>]*class=\"bp-btn-primary\"[^>]*href=\"/match\"", html);
    }

    [Fact]
    public async Task Nav_shows_unread_replies_and_opening_sent_clears_them()
    {
        using var fx = new Fx(app);
        var coachId = h.CoachId(14);
        var now = DateTimeOffset.UtcNow;
        fx.Add(coachId, 0, now.AddDays(-3), seenAt: now.AddDays(-2), reply: InterestReply.Accepted, repliedAt: now.AddDays(-1));
        fx.Add(coachId, 1, now.AddDays(-3), seenAt: now.AddDays(-2), reply: InterestReply.Declined, repliedAt: now.AddDays(-1));
        fx.Add(coachId, 2, now.AddDays(-3));   // sent, no reply: not counted
        var expected = h.Db(db => db.Interests.Count(i => i.CoachId == coachId && i.Reply != InterestReply.None && i.ReplyReadAt == null));
        Assert.True(expected >= 2);
        var client = await h.LoginAsync("coach", coachId);

        Assert.Equal(expected, Unread(await Html(client, "/shortlist")));

        await Html(client, "/me/sent");

        Assert.Equal(0, Unread(await Html(client, "/shortlist")));
        Assert.Contains("href=\"/me/sent\"", await Html(client, "/shortlist"));
    }

    // ---------- 7. seed ----------

    [Fact]
    public void Seeded_lifecycle_is_30_percent_seen_15_percent_replied_and_deterministic()
    {
        var reset = new DateTimeOffset(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);
        var a = DemoDataGenerator.Generate(42).Interests;
        var b = DemoDataGenerator.Generate(42).Interests;

        InterestLifecycleSeeder.Apply(a, reset, 42);
        InterestLifecycleSeeder.Apply(b, reset, 42);

        Assert.Equal(40, a.Count);
        Assert.Equal(12, a.Count(i => i.SeenAt is not null));
        Assert.Equal(6, a.Count(i => i.Reply != InterestReply.None));
        Assert.True(a.Count(i => i.Reply == InterestReply.Accepted) > 0);
        Assert.True(a.Count(i => i.Reply == InterestReply.Declined) > 0);
        Assert.All(a.Where(i => i.Reply != InterestReply.None), i => Assert.NotNull(i.SeenAt));
        Assert.All(a.Where(i => i.SeenAt is not null), i => Assert.InRange(i.SeenAt!.Value, i.CreatedAt.AddTicks(1), reset));
        Assert.All(a.Where(i => i.RepliedAt is not null), i => Assert.InRange(i.RepliedAt!.Value, i.SeenAt!.Value.AddTicks(1), reset));
        Assert.All(a.Where(i => i.Reply == InterestReply.None), i => Assert.Null(i.RepliedAt));
        Assert.Equal(a.Select(i => (i.SeenAt, i.Reply, i.RepliedAt)), b.Select(i => (i.SeenAt, i.Reply, i.RepliedAt)));
    }

    // ---------- 8. persona switch ----------

    private (int CoachId, int AthleteId, string AthleteName, string CoachName) CoachWithInterests() => h.Db(db =>
    {
        var coachId = db.Interests.Where(i => i.IsDemo).GroupBy(i => i.CoachId).Where(g => g.Count() >= 2).OrderBy(g => g.Key).Select(g => g.Key).First();
        var latest = db.Interests.Where(i => i.CoachId == coachId).OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id).Include(i => i.Athlete).Include(i => i.Coach).First();
        return (coachId, latest.AthleteId, $"{latest.Athlete!.FirstName} {latest.Athlete.LastName}", $"{latest.Coach!.FirstName} {latest.Coach.LastName}");
    });

    [Fact]
    public async Task Switch_round_trip_coach_to_athlete_and_back()
    {
        var (coachId, _, athleteName, coachName) = CoachWithInterests();
        var client = await h.LoginAsync("coach", coachId);
        var page = await Html(client, "/shortlist");
        Assert.Contains("Δείτε το ως αθλήτρια", page);
        Assert.Contains("action=\"/demo/switch?to=athlete\"", page);

        var toAthlete = await h.PostAsync(client, "/demo/switch?to=athlete", new(), tokenPage: "/shortlist");

        Assert.Equal(HttpStatusCode.Redirect, toAthlete.StatusCode);
        Assert.Equal("/me/inbox", toAthlete.Headers.Location!.OriginalString);
        var inbox = await Html(client, "/me/inbox");
        Assert.Contains(athleteName, Regex.Match(inbox, "<span class=\"bp-whoami\"[^>]*>.*?</span>", RegexOptions.Singleline).Value);
        // Gendered label (orchestrator fix): "στην προπονήτρια" for a woman coach, "στον προπονητή" otherwise.
        var coachFirst = h.Db(db => db.Coaches.Where(c => c.Id == coachId).Select(c => c.FirstName).First());
        Assert.Contains(GreekNames.IsFemaleFirstName(coachFirst) ? "Πίσω στην προπονήτρια" : "Πίσω στον προπονητή", inbox);
        Assert.DoesNotContain("Δείτε το ως αθλήτρια", inbox);

        var back = await h.PostAsync(client, "/demo/switch?to=coach", new(), tokenPage: "/me/inbox");

        Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
        Assert.Equal("/me/sent", back.Headers.Location!.OriginalString);
        var sent = await Html(client, "/me/sent");
        Assert.Contains(coachName, Regex.Match(sent, "<span class=\"bp-whoami\"[^>]*>.*?</span>", RegexOptions.Singleline).Value);
        Assert.Contains("Δείτε το ως αθλήτρια", sent);
    }

    [Fact]
    public async Task Switch_falls_back_to_a_showcase_athlete_when_the_coach_sent_nothing()
    {
        var coachId = h.Db(db => db.Coaches.Where(c => c.IsDemo && c.Role == CoachRole.Head && !db.Interests.Any(i => i.CoachId == c.Id)).OrderBy(c => c.Id).Select(c => c.Id).FirstOrDefault());
        Assert.True(coachId != 0, "seed-42 has a head coach without interests");
        var client = await h.LoginAsync("coach", coachId);

        var response = await h.PostAsync(client, "/demo/switch?to=athlete", new(), tokenPage: "/shortlist");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/me/inbox")).StatusCode);
    }

    [Fact]
    public async Task Switch_is_protected()
    {
        var (coachId, athleteId, _, _) = CoachWithInterests();
        var coach = await h.LoginAsync("coach", coachId);
        var athlete = await h.LoginAsync("athlete", athleteId);

        Assert.Equal(HttpStatusCode.BadRequest, (await h.PostAsync(coach, "/demo/switch?to=athlete", new(), withToken: false)).StatusCode);
        var anon = await h.PostAsync(app.Client(), "/demo/switch?to=athlete", new(), tokenPage: "/demo");
        Assert.Equal(HttpStatusCode.Redirect, anon.StatusCode);
        Assert.StartsWith("/demo", anon.Headers.Location!.OriginalString);
        // A plain athlete (not switched from a coach) cannot "go back", and an athlete cannot switch to athlete.
        Assert.Equal(HttpStatusCode.Forbidden, (await h.PostAsync(athlete, "/demo/switch?to=coach", new(), tokenPage: "/me/inbox")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await h.PostAsync(athlete, "/demo/switch?to=athlete", new(), tokenPage: "/me/inbox")).StatusCode);
        var plainInbox = await Html(athlete, "/me/inbox");
        Assert.DoesNotContain("Πίσω στον προπονητή", plainInbox);
        Assert.DoesNotContain("Πίσω στην προπονήτρια", plainInbox);
        Assert.Equal(HttpStatusCode.BadRequest, (await h.PostAsync(coach, "/demo/switch?to=robot", new(), tokenPage: "/shortlist")).StatusCode);
    }

    [Fact]
    public async Task Demo_disabled_hides_the_pill_and_refuses_switch_and_reset()
    {
        var off = app.WithWebHostBuilder(b => b.UseSetting("Demo:Enabled", "false"));
        var coachId = h.CoachId(3);
        var client = off.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = new Dictionary<string, string> { ["personaType"] = "coach", ["personaId"] = coachId.ToString(), ["returnUrl"] = "", ["__RequestVerificationToken"] = FeedHarness.Token(await Html(client, "/demo")) };
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/demo/login", new FormUrlEncodedContent(login))).StatusCode);

        var page = await Html(client, "/shortlist");
        Assert.DoesNotContain("/demo/switch", page);
        Assert.DoesNotContain("/demo/reset", await Html(client, "/demo"));

        var token = FeedHarness.Token(page);
        var form = () => new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token });
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/demo/switch?to=athlete", form())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/demo/reset", form())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/demo/reset")).StatusCode);
    }

    // ---------- 9. reset ----------

    private (int Clubs, int Coaches, int Athletes, int Stats, int Interests, int Shortlist, int Seen, int Replied, int Accepted) Counts() => h.Db(db =>
        (db.Clubs.Count(), db.Coaches.Count(), db.Athletes.Count(), db.StatLines.Count(), db.Interests.Count(), db.ShortlistEntries.Count(),
         db.Interests.Count(i => i.SeenAt != null), db.Interests.Count(i => i.Reply != InterestReply.None), db.Interests.Count(i => i.Reply == InterestReply.Accepted)));

    [Fact]
    public async Task Reset_confirmation_page_explains_and_is_linked_from_demo()
    {
        var demo = await Html(app.Client(), "/demo");
        Assert.Contains("href=\"/demo/reset\"", demo);

        var html = await Html(app.Client(), "/demo/reset");

        Assert.Contains("όλα τα δεδομένα επανέρχονται", html);
        Assert.DoesNotContain("επίδειξης", html);   // flow-release O1
        Assert.Matches("<form[^>]*method=\"post\"[^>]*action=\"/demo/reset\"", html);
        Assert.False(string.IsNullOrEmpty(FeedHarness.Token(html)));
    }

    [Fact]
    public async Task Reset_requires_antiforgery_signs_out_redirects_with_toast_and_is_idempotent()
    {
        // The limiter counts every request of a persona (token or not), so the bad-token probe uses a different persona.
        var probe = await h.LoginAsync("coach", h.CoachId(3));
        Assert.Equal(HttpStatusCode.BadRequest, (await h.PostAsync(probe, "/demo/reset", new(), withToken: false)).StatusCode);
        var coach = await h.LoginAsync("coach", h.CoachId(2));

        var first = await h.PostAsync(coach, "/demo/reset", new(), tokenPage: "/demo/reset");
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        var location = first.Headers.Location!.OriginalString;
        Assert.StartsWith("/", location);
        Assert.False(location.StartsWith("//"));
        Assert.Contains("reset", location);
        var afterFirst = Counts();

        var toast = await Html(coach, location);
        Assert.Contains("data-toast=\"reset\"", toast);
        Assert.Contains("role=\"status\"", toast);
        var signedOut = await coach.GetAsync("/shortlist");
        Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
        Assert.StartsWith("/demo", signedOut.Headers.Location!.OriginalString);

        // The first reset re-created every persona with new ids: another signed-in persona has its own allowance.
        var other = await h.LoginAsync("coach", h.CoachId(2));
        var second = await h.PostAsync(other, "/demo/reset", new(), tokenPage: "/demo/reset");
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        var afterSecond = Counts();

        Assert.Equal(afterFirst, afterSecond);
        // The messages seed adds accepted (seen, replied) interests for its conversations on top of the generated 40.
        var extra = h.Db(db => db.Interests.Count()) - 40;
        Assert.InRange(extra, 15, 25);
        Assert.Equal(40 + extra, afterSecond.Interests);
        Assert.Equal(12 + extra, afterSecond.Seen);
        Assert.Equal(6 + extra, afterSecond.Replied);
        Assert.True(afterSecond.Accepted - extra is > 0 and < 6);
        Assert.Equal(afterSecond.Accepted, h.Db(db => db.Set<Conversation>().Count()));
    }
}
