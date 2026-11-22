using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Find.Ai;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Ballpass.Web.Tests;

/// <summary>Model client for tests: never reaches the network, counts calls, returns a canned reply.</summary>
public sealed class RecordingClaudeClient(string? text = null) : IClaudeClient
{
    public int Calls;
    public Task<ClaudeReply> CompleteAsync(string query, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        return Task.FromResult(new ClaudeReply("end_turn", text ?? "{}"));
    }
}

// Spec: openspec/changes/wow-find/specs/find/spec.md (Match page, AI box seams, /find/ai redirect).
[Collection("db")]
public class FindPagesTests(BallpassAppFactory app)
{
    private static readonly DemoDataSet Data = DemoDataGenerator.Generate(42);
    private static readonly List<Athlete> Women = Data.Athletes.Where(a => a.Gender == Gender.Female).ToList();

    private static StatLine? Latest(Athlete a) => a.Stats.OrderByDescending(s => s.Season, StringComparer.Ordinal).FirstOrDefault();

    // The real client is replaced everywhere in these tests, so no test can ever call the API.
    private WebApplicationFactory<Program> Host(RecordingClaudeClient client, string? key = null) =>
        app.WithWebHostBuilder(b =>
        {
            b.UseSetting("Anthropic:ApiKey", key ?? "");
            b.UseSetting("Ai:Enabled", key is null ? "false" : "true");
            b.ConfigureTestServices(s => s.AddSingleton<IClaudeClient>(client));
        });

    private HttpClient NewClient(WebApplicationFactory<Program>? host = null) =>
        (host ?? Host(new RecordingClaudeClient())).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // ---------- access ----------

    // Orchestrator walkthrough: submitting the AI box with an empty query showed "no filters found" — say nothing instead.
    [Fact]
    public async Task Empty_ai_query_shows_no_status_line()
    {
        var client = app.Client();
        await SignIn(client, "coach");

        var html = await Html(client, "/match?parsed=rules&nlq=");

        Assert.DoesNotContain("data-parsed=", html);
        Assert.Contains("id=\"bp-find-nlq\"", html); // the box itself still renders
    }

    [Fact]
    public async Task Anonymous_match_redirects_to_demo_login()
    {
        var response = await NewClient().GetAsync("/match");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/demo", PathOf(response.Headers.Location!));
    }

