using System.Globalization;
using System.Net;
using System.Resources;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Features.Media;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/flow-release/specs/home/spec.md (SPEC §4.1, §4.9, §8 O1-O3, O7)
[Collection("db")]
public class HomeTests(BallpassAppFactory app)
{
    private readonly FeedHarness h = new(app);

    // O7 personas, set explicitly so the test does not depend on the Development values (WP-B may change the athlete).
    private const string CoachFirst = "Αικατερίνη", CoachLast = "Φωτοπούλου", AthleteSlug = "stavroula-ioannidou", Need = "position=SG&region=Crete";

    private WebApplicationFactory<Program> Featured(bool enabled = true) => app.WithWebHostBuilder(b => b
        .UseSetting("Demo:Enabled", enabled ? "true" : "false")
        .UseSetting("Demo:Featured:CoachFirstName", CoachFirst)
        .UseSetting("Demo:Featured:CoachLastName", CoachLast)
        .UseSetting("Demo:Featured:AthleteSlug", AthleteSlug)
        .UseSetting("Demo:Featured:NeedQuery", Need));

    private static HttpClient Client(WebApplicationFactory<Program> f, string? culture = null)
    {
        var client = f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (culture is not null)
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Culture={Uri.EscapeDataString($"c={culture}|uic={culture}")}");
        return client;
    }

