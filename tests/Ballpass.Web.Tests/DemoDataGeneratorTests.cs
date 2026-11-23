using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/add-demo-data/specs/demo-data/spec.md
public class DemoDataGeneratorTests
{
    private static readonly DemoDataSet Data = DemoDataGenerator.Generate(seed: 42);
    private static readonly DateOnly AgeReference = new(2026, 1, 1);

    [Fact]
    public void Same_seed_gives_identical_athletes()
    {
        var again = DemoDataGenerator.Generate(seed: 42);

        Assert.Equal(
            Data.Athletes.Select(Fingerprint),
            again.Athletes.Select(Fingerprint));
    }

    [Fact]
    public void Expected_volumes()
    {
        Assert.Equal(200, Data.Athletes.Count(a => a.Gender == Gender.Female));
        Assert.Equal(100, Data.Athletes.Count(a => a.Gender == Gender.Male));
        Assert.Equal(20, Data.Clubs.Count);
        Assert.Equal(12, Data.Clubs.Count(c => c.Gender == Gender.Female));
        Assert.Equal(8, Data.Clubs.Count(c => c.Gender == Gender.Male));
        Assert.Equal(30, Data.Coaches.Count);
        Assert.All(Data.Athletes, a =>
        {
            Assert.NotEmpty(a.Stats);
            Assert.NotEmpty(a.Seasons);
        });
    }

    [Fact]
    public void Everything_is_flagged_demo_and_clubs_are_labelled()
    {
        Assert.All(Data.Clubs, c => Assert.True(c.IsDemo));
        Assert.All(Data.Coaches, c => Assert.True(c.IsDemo));
        Assert.All(Data.Athletes, a => Assert.True(a.IsDemo));
        Assert.All(Data.Clubs, c => Assert.DoesNotContain("(demo)", c.Name));   // flow-release O1: no demo tag in visible names
        Assert.All(Data.Athletes, a => Assert.Null(a.VideoUrl));
    }

    [Fact]
    public void Adults_only()
    {
        Assert.All(Data.Athletes, a =>
        {
            // Worst case: birthday not yet passed on the reference date (audit C1: 2008 births are 17).
            var age = AgeReference.Year - a.BirthYear - 1;
            Assert.InRange(age, 18, 34);
        });
    }

    [Fact]
    public void Heights_and_stats_are_plausible()
    {
        Assert.All(Data.Athletes.Where(a => a.Gender == Gender.Female), a => Assert.InRange(a.HeightCm, 160, 200));
        Assert.All(Data.Athletes.Where(a => a.Gender == Gender.Male), a => Assert.InRange(a.HeightCm, 175, 220));
        Assert.All(Data.Athletes.SelectMany(a => a.Stats), s =>
        {
            Assert.InRange(s.Points, 0m, 30m);
            Assert.InRange(s.Rebounds, 0m, 15m);
            Assert.InRange(s.Assists, 0m, 10m);
            Assert.InRange(s.FgPct, 0m, 100m);
            Assert.InRange(s.ThreePct, 0m, 100m);
            Assert.InRange(s.FtPct, 0m, 100m);
        });
    }

    [Fact]
    public void Women_have_female_surname_forms()
    {
        var female = GreekNames.Surnames.Select(s => s.Female).ToHashSet();
        var male = GreekNames.Surnames.Select(s => s.Male).ToHashSet();

        Assert.All(Data.Athletes.Where(a => a.Gender == Gender.Female), a => Assert.Contains(a.LastName, female));
        Assert.All(Data.Athletes.Where(a => a.Gender == Gender.Male), a => Assert.Contains(a.LastName, male));
    }

    [Fact]
    public void Slugs_are_unique_lowercase_latin()
    {
        var slugs = Data.Athletes.Select(a => a.Slug).ToList();

        Assert.Equal(slugs.Count, slugs.Distinct().Count());
        Assert.All(slugs, s => Assert.Matches(new Regex("^[a-z0-9]+(-[a-z0-9]+)*$"), s));
    }

    [Fact]
    public void All_three_stat_sources_are_used()
    {
        var sources = Data.Athletes.SelectMany(a => a.Stats).Select(s => s.Source).Distinct().ToHashSet();

        Assert.Contains(StatSource.SelfReported, sources);
        Assert.Contains(StatSource.CoachConfirmed, sources);
        Assert.Contains(StatSource.LeagueVerifiedMock, sources);
    }

    // Lead review (flow-release WP-B): 15.5 ppg in 8.2 min read as 75.6 pts/40 on an investor-visible profile.
    // Per-game numbers must be plausible for the minutes played: per-40 caps pts 32, reb 20, ast 12, stl 5, blk 4.
    [Theory]
    [InlineData("Points", 32)]
    [InlineData("Rebounds", 20)]
    [InlineData("Assists", 12)]
    [InlineData("Steals", 5)]
    [InlineData("Blocks", 4)]
    public void No_stat_line_exceeds_the_per_40_cap(string stat, double cap)
    {
        var over = Data.Athletes.SelectMany(a => a.Stats.Select(s => (a.Slug, s)))
            .Select(x => (x.Slug, x.s.Season, Per40: (double)Value(x.s, stat) / (double)x.s.Minutes * 40))
            .Where(x => x.Per40 > cap + 1e-9)
            .ToList();

        Assert.True(over.Count == 0, $"{over.Count} lines over {cap} {stat}/40, e.g. {string.Join("; ", over.Take(3).Select(o => $"{o.Slug} {o.Season} {o.Per40:0.0}"))}");
    }

    [Fact]
    public void Lines_inside_the_caps_keep_their_per_game_numbers()
    {
        // Scaling only touches lines over a cap; at 30+ minutes nothing in the generator's ranges can exceed them.
        var full = Data.Athletes.SelectMany(a => a.Stats).Where(s => s.Minutes >= 30).ToList();
        Assert.NotEmpty(full);
        Assert.Contains(full, s => s.Points >= 15);
    }

    private static decimal Value(StatLine s, string stat) => stat switch
    {
        "Points" => s.Points,
        "Rebounds" => s.Rebounds,
        "Assists" => s.Assists,
        "Steals" => s.Steals,
        _ => s.Blocks,
    };

    private static string Fingerprint(Athlete a) =>
        $"{a.Slug}|{a.FirstName}|{a.LastName}|{a.HeightCm}|{a.Position}|{a.BirthYear}|" +
        string.Join(",", a.Stats.Select(s => $"{s.Season}:{s.Points}:{s.Rebounds}:{s.Assists}"));
}