    [Fact]
    public async Task Athlete_is_denied_match_with_403()
    {
        var client = NewClient();
        await SignIn(client, "athlete");

        var match = await client.GetAsync("/match");
        Assert.Equal(HttpStatusCode.Redirect, match.StatusCode);
        var denied = await client.GetAsync(match.Headers.Location);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Find_ai_endpoint_is_coach_only()
    {
        var anon = await NewClient().GetAsync("/find/ai?q=x");
        Assert.Equal(HttpStatusCode.Redirect, anon.StatusCode);
        Assert.StartsWith("/demo", PathOf(anon.Headers.Location!));

        var client = NewClient();
        await SignIn(client, "athlete");
        var athlete = await client.GetAsync("/find/ai?q=x");
        Assert.Equal(HttpStatusCode.Redirect, athlete.StatusCode);
        Assert.StartsWith("/denied", PathOf(athlete.Headers.Location!));
    }

    // ---------- need form ----------

    [Fact]
    public async Task Need_form_has_all_inputs_and_prefills_from_query()
    {
        var client = await CoachClient();
        var html = await Html(client, "/match?position=C&minHeight=185&region=Attica&priority=rebounding&confirmed=true");

        Assert.Contains("Τι ψάχνετε;", html);
        Assert.Matches("<form[^>]*method=\"get\"[^>]*action=\"/match\"", html);
        foreach (var name in new[] { "position", "minHeight", "maxHeight", "minAge", "maxAge", "region", "league", "priority", "confirmed" })
            Assert.Contains($"name=\"{name}\"", html);
        Assert.Matches("name=\"minAge\"[^>]*min=\"18\"|min=\"18\"[^>]*name=\"minAge\"", html);
        Assert.Matches("name=\"position\"[^>]*value=\"C\"[^>]*checked", html);
        Assert.Matches("name=\"minHeight\"[^>]*value=\"185\"", html);
        Assert.Matches("value=\"rebounding\"[^>]*checked|checked[^>]*value=\"rebounding\"", html);
        Assert.Matches("name=\"confirmed\"[^>]*checked|checked[^>]*name=\"confirmed\"", html);
    }

    // ---------- ranked results ----------

    [Fact]
    public async Task Results_are_ranked_with_reasons_sources_and_an_interest_form()
    {
        var client = await CoachClient();
        // Forwards: the profile cohort (same gender + position group, n >= 20 after the minutes floor) exists for them; female centers have none.
        var html = await Html(client, "/match?position=PF&position=SF&minHeight=175&priority=rebounding");

        var articles = Articles(html);
        Assert.InRange(articles.Count, 1, 20);

        var fits = articles.Select(a => int.Parse(Regex.Match(a, "data-fit=\"(\\d+)\"").Groups[1].Value)).ToList();
        Assert.Equal(fits.OrderByDescending(x => x), fits);

        foreach (var a in articles)
        {
            var slug = Regex.Match(a, "data-slug=\"([^\"]+)\"").Groups[1].Value;
            var reasons = Regex.Matches(a, "<li[^>]*class=\"bp-find-reason\"[^>]*>(.*?)</li>", RegexOptions.Singleline);
            Assert.InRange(reasons.Count, 1, 3);                                      // stat reason is missing when the cohort rule yields no percentile
            Assert.All(reasons, r => Assert.Contains("data-source=", r.Value));      // every reason carries its source badge
            Assert.Contains($"href=\"/p/{slug}\"", a);
            Assert.Matches($"<form[^>]*method=\"post\"[^>]*action=\"/p/{slug}/interest\"|<form[^>]*action=\"/p/{slug}/interest\"[^>]*method=\"post\"", a);
            Assert.Contains("__RequestVerificationToken", a);
        }

        Assert.Contains("εκ.", html);
        Assert.Contains("στόχος", html);
        Assert.Contains("ριμπ./40΄", html);
        Assert.DoesNotContain("δείγμα επίδειξης", html);   // flow-release O1: no demo label on the cohort
    }

    [Fact]
    public async Task Results_state_the_honest_label_and_never_call_the_score_ai()
    {
        var client = await CoachClient();
        var html = await Html(client, "/match?position=C&minHeight=185&priority=rebounding");

        Assert.Contains("Υπολογίζεται με προεπιλεγμένα βάρη", html);
        Assert.DoesNotContain("επίδειξης", html);
        var results = Regex.Match(html, "<section[^>]*class=\"bp-find-results\".*?</section>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(results);
        Assert.DoesNotMatch(@"\bAI\b|\bΑΙ\b", results);
    }

    [Fact]
    public async Task Hidden_count_reflects_position_and_confirmed_only_filters()
    {
        var client = await CoachClient();

        var centers = Women.Where(a => a.Position == Position.C).ToList();
        var html = await Html(client, "/match?position=C");
        Assert.Equal(Women.Count - centers.Count, Hidden(html));
        Assert.Contains("κρυμμένοι από φίλτρα", html);

        var confirmed = centers.Count(a => Latest(a) is { Source: StatSource.CoachConfirmed or StatSource.LeagueVerifiedMock });
        var html2 = await Html(client, "/match?position=C&confirmed=true&minHeight=185");
        Assert.Equal(Women.Count - confirmed, Hidden(html2));
        Assert.Equal(confirmed, Total(html2));
    }

    [Fact]
    public async Task Trust_bar_sums_to_100_and_matches_the_results()
    {
        var client = await CoachClient();
        var html = await Html(client, "/match?position=C&minHeight=185");

        var bar = Regex.Match(html, "<div[^>]*class=\"bp-find-trust\"[^>]*>");
        Assert.True(bar.Success);
        int V(string n) => int.Parse(Regex.Match(bar.Value, $"data-{n}=\"(\\d+)\"").Groups[1].Value);
        Assert.Equal(100, V("self") + V("coach") + V("league"));

        var lines = Women.Where(a => a.Position == Position.C).Select(Latest).Where(l => l is not null).ToList();
        // Largest-remainder rounding: each share is within 1 point of the exact percentage.
        Assert.InRange(V("self"), 100.0 * lines.Count(l => l!.Source == StatSource.SelfReported) / lines.Count - 1, 100.0 * lines.Count(l => l!.Source == StatSource.SelfReported) / lines.Count + 1);
        Assert.InRange(V("coach"), 100.0 * lines.Count(l => l!.Source == StatSource.CoachConfirmed) / lines.Count - 1, 100.0 * lines.Count(l => l!.Source == StatSource.CoachConfirmed) / lines.Count + 1);
    }

    [Fact]
    public async Task English_labels()
    {
        var client = await CoachClient();
        await client.GetAsync("/culture/set?culture=en&redirectUri=%2Fmatch");
        var html = await Html(client, "/match?position=C&minHeight=185");

        Assert.Contains("hidden by filters", html);
        Assert.Contains("Computed with default weights", html);
        Assert.DoesNotContain("demo data", html);
        Assert.Contains("What are you looking for?", html);
    }

    [Fact]
    public async Task Partial_data_athlete_is_tagged()
    {
        const string slug = "zz-partial-test";
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
            db.Athletes.Add(new Athlete
            {
                Slug = slug, FirstName = "Τεστ", LastName = "Μερική", FirstNameLatin = "Test", LastNameLatin = "Partial",
                BirthYear = 2000, Gender = Gender.Female, Position = Position.C, HeightCm = 199, City = "Αθήνα", Region = "Attica", IsDemo = true,
                Stats = [new StatLine { Season = "2025-26", League = League.A1, Games = 5, Rebounds = 12m, Source = StatSource.SelfReported }],
            });
            await db.SaveChangesAsync();
        }
        try
        {
            var client = await CoachClient();
            // minHeight 199: she is the tallest, so she ranks first (female centers all lack a cohort percentile and tie on height otherwise).
            var html = await Html(client, "/match?position=C&minHeight=199&priority=rebounding");

            var card = Articles(html).Single(a => a.Contains($"data-slug=\"{slug}\""));
            Assert.Contains("bp-find-partial", card);
        }
        finally
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
            db.Athletes.RemoveRange(db.Athletes.Where(a => a.Slug == slug));
            await db.SaveChangesAsync();
        }
    }

