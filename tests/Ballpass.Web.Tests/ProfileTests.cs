using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Media;

namespace Ballpass.Web.Tests;

// Specs: openspec/changes/add-profile-and-i18n/specs/{athlete-profile,localization}/spec.md
[Collection("db")]
public class ProfileTests(BallpassAppFactory app)
{
    private static readonly DemoDataSet Data = DemoDataGenerator.Generate(42);
    private static readonly Athlete WithClub = Data.Athletes.First(a => a.CurrentClub is not null && a.Stats.Count >= 2);
    private static readonly Athlete NoClub = Data.Athletes.First(a => a.CurrentClub is null);
    private static readonly Athlete CoachConfirmed = Data.Athletes.First(a => a.Stats.Any(s => s.Source == StatSource.CoachConfirmed));

    private static readonly Dictionary<Position, string> GreekPosition = new()
    {
        [Position.PG] = "Πόιντ γκαρντ", [Position.SG] = "Σούτινγκ γκαρντ", [Position.SF] = "Σμολ φόργουορντ",
        [Position.PF] = "Πάουερ φόργουορντ", [Position.C] = "Σέντερ",
    };

    [Fact]
    public async Task Profile_shows_identity_club_stats_and_share_link()
    {
        var (status, html) = await Get($"/p/{WithClub.Slug}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"{WithClub.FirstName} {WithClub.LastName}", html);
        Assert.Contains($"{WithClub.HeightCm}", html);
        Assert.Contains(GreekPosition[WithClub.Position], html);
        Assert.Contains(WithClub.CurrentClub!.Name, html);
        Assert.Equal(WithClub.Stats.Count, Regex.Matches(html, "<tr[^>]*data-season=\"").Count);
        Assert.Contains($"/p/{WithClub.Slug}", html);
        Assert.Contains("class=\"bp-footer\"", html);   // flow-release O1: footer disclosure replaces the banner
        Assert.DoesNotContain("demo-banner", html);
        Assert.DoesNotContain("bp-badge-demo", html);
    }

    [Fact]
    public async Task Coach_confirmed_label_in_greek()
    {
        var (_, html) = await Get($"/p/{CoachConfirmed.Slug}");

        Assert.Contains("Επιβεβαιωμένο από προπονητή", html);
    }

    [Fact]
    public async Task Athlete_without_club_shows_looking_for_team()
    {
        var (_, html) = await Get($"/p/{NoClub.Slug}");

        Assert.Contains("Ψάχνει ομάδα", html);
    }

    [Fact]
    public async Task Unknown_slug_is_404_with_banner()
    {
        var (status, html) = await Get("/p/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Contains("class=\"bp-footer\"", html);   // flow-release O1: footer disclosure replaces the banner
        Assert.DoesNotContain("demo-banner", html);
    }

    [Fact]
    public async Task Open_graph_title_contains_full_name()
    {
        var (_, html) = await Get($"/p/{WithClub.Slug}");

        var og = Regex.Match(html, "<meta[^>]*property=\"og:title\"[^>]*content=\"([^\"]*)\"");
        Assert.True(og.Success, "og:title meta missing");
        Assert.Contains($"{WithClub.FirstName} {WithClub.LastName}", og.Groups[1].Value);
    }

    [Fact]
    public async Task Greek_is_the_default_language()
    {
        var (_, html) = await Get($"/p/{WithClub.Slug}");

        Assert.Matches("<html[^>]*lang=\"el\"", html);
    }

    [Fact]
    public async Task English_cookie_switches_language_names_and_labels()
    {
        var (_, html) = await Get($"/p/{CoachConfirmed.Slug}", culture: "en");

        Assert.Matches("<html[^>]*lang=\"en\"", html);
        Assert.Contains($"{CoachConfirmed.FirstNameLatin} {CoachConfirmed.LastNameLatin}", html);
        Assert.Contains("Coach-confirmed", html);
    }

    [Fact]
    public async Task Culture_switch_sets_cookie_and_redirects_locally()
    {
        var response = await app.Client().GetAsync($"/culture/set?culture=en&redirectUri=/p/{WithClub.Slug}");

        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found);
        Assert.Equal($"/p/{WithClub.Slug}", response.Headers.Location?.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Culture=") && c.Contains("en"));
    }

    [Fact]
    public async Task Culture_switch_blocks_open_redirect()
    {
        var response = await app.Client().GetAsync("/culture/set?culture=en&redirectUri=https://evil.example/");

        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    // ---------- flow-release WP-B (SPEC §4.4, O9) ----------

    private const string FeaturedSlug = "dimitra-kalogeraki";   // lead review: percentile bars and strong best percentiles
    private static readonly Athlete Featured = Data.Athletes.Single(a => a.Slug == FeaturedSlug);

    private static int Primaries(string html) => Regex.Matches(html, "class=\"[^\"]*\\bbp-btn-primary\\b[^\"]*\"").Count;

    [Fact]
    public async Task Profile_has_a_cover_identity_proof_row_and_the_teal_looking_chip()
    {
        var (_, html) = await Get($"/p/{FeaturedSlug}");

        var cover = Regex.Match(html, "<header[^>]*class=\"bp-cover\".*?</header>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(cover);
        Assert.Contains($"src=\"{StockMedia.Cover(FeaturedSlug).Url}\"", cover);
        Assert.Contains("fetchpriority=\"high\"", cover);                       // above the fold: eager
        Assert.Contains("<h1", cover);
        Assert.Matches("class=\"bp-chip-athlete\"[^>]*>Ψάχνει ομάδα<", cover);   // SPEC §3 rule 2: one teal chip
        Assert.DoesNotContain("bp-badge-looking", html);
        var proof = Regex.Match(html, "<section[^>]*class=\"bp-proof\".*?</section>", RegexOptions.Singleline).Value;
        Assert.Equal(3, Regex.Matches(proof, "class=\"bp-proof-num\"").Count);
        var latest = Featured.Stats.OrderByDescending(s => s.Season, StringComparer.Ordinal).First();
        Assert.True(latest.Minutes >= 15, "the featured athlete must clear the Evidence minutes floor");
        Assert.Contains(latest.Points.ToString("0.0", new System.Globalization.CultureInfo("el")), proof);
        var pills = Pills(proof);
        Assert.Equal(2, pills.Count);                                              // her two best percentiles, both >= 70
        Assert.All(pills, p => Assert.InRange(p, 70, 99));
        Assert.Contains("class=\"bp-ev-fill\"", html);                            // percentile bars are drawn
    }

    // Lead decision: the proof row is about strengths; a pill only for a percentile >= 50 (0-2 pills, none if none qualify).
    [Fact]
    public async Task Proof_pills_only_show_percentiles_of_50_or_more()
    {
        // ioanna-papageorgiou (seed 42): best percentiles 40 and 29 -> no pill; the bars below still show every value.
        var (_, weak) = await Get("/p/ioanna-papageorgiou");
        var weakProof = Regex.Match(weak, "<section[^>]*class=\"bp-proof\".*?</section>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(weakProof);
        Assert.Empty(Pills(weakProof));
        Assert.Contains("class=\"bp-ev-fill\"", weak);

        foreach (var a in Data.Athletes.Where(x => x.Gender == Gender.Female).Take(60))
        {
            var (_, html) = await Get($"/p/{a.Slug}");
            var proof = Regex.Match(html, "<section[^>]*class=\"bp-proof\".*?</section>", RegexOptions.Singleline).Value;
            var pills = Pills(proof);
            Assert.InRange(pills.Count, 0, 2);
            Assert.All(pills, p => Assert.True(p >= 50, $"{a.Slug}: pill at percentile {p}"));
            var qualifying = Regex.Matches(html, "class=\"bp-ev-row\" data-metric=\"\\w+\" data-pct=\"(\\d+)\"").Count(m => int.Parse(m.Groups[1].Value) >= 50);
            Assert.Equal(Math.Min(2, qualifying), pills.Count);
        }
    }

    private static List<int> Pills(string proof) =>
        Regex.Matches(proof, "<span class=\"bp-proof-pill\"[^>]*data-pct=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)).ToList();

    [Fact]
    public async Task Featured_profile_shows_three_highlight_clips_and_no_empty_tile()
    {
        var (_, html) = await Get($"/p/{FeaturedSlug}");

        var section = Regex.Match(html, "<section[^>]*id=\"highlights\".*?</section>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(section);
        var tiles = Regex.Matches(section, "<div class=\"bp-media-tile[^\"]*\">(.*?)</div>", RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(3, tiles.Count);
        Assert.All(tiles, t => Assert.Matches("<img[^>]*src=\"[^\"]+\"|<video[^>]*poster=\"[^\"]+\"", t));
        Assert.Contains($"poster=\"{StockMedia.HighlightMain.Poster.Url}\"", section);
        Assert.Contains($"src=\"{StockMedia.HighlightThumb1.Url}\"", section);
        Assert.Contains($"src=\"{StockMedia.HighlightThumb2.Url}\"", section);
        Assert.DoesNotContain("Δεν έχει προστεθεί βίντεο ακόμη", html);
    }

    [Fact]
    public async Task Every_profile_media_tile_has_a_picture()
    {
        foreach (var a in Data.Athletes.Take(40))
        {
            var (_, html) = await Get($"/p/{a.Slug}");
            foreach (Match m in Regex.Matches(html, "<div class=\"bp-media-tile[^\"]*\">(.*?)</div>", RegexOptions.Singleline))
                Assert.Matches("<img[^>]*src=\"[^\"]+\"|<video[^>]*poster=\"[^\"]+\"", m.Groups[1].Value);
        }
    }

    [Fact]
    public async Task Coach_sees_one_primary_in_the_action_rail_and_the_share_card_closed()
    {
        var h = new FeedHarness(app);
        var (coach, slug) = h.Db(db =>
        {
            var c = db.Coaches.Where(x => x.IsDemo && x.Role == CoachRole.Head).OrderBy(x => x.Id).Skip(11).Select(x => x.Id).First();
            var s = db.Athletes.Where(a => a.IsDemo && a.Stats.Count >= 2 && !db.Interests.Any(i => i.CoachId == c && i.AthleteId == a.Id))
                .OrderBy(a => a.Id).Select(a => a.Slug).First();
            return (c, s);
        });
        var client = await h.LoginAsync("coach", coach);

        var html = await FeedHarness.Html(client, $"/p/{slug}");

        Assert.Equal(1, Primaries(html));
        var rail = Regex.Match(html, "<aside[^>]*class=\"bp-action-rail\".*?</aside>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(rail);
        Assert.Matches("<button[^>]*class=\"bp-btn-primary\"[^>]*>Εκδήλωση ενδιαφέροντος<", rail);
        Assert.Contains("class=\"bp-rail-more\"", rail);
        // The share card (og.png preview) only exists inside a closed <details> (never an empty box on the page).
        var share = Regex.Match(html, "<details(?<attrs>[^>]*)class=\"bp-rail-share\"(?<attrs2>[^>]*)>(?<body>.*?)</details>", RegexOptions.Singleline);
        Assert.True(share.Success, "share details missing");
        Assert.DoesNotContain(" open", share.Groups["attrs"].Value + share.Groups["attrs2"].Value);
        Assert.Contains($"/p/{slug}/og.png", share.Groups["body"].Value);
        Assert.Single(Regex.Matches(html, $"<img[^>]*src=\"/p/{slug}/og\\.png"));
    }

    [Fact]
    public async Task Anonymous_profile_has_no_primary_and_keeps_the_share_card_closed()
    {
        var (_, html) = await Get($"/p/{FeaturedSlug}");

        Assert.Equal(0, Primaries(html));
        Assert.Matches("<details[^>]*class=\"bp-rail-share\"", html);
        Assert.DoesNotContain("data-action=\"interest\"", html);
    }

    private async Task<(HttpStatusCode, string)> Get(string path, string? culture = null)
    {
        var response = await app.Client(culture).GetAsync(path);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        return (response.StatusCode, html);
    }
}
