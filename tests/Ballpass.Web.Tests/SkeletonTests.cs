using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/add-skeleton/specs/platform/spec.md (MVP item M0)
public class SkeletonTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_returns_200_Healthy_when_database_reachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_returns_503_when_database_unreachable()
    {
        var client = factory.WithWebHostBuilder(b => b.UseSetting(
                "ConnectionStrings:Ballpass",
                "Host=127.0.0.1;Port=1;Database=ballpass;Username=postgres;Password=x;Timeout=2"))
            .CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/this-page-does-not-exist")]
    public async Task Every_page_shows_one_footer_disclosure_and_no_banner(string path)
    {
        // Spec: openspec/changes/flow-release/specs/platform/spec.md (the banner is replaced by one quiet footer line, SPEC §8 O1)
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        if (path == "/") Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("demo-banner", html);
        var footer = Regex.Match(html, "<footer[^>]*class=\"bp-footer\"[^>]*>(.*?)</footer>", RegexOptions.Singleline);
        Assert.True(footer.Success, $"footer disclosure missing on {path}");
        Assert.Contains("Fictional people and data", footer.Groups[1].Value);   // unpinned host: shipped default culture (en)
    }

    [Fact]
    public void Tracked_appsettings_contain_no_Ballpass_connection_string()
    {
        var webDir = Path.Combine(RepoRoot(), "src", "Ballpass.Web");

        foreach (var file in Directory.GetFiles(webDir, "appsettings*.json"))
        {
            var json = File.ReadAllText(file);
            Assert.DoesNotMatch("\"Ballpass\"\\s*:\\s*\"[^\"]+\"", json);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ballpass.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Ballpass.slnx not found");
    }
}
