using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Demo;
using Microsoft.Extensions.DependencyInjection;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/fix-audit-findings/specs/hardening/spec.md (findings from the independent audit)
[Collection("db")]
public class AuditFixTests(BallpassAppFactory app)
{
    // ---------- M2 open redirect ----------

    [Fact]
    public async Task Culture_switch_blocks_tab_bypass()
    {
        var response = await app.Client().GetAsync("/culture/set?culture=en&redirectUri=/%09/evil.example");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Culture_switch_with_line_breaks_redirects_home_not_500()
    {
        var response = await app.Client().GetAsync("/culture/set?culture=en&redirectUri=/%0d%0a/x");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Login_return_url_with_control_characters_is_not_followed()
    {
        var client = app.Client();
        var form = await PersonaForm(client, "coach", 0);
        form.fields["returnUrl"] = "/\t/evil.example";

        var response = await client.PostAsync("/demo/login", new FormUrlEncodedContent(form.fields));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("evil.example", response.Headers.Location!.OriginalString);
    }

    // ---------- M3 / M5 antiforgery ----------

    [Fact]
    public async Task Shortlist_without_token_is_400()
    {
        var client = app.Client();
        await SignIn(client, "coach", 4);
        var slug = await AthleteSlug(client, 4);

        var response = await client.PostAsync($"/p/{slug}/shortlist", new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_without_token_is_400_and_keeps_session()
    {
        var client = app.Client();
        await SignIn(client, "coach", 4);

        var response = await client.PostAsync("/demo/logout", new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/search")).StatusCode);
    }

    [Fact]
    public async Task Interest_with_invalid_token_is_400()
    {
        var client = app.Client();
        await SignIn(client, "coach", 4);
        var slug = await AthleteSlug(client, 4);

        var response = await client.PostAsync($"/p/{slug}/interest", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = "not-a-token", ["note"] = "x" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_post_without_body_is_400_not_500()
    {
        // Found by the orchestrator after the audit fixes: the 400 was re-executed as POST /status/400 and the
        // component endpoint crashed reading a missing form body ("Incorrect Content-Type").
        var slug = await AthleteSlug(app.Client(), 0);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/p/{slug}/shortlist"); // no content at all

        var response = await app.Client().SendAsync(request);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("class=\"bp-footer\"", html);   // flow-release O1: footer disclosure replaces the banner
        Assert.DoesNotContain("demo-banner", html);
    }

    // ---------- M5 403 page from a POST ----------

    [Fact]
    public async Task Athlete_posting_shortlist_gets_403_denied_page_with_banner()
    {
        var client = app.Client();
        var slug = await AthleteSlug(client, 0);
        await SignInAthlete(client, slug);
        var token = Token(await Html(client, $"/p/{slug}"));

        var response = await client.PostAsync($"/p/{slug}/shortlist", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("class=\"bp-footer\"", html);   // flow-release O1: footer disclosure replaces the banner
        Assert.DoesNotContain("demo-banner", html);
        Assert.Contains("μόνο για προπονητές", html);
    }

    // ---------- M4 stale session after reset ----------

    [Fact]
    public async Task Coach_session_after_reset_is_signed_out_not_500()
    {
        var client = app.Client();
        await SignIn(client, "coach", 6);

        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().ResetAsync();

        var response = await client.GetAsync("/search");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/demo", response.Headers.Location!.OriginalString);
    }

    // ---------- m1 duplicate submit race ----------

    [Fact]
    public async Task Parallel_duplicate_interests_store_one_and_never_500()
    {
        var coach = app.Client();
        var coachName = await SignIn(coach, "coach", 5);
        var slug = await FreeAthleteFor(coachName);
        var token = Token(await Html(coach, $"/p/{slug}"));

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            coach.PostAsync($"/p/{slug}/interest", new FormUrlEncodedContent(
                new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["note"] = "parallel" }))));

        Assert.DoesNotContain(responses, r => r.StatusCode == HttpStatusCode.InternalServerError);
        Assert.Equal(1, await InterestsInInbox(slug, coachName));
    }

    // ---------- helpers (shared shape with InterestShortlistTests) ----------

    /// First athlete persona whose inbox has no interest from this coach.
    private async Task<string> FreeAthleteFor(string coachName)
    {
        for (var i = 0; i < 12; i++)
        {
            var slug = await AthleteSlug(app.Client(), i);
            if (await InterestsInInbox(slug, coachName) == 0) return slug;
        }
        throw new InvalidOperationException("no athlete persona without an interest from this coach");
    }

    private async Task<int> InterestsInInbox(string slug, string coachName)
    {
        var athlete = app.Client();
        await SignInAthlete(athlete, slug);
        var inbox = await Html(athlete, "/me/inbox");
        return Regex.Matches(inbox, $"<li[^>]*class=\"interest\"[^>]*data-coach=\"{Regex.Escape(coachName)}\"").Count;
    }

    private static async Task<string> AthleteSlug(HttpClient client, int index)
    {
        var demo = await Html(client, "/demo");
        var slugs = Regex.Matches(demo, "<form[^>]*data-persona=\"athlete\"[^>]*>")
            .Select(m => Regex.Match(m.Value, "data-slug=\"([^\"]+)\"").Groups[1].Value).ToList();
        return slugs[index];
    }

    private static async Task<(Dictionary<string, string> fields, string name)> PersonaForm(HttpClient client, string type, int index)
    {
        var page = await Html(client, "/demo");
        var form = Regex.Matches(page, $"<form[^>]*data-persona=\"{type}\"[^>]*>.*?</form>", RegexOptions.Singleline)[index].Value;
        var fields = Regex.Matches(form, "<input[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"")
            .ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));
        var name = WebUtility.HtmlDecode(Regex.Match(form, "data-name=\"([^\"]+)\"").Groups[1].Value);
        return (fields, name);
    }

    private static async Task<string> SignIn(HttpClient client, string type, int index)
    {
        var (fields, name) = await PersonaForm(client, type, index);
        var response = await client.PostAsync("/demo/login", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return name;
    }

    private static async Task SignInAthlete(HttpClient client, string slug)
    {
        var page = await Html(client, "/demo");
        var form = Regex.Matches(page, "<form[^>]*data-persona=\"athlete\"[^>]*>.*?</form>", RegexOptions.Singleline)
            .Select(m => m.Value).First(f => f.Contains($"data-slug=\"{slug}\""));
        var fields = Regex.Matches(form, "<input[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"")
            .ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));
        var response = await client.PostAsync("/demo/login", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> Html(HttpClient client, string path) =>
        WebUtility.HtmlDecode(await (await client.GetAsync(path)).Content.ReadAsStringAsync());

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
}
