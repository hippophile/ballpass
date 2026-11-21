using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-look/specs/look/spec.md
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
    public async Task Every_route_is_dark_and_keeps_the_demo_banner(string path, string? persona)
    {
        var client = app.Client();
        if (persona is not null) await SignIn(client, persona);
        path = path.Replace("SLUG", WithLeagueStat.Slug);

        var html = await Html(client, path);

        Assert.Matches("<html[^>]*data-theme=\"dark\"", html);
        Assert.Matches("<html[^>]*data-bs-theme=\"dark\"", html);
        var banner = Regex.Match(html, "<[^>]*class=\"demo-banner\"[^>]*>(.*?)</div>", RegexOptions.Singleline);
        Assert.True(banner.Success, $"demo banner missing on {path}");
        Assert.Contains("role=\"status\"", banner.Value);
        Assert.Contains("φανταστικοί", banner.Groups[1].Value);
        Assert.Contains("are fictional", banner.Groups[1].Value);
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

    // ---------- contrast ----------

    [Fact]
    public void Text_tokens_meet_4_5_on_all_surfaces()
    {
        var t = Tokens();
        string[] text = ["--bp-text", "--bp-muted", "--bp-accent", "--bp-success", "--bp-src-self", "--bp-src-coach", "--bp-src-league"];
        foreach (var fg in text)
            foreach (var bg in new[] { "--bp-bg", "--bp-s1", "--bp-s2" })
                Assert.True(Ratio(t[fg], t[bg]) >= 4.5, $"{fg} on {bg} = {Ratio(t[fg], t[bg]):0.00}");
    }

    [Fact]
    public void Ui_border_meets_3_on_bg_s1_s2()
    {
        var t = Tokens();
        foreach (var bg in new[] { "--bp-bg", "--bp-s1", "--bp-s2" })
            Assert.True(Ratio(t["--bp-border-ui"], t[bg]) >= 3, $"--bp-border-ui on {bg} = {Ratio(t["--bp-border-ui"], t[bg]):0.00}");
    }

    [Fact]
    public void Dark_text_on_accent_meets_4_5()
    {
        var t = Tokens();
        Assert.True(Ratio(t["--bp-accent-contrast"], t["--bp-accent"]) >= 4.5, $"{Ratio(t["--bp-accent-contrast"], t["--bp-accent"]):0.00}");
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
