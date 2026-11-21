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
        var coach = app.Client();
        var coachName = await SignIn(coach, "coach", 0);
        var slug = await FirstAthleteSlug(coach);

        await PostAction(coach, slug, "interest", new() { ["note"] = "Θα θέλαμε να σας δούμε σε προπόνηση." });
        await PostAction(coach, slug, "interest", new() { ["note"] = "Δεύτερη φορά." });

        Assert.DoesNotContain("data-action=\"interest\"", await Html(coach, $"/p/{slug}")); // confirmation replaces form

        var athlete = app.Client();
        await SignInAthlete(athlete, slug);
        var inbox = await Html(athlete, "/me/inbox");
        Assert.Single(Regex.Matches(inbox, $"<li[^>]*class=\"interest\"[^>]*data-coach=\"{Regex.Escape(coachName)}\""));
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

    // ---------- helpers ----------

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