    private static Dictionary<string, string> Fields(string form) =>
        Regex.Matches(form, "<input[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"")
            .ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));

    private static string Door(string html, string side) =>
        Regex.Match(html, $"<form[^>]*data-door=\"{side}\"[^>]*>.*?</form>", RegexOptions.Singleline).Value;

    // ---------- doors ----------

    [Fact]
    public async Task Home_has_two_doors_posting_the_demo_login_with_the_featured_personas()
    {
        var (coachId, athleteId) = h.Db(db => (
            db.Coaches.Where(c => c.IsDemo && c.FirstName == CoachFirst && c.LastName == CoachLast).Select(c => c.Id).Single(),
            db.Athletes.Where(a => a.Slug == AthleteSlug).Select(a => a.Id).Single()));
        var client = Client(Featured());

        var html = await FeedHarness.Html(client, "/");

        Assert.Equal(2, Regex.Matches(html, "<form[^>]*action=\"/demo/login\"[^>]*data-door=").Count);
        var coach = Fields(Door(html, "coach"));
        Assert.Equal(("coach", coachId.ToString(), "/match?need=featured"), (coach["personaType"], coach["personaId"], coach["returnUrl"]));
        var athlete = Fields(Door(html, "athlete"));
        Assert.Equal(("athlete", athleteId.ToString(), "/me/inbox"), (athlete["personaType"], athlete["personaId"], athlete["returnUrl"]));
        Assert.Contains("bp-btn-primary", Door(html, "coach"));
        Assert.Contains("bp-btn-athlete", Door(html, "athlete"));

        // The coach door lands on /match with the featured need (WP-B's ?need=featured shortcut reads Demo:Featured:NeedQuery).
        var login = await client.PostAsync("/demo/login", new FormUrlEncodedContent(coach));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/match?need=featured", login.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Coach_door_uses_the_featured_shortcut_and_never_copies_the_need_into_the_page()
    {
        var html = await FeedHarness.Html(Client(Featured()), "/");

        Assert.Equal("/match?need=featured", Fields(Door(html, "coach"))["returnUrl"]);
        Assert.DoesNotContain(Need, html);
    }

    [Fact]
    public async Task Doors_link_to_the_demo_page_when_the_demo_is_disabled()
    {
        var html = await FeedHarness.Html(Client(Featured(enabled: false)), "/");

        Assert.DoesNotContain("action=\"/demo/login\"", html);
        Assert.Equal(2, Regex.Matches(html, "<a[^>]*data-door=\"(coach|athlete)\"[^>]*href=\"/demo\"").Count);
    }

    [Fact]
    public async Task Home_opens_on_the_hero_video_and_the_old_blocks_are_gone()
    {
        var html = await FeedHarness.Html(app.Client(), "/");

        Assert.Matches("<video\\b[^>]*autoplay", html);
        Assert.Contains($"src=\"{StockMedia.Hero.Url}\"", html);
        Assert.Matches("<h1[^>]*class=\"[^\"]*bp-hero-title", html);
        Assert.Contains(StockMedia.DoorCoach.Url, html);
        Assert.Contains(StockMedia.DoorAthlete.Url, html);
        Assert.Contains(StockMedia.ConnectCoachPlayer.Url, html);
        Assert.DoesNotContain("data-cta=\"demo\"", html);
        Assert.DoesNotContain("data-cta=\"sample\"", html);
        Assert.DoesNotContain("class=\"bp-steps\"", html);
        Assert.DoesNotContain("class=\"bp-trust\"", html);
        // Connection band: three steps, athlete steps teal, coach step orange, the source badges under step 2.
        var steps = Regex.Matches(html, "<li[^>]*class=\"(bp-step-(?:coach|athlete)) bp-step-done\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["bp-step-athlete", "bp-step-coach", "bp-step-athlete"], steps);
    }

    [Fact]
    public async Task Home_in_english()
    {
        var html = await FeedHarness.Html(app.Client("en"), "/");

        Assert.Contains("Find a player", html);
        Assert.Matches("<html[^>]*lang=\"en\"", html);
    }

    // ---------- counters, connections, activity ----------

    [Fact]
    public async Task Counters_equal_the_database_counts()
    {
        var accepted = AddAcceptedInterest();
        try
        {
            var html = await FeedHarness.Html(app.Client(), "/");

            var (looking, interests, connections) = h.Db(db => (
                db.Athletes.Count(a => a.IsDemo && a.LookingForTeam),
                db.Interests.Count(),
                db.Interests.Count(i => i.Reply == InterestReply.Accepted)));
            Assert.True(connections >= 1);
            Assert.Equal(looking, Count(html, "looking"));
            Assert.Equal(interests, Count(html, "interests"));
            Assert.Equal(connections, Count(html, "connections"));
            Assert.DoesNotContain("data-count=\"athletes\"", html);
        }
        finally { h.Db(db => db.Interests.Where(i => i.Id == accepted).ExecuteDelete()); }
    }

    [Fact]
    public async Task Recent_connections_show_the_latest_six_accepted_and_hide_when_there_are_none()
    {
        var accepted = AddAcceptedInterest();
        try
        {
            var html = await FeedHarness.Html(app.Client(), "/");
            var total = h.Db(db => db.Interests.Count(i => i.Reply == InterestReply.Accepted));
            var cards = Regex.Matches(html, "data-connection-id=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)).ToList();
            Assert.Equal(Math.Min(6, total), cards.Count);
            Assert.Equal(accepted, cards[0]);   // newest first

            // Temporarily no accepted interests: the section disappears.
            var ids = h.Db(db => db.Interests.Where(i => i.Reply == InterestReply.Accepted).Select(i => i.Id).ToList());
            h.Db(db => db.Interests.Where(i => ids.Contains(i.Id)).ExecuteUpdate(s => s.SetProperty(i => i.Reply, InterestReply.None)));
            try
            {
                Assert.DoesNotContain("data-connection-id", await FeedHarness.Html(app.Client(), "/"));
            }
            finally { h.Db(db => db.Interests.Where(i => ids.Contains(i.Id)).ExecuteUpdate(s => s.SetProperty(i => i.Reply, InterestReply.Accepted))); }
        }
        finally { h.Db(db => db.Interests.Where(i => i.Id == accepted).ExecuteDelete()); }
    }

    [Fact]
    public async Task Activity_cards_have_a_feed_pool_photo_header()
    {
        var html = await FeedHarness.Html(app.Client(), "/");

        var section = Regex.Match(html, "<section[^>]*class=\"bp-feed-preview\".*?</section>", RegexOptions.Singleline).Value;
        var ids = Regex.Matches(section, "data-post-id=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToList();
        Assert.Equal(3, ids.Count);
        foreach (var id in ids) Assert.Contains($"src=\"{StockMedia.Feed(id).Url}\"", section);
    }

    // ---------- copy ----------

    private static readonly Regex Singular = new(
        @"(?<!\p{L})(Δες|Βρες|Διάλεξε|Ψάχνεις|Μπες|Στείλε|Ακολούθησε|Συνδέσου|Δοκίμασε|Ξεκίνα|Πάτα|Επίλεξε|Γράψε|Έχεις|σε θέλει|σου|εσένα)(?!\p{L})",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    [Fact]
    public void Flow_resource_uses_the_formal_register_and_has_both_languages()
    {
        var manager = new ResourceManager("Ballpass.Web.Resources.FlowResource", typeof(FlowResource).Assembly);
        var el = manager.GetResourceSet(new CultureInfo("el"), createIfNotExists: true, tryParents: true)!;
        var en = manager.GetResourceSet(new CultureInfo("en"), createIfNotExists: true, tryParents: false)!;
        var offenders = new List<string>();
        var keys = new List<string>();
        foreach (System.Collections.DictionaryEntry e in el)
        {
            keys.Add((string)e.Key);
            if (e.Value is string text && Singular.Match(text) is { Success: true } m) offenders.Add($"{e.Key}: «{m.Value}»");
            if (e.Value is string t2 && Regex.IsMatch(t2, "(?i)demo|επίδειξ|δείγμα|φανταστικ")) offenders.Add($"{e.Key}: demo word");
        }
        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
        Assert.NotEmpty(keys);
        foreach (var k in keys) Assert.False(string.IsNullOrEmpty(en.GetString(k)), $"{k} has no English text");
    }

    [Fact]
    public void A_woman_coach_never_gets_a_photo_of_a_man()
    {
        // Every face in the coach pool is the same man (O3); the face-free scene must be credited faces:false.
        var credits = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo(), "src", "Ballpass.Web", "wwwroot", "media", "stock", "credits.json")))
            .RootElement.EnumerateArray().ToDictionary(e => e.GetProperty("name").GetString()!, e => e.GetProperty("faces").GetBoolean());
        Assert.False(credits[Ballpass.Web.Features.Flow.FlowMedia.CoachNoFace.File]);
        Assert.Equal(Ballpass.Web.Features.Flow.FlowMedia.CoachNoFace, Ballpass.Web.Features.Flow.FlowMedia.CoachScene(7, female: true));
        Assert.Equal(Ballpass.Web.Features.Flow.FlowMedia.CoachNoFace, Ballpass.Web.Features.Flow.FlowMedia.FeaturedCoach(female: true));
        Assert.Equal(StockMedia.Coach(7), Ballpass.Web.Features.Flow.FlowMedia.CoachScene(7, female: false));
    }

    private static string Repo()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Ballpass.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repo root not found");
    }

    // ---------- not found ----------

    [Fact]
    public async Task Not_found_has_a_photo_band_and_leads_back_to_the_feed_or_to_match()
    {
        var anon = await FeedHarness.Html(app.Client(), "/does-not-exist");
        Assert.Contains($"src=\"{StockMedia.NotFound.Url}\"", anon);
        Assert.Contains("Δεν βρέθηκε", anon);
        Assert.Matches("<a[^>]*class=\"bp-btn-primary\"[^>]*href=\"/feed\"", anon);

        var coach = await FeedHarness.Html(await h.LoginAsync("coach", h.CoachId(1)), "/does-not-exist");
        Assert.Matches("<a[^>]*class=\"bp-btn-primary\"[^>]*href=\"/match\"", coach);
    }

    // ---------- helpers ----------

    private static int Count(string html, string key) =>
        int.Parse(Regex.Match(html, $"data-count=\"{key}\"[^>]*>\\s*(\\d+)\\s*<").Groups[1].Value);

    /// <summary>A newly accepted interest between a head coach and a woman with no interest from that coach; returns its id.</summary>
    private int AddAcceptedInterest() => h.Db(db =>
    {
        var coach = db.Coaches.Where(c => c.IsDemo && c.Role == CoachRole.Head && c.Club!.Gender == Gender.Female).OrderBy(c => c.Id).First();
        var athlete = db.Athletes.Where(a => a.IsDemo && a.Gender == Gender.Female && !db.Interests.Any(i => i.CoachId == coach.Id && i.AthleteId == a.Id))
            .OrderByDescending(a => a.Id).First();
        var now = DateTimeOffset.UtcNow;
        var interest = new Interest
        {
            CoachId = coach.Id, AthleteId = athlete.Id, CreatedAt = now.AddMinutes(-5), SeenAt = now.AddMinutes(-3),
            Reply = InterestReply.Accepted, RepliedAt = now.AddMinutes(-1), Note = "Δοκιμή", IsDemo = true,
        };
        db.Interests.Add(interest);
        db.SaveChanges();
        return interest.Id;
    });
}
