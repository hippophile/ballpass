using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/why-page/specs/why/spec.md + specs/home/spec.md
[Collection("db")]
public class WhyTests(BallpassAppFactory app)
{
    private readonly FeedHarness h = new(app);

    private static readonly string[] Sections = ["hook", "today", "cost", "how", "proof", "doors"];

    private static string Section(string html, string name) =>
        Regex.Match(html, $"<section[^>]*data-why=\"{name}\".*?</section>", RegexOptions.Singleline).Value;

    private static string Text(string markup) => Regex.Replace(markup, "<[^>]+>", " ");

    // ---------- page ----------

    [Theory]
    [InlineData(null, "el", "Γιατί Ballpass")]
    [InlineData("en", "en", "Why Ballpass")]
    public async Task Why_renders_all_six_sections_in_order(string? culture, string lang, string title)
    {
        var response = await app.Client(culture).GetAsync("/why");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Matches($"<html[^>]*lang=\"{lang}\"", html);
        Assert.Contains($"<title>{title}", html);
        var order = Regex.Matches(html, "<section[^>]*data-why=\"(\\w+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(Sections, order);
    }

    [Fact]
    public async Task Today_shows_both_sides_and_the_fragments()
    {
        var today = Section(await FeedHarness.Html(app.Client(), "/why"), "today");

        Assert.Contains("data-side=\"athlete\"", today);
        Assert.Contains("data-side=\"coach\"", today);
        Assert.Contains("Δεν ξέρω ποιος ψάχνει", today);
        Assert.Contains("Δεν ξέρω ποια υπάρχει", today);
        var kinds = Regex.Matches(today, "data-fragment=\"(\\w+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        Assert.Superset(new HashSet<string> { "forward", "call", "pdf", "video", "group" }, kinds);
    }

    [Fact]
    public async Task How_has_four_coloured_steps_each_with_a_preview()
    {
        var how = Section(await FeedHarness.Html(app.Client(), "/why"), "how");

        var steps = Regex.Matches(how, "data-step=\"(\\w+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["athlete", "coach", "athlete", "connected"], steps);
        Assert.Equal(4, Regex.Matches(how, "class=\"bp-why-preview[\" ]").Count);
    }

    [Fact]
    public async Task Proof_counters_equal_the_database_counts()
    {
        var proof = Section(await FeedHarness.Html(app.Client(), "/why"), "proof");

        var (looking, interests, connections) = h.Db(db => (
            db.Athletes.Count(a => a.IsDemo && a.LookingForTeam),
            db.Interests.Count(),
            db.Interests.Count(i => i.Reply == InterestReply.Accepted)));
        Assert.Equal(looking, Count(proof, "looking"));
        Assert.Equal(interests, Count(proof, "interests"));
        Assert.Equal(connections, Count(proof, "connections"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("en")]
    public async Task Every_figure_in_today_and_cost_has_a_source_link(string? culture)
    {
        var html = await FeedHarness.Html(app.Client(culture), "/why");

        foreach (var name in new[] { "today", "cost" })
        {
            var section = Section(html, name);
            Assert.NotEmpty(section);
            var links = Regex.Matches(section, "<a[^>]*href=\"(https://[^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
            foreach (Match fig in Regex.Matches(section, "<strong[^>]*data-source=\"([^\"]*)\"[^>]*>[^<]*</strong>"))
            {
                Assert.StartsWith("https://", fig.Groups[1].Value);
                Assert.Contains(fig.Groups[1].Value, links);
            }
            // Remove the figures, the source links and decorative SVG (jersey numbers): no other digit may remain.
            var rest = Regex.Replace(section, "<svg\\b.*?</svg>", " ", RegexOptions.Singleline);
            rest = Regex.Replace(rest, "<strong[^>]*data-source=\"https://[^\"]*\"[^>]*>[^<]*</strong>", " ");
            rest = Regex.Replace(rest, "<a[^>]*href=\"https://[^\"]+\"[^>]*>[^<]*</a>", " ");
            var digits = Regex.Match(Text(rest), "[^\\s]*\\d[^\\s]*");
            Assert.False(digits.Success, $"{name}: unsourced figure «{digits.Value}»");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("en")]
    public async Task Why_has_no_demo_labels(string? culture)
    {
        var html = await FeedHarness.Html(app.Client(culture), "/why");
        var main = Regex.Match(html, "<main\\b[^>]*>.*?</main>", RegexOptions.Singleline).Value;

        Assert.NotEmpty(main);
        Assert.DoesNotMatch("(?i)demo|δείγμα|επίδειξ|fictional|φανταστικ|sample", Text(main));
    }

    [Fact]
    public async Task Why_doors_post_the_demo_login()
    {
        var doors = Section(await FeedHarness.Html(app.Client(), "/why"), "doors");

        Assert.Matches("<form[^>]*action=\"/demo/login\"[^>]*data-why-door=\"coach\"", doors);
        Assert.Matches("<form[^>]*action=\"/demo/login\"[^>]*data-why-door=\"athlete\"", doors);
    }

    [Fact]
    public async Task Why_doors_link_to_the_demo_page_when_the_demo_is_disabled()
    {
        var off = app.WithWebHostBuilder(b => b.UseSetting("Demo:Enabled", "false"));
        var client = off.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var doors = Section(await FeedHarness.Html(client, "/why"), "doors");

        Assert.DoesNotContain("action=\"/demo/login\"", doors);
        Assert.Equal(2, Regex.Matches(doors, "<a[^>]*data-why-door=\"(coach|athlete)\"[^>]*href=\"/demo\"").Count);
    }

    // ---------- home band ----------

    [Fact]
    public async Task Home_has_the_problem_band_after_the_doors_with_a_link_to_why()
    {
        var html = await FeedHarness.Html(app.Client(), "/");

        var coachDoor = html.IndexOf("data-door=\"coach\"", StringComparison.Ordinal);
        var athleteDoor = html.IndexOf("data-door=\"athlete\"", StringComparison.Ordinal);
        var band = html.IndexOf("data-why-band", StringComparison.Ordinal);
        var how = html.IndexOf("class=\"bp-howband\"", StringComparison.Ordinal);
        Assert.True(coachDoor >= 0 && athleteDoor > coachDoor, "doors missing");
        Assert.True(band > athleteDoor, "band must follow both doors");
        Assert.True(how > band, "band must precede the connection band");

        var section = Regex.Match(html, "<section[^>]*data-why-band.*?</section>", RegexOptions.Singleline).Value;
        Assert.Equal(3, Regex.Matches(section, "data-why-row=\"today\"").Count);
        Assert.Equal(3, Regex.Matches(section, "data-why-row=\"with\"").Count);
        Assert.Matches("<a[^>]*href=\"/why\"", section);
        Assert.Contains("Το πρόβλημα", section);
    }

    // ---------- nav + footer ----------

    [Fact]
    public async Task Signed_out_nav_and_footer_link_to_why()
    {
        var html = await FeedHarness.Html(app.Client(), "/");

        Assert.Matches("<a[^>]*href=\"/why\"", Nav(html));
        Assert.Matches("<a[^>]*href=\"/why\"", Footer(html));
    }

    [Fact]
    public async Task Signed_in_role_nav_has_no_why_link_but_the_footer_does()
    {
        var coach = await h.LoginAsync("coach", h.CoachId(1));
        var html = await FeedHarness.Html(coach, "/shortlist");

        Assert.DoesNotContain("href=\"/why\"", Nav(html));
        Assert.Matches("<a[^>]*href=\"/why\"", Footer(html));
    }

    // ---------- copy ----------

    private static readonly Regex Singular = new(
        @"(?<!\p{L})(Δες|Βρες|Διάλεξε|Ψάχνεις|Μπες|Στείλε|Ακολούθησε|Συνδέσου|Δοκίμασε|Ξεκίνα|Πάτα|Επίλεξε|Γράψε|Έχεις|σε θέλει|σου|εσένα)(?!\p{L})",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    [Fact]
    public void Why_resource_uses_the_formal_register_and_has_both_languages()
    {
        var manager = new ResourceManager("Ballpass.Web.Resources.WhyResource", typeof(WhyResource).Assembly);
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
        foreach (System.Collections.DictionaryEntry e in en)
            if (e.Value is string t3 && Regex.IsMatch(t3, "(?i)demo|sample|fictional")) offenders.Add($"{e.Key} (en): demo word");
        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
        Assert.NotEmpty(keys);
        foreach (var k in keys) Assert.False(string.IsNullOrEmpty(en.GetString(k)), $"{k} has no English text");
    }

    // ---------- helpers ----------

    private static string Nav(string html) =>
        Regex.Match(html, "<nav class=\"bp-nav\".*?</nav>", RegexOptions.Singleline).Value is { Length: > 0 } nav
            ? nav : throw new Xunit.Sdk.XunitException("nav not found");

    private static string Footer(string html) =>
        Regex.Match(html, "<footer[^>]*>.*?</footer>", RegexOptions.Singleline).Value is { Length: > 0 } f
            ? f : throw new Xunit.Sdk.XunitException("footer not found");

    private static int Count(string html, string key) =>
        int.Parse(Regex.Match(html, $"data-count=\"{key}\"[^>]*>\\s*(\\d+)\\s*<").Groups[1].Value);
}
