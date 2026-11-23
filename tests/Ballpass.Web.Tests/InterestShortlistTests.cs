using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Demo;

namespace Ballpass.Web.Tests;

// Specs: openspec/changes/add-interest-and-shortlist/specs/{interest,shortlist}/spec.md
[Collection("db")]
public class InterestShortlistTests(BallpassAppFactory app)
{
    [Fact]
    public void Generator_seeds_40_interests_and_3_to_5_shortlist_entries_per_head_coach()
    {
        var data = DemoDataGenerator.Generate(42);

        Assert.Equal(40, data.Interests.Count);
        var perCoach = data.Shortlist.GroupBy(s => s.Coach).ToList();
        Assert.Equal(20, perCoach.Count);
        Assert.All(perCoach, g => Assert.InRange(g.Count(), 3, 5));
        Assert.All(data.Interests, i => Assert.True(i.IsDemo));
    }

    [Fact]
    public async Task Anonymous_profile_has_no_actions()
    {
        var slug = await FirstAthleteSlug(app.Client());

        var html = await Html(app.Client(), $"/p/{slug}");

        Assert.DoesNotContain("data-action=\"interest\"", html);
        Assert.DoesNotContain("data-action=\"shortlist\"", html);
    }

    [Fact]
    public async Task Coach_profile_has_interest_and_shortlist_actions()
    {
        // Pair #3/#3 is used by no other test, so the interest form is still present regardless of test order.
        var coach = app.Client();
        await SignIn(coach, "coach", 3);
        var slug = await AthleteSlug(coach, 3);

        var html = await Html(coach, $"/p/{slug}");

        Assert.Contains("data-action=\"interest\"", html);
        Assert.Contains("data-action=\"shortlist\"", html);
    }

    [Fact]
    public async Task Interest_sent_twice_appears_once_in_athlete_inbox()
    {
        // Audit M1: the pair must start with NO interest, otherwise the test passes without anything being saved.
        var coach = app.Client();
        var coachName = await SignIn(coach, "coach", 0);
        string? slug = null;
        for (var i = 0; i < 12 && slug is null; i++)
        {
            var candidate = await AthleteSlug(app.Client(), i);
            if (await InboxCount(candidate, coachName) == 0) slug = candidate;
        }
        Assert.NotNull(slug);

        await PostAction(coach, slug, "interest", new() { ["note"] = "Θα θέλαμε να σας δούμε σε προπόνηση." });
        Assert.Equal(1, await InboxCount(slug, coachName));

        await PostAction(coach, slug, "interest", new() { ["note"] = "Δεύτερη φορά." });
        Assert.Equal(1, await InboxCount(slug, coachName));

        var profile = await Html(coach, $"/p/{slug}");
        Assert.DoesNotContain("data-action=\"interest\"", profile); // confirmation replaces form
        Assert.Contains("data-rail=\"sent\"", profile);                 // flow-release §4.4: the rail swaps to the sent card
        Assert.Contains("✓ Στάλθηκε", profile);
    }

    [Fact]
    public async Task Note_over_280_chars_is_rejected()
    {
        var coach = app.Client();
        var coachName = await SignIn(coach, "coach", 1);
        var slug = await AthleteSlug(coach, 1);
        var athlete = app.Client();
        await SignInAthlete(athlete, slug);
        var before = InterestCount(await Html(athlete, "/me/inbox"), coachName);

        await PostAction(coach, slug, "interest", new() { ["note"] = new string('α', 281) });

        Assert.Equal(before, InterestCount(await Html(athlete, "/me/inbox"), coachName));
    }

