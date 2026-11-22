using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Ballpass.Web.Data;
using Ballpass.Web.Demo;
using Ballpass.Web.Features.Evidence;
using Microsoft.Extensions.DependencyInjection;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-evidence/specs/evidence/spec.md
// Pure logic first (no DB), then pages against the seed-42 data in ballpass_test.
[Collection("db")]
public class EvidenceTests(BallpassAppFactory app)
{
    private static readonly DemoDataSet Data = DemoDataGenerator.Generate(42);
    private static readonly StatAggregator Stats = new();
    private static readonly string WebRoot = Path.Combine(RepoRoot(), "src", "Ballpass.Web", "wwwroot");

    private static StatLine Line(string season, int games = 20, double minutes = 20, double points = 10, double rebounds = 4,
        double assists = 3, double steals = 1, double fg = 45, StatSource source = StatSource.SelfReported) => new()
    {
        Season = season, Games = games, Minutes = (decimal)minutes, Points = (decimal)points, Rebounds = (decimal)rebounds,
        Assists = (decimal)assists, Steals = (decimal)steals, FgPct = (decimal)fg, Source = source,
    };

    // ---------- percentile math ----------

    [Fact]
    public void Mid_rank_percentile_worked_examples()
    {
        var hundred = Enumerable.Range(1, 100).Select(i => (double)i).ToList();

        Assert.Equal(78, CohortCalculator.MidRankPercentile(hundred, 78.5));   // 78 below, 0 equal
        Assert.Equal(79, CohortCalculator.MidRankPercentile(hundred, 79));     // 78 below, 1 equal: 78.5 -> 79 (away from zero)
    }

    [Fact]
    public void Mid_rank_counts_ties_as_half()
    {
        double[] peers = [10, 10, 10, 20];

        Assert.Equal(38, CohortCalculator.MidRankPercentile(peers, 10));       // (0 + 3*0.5)/4 = 37.5 -> 38
        Assert.Equal(88, CohortCalculator.MidRankPercentile(peers, 20));       // (3 + 0.5)/4 = 87.5 -> 88
    }

    [Fact]
    public void Percentile_is_clamped_to_1_and_99()
    {
        var hundred = Enumerable.Range(1, 100).Select(i => (double)i).ToList();

        Assert.Equal(99, CohortCalculator.MidRankPercentile(hundred, 1000));
        Assert.Equal(1, CohortCalculator.MidRankPercentile(hundred, -5));
    }

    [Fact]
    public void Rank_is_one_plus_peers_strictly_above()
    {
        Assert.Equal(2, CohortCalculator.Rank([5, 3, 9], 6));
        Assert.Equal(1, CohortCalculator.Rank([5, 3, 9], 10));
        Assert.Equal(1, CohortCalculator.Rank([9, 9, 3], 9));   // ties share the best rank
    }

    [Fact]
    public void Percentile_78_fills_156_of_200()
    {
        Assert.Equal(156, ChartGeometry.FillWidth(78));
        Assert.Equal(200, ChartGeometry.TrackWidth);
        Assert.Equal(2, ChartGeometry.FillWidth(1));
    }

    [Fact]
    public void Position_groups()
    {
        Assert.Equal(PositionGroup.Guard, CohortCalculator.GroupOf(Position.PG));
        Assert.Equal(PositionGroup.Guard, CohortCalculator.GroupOf(Position.SG));
        Assert.Equal(PositionGroup.Forward, CohortCalculator.GroupOf(Position.SF));
        Assert.Equal(PositionGroup.Forward, CohortCalculator.GroupOf(Position.PF));
        Assert.Equal(PositionGroup.Center, CohortCalculator.GroupOf(Position.C));
        Assert.Equal([Position.PG, Position.SG], CohortCalculator.PositionsOf(PositionGroup.Guard));
        Assert.Equal([Position.C], CohortCalculator.PositionsOf(PositionGroup.Center));
    }

    // ---------- thresholds ----------

    private static List<CohortMember> Cohort(int count, int firstId = 100, int games = 20, StatSource source = StatSource.SelfReported) =>
        Enumerable.Range(0, count).Select(i => new CohortMember(firstId + i, Line("2025-26", games: games, points: 5 + i, source: source))).ToList();

    private static CohortEvidence Eval(StatLine subject, IReadOnlyList<CohortMember> cohort, bool confirmedOnly = false) =>
        CohortCalculator.Evaluate(1, Gender.Female, PositionGroup.Guard, subject, cohort, confirmedOnly, Stats);

    [Fact]
    public void Twenty_peers_and_ten_games_give_a_percentile()
    {
        var result = Eval(Line("2025-26", games: 10, points: 15, minutes: 20), Cohort(20));

        var points = result.Get(EvidenceMetric.Points)!;
        Assert.Equal(InsufficientReason.None, points.Reason);
        Assert.NotNull(points.Percentile);
        Assert.Equal(20, result.PeerCount);
    }