    // ---------- seams on /search and the layout ----------

    [Fact]
    public async Task Search_links_to_match_and_has_the_ai_box_and_the_nav_link_is_coach_only()
    {
        var coach = await CoachClient();
        var html = await Html(coach, "/search");

        Assert.Contains("class=\"bp-find-aibox\"", html);
        Assert.Matches("<form[^>]*method=\"get\"[^>]*action=\"/find/ai\"", html);
        Assert.Contains("href=\"/match\"", html);
        Assert.Contains("bp-find-matchlink", html);

        var athlete = NewClient();
        await SignIn(athlete, "athlete");
        var inbox = await Html(athlete, "/me/inbox");
        Assert.DoesNotContain("href=\"/match\"", inbox);
    }

    [Fact]
    public async Task Ai_box_shows_editable_chips_and_the_ai_label()
    {
        var client = await CoachClient();
        var html = await Html(client, "/search?parsed=ai&position=C&minHeight=185&unsupported=handedness&nlq=test");

        Assert.Contains("Αναζήτηση με AI. Ελέγξτε τα φίλτρα.", html);
        Assert.Contains("data-chip=\"position\"", html);
        Assert.Contains("data-chip=\"minHeight\"", html);
        Assert.Contains("data-chip=\"unsupported\"", html);
        Assert.Contains("Δεν φιλτράρεται", html);
        Assert.DoesNotContain("Από κανόνες", html);
    }

    [Fact]
    public async Task Ai_box_labels_the_rule_path_and_english()
    {
        var client = await CoachClient();
        var el = await Html(client, "/search?parsed=rules&position=C");
        Assert.Contains("Από κανόνες (χωρίς μοντέλο AI)", el);
        Assert.DoesNotContain("Ελέγξτε τα φίλτρα", el);

        await client.GetAsync("/culture/set?culture=en&redirectUri=%2Fsearch");
        Assert.Contains("From rules (no AI model)", await Html(client, "/search?parsed=rules&position=C"));
        Assert.Contains("AI-interpreted search. Check the filters.", await Html(client, "/search?parsed=ai&position=C"));
    }

    [Fact]
    public async Task Search_without_parsed_param_shows_no_chips_and_old_params_still_work()
    {
        var client = await CoachClient();
        var html = await Html(client, "/search?position=C");

        Assert.DoesNotContain("data-chip=", html);
        Assert.Contains("data-total=", html);
    }

    // ---------- /find/ai redirect ----------

