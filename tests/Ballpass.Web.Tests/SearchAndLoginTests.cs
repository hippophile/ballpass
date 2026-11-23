using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Loop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ballpass.Web.Tests;

// Specs: openspec/changes/add-demo-login-and-search/specs/{demo-login,coach-search}/spec.md + flow-release/specs/demo-login/spec.md
[Collection("db")]
public class SearchAndLoginTests(BallpassAppFactory app)
{
    private static readonly DemoDataSet Data = DemoDataGenerator.Generate(42);
    private static readonly List<Athlete> Women = Data.Athletes.Where(a => a.Gender == Gender.Female).ToList();
    private static readonly Dictionary<string, Athlete> BySlug = Data.Athletes.ToDictionary(a => a.Slug);
    private static int Age(Athlete a) => DateTime.UtcNow.Year - a.BirthYear;

    // ---------- login / authorization ----------

    [Fact]
    public async Task Anonymous_search_redirects_to_demo_login()
    {
        var response = await app.Client().GetAsync("/search");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/demo", PathOf(response.Headers.Location!));
        Assert.Contains("ReturnUrl", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Coach_login_from_the_list_redirects_to_search_and_shows_name()
    {
        var client = app.Client();
        var (login, name) = await SignIn(client, "coach", listRow: true);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/search", PathOf(login.Headers.Location!));
        var html = await Html(client, "/search");
        Assert.Contains(name, html);
    }

    // ---------- /demo layout (flow-release §4.2, O7) ----------

    private Featured FeaturedPersonas()
    {
        var f = app.Services.GetRequiredService<IOptions<DemoOptions>>().Value.Featured;
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
        return new(db.Coaches.Where(c => c.IsDemo && c.FirstName == f.CoachFirstName && c.LastName == f.CoachLastName).Select(c => c.Id).Single(),
            db.Athletes.Where(a => a.IsDemo && a.Slug == f.AthleteSlug).Select(a => a.Id).Single(),
            "/match?need=featured");
    }

    private sealed record Featured(int CoachId, int AthleteId, string CoachReturn);

    [Fact]
    public async Task Demo_page_leads_with_the_two_featured_personas_in_photo_cards()
    {
        var featured = FeaturedPersonas();
        var page = await Html(app.Client(), "/demo");

        var cards = Regex.Matches(page, "<div[^>]*class=\"[^\"]*bp-persona-hero-card[^\"]*\"[^>]*>.*?</form>", RegexOptions.Singleline).Select(m => m.Value).ToList();
        Assert.Equal(2, cards.Count);
        Assert.All(cards, c => Assert.Matches("<img[^>]*src=\"/media/stock/", c));
        var coach = Regex.Match(page, "<form[^>]*data-persona=\"coach\"[^>]*>.*?</form>", RegexOptions.Singleline).Value;
        var athlete = Regex.Match(page, "<form[^>]*data-persona=\"athlete\"[^>]*>.*?</form>", RegexOptions.Singleline).Value;
        Assert.Contains("data-featured", coach);
        Assert.Contains("data-featured", athlete);
        Assert.Contains($"name=\"personaId\" value=\"{featured.CoachId}\"", coach);
        Assert.Contains($"name=\"returnUrl\" value=\"{featured.CoachReturn}\"", coach);
        Assert.Contains($"name=\"personaId\" value=\"{featured.AthleteId}\"", athlete);
        Assert.Contains("bp-btn-primary", coach);
        Assert.Contains("bp-btn-athlete", athlete);
        // Featured personas are not repeated in the lists.
        Assert.Single(Regex.Matches(page, $"name=\"personaType\" value=\"coach\"[^>]*>\\s*<input[^>]*name=\"personaId\" value=\"{featured.CoachId}\""));
        Assert.Single(Regex.Matches(page, $"name=\"personaType\" value=\"athlete\"[^>]*>\\s*<input[^>]*name=\"personaId\" value=\"{featured.AthleteId}\""));
    }

    [Fact]
    public async Task Featured_athlete_card_signs_in_and_opens_the_inbox()
    {
        var client = app.Client();
        var (login, _) = await SignIn(client, "athlete");

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/me/inbox", login.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/me/inbox")).StatusCode);
    }

    [Theory]
    [InlineData("coaches")]
    [InlineData("athletes")]
    public async Task Demo_lists_show_at_most_8_rows_before_a_details_with_the_total(string list)
    {
        var page = await Html(app.Client(), "/demo");

        var section = Regex.Match(page, $"<section[^>]*data-list=\"{list}\"[^>]*>.*?</section>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(section);
        var details = section.IndexOf("<details", StringComparison.Ordinal);
        var visible = details < 0 ? section : section[..details];
        var shown = Regex.Matches(visible, "<form[^>]*class=\"bp-persona-row\"").Count;
        var total = Regex.Matches(section, "<form[^>]*class=\"bp-persona-row\"").Count;
        Assert.InRange(shown, 1, 8);
        if (total > 8)
            Assert.Matches($"<summary[^>]*>[^<]*\\({total}\\)", section);
    }

    [Fact]
    public async Task Login_blocks_open_redirect()
    {
        var client = app.Client();
        var (login, _) = await SignIn(client, "coach", returnUrl: "https://evil.example/");

        Assert.DoesNotContain("evil.example", login.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Athlete_is_denied_search_with_403()
    {
        var client = app.Client();
        await SignIn(client, "athlete");

        var search = await client.GetAsync("/search");
        Assert.Equal(HttpStatusCode.Redirect, search.StatusCode);
        var denied = await client.GetAsync(search.Headers.Location);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Logout_removes_access()
    {
        var client = app.Client();
        await SignIn(client, "coach");
        var token = Token(await Html(client, "/search"));

        var logout = await client.PostAsync("/demo/logout",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        var search = await client.GetAsync("/search");
        Assert.Equal(HttpStatusCode.Redirect, search.StatusCode);
        Assert.StartsWith("/demo", PathOf(search.Headers.Location!));
    }

    // ---------- search ----------

    [Fact]
    public async Task Default_search_shows_all_women_paged_by_20()
    {
        var (total, slugs) = await Search("");

        Assert.Equal(Women.Count, total);
        Assert.Equal(20, slugs.Count);
        Assert.All(slugs, s => Assert.Equal(Gender.Female, BySlug[s].Gender));
    }

    [Fact]
    public async Task Position_and_min_height_filter()
    {
        var expected = Women.Count(a => a.Position == Position.C && a.HeightCm >= 185);

        var (total, slugs) = await Search("position=C&minHeight=185");

        Assert.Equal(expected, total);
        Assert.All(slugs, s => Assert.True(BySlug[s].Position == Position.C && BySlug[s].HeightCm >= 185));
    }

    [Fact]
    public async Task Looking_for_team_filter()
    {
        var (total, _) = await Search("looking=true");

        Assert.Equal(Women.Count(a => a.LookingForTeam), total);
    }

    [Fact]
    public async Task Region_league_and_age_filters()
    {
        var region = Women.GroupBy(a => a.Region).OrderByDescending(g => g.Count()).First().Key;
        Assert.Equal(Women.Count(a => a.Region == region), (await Search($"region={Uri.EscapeDataString(region)}")).total);

        Assert.Equal(Women.Count(a => a.CurrentClub?.League == League.A1), (await Search("league=A1")).total);

        Assert.Equal(Women.Count(a => Age(a) is >= 20 and <= 25), (await Search("minAge=20&maxAge=25")).total);
    }

    [Fact]
    public async Task Name_search_by_surname()
    {
        var surname = Women[0].LastName;

        var (total, slugs) = await Search($"q={Uri.EscapeDataString(surname)}");

        Assert.Equal(Women.Count(a => a.LastName == surname), total);
        Assert.All(slugs, s => Assert.Equal(surname, BySlug[s].LastName));
    }

    [Fact]
    public async Task Page_two_differs_from_page_one()
    {
        var (_, page1) = await Search("");
        var (_, page2) = await Search("page=2");

        Assert.Equal(20, page2.Count);
        Assert.Empty(page1.Intersect(page2));
    }

    // ---------- helpers ----------

    private async Task<(int total, List<string> slugs)> Search(string query)
    {
        var client = app.Client();
        await SignIn(client, "coach");
        var html = await Html(client, "/search?" + query);

        var total = int.Parse(Regex.Match(html, "data-total=\"(\\d+)\"").Groups[1].Value);
        var slugs = Regex.Matches(html, "<article[^>]*class=\"(?:result|bp-find-match)\"[^>]*data-slug=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value).ToList();
        return (total, slugs);
    }

    /// Submits the first persona form of the given type on /demo. Returns the login response and the persona name.
    private static async Task<(HttpResponseMessage response, string name)> SignIn(HttpClient client, string type, string? returnUrl = null, bool listRow = false)
    {
        var page = await Html(client, "/demo");
        var form = Regex.Match(page, $"<form{(listRow ? "(?![^>]*data-featured)" : "")}[^>]*data-persona=\"{type}\"[^>]*>(.*?)</form>", RegexOptions.Singleline);
        Assert.True(form.Success, $"no {type} persona form on /demo");

        var fields = Regex.Matches(form.Groups[1].Value, "<input[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"")
            .ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));
        if (returnUrl is not null) fields["returnUrl"] = returnUrl;
        var name = WebUtility.HtmlDecode(Regex.Match(form.Groups[0].Value, "data-name=\"([^\"]+)\"").Groups[1].Value);

        var response = await client.PostAsync("/demo/login", new FormUrlEncodedContent(fields));
        return (response, name);
    }

    private static async Task<string> Html(HttpClient client, string path) =>
        WebUtility.HtmlDecode(await (await client.GetAsync(path)).Content.ReadAsStringAsync());

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

    private static string PathOf(Uri location) =>
        location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?')[0];
}