    [Fact]
    public void Nineteen_peers_give_no_fill_and_report_n()
    {
        var result = Eval(Line("2025-26", points: 15), Cohort(19));

        Assert.All(result.Metrics, m =>
        {
            Assert.Null(m.Percentile);
            Assert.Equal(InsufficientReason.TooFewPeers, m.Reason);
            Assert.Equal(19, m.N);
        });
    }

    [Fact]
    public void Nine_games_give_no_fill_even_with_a_big_cohort()
    {
        var result = Eval(Line("2025-26", games: 9, points: 15), Cohort(50));

        var points = result.Get(EvidenceMetric.Points)!;
        Assert.Null(points.Percentile);
        Assert.Equal(InsufficientReason.TooFewGames, points.Reason);
        Assert.Equal(9, points.N);
    }

    [Fact]
    public void Peers_under_ten_games_and_the_athlete_itself_are_not_peers()
    {
        var cohort = Cohort(25).Concat(Cohort(6, firstId: 500, games: 9)).ToList();
        cohort.Add(new CohortMember(1, Line("2025-26", points: 99)));   // the subject (athleteId 1)

        var result = Eval(Line("2025-26", points: 15), cohort);

        Assert.Equal(25, result.PeerCount);
    }

    [Fact]
    public void Confirmed_only_recompute_changes_the_cohort()
    {
        var cohort = Cohort(30).Concat(Cohort(24, firstId: 300, source: StatSource.CoachConfirmed)).ToList();
        var subject = Line("2025-26", points: 15);

        var all = Eval(subject, cohort);
        var confirmed = Eval(subject, cohort, confirmedOnly: true);

        Assert.Equal(54, all.PeerCount);
        Assert.Equal(24, confirmed.PeerCount);
        Assert.True(confirmed.ConfirmedOnly);
        Assert.NotEqual(all.Get(EvidenceMetric.Points)!.Percentile, confirmed.Get(EvidenceMetric.Points)!.Percentile);
    }

    [Fact]
    public void Confirmed_only_below_twenty_confirmed_peers_has_no_fill()
    {
        var cohort = Cohort(40).Concat(Cohort(8, firstId: 300, source: StatSource.CoachConfirmed)).ToList();

        var confirmed = Eval(Line("2025-26"), cohort, confirmedOnly: true);

        Assert.Equal(8, confirmed.PeerCount);
        Assert.Null(confirmed.Get(EvidenceMetric.Points)!.Percentile);
    }

    // ---------- aggregates ----------

    [Fact]
    public void Per_40_scales_counting_stats_by_minutes_but_not_fg_pct()
    {
        var latest = Stats.Latest([Line("2025-26", minutes: 25, points: 10, rebounds: 5, assists: 2.5, steals: 1, fg: 44)])!;

        Assert.Equal(10, latest.PerGame.Points);
        Assert.Equal(16, latest.Per40.Points, 6);
        Assert.Equal(8, latest.Per40.Rebounds, 6);
        Assert.Equal(4, latest.Per40.Assists, 6);
        Assert.Equal(44, latest.Per40.FgPct, 6);
        Assert.Equal(16, Stats.MetricValue(Line("2025-26", minutes: 25, points: 10), EvidenceMetric.Points), 6);
        Assert.Equal(44, Stats.MetricValue(Line("2025-26", fg: 44), EvidenceMetric.FgPct), 6);
    }

    [Fact]
    public void Latest_picks_the_newest_season_by_ordinal_order()
    {
        var lines = new[] { Line("2025-26", points: 12), Line("2023-24", points: 4), Line("2024-25", points: 8) };

        Assert.Equal("2025-26", Stats.LatestLine(lines)!.Season);
        Assert.Equal(12, Stats.Latest(lines)!.PerGame.Points);
        Assert.Null(Stats.Latest([]));
    }

    [Fact]
    public void Career_is_weighted_by_games_and_minutes()
    {
        // 10 games x 10 pts (20 min) + 30 games x 20 pts (30 min)
        var lines = new[] { Line("2024-25", games: 10, points: 10, minutes: 20), Line("2025-26", games: 30, points: 20, minutes: 30) };

        var career = Stats.Career(lines)!;

        Assert.Equal(40, career.Games);
        Assert.Equal(17.5, career.PerGame.Points, 6);                    // (100 + 600) / 40
        Assert.Equal(700.0 / 1100.0 * 40, career.Per40.Points, 6);       // total points / total minutes * 40
        Assert.Equal(1100, career.TotalMinutes, 6);
    }

