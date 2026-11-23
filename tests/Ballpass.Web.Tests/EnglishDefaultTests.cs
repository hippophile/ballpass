using System.Globalization;
using System.Net;
using System.Resources;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/english-default/specs/localization/spec.md

/// <summary>The app with its shipped configuration (no culture pin), on its own database so it never races the shared one.</summary>
public class DefaultCultureAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Ballpass", TestDb.ConnectionString("ballpass_endefault"));

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().ResetAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}

public class EnglishDefaultTests(DefaultCultureAppFactory app) : IClassFixture<DefaultCultureAppFactory>
{
    private HttpClient Client(string? acceptLanguage = null)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (acceptLanguage is not null) client.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);
        return client;
    }

    private string Config(string key) => app.Services.GetRequiredService<IConfiguration>()[key] ?? "";

    private T Db<T>(Func<BallpassDbContext, T> query)
    {
        using var scope = app.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<BallpassDbContext>());
    }

    private async Task<HttpClient> SignedIn(string type, string? acceptLanguage)
    {
        var id = type == "coach"
            ? Db(db => db.Coaches.Where(c => c.IsDemo && c.FirstName == Config("Demo:Featured:CoachFirstName") && c.LastName == Config("Demo:Featured:CoachLastName")).Select(c => c.Id).Single())
            : Db(db => db.Athletes.Where(a => a.Slug == Config("Demo:Featured:AthleteSlug")).Select(a => a.Id).Single());
        var client = Client(acceptLanguage);
        var token = FeedHarness.Token(await FeedHarness.Html(client, "/demo"));
        var response = await client.PostAsync("/demo/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["personaType"] = type, ["personaId"] = id.ToString(), ["returnUrl"] = "", ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    // The footer is on every page: its English line proves the chrome is English, its Greek line must be gone.
    private static void AssertEnglish(string html, string path)
    {
        Assert.True(Regex.IsMatch(html, "<html[^>]*lang=\"en\""), $"{path}: <html lang> is not en");
        Assert.True(html.Contains("Fictional people and data"), $"{path}: English footer missing");
        Assert.False(html.Contains("Φανταστικά πρόσωπα"), $"{path}: Greek footer present");
    }

    public static TheoryData<string, string?, string?> Pages()
    {
        var data = new TheoryData<string, string?, string?>();
        foreach (var lang in new string?[] { null, "el", "el-GR,el;q=0.9" })
        {
            data.Add("/", null, lang);
            data.Add("/why", null, lang);
            data.Add("/demo", null, lang);
            data.Add("/feed", "coach", lang);
            data.Add("/match?need=featured", "coach", lang);
            data.Add("/p/{featured}", null, lang);
            data.Add("/messages", "coach", lang);
            data.Add("/me/inbox", "athlete", lang);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Shipped_config_renders_English_without_a_cookie_whatever_the_browser_language(string path, string? persona, string? acceptLanguage)
    {
        var client = persona is null ? Client(acceptLanguage) : await SignedIn(persona, acceptLanguage);
        var url = path.Replace("{featured}", Config("Demo:Featured:AthleteSlug"));

        var response = await client.GetAsync(url);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertEnglish(html, url);
    }

    [Fact]
    public void Shipped_default_culture_is_english()
    {
        Assert.Equal("en", Config("Localization:DefaultCulture"));
    }

    [Fact]
    public async Task Toggle_cookie_switches_to_greek_and_back()
    {
        var client = Client("en-US");

        var set = await client.GetAsync("/culture/set?culture=el&redirectUri=/why");
        Assert.Equal(HttpStatusCode.Redirect, set.StatusCode);
        var cookie = set.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Culture="));
        client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        var greek = WebUtility.HtmlDecode(await (await client.GetAsync("/why")).Content.ReadAsStringAsync());

        Assert.Matches("<html[^>]*lang=\"el\"", greek);
        Assert.Contains("Φανταστικά πρόσωπα και δεδομένα", greek);
        Assert.DoesNotContain("Fictional people and data", greek);
    }

    [Fact]
    public async Task Language_toggle_lists_EN_before_EL()
    {
        var html = await FeedHarness.Html(Client(), "/");

        var toggle = Regex.Match(html, "<nav[^>]*class=\"lang-toggle\"[^>]*>(.*?)</nav>", RegexOptions.Singleline).Groups[1].Value;
        var order = Regex.Matches(toggle, "hreflang=\"(\\w+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["en", "el"], order);
        Assert.Matches("hreflang=\"en\"[^>]*class=\"active\"", toggle);
    }

    // ---------- resources ----------

    private static readonly string[] ResourceNames = typeof(SharedResource).Assembly.GetManifestResourceNames()
        .Select(n => Regex.Match(n, @"^Ballpass\.Web\.Resources\.(\w+)\.resources$")).Where(m => m.Success)
        .Select(m => m.Groups[1].Value).Order().ToArray();

    public static TheoryData<string> Resources() => new(ResourceNames);

    private static Dictionary<string, string> Load(string resource, string culture, bool tryParents)
    {
        var manager = new ResourceManager($"Ballpass.Web.Resources.{resource}", typeof(SharedResource).Assembly);
        var set = manager.GetResourceSet(new CultureInfo(culture), createIfNotExists: true, tryParents: tryParents)!;
        return set.Cast<System.Collections.DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!);
    }

    [Fact]
    public void Every_resource_has_an_english_file()
    {
        Assert.Equal(11, ResourceNames.Length);
    }

    [Theory]
    [MemberData(nameof(Resources))]
    public void Every_key_exists_in_both_languages(string resource)
    {
        var el = Load(resource, "el", tryParents: true);
        var en = Load(resource, "en", tryParents: false);

        Assert.Equal(el.Keys.Order(), en.Keys.Order());
    }

    private static readonly Regex Greek = new(@"[Ͱ-Ͽἀ-῿]");

    [Theory]
    [MemberData(nameof(Resources))]
    public void No_greek_leaks_into_english(string resource)
    {
        var el = Load(resource, "el", tryParents: true);
        var en = Load(resource, "en", tryParents: false);

        // Greek letters in an English value, or an English value copied from a Greek one that has Greek letters.
        var offenders = en.Where(e => Greek.IsMatch(e.Value) || (el.TryGetValue(e.Key, out var g) && g == e.Value && Greek.IsMatch(g)))
            .Select(e => $"{e.Key}: «{e.Value}»").ToList();
        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    [Theory]
    [MemberData(nameof(Resources))]
    public void English_has_no_demo_words_outside_the_disclosures(string resource)
    {
        string[] allowed = ["Footer_Disclosure", "Demo_Intro"];
        var en = Load(resource, "en", tryParents: false);

        var offenders = en.Where(e => !(resource == "SharedResource" && allowed.Contains(e.Key)) && Regex.IsMatch(e.Value, @"(?i)\b(demo|sample|fictional)\b"))
            .Select(e => $"{e.Key}: «{e.Value}»").ToList();
        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }
}
