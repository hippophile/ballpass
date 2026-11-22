using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-look/specs/look/spec.md + openspec/changes/flow-release/specs/{look,platform}/spec.md
[Collection("db")]
public class LookTests(BallpassAppFactory app)
{
    private static readonly DemoDataSet Data = DemoDataGenerator.Generate(42);
    private static readonly Athlete WithLeagueStat = Data.Athletes.First(a => a.Stats.Any(s => s.Source == StatSource.LeagueVerifiedMock));
    private static readonly string WebRoot = Path.Combine(RepoRoot(), "src", "Ballpass.Web", "wwwroot");

    // ---------- one dark theme on every route ----------

    [Theory]
    [InlineData("/", null)]
    [InlineData("/demo", null)]
    [InlineData("/search", "coach")]
    [InlineData("/p/SLUG", null)]
    [InlineData("/shortlist", "coach")]
    [InlineData("/me/inbox", "athlete")]
    [InlineData("/denied", null)]
    [InlineData("/does-not-exist", null)]
    public async Task Every_route_is_dark_has_no_banner_and_one_footer_disclosure(string path, string? persona)
    {
        var client = app.Client();
        if (persona is not null) await SignIn(client, persona);
        path = path.Replace("SLUG", WithLeagueStat.Slug);

        var html = await Html(client, path);

        Assert.Matches("<html[^>]*data-theme=\"dark\"", html);
        Assert.Matches("<html[^>]*data-bs-theme=\"dark\"", html);
        Assert.DoesNotContain("demo-banner", html);
        var footers = Regex.Matches(html, "<footer[^>]*class=\"bp-footer\"[^>]*>(.*?)</footer>", RegexOptions.Singleline);
        Assert.Single(footers);
        Assert.Contains("Φανταστικά πρόσωπα και δεδομένα", footers[0].Groups[1].Value);
    }

    [Fact]
    public async Task Footer_disclosure_is_english_under_the_english_culture()
    {
        var html = await Html(app.Client("en"), "/");

        var footer = Regex.Match(html, "<footer[^>]*class=\"bp-footer\"[^>]*>(.*?)</footer>", RegexOptions.Singleline);
        Assert.True(footer.Success, "footer disclosure missing");
        Assert.Contains("Fictional people and data", footer.Groups[1].Value);
    }

    // ---------- O1: no demo labels in visible text (SPEC §8) ----------

    [Theory]
    [InlineData("/", null, "el")]
    [InlineData("/", null, "en")]
    [InlineData("/feed", null, "el")]
    [InlineData("/feed", null, "en")]
    [InlineData("/p/SLUG", null, "el")]
    [InlineData("/p/SLUG", null, "en")]
    [InlineData("/p/FEATURED", null, "el")]
    [InlineData("/p/SLUG/season", null, "el")]
    [InlineData("/match", "coach", "el")]
    [InlineData("/match", "coach", "en")]
    [InlineData("/search", "coach", "el")]
    [InlineData("/shortlist", "coach", "el")]
    [InlineData("/me/sent", "coach", "el")]
    [InlineData("/me/inbox", "athlete", "el")]
    [InlineData("/me/inbox", "athlete", "en")]
    [InlineData("/does-not-exist", null, "el")]
    public async Task Visible_text_has_no_demo_labels_outside_the_footer(string path, string? persona, string culture)
    {
        var client = app.Client(culture);
        if (persona is not null) await SignIn(client, persona);
        path = path.Replace("FEATURED", "danai-bakali").Replace("SLUG", WithLeagueStat.Slug);

        var text = VisibleText(await Html(client, path));

        var hit = Regex.Match(text, ".{0,40}(demo|δείγμα|δειγμα|επίδειξ|seeded|fictional|φανταστικ).{0,40}", RegexOptions.IgnoreCase);
        Assert.False(hit.Success, $"{path} ({culture}) shows a demo label: \"{hit.Value}\"");
    }