    [Fact]
    public async Task Athlete_cannot_express_interest()
    {
        var athlete = app.Client();
        var slug = await FirstAthleteSlug(athlete);
        await SignInAthlete(athlete, slug);
        var token = Token(await Html(athlete, $"/p/{slug}"));

        var response = await athlete.PostAsync($"/p/{slug}/interest", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["note"] = "x" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Shortlist_toggle_adds_then_removes()
    {
        var coach = app.Client();
        await SignIn(coach, "coach", 2);
        var slug = await AthleteSlug(coach, 2);
        var initiallyIn = (await Html(coach, "/shortlist")).Contains($"data-slug=\"{slug}\"");

        await PostAction(coach, slug, "shortlist", new());
        Assert.Equal(!initiallyIn, (await Html(coach, "/shortlist")).Contains($"data-slug=\"{slug}\""));

        await PostAction(coach, slug, "shortlist", new());
        Assert.Equal(initiallyIn, (await Html(coach, "/shortlist")).Contains($"data-slug=\"{slug}\""));
    }

    [Fact]
    public async Task Shortlist_requires_coach()
    {
        var response = await app.Client().GetAsync("/shortlist");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/demo", response.Headers.Location!.OriginalString);
    }

    // ---------- flow-release WP-B: sent rail (SPEC §4.4) and shortlist grid (SPEC §4.6) ----------

    private static int Primaries(string html) => Regex.Matches(html, "class=\"[^\"]*\\bbp-btn-primary\\b[^\"]*\"").Count;

    [Fact]
    public async Task Interest_sent_rail_offers_the_switch_to_the_athlete()
    {
        var h = new FeedHarness(app);
        var (coach, slug, firstName) = h.Db(db =>
        {
            var c = db.Coaches.Where(x => x.IsDemo && x.Role == Ballpass.Web.Data.CoachRole.Head && x.Club!.Gender == Ballpass.Web.Data.Gender.Female)
                .OrderBy(x => x.Id).Skip(4).Select(x => x.Id).First();
            var a = db.Athletes.Where(x => x.IsDemo && x.Gender == Ballpass.Web.Data.Gender.Female && !db.Interests.Any(i => i.CoachId == c && i.AthleteId == x.Id))
                .OrderByDescending(x => x.Id).Select(x => new { x.Slug, x.FirstName }).First();
            return (c, a.Slug, a.FirstName);
        });
        var client = await h.LoginAsync("coach", coach);
        await PostAction(client, slug, "interest", new());

        var html = await Html(client, $"/p/{slug}?status=interest-sent");

        var rail = Regex.Match(html, "<aside[^>]*class=\"bp-action-rail\".*?</aside>", RegexOptions.Singleline).Value;
        Assert.Contains("data-rail=\"sent\"", rail);
        Assert.Contains("✓ Στάλθηκε", rail);
        Assert.Matches("<ol[^>]*class=\"bp-stepper[^\"]*\"", rail);
        var form = Regex.Match(rail, "<form[^>]*action=\"/demo/switch\\?to=athlete\"[^>]*>(.*?)</form>", RegexOptions.Singleline);
        Assert.True(form.Success, "switch form missing");
        Assert.Matches("method=\"post\"", form.Value);
        Assert.Contains("__RequestVerificationToken", form.Value);
        Assert.Contains($"Δείτε τι βλέπει η {firstName}", form.Value);
        Assert.Contains("href=\"/me/sent\"", rail);
        Assert.DoesNotContain("data-action=\"interest\"", html);
        Assert.Equal(0, Primaries(html));
        Assert.DoesNotContain("class=\"bp-status\"", html);                // the rail replaces the status line
    }

    [Fact]
    public async Task Shortlist_is_a_card_grid_with_one_primary_a_ghost_remove_and_a_compare_panel()
    {
        var h = new FeedHarness(app);
        var coach = h.Db(db => db.Coaches.Where(c => c.IsDemo && c.Role == Ballpass.Web.Data.CoachRole.Head && db.ShortlistEntries.Count(s => s.CoachId == c.Id) >= 3)
            .OrderBy(c => c.Id).Skip(6).Select(c => c.Id).First());
        var client = await h.LoginAsync("coach", coach);

        var html = await Html(client, "/shortlist");

        var cards = Regex.Matches(html, "<article[^>]*class=\"bp-find-match\"[^>]*data-variant=\"shortlist\".*?</article>", RegexOptions.Singleline).Select(m => m.Value).ToList();
        Assert.True(cards.Count >= 3, $"{cards.Count} shortlist cards");
        Assert.Equal(1, Primaries(html));
        var firstOpen = cards.FindIndex(c => c.Contains("data-action=\"interest\""));
        if (firstOpen >= 0) Assert.Contains("class=\"bp-btn-primary\"", cards[firstOpen]);
        Assert.All(cards, c => Assert.Matches("<button[^>]*class=\"bp-btn-ghost[^\"]*\"[^>]*>Αφαίρεση<", c));
        Assert.All(cards, c => Assert.Equal(3, Regex.Matches(c, "class=\"bp-find-stat\"").Count + Regex.Matches(c, "class=\"bp-find-stat bp-find-stat-none\"").Count));
        Assert.Matches("<aside[^>]*class=\"bp-shortlist-compare\"", html);
        Assert.Contains("class=\"bp-shortlist\"", html);
    }

    [Fact]
    public async Task Shortlist_card_shows_a_sent_pill_after_an_interest()
    {
        var h = new FeedHarness(app);
        var (coach, slug) = h.Db(db =>
        {
            var e = db.ShortlistEntries.Where(s => s.Coach!.IsDemo && s.Coach.Role == Ballpass.Web.Data.CoachRole.Head
                    && !db.Interests.Any(i => i.CoachId == s.CoachId && i.AthleteId == s.AthleteId))
                .OrderBy(s => s.CoachId).Skip(30).Select(s => new { s.CoachId, s.Athlete!.Slug }).First();
            return (e.CoachId, e.Slug);
        });
        var client = await h.LoginAsync("coach", coach);
        await PostAction(client, slug, "interest", new());

        var html = await Html(client, "/shortlist");

        var card = Regex.Match(html, $"<article[^>]*data-slug=\"{slug}\".*?</article>", RegexOptions.Singleline).Value;
        Assert.Contains("class=\"bp-find-sent\"", card);
        Assert.Contains("✓ Στάλθηκε", card);
        Assert.DoesNotContain("data-action=\"interest\"", card);
    }

    [Fact]
    public async Task Empty_shortlist_shows_a_photo_band_and_a_cta_to_match()
    {
        var h = new FeedHarness(app);
        var coach = h.Db(db => db.Coaches.Where(c => c.IsDemo && !db.ShortlistEntries.Any(s => s.CoachId == c.Id)).OrderBy(c => c.Id).Select(c => c.Id).First());
        var client = await h.LoginAsync("coach", coach);

        var html = await Html(client, "/shortlist");

        var band = Regex.Match(html, "<section[^>]*class=\"bp-shortlist-empty\".*?</section>", RegexOptions.Singleline).Value;
        Assert.Contains($"src=\"{Ballpass.Web.Features.Media.StockMedia.ShortlistEmpty.Url}\"", band);
        Assert.Contains("Η λίστα σας είναι άδεια", band);
        Assert.Matches("<a[^>]*class=\"bp-btn-primary\"[^>]*href=\"/match\"|<a[^>]*href=\"/match\"[^>]*class=\"bp-btn-primary\"", band);
        Assert.Equal(1, Primaries(html));
    }

    // ---------- helpers ----------

    private async Task<int> InboxCount(string slug, string coachName)
    {
        var athlete = app.Client();
        await SignInAthlete(athlete, slug);
        return InterestCount(await Html(athlete, "/me/inbox"), coachName);
    }

    private static int InterestCount(string inbox, string coachName) =>
        Regex.Matches(inbox, $"<li[^>]*class=\"interest\"[^>]*data-coach=\"{Regex.Escape(coachName)}\"").Count;

    private static async Task PostAction(HttpClient client, string slug, string action, Dictionary<string, string> fields)
    {
        var page = await Html(client, $"/p/{slug}");
        // The interest form is replaced by a confirmation once sent, so a repeat post (the duplicate test)
        // uses any antiforgery token on the page.
        var token = Token(page);
        Assert.False(string.IsNullOrEmpty(token), $"no antiforgery token on profile for {action}");
        fields["__RequestVerificationToken"] = token;
        var response = await client.PostAsync($"/p/{slug}/{action}", new FormUrlEncodedContent(fields));
        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.OK or HttpStatusCode.BadRequest,
            $"unexpected {(int)response.StatusCode}");
    }

    private static Task<string> FirstAthleteSlug(HttpClient client) => AthleteSlug(client, 0);

    private static async Task<string> AthleteSlug(HttpClient client, int index)
    {
        var demo = await Html(client, "/demo");
        var slugs = Regex.Matches(demo, "<form[^>]*data-persona=\"athlete\"[^>]*data-slug=\"([^\"]+)\"|<form[^>]*data-slug=\"([^\"]+)\"[^>]*data-persona=\"athlete\"")
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).ToList();
        Assert.True(slugs.Count > index, "athlete persona forms need data-slug");
        return slugs[index];
    }

    private static async Task<string> SignIn(HttpClient client, string type, int index)
    {
        var page = await Html(client, "/demo");
        var form = Regex.Matches(page, $"<form[^>]*data-persona=\"{type}\"[^>]*>.*?</form>", RegexOptions.Singleline)[index].Value;
        return await Submit(client, form);
    }

    private static async Task SignInAthlete(HttpClient client, string slug)
    {
        var page = await Html(client, "/demo");
        var form = Regex.Matches(page, "<form[^>]*data-persona=\"athlete\"[^>]*>.*?</form>", RegexOptions.Singleline)
            .Select(m => m.Value).First(f => f.Contains($"data-slug=\"{slug}\""));
        await Submit(client, form);
    }

    private static async Task<string> Submit(HttpClient client, string form)
    {
        var fields = Regex.Matches(form, "<input[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"")
            .ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));
        var name = WebUtility.HtmlDecode(Regex.Match(form, "data-name=\"([^\"]+)\"").Groups[1].Value);
        var response = await client.PostAsync("/demo/login", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return name;
    }

    private static async Task<string> Html(HttpClient client, string path) =>
        WebUtility.HtmlDecode(await (await client.GetAsync(path)).Content.ReadAsStringAsync());

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
}
