using System.Globalization;
using System.Net;
using System.Resources;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Features.Evidence;
using Ballpass.Web.Features.Find.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-audit-fixes/specs/find/spec.md
[Collection("db")]
public class AuditFindTests(BallpassAppFactory app)
{
    private readonly FeedHarness h = new(app);

    private async Task<HttpClient> Coach(string? culture = null) => await h.LoginAsync("coach", h.CoachId(9), culture);

    private static List<string> Articles(string html) =>
        Regex.Matches(html, "<article[^>]*class=\"bp-find-match\".*?</article>", RegexOptions.Singleline).Select(m => m.Value).ToList();

    private static string Slug(string article) => Regex.Match(article, "data-slug=\"([^\"]+)\"").Groups[1].Value;

    private static string StatReason(string article) =>
        Regex.Match(article, "<li[^>]*data-criterion=\"Stat\"[^>]*>(.*?)</li>", RegexOptions.Singleline).Groups[1].Value;

    private async Task<Dictionary<string, int?>> ProfileRebounds(IEnumerable<string> slugs)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
        var cohorts = scope.ServiceProvider.GetRequiredService<ICohortService>();
        var result = new Dictionary<string, int?>();
        foreach (var slug in slugs)
        {
            var athlete = await db.Athletes.AsNoTracking().Include(a => a.Stats).SingleAsync(a => a.Slug == slug);
            result[slug] = (await cohorts.EvaluateAsync(athlete))?.Get(EvidenceMetric.Rebounds)?.Percentile;
        }
        return result;
    }

    // ---------- 2. Match = profile ----------

    // Covers centers too since Evidence:MinPeers became config (default 15): match and profile must agree for both groups.
    [Theory]
    [InlineData("position=PF&position=SF", "φόργουορντ")]
    [InlineData("position=C", "σέντερ")]
    public async Task Match_top_percent_is_100_minus_the_profile_percentile(string positions, string group)
    {
        var client = await Coach();

        var html = await FeedHarness.Html(client, $"/match?gender=Female&{positions}&priority=rebounding");

        var articles = Articles(html);
        var profile = await ProfileRebounds(articles.Select(Slug));
        Assert.Contains(profile.Values, p => p is not null);
        var withTop = 0;
        foreach (var article in articles)
        {
            var percentile = profile[Slug(article)];
            var reason = StatReason(article);
            if (percentile is null)
            {
                Assert.Equal("", reason);                                       // missing: no reason at all
                Assert.Contains("bp-find-partial", article);
                continue;
            }
            Assert.NotEqual("", reason);
            // Only the claim "— top N%"; the below-average text says "κάτω από το top 50%" and must not match.
            var top = Regex.Match(reason, @"— top (\d+)%");
            if (top.Success)
            {
                withTop++;
                var topValue = int.Parse(top.Groups[1].Value);
                Assert.True(Math.Abs(topValue - (100 - percentile.Value)) <= 1,
                    $"{Slug(article)}: match says top {topValue}% but profile percentile is {percentile} (reason: {reason})");
                Assert.InRange(percentile.Value, 50, 99);
            }
            else Assert.True(percentile < 50, $"{Slug(article)} percentile {percentile} but no top-% text");
            Assert.Contains("ριμπ./40΄", reason);
            Assert.DoesNotContain("δείγμα", reason);   // flow-release O1
            Assert.Contains(group, reason);
        }
        Assert.True(withTop > 0, $"expected at least one top-% reason among {group}");
    }

    [Fact]
    public async Task Batch_percentiles_equal_the_profile_percentiles()
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
        var cohorts = scope.ServiceProvider.GetRequiredService<ICohortService>();
        var women = await db.Athletes.AsNoTracking().Include(a => a.Stats).Where(a => a.IsDemo && a.Gender == Gender.Female && a.Stats.Any()).ToListAsync();

        foreach (var metric in new[] { EvidenceMetric.Rebounds, EvidenceMetric.Points, EvidenceMetric.Assists })
        {
            var batch = await cohorts.PercentilesAsync(Gender.Female, metric);
            foreach (var athlete in women)
            {
                var profile = (await cohorts.EvaluateAsync(athlete))!.Get(metric)!.Percentile;
                if (profile is null) Assert.False(batch.ContainsKey(athlete.Id), $"{athlete.Slug} {metric}: batch has a value the profile does not show");
                else Assert.Equal(profile, batch[athlete.Id].Percentile);
            }
        }
    }

    // ---------- 8. lenient query parsing ----------

    public static IEnumerable<object[]> BadQueries =>
        new[]
        {
            "minHeight=abc", "maxHeight=abc", "minAge=abc", "maxAge=abc", "minHeight=999999999999", "minAge=-5", "page=abc", "page=999999999999",
            "confirmed=maybe", "looking=maybe", "q=%00", "q=a%00b%07c", "position=%00", "region=%00", "league=%00", "priority=%00", "gender=%00",
        }.SelectMany(q => new[] { new object[] { "/search", q }, new object[] { "/match", q } });

    [Theory]
    [MemberData(nameof(BadQueries))]
    public async Task Bad_query_values_never_fail_the_page(string page, string query)
    {
        var client = await Coach();

        var response = await client.GetAsync($"{page}?{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_values_are_treated_as_absent_and_q_is_stripped_of_control_characters()
    {
        var client = await Coach();
        var baseline = await FeedHarness.Html(client, "/search");

        var bad = await FeedHarness.Html(client, "/search?minHeight=abc&minAge=abc&looking=maybe&page=abc");
        Assert.Equal(Regex.Match(baseline, "data-total=\"(\\d+)\"").Value, Regex.Match(bad, "data-total=\"(\\d+)\"").Value);

        var q = await FeedHarness.Html(client, "/search?q=%00%07x");
        var input = Regex.Match(q, "<input[^>]*id=\"q\"[^>]*>").Value;
        Assert.DoesNotContain('\0', q);
        Assert.Matches("value=\"x\"", input);

        var match = await FeedHarness.Html(client, "/match?gender=Female&minHeight=abc&confirmed=maybe");
        Assert.Equal(Regex.Match(await FeedHarness.Html(client, "/match?gender=Female"), "data-total=\"(\\d+)\"").Value,
            Regex.Match(match, "data-total=\"(\\d+)\"").Value);
    }

    // ---------- 6 / 7 rule parser ----------

    [Theory]
    [InlineData("πόιντ γκαρντ", "PG")]
    [InlineData("ποιντ γκαρντ", "PG")]
    [InlineData("Πόιντ Γκαρντ γυναίκες", "PG")]
    [InlineData("σούτινγκ γκαρντ", "SG")]
    public void Greek_point_and_shooting_guard_map_to_one_position(string query, string expected)
    {
        var f = SearchFilterValidator.Validate(new RuleParser().Parse(query));

        Assert.Equal([expected], f.Positions);
    }

    [Fact]
    public void Plain_guard_is_still_both_guard_positions() =>
        Assert.Equal(["PG", "SG"], SearchFilterValidator.Validate(new RuleParser().Parse("γκαρντ")).Positions.OrderBy(x => x).ToArray());

    [Theory]
    [InlineData("γυναίκες κάτω των 16")]
    [InlineData("under 16")]
    public void Under_18_clamps_min_age_leaves_max_age_empty_and_keeps_the_minors_flag(string query)
    {
        var f = SearchFilterValidator.Validate(new RuleParser().Parse(query));

        Assert.Equal(18, f.MinAge);
        Assert.Null(f.MaxAge);
        Assert.Contains("minors", f.Unsupported);
    }

    [Fact]
    public async Task Under_16_search_has_results_and_shows_the_minors_chip()
    {
        var client = await Coach();
        var redirect = await client.GetAsync("/find/ai?q=" + Uri.EscapeDataString("γυναίκες κάτω των 16"));
        var url = redirect.Headers.Location!.OriginalString;
        Assert.Contains("minAge=18", url);
        Assert.DoesNotContain("maxAge", url);

        var html = await FeedHarness.Html(client, url);

        Assert.True(int.Parse(Regex.Match(html, "data-total=\"(\\d+)\"").Groups[1].Value) > 0);
        Assert.Matches("data-chip=\"unsupported\"", html);
        Assert.Contains("Μόνο ενήλικες", html);
    }

    // ---------- 9. one unsupported key ----------

    private static readonly ResourceManager FindRes = new("Ballpass.Web.Resources.FindResource", typeof(FindResource).Assembly);

    [Fact]
    public void Every_unsupported_key_the_validator_can_emit_has_a_greek_label()
    {
        Assert.Contains("stats", SearchFilterValidator.UnsupportedKeys);
        foreach (var key in SearchFilterValidator.UnsupportedKeys)
            Assert.False(string.IsNullOrWhiteSpace(FindRes.GetString($"Unsupported_{key}", new CultureInfo("el"))), $"Unsupported_{key} missing");
    }

    [Fact]
    public void Validator_only_emits_known_keys_and_maps_statistics_to_stats()
    {
        var f = SearchFilterValidator.Validate(new SearchFilters { Unsupported = ["statistics", "handedness", "favourite colour", "stats"] });

        Assert.Equal(["stats", "handedness", "other"], f.Unsupported);
        Assert.All(f.Unsupported, k => Assert.Contains(k, SearchFilterValidator.UnsupportedKeys));
    }

    [Fact]
    public void Rule_parser_keys_are_all_known_and_the_prompt_names_the_same_keys()
    {
        var parsed = new RuleParser().Parse("αριστερόχειρας μισθός ιθαγένεια ριμπάουντ");
        Assert.Equal(["handedness", "nationality", "salary", "stats"], parsed.Unsupported.OrderBy(x => x).ToArray());
        Assert.All(parsed.Unsupported, k => Assert.Contains(k, SearchFilterValidator.UnsupportedKeys));

        foreach (var key in SearchFilterValidator.ModelUnsupportedKeys) Assert.Contains($"\"{key}\"", ClaudeParser.SystemPrompt);
        Assert.DoesNotContain("\"statistics\"", ClaudeParser.SystemPrompt);   // the old key is gone, only the explanation mentions the word
    }

    // ---------- 11. singular counts ----------

    [Fact]
    public async Task Count_of_exactly_one_uses_the_singular_wording()
    {
        // Shrink the male cohort to two athletes (restored below): position PG leaves 1 result and 1 hidden. The "db" collection runs sequentially.
        var keep = h.Db(db => db.Athletes.Where(a => a.IsDemo && a.Gender == Gender.Male)
            .OrderBy(a => a.Id).Select(a => new { a.Id, a.Position }).ToList());
        var a1 = keep.First(a => a.Position == Position.PG).Id;
        var a2 = keep.First(a => a.Position == Position.SG).Id;
        var others = keep.Select(a => a.Id).Where(id => id != a1 && id != a2).ToList();
        h.Db(db => db.Athletes.Where(a => others.Contains(a.Id)).ExecuteUpdate(s => s.SetProperty(a => a.IsDemo, false)));
        try
        {
            var client = await Coach();
            var el = await FeedHarness.Html(client, "/match?gender=Male&position=PG");
            Assert.Contains(">1 αποτέλεσμα<", el);
            Assert.Contains(">1 κρυμμένος από φίλτρα<", el);

            await client.GetAsync("/culture/set?culture=en&redirectUri=%2Fmatch");
            var en = await FeedHarness.Html(client, "/match?gender=Male&position=PG");
            Assert.Contains(">1 result<", en);
            Assert.Contains(">1 hidden by filters<", en);
        }
        finally
        {
            h.Db(db => db.Athletes.Where(a => others.Contains(a.Id)).ExecuteUpdate(s => s.SetProperty(a => a.IsDemo, true)));
        }

        Assert.Equal(("2 αποτελέσματα", "2 κρυμμένοι από φίλτρα"), AuditCounts.Singular(new CultureInfo("el"), 2));
        Assert.Equal(("0 αποτελέσματα", "0 κρυμμένοι από φίλτρα"), AuditCounts.Singular(new CultureInfo("el"), 0));
    }
}

/// <summary>Resolves the count wording the way Match.razor does (keys Results_Count[_One] / Results_Hidden[_One]).</summary>
internal static class AuditCounts
{
    private static readonly ResourceManager Find = new("Ballpass.Web.Resources.FindResource", typeof(FindResource).Assembly);

    public static (string Count, string Hidden) Singular(CultureInfo culture, int n)
    {
        string Get(string baseKey) => string.Format(culture, Find.GetString(n == 1 ? baseKey + "_One" : baseKey, culture) ?? "MISSING", n);
        return (Get("Results_Count"), Get("Results_Hidden"));
    }
}
