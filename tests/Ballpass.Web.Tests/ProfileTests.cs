using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;

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
        Assert.Contains("class=\"demo-banner\"", html);
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
        Assert.Contains("class=\"demo-banner\"", html);
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

    private async Task<(HttpStatusCode, string)> Get(string path, string? culture = null)
    {
        var response = await app.Client(culture).GetAsync(path);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        return (response.StatusCode, html);
    }
}