    [Theory]
    [InlineData("el")]
    [InlineData("en")]
    public async Task Profile_share_meta_has_no_demo_words(string culture)
    {
        var html = await Html(app.Client(culture), $"/p/{WithLeagueStat.Slug}");

        var metas = Regex.Matches(html, "<meta[^>]*(?:property|name)=\"(?:og|twitter):[^\"]+\"[^>]*content=\"([^\"]*)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(metas);
        Assert.All(metas, c => Assert.DoesNotMatch("(?i)demo|επίδειξ|φανταστικ|fictional", c));
    }

    // ---------- shell ----------

    [Fact]
    public async Task Coach_nav_has_three_primary_links_and_a_user_menu()
    {
        var client = app.Client();
        await SignIn(client, "coach");

        var html = await Html(client, "/match");

        var nav = Regex.Match(html, "<nav[^>]*class=\"bp-nav\"[^>]*>(.*?)</nav>", RegexOptions.Singleline);
        Assert.True(nav.Success, "primary nav missing");
        var links = Regex.Matches(nav.Groups[1].Value, "<a[^>]*href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["/match", "/shortlist", "/me/sent"], links);
        Assert.Matches("<a[^>]*href=\"/match\"[^>]*aria-current=\"page\"|<a[^>]*aria-current=\"page\"[^>]*href=\"/match\"", nav.Groups[1].Value);
        var menu = Regex.Match(html, "<details[^>]*class=\"bp-usermenu\"[^>]*>(.*?)</details>", RegexOptions.Singleline);
        Assert.True(menu.Success, "user menu missing");
        Assert.Matches("<summary[^>]*>\\s*<svg[^>]*class=\"bp-jersey[^\"]*\"[^>]*width=\"32\"", menu.Groups[1].Value);
        Assert.Contains("href=\"/feed\"", menu.Groups[1].Value);
        Assert.Contains("href=\"/search\"", menu.Groups[1].Value);
        Assert.Contains("action=\"/demo/logout\"", menu.Groups[1].Value);
        Assert.DoesNotContain("data-demo-switch", menu.Groups[1].Value);
        Assert.Contains("data-demo-switch=\"athlete\"", html);      // persona switch stays visible
        Assert.Matches("<nav[^>]*class=\"lang-toggle\"", html);
    }

    [Fact]
    public async Task Athlete_nav_has_two_primary_links()
    {
        var client = app.Client();
        await SignIn(client, "athlete");

        var html = await Html(client, "/me/inbox");

        var nav = Regex.Match(html, "<nav[^>]*class=\"bp-nav\"[^>]*>(.*?)</nav>", RegexOptions.Singleline);
        Assert.True(nav.Success, "primary nav missing");
        var links = Regex.Matches(nav.Groups[1].Value, "<a[^>]*href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(2, links.Count);
        Assert.Equal("/me/inbox", links[0]);
        Assert.Matches("^/p/[a-z0-9-]+$", links[1]);
        var menu = Regex.Match(html, "<details[^>]*class=\"bp-usermenu\"[^>]*>(.*?)</details>", RegexOptions.Singleline);
        Assert.True(menu.Success, "user menu missing");
        Assert.Contains("href=\"/feed\"", menu.Groups[1].Value);
        Assert.Contains("action=\"/demo/logout\"", menu.Groups[1].Value);
    }

    [Fact]
    public void Shell_uses_one_page_width_and_aligns_the_logo_with_the_content()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Ballpass.Web", "Components", "Layout", "MainLayout.razor.css"));
        var compact = Regex.Replace(css, "\\s+", "");

        Assert.Contains("max-width:var(--bp-w-page)", compact);
        Assert.Contains("calc((100%-var(--bp-w-page))/2+32px)", compact);
        Assert.Matches("--bp-w-page\\s*:\\s*1280px", File.ReadAllText(Path.Combine(WebRoot, "css", "bp-tokens.css")));
    }

    [Fact]
    public void Base_css_defines_the_flow_primitives()
    {
        var css = Regex.Replace(File.ReadAllText(Path.Combine(WebRoot, "css", "bp-base.css")), "\\s+", " ");

        foreach (var cls in new[] { ".bp-btn-primary", ".bp-btn-athlete", ".bp-btn-outline", ".bp-btn-ghost", ".bp-chip-athlete", ".bp-card", ".bp-photo", ".bp-photo-scrim", ".bp-stepper" })
            Assert.Matches(Regex.Escape(cls) + "[ ,{:.\\[]", css);
        var primary = Regex.Match(css, "\\.bp-btn-primary \\{([^}]*)\\}").Groups[1].Value;
        Assert.Contains("var(--bp-grad-cta)", primary);
        Assert.Contains("var(--bp-accent-contrast)", primary);
        var athlete = Regex.Match(css, "\\.bp-btn-athlete \\{([^}]*)\\}").Groups[1].Value;
        Assert.Contains("var(--bp-grad-athlete)", athlete);
        Assert.Contains("var(--bp-athlete-contrast)", athlete);
    }

    [Fact]
    public async Task Design_css_loads_after_bootstrap()
    {
        var html = await Html(app.Client(), "/");

        var hrefs = Regex.Matches(html, "<link[^>]*rel=\"stylesheet\"[^>]*href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        var bootstrap = hrefs.FindIndex(h => h.Contains("bootstrap"));
        var tokens = hrefs.FindIndex(h => h.Contains("bp-tokens"));
        var baseCss = hrefs.FindIndex(h => h.Contains("bp-base"));
        Assert.True(tokens >= 0, "bp-tokens.css not linked");
        Assert.True(baseCss >= 0, "bp-base.css not linked");
        Assert.True(bootstrap >= 0 && bootstrap < tokens && tokens < baseCss, "order must be bootstrap, tokens, base");
    }

    // ---------- self-hosted only ----------

    [Theory]
    [InlineData("/")]
    [InlineData("/demo")]
    public async Task Pages_and_css_reference_no_external_hosts(string path)
    {
        var html = await Html(app.Client(), path);

        var refs = Regex.Matches(html, "<(?:link|script)[^>]*?(?:href|src)=\"([^\"]+)\"").Select(m => m.Groups[1].Value);
        Assert.All(refs, r => Assert.DoesNotMatch("^(https?:)?//", r));

        var cssFiles = Directory.GetFiles(Path.Combine(WebRoot, "css"), "*.css").Append(Path.Combine(WebRoot, "app.css")).ToList();
        Assert.Contains(cssFiles, f => f.EndsWith("bp-base.css"));
        foreach (var file in cssFiles)
        {
            var css = File.ReadAllText(file);
            Assert.DoesNotMatch("@import\\s+(url\\()?['\"]?(https?:)?//", css);
            foreach (Match m in Regex.Matches(css, "url\\(\\s*['\"]?([^'\")]+)", RegexOptions.IgnoreCase))
                Assert.DoesNotMatch("^(https?:)?//", m.Groups[1].Value.Trim());
        }
    }

    [Fact]
    public void Font_faces_point_to_shipped_woff2_files_with_licenses()
    {
        var css = File.ReadAllText(Path.Combine(WebRoot, "css", "bp-base.css"));

        var faces = Regex.Matches(css, "@font-face\\s*\\{[^}]*\\}").Select(m => m.Value).ToList();
        Assert.Contains(faces, f => f.Contains("\"BP Text\""));
        Assert.Contains(faces, f => f.Contains("\"BP Display\""));
        foreach (var face in faces)
        {
            var src = Regex.Match(face, "url\\(\\s*['\"]?(/fonts/[^'\")]+\\.woff2)").Groups[1].Value;
            Assert.NotEmpty(src);
            Assert.True(File.Exists(Path.Combine(WebRoot, src.TrimStart('/'))), $"missing font file {src}");
        }
        Assert.True(File.Exists(Path.Combine(WebRoot, "fonts", "OFL-Inter.txt")));
        Assert.True(File.Exists(Path.Combine(WebRoot, "fonts", "OFL-RobotoCondensed.txt")));
    }

    // ---------- contrast (flow-release SPEC §3 table, WCAG 2.x relative luminance) ----------

    [Fact]
    public void Text_tokens_meet_4_5_on_all_surfaces()
    {
        var t = Tokens();
        string[] text = ["--bp-text", "--bp-muted", "--bp-faint", "--bp-accent", "--bp-accent-hi", "--bp-athlete", "--bp-danger", "--bp-success", "--bp-src-self", "--bp-src-coach", "--bp-src-league"];
        foreach (var fg in text)
            foreach (var bg in new[] { "--bp-bg", "--bp-s1", "--bp-s2" })
                Assert.True(Ratio(t[fg], t[bg]) >= 4.5, $"{fg} on {bg} = {Ratio(t[fg], t[bg]):0.00}");
    }

    [Theory]
    [InlineData("--bp-text", 18.18, 16.44, 14.22, 11.14)]
    [InlineData("--bp-muted", 8.96, 8.10, 7.01, 5.49)]
    [InlineData("--bp-faint", 7.52, 6.80, 5.88, 4.61)]
    [InlineData("--bp-accent", 6.90, 6.23, 5.39, 4.22)]
    [InlineData("--bp-accent-hi", 8.44, 7.63, 6.60, 5.17)]
    [InlineData("--bp-athlete", 10.59, 9.57, 8.28, 6.48)]
    [InlineData("--bp-danger", 8.03, 7.26, 6.28, 4.92)]
    [InlineData("--bp-src-coach", 7.75, 7.01, 6.06, 4.75)]
    [InlineData("--bp-border-ui", 5.86, 5.29, 4.58, 3.59)]
    public void Token_contrast_matches_the_spec_table(string fg, double bg, double s1, double s2, double s3)
    {
        var t = Tokens();
        var expected = new[] { bg, s1, s2, s3 };
        var surfaces = new[] { "--bp-bg", "--bp-s1", "--bp-s2", "--bp-s3" };
        Assert.True(t.ContainsKey(fg), $"{fg} missing");
        for (var i = 0; i < 4; i++)
            Assert.True(Math.Abs(Math.Round(Ratio(t[fg], t[surfaces[i]]), 2) - expected[i]) <= 0.01,
                $"{fg} on {surfaces[i]} = {Ratio(t[fg], t[surfaces[i]]):0.00}, spec {expected[i]:0.00}");
    }

    [Fact]
    public void Ui_border_meets_3_on_every_surface()
    {
        var t = Tokens();
        foreach (var bg in new[] { "--bp-bg", "--bp-s1", "--bp-s2", "--bp-s3" })
            Assert.True(Ratio(t["--bp-border-ui"], t[bg]) >= 3, $"--bp-border-ui on {bg} = {Ratio(t["--bp-border-ui"], t[bg]):0.00}");
    }

    [Theory]
    [InlineData("--bp-accent-contrast", "--bp-accent-hi", 8.44)]
    [InlineData("--bp-accent-contrast", "--bp-accent", 6.90)]
    [InlineData("--bp-accent-contrast", "--bp-accent-lo", 5.69)]
    [InlineData("--bp-athlete-contrast", "--bp-athlete-hi", 11.56)]
    [InlineData("--bp-athlete-contrast", "--bp-athlete", 9.18)]
    [InlineData("--bp-athlete-contrast", "--bp-athlete-lo", 6.87)]
    public void Button_ink_on_button_fills_matches_the_spec(string ink, string fill, double expected)
    {
        var t = Tokens();
        Assert.True(t.ContainsKey(ink) && t.ContainsKey(fill), $"{ink} or {fill} missing");
        Assert.True(Math.Abs(Math.Round(Ratio(t[ink], t[fill]), 2) - expected) <= 0.01, $"{ink} on {fill} = {Ratio(t[ink], t[fill]):0.00}");
    }

    [Fact]
    public void Gradient_stops_are_the_accent_tokens()
    {
        var css = Regex.Replace(File.ReadAllText(Path.Combine(WebRoot, "css", "bp-tokens.css")), "\\s+", "");
        Assert.Contains("--bp-grad-cta:linear-gradient(180deg,#FF8A4C0%,#FF6A2B55%,#F2541A100%)", css);
        Assert.Contains("--bp-grad-athlete:linear-gradient(180deg,#5EEAD40%,#2DD4BF55%,#14B8A6100%)", css);
    }

    [Fact]
    public void Text_on_the_scrim_over_pure_white_meets_4_5()
    {
        // The text zone of --bp-scrim starts at its 35% stop; composite that rgba over a white pixel (sRGB, as browsers blend).
        var css = File.ReadAllText(Path.Combine(WebRoot, "css", "bp-tokens.css"));
        var scrim = Regex.Match(css, "--bp-scrim\\s*:\\s*linear-gradient\\(([^;]+)\\);").Groups[1].Value;
        var stop = Regex.Match(scrim, "rgb\\((\\d+) (\\d+) (\\d+)\\s*/\\s*(\\.\\d+|[01](?:\\.\\d+)?)\\)\\s*35%");
        Assert.True(stop.Success, "35% stop of --bp-scrim not found");
        var alpha = double.Parse(stop.Groups[4].Value, CultureInfo.InvariantCulture);
        Assert.True(alpha >= 0.80, $"scrim alpha at 35% = {alpha}");
        int Blend(int g) => (int)Math.Round(alpha * int.Parse(stop.Groups[g].Value) + (1 - alpha) * 255);
        var composite = $"#{Blend(1):X2}{Blend(2):X2}{Blend(3):X2}";

        var t = Tokens();
        Assert.True(Ratio(t["--bp-text"], composite) >= 4.5, $"text on scrim over white = {Ratio(t["--bp-text"], composite):0.00}");
        Assert.True(Ratio(t["--bp-muted"], composite) >= 4.5, $"muted on scrim over white = {Ratio(t["--bp-muted"], composite):0.00}");
        Assert.True(Math.Abs(Math.Round(Ratio(t["--bp-text"], composite), 2) - 10.69) <= 0.01, $"text {Ratio(t["--bp-text"], composite):0.00}, spec 10.69");
    }

    // ---------- source badges ----------

    [Theory]
    [InlineData("el")]
    [InlineData("en")]
    public async Task Source_badges_have_icon_and_text_and_league_says_mock(string culture)
    {
        var html = await Html(app.Client(culture), $"/p/{WithLeagueStat.Slug}");

        var badges = Regex.Matches(html, "data-source=\"(\\w+)\"[^>]*>\\s*(<svg.*?</svg>)\\s*<span class=\"bp-badge-text\">([^<]+)</span>", RegexOptions.Singleline)
            .Select(m => (Source: m.Groups[1].Value, Text: m.Groups[3].Value)).ToList();
        Assert.Equal(WithLeagueStat.Stats.Count, badges.Count);
        Assert.All(badges, b => Assert.False(string.IsNullOrWhiteSpace(b.Text)));
        var league = badges.First(b => b.Source == nameof(StatSource.LeagueVerifiedMock));
        Assert.Contains("mock", league.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Home_trust_strip_uses_icon_badges_for_all_three_sources()
    {
        var html = await Html(app.Client("en"), "/");

        foreach (var source in new[] { "SelfReported", "CoachConfirmed", "LeagueVerifiedMock" })
            Assert.Matches($"data-source=\"{source}\"[^>]*>\\s*<svg", html);
    }

    // ---------- helpers ----------

    /// <summary>Text a reader can see: head, scripts, styles, the footer disclosure and every tag (with its attributes) removed.</summary>
    private static string VisibleText(string html)
    {
        var body = Regex.Replace(html, "<head\\b.*?</head>|<script\\b.*?</script>|<style\\b.*?</style>|<footer[^>]*class=\"bp-footer\".*?</footer>", " ",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return Regex.Replace(Regex.Replace(body, "<[^>]+>", " "), "\\s+", " ");
    }

    private static Dictionary<string, string> Tokens()
    {
        var css = File.ReadAllText(Path.Combine(WebRoot, "css", "bp-tokens.css"));
        return Regex.Matches(css, "(--bp-[\\w-]+)\\s*:\\s*(#[0-9a-fA-F]{6})\\b")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);
    }

    private static double Luminance(string hex)
    {
        double Lin(int shift)
        {
            var c = int.Parse(hex.AsSpan(1 + shift, 2), NumberStyles.HexNumber) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Lin(0) + 0.7152 * Lin(2) + 0.0722 * Lin(4);
    }

    private static double Ratio(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

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

    private static async Task<string> Html(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        if (response.StatusCode is HttpStatusCode.Redirect) response = await client.GetAsync(response.Headers.Location);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ballpass.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Ballpass.slnx not found");
    }
}