    [Fact]
    public void Source_mix_counts_seasons_per_source()
    {
        var mix = Stats.SourceMix([Line("a", source: StatSource.CoachConfirmed), Line("b", source: StatSource.CoachConfirmed), Line("c")]);

        Assert.Equal(2, mix[StatSource.CoachConfirmed]);
        Assert.Equal(1, mix[StatSource.SelfReported]);
        Assert.Equal(0, mix[StatSource.LeagueVerifiedMock]);
    }

    // ---------- trend ----------

    [Theory]
    [InlineData(11.0, 10.0, 20, 20, TrendDirection.Up)]           // exactly +10%
    [InlineData(10.9, 10.0, 20, 20, TrendDirection.Flat)]
    [InlineData(10.5, 10.0, 20, 20, TrendDirection.Flat)]
    [InlineData(10.0, 10.0, 20, 20, TrendDirection.Flat)]
    [InlineData(9.1, 10.0, 20, 20, TrendDirection.Flat)]
    [InlineData(9.0, 10.0, 20, 20, TrendDirection.Down)]          // exactly -10%
    [InlineData(5.0, 10.0, 20, 20, TrendDirection.Down)]
    [InlineData(15.0, 10.0, 7, 20, TrendDirection.NotAvailable)]  // latest season under 8 games
    [InlineData(15.0, 10.0, 20, 7, TrendDirection.NotAvailable)]  // previous season under 8 games
    [InlineData(15.0, 10.0, 8, 8, TrendDirection.Up)]             // 8 games is enough
    [InlineData(15.0, 0.0, 20, 20, TrendDirection.NotAvailable)]  // no baseline
    public void Trend_follows_the_ten_percent_rule(double latest, double previous, int latestGames, int previousGames, TrendDirection expected)
    {
        var lines = new[] { Line("2024-25", games: previousGames, points: previous), Line("2025-26", games: latestGames, points: latest) };

        Assert.Equal(expected, Stats.Trend(lines).Direction);
    }

    [Fact]
    public void Trend_is_not_available_for_a_single_season_or_a_gap()
    {
        Assert.Equal(TrendDirection.NotAvailable, Stats.Trend([Line("2025-26")]).Direction);
        Assert.Equal(TrendDirection.NotAvailable, Stats.Trend([Line("2023-24", points: 5), Line("2025-26", points: 20)]).Direction);
        Assert.Equal(TrendDirection.NotAvailable, Stats.Trend([]).Direction);
    }

    [Fact]
    public void Trend_reports_the_change()
    {
        var trend = Stats.Trend([Line("2024-25", points: 10), Line("2025-26", points: 12)]);

        Assert.Equal(TrendDirection.Up, trend.Direction);
        Assert.Equal(20m, trend.ChangePct);
    }

    // ---------- season line ----------

    [Fact]
    public void Missing_season_is_a_null_slot_and_breaks_the_line()
    {
        var slots = Stats.Progression([Line("2022-23", points: 4), Line("2023-24", points: 5), Line("2025-26", points: 9)]);

        Assert.Equal(["2022-23", "2023-24", "2024-25", "2025-26"], slots.Select(s => s.Season).ToArray());
        Assert.Null(slots[2].Line);

        var layout = ChartGeometry.Layout(slots, 320, 140);
        Assert.Equal(2, layout.Segments.Count);
        Assert.Equal(2, layout.Segments[0].Count);
        Assert.Single(layout.Segments[1]);
        Assert.Equal(4, layout.Axis.Count);
    }

    [Fact]
    public void Layout_points_stay_inside_the_chart_and_higher_values_sit_higher()
    {
        var slots = Stats.Progression([Line("2024-25", points: 5), Line("2025-26", points: 15)]);

        var points = ChartGeometry.Layout(slots, 320, 140).Segments.Single();

        Assert.All(points, p => { Assert.InRange(p.X, 0, 320); Assert.InRange(p.Y, 0, 140); });
        Assert.True(points[1].Y < points[0].Y, "SVG y grows downward: bigger value = smaller y");
        Assert.True(points[1].X > points[0].X);
    }

    // ---------- invariant culture ----------

    [Fact]
    public void Svg_numbers_use_dots_under_greek_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("el");
            Assert.Equal("12.5", ChartGeometry.Fmt(12.5));
            Assert.Equal("0.3", ChartGeometry.Fmt(0.1 + 0.2));
            Assert.Equal("7", ChartGeometry.Fmt(7.0));
            var points = ChartGeometry.PointsAttr(ChartGeometry.Layout(Stats.Progression([Line("2024-25", points: 5.5), Line("2025-26", points: 9.5)]), 320, 140).Segments.Single());
            Assert.Matches(@"^-?\d+(\.\d+)?,-?\d+(\.\d+)?( -?\d+(\.\d+)?,-?\d+(\.\d+)?)*$", points);
            Assert.Contains(".", points);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    // ---------- badges ----------

