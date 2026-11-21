using System.Net;
using System.Text.RegularExpressions;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/polish-home-and-mobile/specs/home/spec.md
[Collection("db")]
public class HomeTests(BallpassAppFactory app)
{
    [Fact]
    public async Task Home_has_demo_cta_and_working_sample_profile_link()
    {
        var html = await Html("/");

        Assert.Matches("<a[^>]*data-cta=\"demo\"[^>]*href=\"/demo\"|<a[^>]*href=\"/demo\"[^>]*data-cta=\"demo\"", html);
        var sample = Regex.Match(html, "<a[^>]*data-cta=\"sample\"[^>]*href=\"(/p/[^\"]+)\"|<a[^>]*href=\"(/p/[^\"]+)\"[^>]*data-cta=\"sample\"");
        Assert.True(sample.Success, "sample profile CTA missing");
        var path = sample.Groups[1].Success ? sample.Groups[1].Value : sample.Groups[2].Value;
        Assert.Equal(HttpStatusCode.OK, (await app.Client().GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Home_shows_live_demo_counts()
    {
        var html = await Html("/");

        Assert.Matches("data-count=\"athletes\"[^>]*>\\s*300\\s*<", html);
        Assert.Matches("data-count=\"clubs\"[^>]*>\\s*20\\s*<", html);
    }

    [Fact]
    public async Task Home_in_english()
    {
        var html = await Html("/", "en");

        Assert.Contains("Try as a coach", html);
        Assert.Matches("<html[^>]*lang=\"en\"", html);
    }

    private async Task<string> Html(string path, string? culture = null) =>
        WebUtility.HtmlDecode(await (await app.Client(culture).GetAsync(path)).Content.ReadAsStringAsync());
}