    [Fact]
    public async Task Find_ai_redirects_to_search_with_rule_filters_when_no_key()
    {
        var model = new RecordingClaudeClient();
        var client = NewClient(Host(model));
        await SignIn(client, "coach");

        var response = await client.GetAsync("/find/ai?q=" + Uri.EscapeDataString("σέντερ πάνω από 185 Αττική"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var url = response.Headers.Location!.OriginalString;
        Assert.StartsWith("/search?", url);
        Assert.Contains("position=C", url);
        Assert.Contains("minHeight=185", url);
        Assert.Contains("region=Attica", url);
        Assert.Contains("parsed=rules", url);
        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public async Task Find_ai_can_target_match_and_ignores_unknown_targets()
    {
        var client = NewClient();
        await SignIn(client, "coach");

        var match = await client.GetAsync("/find/ai?q=center&to=match");
        Assert.StartsWith("/match?", match.Headers.Location!.OriginalString);

        var evil = await client.GetAsync("/find/ai?q=center&to=https://evil.example/");
        Assert.StartsWith("/search?", evil.Headers.Location!.OriginalString);
        Assert.DoesNotContain("evil", evil.Headers.Location.OriginalString);
    }

    [Fact]
    public async Task Find_ai_uses_the_model_when_a_key_is_set_and_caches_repeats()
    {
        var model = new RecordingClaudeClient(
            """{"gender":"Female","positions":["PF"],"minHeight":null,"maxHeight":null,"minAge":null,"maxAge":null,"region":"Crete","league":null,"looking":false,"q":null,"unsupported":[]}""");
        var client = NewClient(Host(model, key: "test-key-not-real"));
        await SignIn(client, "coach");

        var first = await client.GetAsync("/find/ai?q=" + Uri.EscapeDataString("μια δυνατή ψηλή από νησί"));
        var second = await client.GetAsync("/find/ai?q=" + Uri.EscapeDataString("μια  ΔΥΝΑΤΗ ψηλη απο νησι"));

        var url = first.Headers.Location!.OriginalString;
        Assert.Contains("parsed=ai", url);
        Assert.Contains("position=PF", url);
        Assert.Contains("region=Crete", url);
        Assert.Equal(StripNlq(url), StripNlq(second.Headers.Location!.OriginalString));
        Assert.Equal(1, model.Calls);
    }

    [Fact]
    public async Task Find_ai_falls_back_to_rules_when_the_model_refuses()
    {
        var refusing = new RefusingClient();
        var host = app.WithWebHostBuilder(b =>
        {
            b.UseSetting("Anthropic:ApiKey", "test-key-not-real");
            b.UseSetting("Ai:Enabled", "true");
            b.ConfigureTestServices(s => s.AddSingleton<IClaudeClient>(refusing));
        });
        var client = NewClient(host);
        await SignIn(client, "coach");

        var response = await client.GetAsync("/find/ai?q=" + Uri.EscapeDataString("σέντερ Κρήτη"));

        var url = response.Headers.Location!.OriginalString;
        Assert.Contains("parsed=rules", url);
        Assert.Contains("region=Crete", url);
    }

    private sealed class RefusingClient : IClaudeClient
    {
        public Task<ClaudeReply> CompleteAsync(string query, CancellationToken ct) => Task.FromResult(new ClaudeReply("refusal", null));
    }

    // ---------- helpers ----------

    private async Task<HttpClient> CoachClient()
    {
        var client = NewClient();
        await SignIn(client, "coach");
        return client;
    }

    private static List<string> Articles(string html) =>
        Regex.Matches(html, "<article[^>]*class=\"bp-find-match\".*?</article>", RegexOptions.Singleline).Select(m => m.Value).ToList();

    private static string StripNlq(string url) => Regex.Replace(url, "&?nlq=[^&]*", "");

    private static int Hidden(string html) => int.Parse(Regex.Match(html, "data-hidden=\"(\\d+)\"").Groups[1].Value);

    private static int Total(string html) => int.Parse(Regex.Match(html, "data-total=\"(\\d+)\"").Groups[1].Value);

    private static async Task SignIn(HttpClient client, string type)
    {
        var page = await Html(client, "/demo");
        var form = Regex.Match(page, $"<form[^>]*data-persona=\"{type}\"[^>]*>(.*?)</form>", RegexOptions.Singleline);
        Assert.True(form.Success, $"no {type} persona form on /demo");
        var fields = Regex.Matches(form.Groups[1].Value, "<input[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\"")
            .ToDictionary(m => m.Groups[1].Value, m => WebUtility.HtmlDecode(m.Groups[2].Value));
        var response = await client.PostAsync("/demo/login", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> Html(HttpClient client, string path) =>
        WebUtility.HtmlDecode(await (await client.GetAsync(path)).Content.ReadAsStringAsync());

    private static string PathOf(Uri location) =>
        location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?')[0];
}