    private static readonly BadgeService Badges = new(Stats);

    private static string[] Rules(IEnumerable<StatLine> lines, int? pointsPercentile = null) =>
        Badges.Evaluate(lines, pointsPercentile).Select(b => b.Rule).ToArray();

    [Fact]
    public void Coach_confirmed_badge_counts_confirmed_seasons()
    {
        var lines = new[] { Line("2024-25", source: StatSource.CoachConfirmed), Line("2025-26", source: StatSource.CoachConfirmed) };

        var badge = Badges.Evaluate(lines, null).Single(b => b.Rule == "coach-confirmed");

        Assert.Equal(2, badge.Args[0]);
        Assert.Equal(StatSource.CoachConfirmed, badge.Source);
        Assert.DoesNotContain("coach-confirmed", Rules([Line("2025-26")]));
    }

    [Fact]
    public void League_verified_badge_counts_league_seasons()
    {
        var badge = Badges.Evaluate([Line("2025-26", source: StatSource.LeagueVerifiedMock)], null).Single(b => b.Rule == "league-verified");

        Assert.Equal(1, badge.Args[0]);
        Assert.Equal(StatSource.LeagueVerifiedMock, badge.Source);
        Assert.DoesNotContain("league-verified", Rules([Line("2025-26", source: StatSource.CoachConfirmed)]));
    }

    [Fact]
    public void Multi_season_badge_needs_three_seasons()
    {
        Assert.Contains("multi-season", Rules([Line("2023-24"), Line("2024-25"), Line("2025-26")]));
        Assert.DoesNotContain("multi-season", Rules([Line("2024-25"), Line("2025-26")]));
    }

    [Fact]
    public void Regular_badge_needs_25_games_in_the_latest_season()
    {
        Assert.Contains("regular", Rules([Line("2025-26", games: 25)]));
        Assert.DoesNotContain("regular", Rules([Line("2025-26", games: 24)]));
        Assert.DoesNotContain("regular", Rules([Line("2024-25", games: 30), Line("2025-26", games: 12)]));
    }

    [Fact]
    public void Rising_badge_follows_the_trend_rule()
    {
        Assert.Contains("rising", Rules([Line("2024-25", points: 10), Line("2025-26", points: 11)]));
        Assert.DoesNotContain("rising", Rules([Line("2024-25", points: 10), Line("2025-26", points: 10.5)]));
        Assert.DoesNotContain("rising", Rules([Line("2024-25", points: 10, games: 5), Line("2025-26", points: 20)]));
    }

    [Fact]
    public void Top_quartile_badge_needs_a_valid_percentile_of_75_or_more()
    {
        var lines = new[] { Line("2025-26") };

        Assert.Equal(80, Badges.Evaluate(lines, 80).Single(b => b.Rule == "top-quartile").Args[0]);
        Assert.Contains("top-quartile", Rules(lines, 75));
        Assert.DoesNotContain("top-quartile", Rules(lines, 74));
        Assert.DoesNotContain("top-quartile", Rules(lines, null));
    }

    [Fact]
    public void No_stats_no_badges()
    {
        Assert.Empty(Badges.Evaluate([], null));
    }

    // ---------- chart tokens ----------

    [Fact]
    public void Chart_tokens_meet_3_to_1_on_the_page_background()
    {
        var bg = Regex.Match(File.ReadAllText(Path.Combine(WebRoot, "css", "bp-tokens.css")), "--bp-bg\\s*:\\s*(#[0-9a-fA-F]{6})").Groups[1].Value;
        Assert.Equal("#0B0F14", bg.ToUpperInvariant());
        var css = File.ReadAllText(Path.Combine(WebRoot, "css", "bp-evidence.css"));
        var tokens = Regex.Matches(css, "(--bp-chart-[a-e])\\s*:\\s*(#[0-9a-fA-F]{6})").ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);

        Assert.Equal(["--bp-chart-a", "--bp-chart-b", "--bp-chart-c", "--bp-chart-d", "--bp-chart-e"], tokens.Keys.Order().ToArray());
        foreach (var (name, hex) in tokens)
            Assert.True(Ratio(hex, bg) >= 3, $"{name} {hex} on {bg} = {Ratio(hex, bg):0.00}");
    }

    [Fact]
    public void Chart_tokens_are_not_defined_in_the_shared_token_file()
    {
        Assert.DoesNotContain("--bp-chart", File.ReadAllText(Path.Combine(WebRoot, "css", "bp-tokens.css")));
    }

    [Fact]
    public async Task Evidence_stylesheet_is_linked_after_the_base_styles()
    {
        var html = await Html(app.Client(), $"/p/{Slug(Female(Position.PG))}");

        var hrefs = Regex.Matches(html, "<link[^>]*rel=\"stylesheet\"[^>]*href=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.True(hrefs.FindIndex(h => h.Contains("bp-evidence")) > hrefs.FindIndex(h => h.Contains("bp-base")), string.Join(", ", hrefs));
    }

    // ---------- profile ----------

    private static Athlete Female(Position p) => Data.Athletes.First(a => a.Gender == Gender.Female && a.Position == p && a.Stats.Count >= 2
        && a.Stats.OrderByDescending(s => s.Season, StringComparer.Ordinal).First() is { Games: >= 10, Minutes: >= 15 });
    private static string Slug(Athlete a) => a.Slug;

    private static double Metric(StatLine l, EvidenceMetric m) => m switch
    {
        EvidenceMetric.Points => (double)l.Points / (double)l.Minutes * 40,
        EvidenceMetric.Rebounds => (double)l.Rebounds / (double)l.Minutes * 40,
        EvidenceMetric.Assists => (double)l.Assists / (double)l.Minutes * 40,
        EvidenceMetric.Steals => (double)l.Steals / (double)l.Minutes * 40,
        _ => (double)l.FgPct,
    };

    private static StatLine LatestOf(Athlete a) => a.Stats.OrderByDescending(s => s.Season, StringComparer.Ordinal).First();

    /// Independent LINQ expectation (does not call the production calculator).
    private static (int Peers, int Percentile) Expected(Athlete subject, EvidenceMetric metric, bool confirmedOnly)
    {
        Position[] group = subject.Position switch
        {
            Position.PG or Position.SG => [Position.PG, Position.SG],
            Position.SF or Position.PF => [Position.SF, Position.PF],
            _ => [Position.C],
        };
        var peers = Data.Athletes
            .Where(a => a != subject && a.Gender == subject.Gender && group.Contains(a.Position) && a.Stats.Count > 0)
            .Select(LatestOf)
            .Where(l => l.Games >= 10 && l.Minutes >= 15 && (!confirmedOnly || l.Source == StatSource.CoachConfirmed))
            .Select(l => Metric(l, metric)).ToList();
        var v = Metric(LatestOf(subject), metric);
        var pct = (peers.Count(x => x < v) + 0.5 * peers.Count(x => x == v)) / peers.Count * 100;
        return (peers.Count, (int)Math.Clamp(Math.Round(pct, MidpointRounding.AwayFromZero), 1, 99));
    }

    [Fact]
    public async Task Profile_stays_200_with_stats_table_and_evidence_panel()
    {
        var a = Female(Position.PG);

        var (status, html) = await Get(app.Client(), $"/p/{a.Slug}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(a.Stats.Count, Regex.Matches(html, "<tr[^>]*data-season=\"").Count);
        Assert.Contains("data-evidence", html);
        var keyStrip = html.IndexOf("class=\"key-stats\"", StringComparison.Ordinal);
        var evidence = html.IndexOf("data-evidence", StringComparison.Ordinal);
        var table = html.IndexOf("<table class=\"stats\"", StringComparison.Ordinal);
        Assert.True(keyStrip < evidence && evidence < table, "evidence sits between the key stats and the stats table");
    }

    [Fact]
    public async Task Percentile_bars_match_independent_computation_and_fill_is_two_units_per_percent()
    {
        var a = Female(Position.PG);
        var (_, html) = await Get(app.Client(), $"/p/{a.Slug}");

        foreach (var metric in Enum.GetValues<EvidenceMetric>())
        {
            var (peers, pct) = Expected(a, metric, confirmedOnly: false);
            var row = Regex.Match(html, $"<div class=\"bp-ev-row\"[^>]*data-metric=\"{metric}\"[^>]*>(.*?)</div>", RegexOptions.Singleline);
            Assert.True(row.Success, $"row for {metric}");
            Assert.Contains($"data-pct=\"{pct}\"", row.Value);
            Assert.Matches($"<rect[^>]*class=\"bp-ev-fill\"[^>]*width=\"{pct * 2}\"", row.Value);
            Assert.Matches("<svg[^>]*role=\"img\"[^>]*aria-label=\"[^\"]+\"", row.Value);
            Assert.Equal(peers, int.Parse(Regex.Match(html, "class=\"bp-ev-cohort\"[^>]*data-n=\"(\\d+)\"").Groups[1].Value));
        }
    }

    [Fact]
    public async Task Cohort_label_and_every_percentile_heading_say_demo_sample()
    {
        var (_, html) = await Get(app.Client(), $"/p/{Female(Position.C).Slug}");

        var evidence = Section(html);
        Assert.Matches("δείγμα επίδειξης: \\d+ σέντερ \\(φανταστικά\\)", evidence);
        var headings = Regex.Matches(evidence, "<h[23][^>]*>(.*?)</h[23]>", RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToList();
        Assert.Contains(headings, h => h.Contains("δείγμα επίδειξης"));
        Assert.All(headings.Where(h => h.Contains("Εκατοστημόρια") || h.Contains("Percentile")), h => Assert.Contains("δείγμα επίδειξης", h));
    }

    [Fact]
    public async Task Percentile_content_has_a_visually_hidden_table_fallback()
    {
        var (_, html) = await Get(app.Client(), $"/p/{Female(Position.PG).Slug}");

        var table = Regex.Match(Section(html), "<table class=\"bp-ev-sr\"[^>]*>(.*?)</table>", RegexOptions.Singleline);
        Assert.True(table.Success);
        Assert.Equal(5, Regex.Matches(table.Value, "<tr[^>]*data-metric=").Count);
    }

    [Fact]
    public async Task Small_cohort_shows_no_fill_and_the_not_enough_data_message()
    {
        var maleCenter = Data.Athletes.First(a => a.Gender == Gender.Male && a.Position == Position.C && a.Stats.Count > 0);
        var (peers, _) = Expected(maleCenter, EvidenceMetric.Points, false);
        Assert.True(peers < 20, "seed assumption: fewer than 20 male centers");

        var (_, html) = await Get(app.Client(), $"/p/{maleCenter.Slug}");

        var evidence = Section(html);
        Assert.DoesNotContain("bp-ev-fill", evidence);
        Assert.Contains($"Δεν υπάρχουν αρκετά δεδομένα (n={peers})", evidence);
    }

    [Fact]
    public async Task Confirmed_only_toggle_recomputes_the_cohort()
    {
        var a = Female(Position.PG);
        var all = Expected(a, EvidenceMetric.Points, false).Peers;
        var confirmed = Expected(a, EvidenceMetric.Points, true).Peers;
        Assert.NotEqual(all, confirmed);

        var (_, plain) = await Get(app.Client(), $"/p/{a.Slug}");
        var (_, only) = await Get(app.Client(), $"/p/{a.Slug}?confirmed=1");

        Assert.Equal(all, int.Parse(Regex.Match(plain, "class=\"bp-ev-cohort\"[^>]*data-n=\"(\\d+)\"").Groups[1].Value));
        Assert.Equal(confirmed, int.Parse(Regex.Match(only, "class=\"bp-ev-cohort\"[^>]*data-n=\"(\\d+)\"").Groups[1].Value));
        Assert.Contains("data-confirmed-only=\"true\"", only);
    }

    [Theory]
    [InlineData("el")]
    [InlineData("en")]
    public async Task Svg_numbers_in_the_profile_use_dots_in_every_culture(string culture)
    {
        var a = Female(Position.PG);

        var (_, html) = await Get(app.Client(culture), $"/p/{a.Slug}");

        var evidence = Section(html);
        Assert.Contains("<polyline", evidence);
        foreach (Match m in Regex.Matches(evidence, "points=\"([^\"]*)\""))
            foreach (var pair in m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                Assert.Matches(@"^-?\d+(\.\d+)?,-?\d+(\.\d+)?$", pair);
        foreach (Match m in Regex.Matches(evidence, "<(?:rect|circle|line|text)[^>]*>"))
            Assert.DoesNotMatch("\\s(?:x|y|cx|cy|r|x1|x2|y1|y2|width|height)=\"[^\"]*,", m.Value);
    }

    [Fact]
    public async Task Season_line_has_a_trend_and_a_text_table()
    {
        var a = Data.Athletes.First(x => x.Stats.Count >= 2 && x.Stats.All(s => s.Games >= 8));

        var (_, html) = await Get(app.Client(), $"/p/{a.Slug}");

        var evidence = Section(html);
        Assert.Matches("class=\"bp-ev-trend\"[^>]*data-trend=\"(Up|Down|Flat)\"", evidence);
        Assert.Matches("(▲|▼|▬)", evidence);
        Assert.Matches("<svg[^>]*class=\"bp-ev-line\"[^>]*role=\"img\"", evidence);
        Assert.Equal(a.Stats.Count, Regex.Matches(evidence, "<tr[^>]*data-line-season=").Count);
    }

    [Fact]
    public async Task Single_season_athlete_shows_na_trend()
    {
        var a = Data.Athletes.First(x => x.Stats.Count == 1);

        var (_, html) = await Get(app.Client(), $"/p/{a.Slug}");

        Assert.Matches("class=\"bp-ev-trend\"[^>]*data-trend=\"NotAvailable\"[^>]*>[^<]*n/a", Section(html));
    }

    [Fact]
    public async Task Badge_text_names_its_rule_in_greek()
    {
        var a = Data.Athletes.First(x => x.Stats.Count(s => s.Source == StatSource.CoachConfirmed) == 2);

        var (_, html) = await Get(app.Client(), $"/p/{a.Slug}");

        var badges = Section(html);
        Assert.Matches("data-rule=\"coach-confirmed\"[^>]*>[^<]*Επιβεβαιωμένο από προπονητή: 2 σεζόν", badges);
    }

    [Fact]
    public async Task Compare_link_is_only_for_coaches()
    {
        var a = Female(Position.PG);
        var coach = app.Client();
        await SignIn(coach, "coach");

        var (_, coachHtml) = await Get(coach, $"/p/{a.Slug}");
        var (_, anonHtml) = await Get(app.Client(), $"/p/{a.Slug}");

        Assert.Contains($"href=\"/compare?a={a.Slug}\"", coachHtml);
        Assert.Contains("Σύγκριση με…", coachHtml);
        Assert.DoesNotContain("/compare", anonHtml);
    }

    // ---------- ICohortRank ----------

    [Fact]
    public async Task Cohort_rank_matches_independent_ranking()
    {
        var a = Female(Position.PG);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
        var id = db.Athletes.Where(x => x.Slug == a.Slug).Select(x => x.Id).Single();
        var rank = scope.ServiceProvider.GetRequiredService<ICohortRank>();

        var result = await rank.RankInCohort(id, EvidenceMetric.Points);

        var (peers, _) = Expected(a, EvidenceMetric.Points, false);
        var v = Metric(LatestOf(a), EvidenceMetric.Points);
        var better = Data.Athletes
            .Where(x => x != a && x.Gender == a.Gender && (x.Position is Position.PG or Position.SG) && x.Stats.Count > 0
                && LatestOf(x) is { Games: >= 10, Minutes: >= 15 })
            .Count(x => Metric(LatestOf(x), EvidenceMetric.Points) > v);
        Assert.NotNull(result);
        Assert.Equal(better + 1, result!.Value.Rank);
        Assert.Equal(peers + 1, result.Value.N);
    }

    [Fact]
    public async Task Cohort_rank_is_null_for_a_too_small_cohort_or_unknown_athlete()
    {
        var maleCenter = Data.Athletes.First(a => a.Gender == Gender.Male && a.Position == Position.C && a.Stats.Count > 0);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
        var id = db.Athletes.Where(x => x.Slug == maleCenter.Slug).Select(x => x.Id).Single();
        var rank = scope.ServiceProvider.GetRequiredService<ICohortRank>();

        Assert.Null(await rank.RankInCohort(id, EvidenceMetric.Points));
        Assert.Null(await rank.RankInCohort(-1, EvidenceMetric.Points));
    }

    // ---------- compare ----------

    [Fact]
    public async Task Compare_anonymous_is_sent_to_demo_login()
    {
        var response = await app.Client().GetAsync($"/compare?a={Female(Position.PG).Slug}&b={Female(Position.SG).Slug}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/demo", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Compare_athlete_is_denied_with_403()
    {
        var client = app.Client();
        await SignIn(client, "athlete");

        var redirect = await client.GetAsync($"/compare?a={Female(Position.PG).Slug}&b={Female(Position.SG).Slug}");
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        var denied = await client.GetAsync(redirect.Headers.Location);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private async Task<string> CompareHtml(Athlete a, Athlete b, string? culture = null)
    {
        var coach = app.Client(culture);
        await SignIn(coach, "coach");
        var (status, html) = await Get(coach, $"/compare?a={a.Slug}&b={b.Slug}");
        Assert.Equal(HttpStatusCode.OK, status);
        return html;
    }

    private static Athlete Other(Position p, Athlete not) => Data.Athletes.First(a => a.Gender == Gender.Female && a.Position == p && a != not && a.Stats.Count > 0);

    [Fact]
    public async Task Compare_table_numbers_equal_svg_labels()
    {
        var a = Female(Position.PG);
        var b = Other(Position.SG, a);

        var html = await CompareHtml(a, b);

        foreach (var metric in Enum.GetValues<EvidenceMetric>())
            foreach (var who in new[] { "a", "b" })
            {
                var svg = Regex.Match(html, $"<text[^>]*data-athlete=\"{who}\"[^>]*data-metric=\"{metric}\"[^>]*>([^<]*)</text>").Groups[1].Value.Trim();
                var cell = Regex.Match(html, $"<tr[^>]*data-metric=\"{metric}\"[^>]*>.*?<td[^>]*data-athlete=\"{who}\"[^>]*data-pct[^>]*>([^<]*)</td>", RegexOptions.Singleline).Groups[1].Value.Trim();
                Assert.NotEqual("", svg);
                Assert.Equal(svg, cell);
            }
        var expectedA = Expected(a, EvidenceMetric.Points, false).Percentile;
        Assert.Matches($"<text[^>]*data-athlete=\"a\"[^>]*data-metric=\"Points\"[^>]*>\\s*{expectedA}\\s*</text>", html);
    }

    [Fact]
    public async Task Compare_markers_differ_by_shape_and_dash_not_colour_alone()
    {
        var html = await CompareHtml(Female(Position.PG), Other(Position.SG, Female(Position.PG)));

        Assert.Contains("data-marker=\"circle\"", html);
        Assert.Contains("data-marker=\"square\"", html);
        Assert.Matches("<circle[^>]*data-marker=\"circle\"", html);
        Assert.Matches("<rect[^>]*data-marker=\"square\"", html);
        Assert.Matches("class=\"bp-ev-cmp-fill\"[^>]*data-athlete=\"b\"[^>]*stroke-dasharray=\"[\\d. ]+\"", html);
        Assert.DoesNotMatch("class=\"bp-ev-cmp-fill\"[^>]*data-athlete=\"a\"[^>]*stroke-dasharray", html);
    }

    [Fact]
    public async Task Compare_warns_when_position_groups_differ_and_not_otherwise()
    {
        var pg = Female(Position.PG);
        var center = Female(Position.C);

        var differ = await CompareHtml(pg, center);
        var same = await CompareHtml(pg, Other(Position.SG, pg));

        Assert.Contains("class=\"bp-ev-warning\"", differ);
        Assert.DoesNotContain("class=\"bp-ev-warning\"", same);
    }

    [Fact]
    public async Task Compare_headings_say_demo_sample_and_unknown_slug_is_404()
    {
        var a = Female(Position.PG);
        var b = Other(Position.SG, a);
        var html = await CompareHtml(a, b);
        Assert.Contains("δείγμα επίδειξης", Regex.Match(html, "<h2[^>]*>(.*?)</h2>", RegexOptions.Singleline).Value);

        var coach = app.Client();
        await SignIn(coach, "coach");
        var (status, _) = await Get(coach, $"/compare?a={a.Slug}&b=does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Theory]
    [InlineData("el")]
    [InlineData("en")]
    public async Task Compare_svg_numbers_use_dots(string culture)
    {
        var a = Female(Position.PG);
        var html = await CompareHtml(a, Other(Position.SG, a), culture);

        foreach (Match m in Regex.Matches(html, "<(?:rect|circle|line)[^>]*class=\"bp-ev-[^>]*>"))
            Assert.DoesNotMatch("\\s(?:x|y|cx|cy|r|x1|x2|y1|y2|width|height)=\"[^\"]*,", m.Value);
    }

    [Fact]
    public async Task Compare_with_only_one_athlete_offers_a_chooser_from_the_shortlist()
    {
        var a = Female(Position.PG);
        var coach = app.Client();
        await SignIn(coach, "coach");

        var (status, html) = await Get(coach, $"/compare?a={a.Slug}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Matches("<form[^>]*method=\"get\"[^>]*action=\"/compare\"", html);
        Assert.Contains($"name=\"a\" value=\"{a.Slug}\"", html);
    }

    [Fact]
    public async Task Shortlist_has_a_compare_form_that_gets_compare()
    {
        var coach = app.Client();
        await SignIn(coach, "coach");

        var (_, html) = await Get(coach, "/shortlist");

        var form = Regex.Match(html, "<form[^>]*data-compare-launcher[^>]*>(.*?)</form>", RegexOptions.Singleline);
        Assert.True(form.Success, "compare launcher form on /shortlist");
        Assert.Matches("method=\"get\"", form.Value);
        Assert.Matches("action=\"/compare\"", form.Value);
        Assert.Contains("name=\"a\"", form.Value);
        Assert.Contains("name=\"b\"", form.Value);
        Assert.Contains("Σύγκριση", form.Value);
    }

    // ---------- helpers ----------

    private static string Section(string html)
    {
        var start = html.IndexOf("data-evidence", StringComparison.Ordinal);
        Assert.True(start >= 0, "evidence panel missing");
        var end = html.IndexOf("<table class=\"stats\"", start, StringComparison.Ordinal);
        return html[start..(end < 0 ? html.Length : end)];
    }

    private static async Task<(HttpStatusCode, string)> Get(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        return (response.StatusCode, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    private static async Task<string> Html(HttpClient client, string path) => (await Get(client, path)).Item2;

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

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ballpass.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Ballpass.slnx not found above the test binaries");
    }
}
