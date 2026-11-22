using Ballpass.Web.Data;
using Ballpass.Web.Features.Evidence;
using Ballpass.Web.Features.Find.Fit;

namespace Ballpass.Web.Tests;

// Spec: openspec/changes/wow-find/specs/find/spec.md (Fit scoring, hard filters, trust bar). Pure unit tests, no DB.
public class FitScorerTests
{
    private static FitStat Stat(decimal reb = 0, decimal pts = 0, decimal ast = 0, int games = 12,
        StatSource src = StatSource.SelfReported, string season = "2025-26") => new(season, games, pts, reb, ast, src);

    // pct = the whole-percent cohort percentile Evidence computed (passed in, the scorer never ranks); null = the cohort rule gave none.
    private static FitCandidate Cand(string slug, Position pos, int h, int age = 22, string region = "Attica",
        League? league = null, FitStat? stat = null, int? pct = null) =>
        new(slug, pos, h, age, region, league, stat, pct is { } p ? new FitStatRank(stat is null ? 0 : (double)stat.Rebounds * 2, p) : null);

    private static FitScorer Scorer(Action<FitOptions>? configure = null)
    {
        var o = new FitOptions();
        configure?.Invoke(o);
        return new FitScorer(o);
    }

    private static string Order(FitRanking r) => string.Join(",", r.Results.Select(x => x.Candidate.Slug));

    // ---------- worked example 1: renormalised height + rebounds, source tie-break ----------

    [Fact]
    public void Fixture1_center_need_scores_and_orders()
    {
        var need = new FitNeed { Positions = [Position.C], MinHeight = 185, Priority = FitPriority.Rebounding };
        FitCandidate[] cohort =
        [
            Cand("a", Position.C, 191, stat: Stat(reb: 9.0m), pct: 90),
            Cand("b", Position.C, 183, stat: Stat(reb: 7.0m, src: StatSource.CoachConfirmed), pct: 70),
            Cand("c", Position.C, 180, stat: Stat(reb: 3.0m), pct: 30),
            Cand("d", Position.C, 186, stat: Stat(reb: 5.0m), pct: 50),
            Cand("e", Position.C, 170, stat: Stat(reb: 2.0m), pct: 10),
        ];

        var r = Scorer().Rank(need, cohort);

        Assert.Equal(0, r.Hidden);
        // b and d both score 75: the better stat source (coach-confirmed) puts b first.
        Assert.Equal("a,b,d,c,e", Order(r));
        Assert.Equal([95, 75, 75, 40, 5], r.Results.Select(x => x.FitPercent!.Value).ToArray());
        Assert.All(r.Results, x => Assert.False(x.Partial));
    }

    // ---------- worked example 2: missing data renormalises; coverage beats best source ----------

    [Fact]
    public void Fixture2_partial_data_ties_on_score_and_loses_on_coverage()
    {
        var need = new FitNeed { Positions = [Position.C], MinHeight = 185, Priority = FitPriority.Rebounding };
        FitCandidate[] cohort =
        [
            Cand("q", Position.C, 182, stat: Stat(reb: 8.0m), pct: 75),
            Cand("r", Position.C, 180, stat: Stat(reb: 4.0m), pct: 25),
            // Only 5 games: rebounds count as missing. Better source than q, still loses on coverage.
            Cand("p", Position.C, 181, stat: Stat(reb: 5.0m, games: 5, src: StatSource.LeagueVerifiedMock)),
        ];

        var r = Scorer(o => o.HeightFalloffCm = 20).Rank(need, cohort);

        Assert.Equal("q,p,r", Order(r));
        Assert.Equal([80, 80, 50], r.Results.Select(x => x.FitPercent!.Value).ToArray());
        var p = r.Results[1];
        Assert.True(p.Partial);
        Assert.Equal([FitCriterion.Stat], p.Missing);
        Assert.Equal(0.5, p.Coverage, 3);
        Assert.Equal(1.0, r.Results[0].Coverage, 3);
    }

    // ---------- worked example 3: hard filters + several soft criteria ----------

    [Fact]
    public void Fixture3_hard_filters_hide_and_count_and_soft_criteria_renormalise()
    {
        var need = new FitNeed
        {
            Positions = [Position.PG, Position.SG],
            Region = "Attica", MinAge = 20, MaxAge = 24,
            Priority = FitPriority.Playmaking,
            ConfirmedOnly = true,
        };
        FitCandidate[] cohort =
        [
            Cand("x", Position.PG, 175, age: 22, region: "Attica", stat: Stat(ast: 6.0m, src: StatSource.CoachConfirmed), pct: 88),
            Cand("y", Position.PG, 170, age: 26, region: "Crete", stat: Stat(ast: 4.0m, src: StatSource.CoachConfirmed), pct: 63),
            Cand("v1", Position.PG, 170, age: 21, region: "Attica", stat: Stat(ast: 3.0m)),   // self-reported: hidden
            Cand("v2", Position.PG, 170, age: 21, region: "Attica", stat: Stat(ast: 2.0m)),   // self-reported: hidden
            Cand("z", Position.SG, 178, age: 30, region: "Attica", stat: Stat(ast: 3.0m, src: StatSource.LeagueVerifiedMock), pct: 50),
            Cand("u", Position.C, 195, age: 22, region: "Attica", stat: Stat(ast: 1.0m, src: StatSource.CoachConfirmed)), // wrong position: hidden
        ];

        var r = Scorer().Rank(need, cohort);

        Assert.Equal(3, r.Hidden);
        Assert.Equal("x,z,y", Order(r));
        Assert.Equal([94, 50, 44], r.Results.Select(x => x.FitPercent!.Value).ToArray());
    }

    // ---------- renormalisation, missing data, config ----------

    [Fact]
    public void Weights_renormalise_over_requested_criteria_only()
    {
        var need = new FitNeed { MinHeight = 180 };
        var c = Cand("a", Position.C, 190, stat: Stat(reb: 1));

        // Stat/age/league weights are irrelevant when not requested: height alone decides.
        var heavy = Scorer(o => { o.Weights.Stat = 0.9; o.Weights.Age = 0.9; });
        Assert.Equal(100, heavy.Rank(need, [c]).Results[0].FitPercent);

        // Height .30 and region .15 requested, region wrong: 0.30 / 0.45 = 67.
        var both = new FitNeed { MinHeight = 180, Region = "Crete" };
        Assert.Equal(67, Scorer().Rank(both, [c]).Results[0].FitPercent);
    }

    [Fact]
    public void Weights_come_from_options()
    {
        var need = new FitNeed { MinHeight = 180, Region = "Crete" };
        var c = Cand("a", Position.C, 190);
        var r = Scorer(o => { o.Weights.Height = 0.50; o.Weights.Region = 0.50; }).Rank(need, [c]);
        Assert.Equal(50, r.Results[0].FitPercent);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public void Fewer_than_ten_games_is_missing(int games, bool missing)
    {
        var need = new FitNeed { MinHeight = 180, Priority = FitPriority.Scoring };
        var r = Scorer().Rank(need, [Cand("a", Position.SG, 185, stat: Stat(pts: 10, games: games), pct: 60)]).Results[0];

        Assert.Equal(missing, r.Missing.Contains(FitCriterion.Stat));
        Assert.Equal(missing, r.Partial);
    }

    [Fact]
    public void No_stat_line_at_all_is_missing_not_zero()
    {
        var need = new FitNeed { MinHeight = 180, Priority = FitPriority.Rebounding };
        var r = Scorer().Rank(need, [Cand("a", Position.C, 190)]).Results[0];

        Assert.Equal(100, r.FitPercent);
        Assert.Equal([FitCriterion.Stat], r.Missing);
    }

    [Fact]
    public void Without_any_soft_criterion_there_is_no_percentage()
    {
        var need = new FitNeed { Positions = [Position.C] };
        var r = Scorer().Rank(need, [Cand("a", Position.C, 190), Cand("b", Position.PG, 170)]);

        Assert.Equal(1, r.Hidden);
        Assert.Null(r.Results[0].FitPercent);
    }

    [Theory]
    [InlineData(League.A1, 100)]
    [InlineData(League.A2, 50)]
    [InlineData(League.B, 0)]
    public void League_scores_by_distance_between_levels(League playsIn, int expected)
    {
        var need = new FitNeed { League = League.A1 };
        var r = Scorer().Rank(need, [Cand("a", Position.PG, 170, league: playsIn)]);
        Assert.Equal(expected, r.Results[0].FitPercent);
    }

    // ---------- tie-breaks ----------

    [Fact]
    public void Ties_break_on_source_then_recency_then_slug()
    {
        var need = new FitNeed { MinHeight = 180 };
        FitCandidate[] cohort =
        [
            Cand("zz-old", Position.C, 190, stat: Stat(src: StatSource.SelfReported, season: "2024-25")),
            Cand("zz-new", Position.C, 190, stat: Stat(src: StatSource.SelfReported, season: "2025-26")),
            Cand("bb", Position.C, 190, stat: Stat(src: StatSource.SelfReported, season: "2025-26")),
            Cand("aa-verified", Position.C, 190, stat: Stat(src: StatSource.LeagueVerifiedMock, season: "2023-24")),
            Cand("none", Position.C, 190),
        ];

        var r = Scorer().Rank(need, cohort);

        // verified source first, then recency (2025-26 before 2024-25), then slug (bb < zz-new), no stat line last.
        Assert.Equal("aa-verified,bb,zz-new,zz-old,none", Order(r));
    }

    // ---------- reasons ----------

    [Fact]
    public void Every_reason_carries_a_source_and_there_are_two_or_three()
    {
        var need = new FitNeed { Positions = [Position.C], MinHeight = 185, Region = "Attica", MinAge = 20, MaxAge = 24, Priority = FitPriority.Rebounding };
        FitCandidate[] cohort =
        [
            Cand("a", Position.C, 191, stat: Stat(reb: 9.0m, src: StatSource.CoachConfirmed), pct: 83),
            Cand("b", Position.C, 183, stat: Stat(reb: 7.0m), pct: 50),
            Cand("c", Position.C, 180, stat: Stat(reb: 3.0m), pct: 17),
        ];

        var r = Scorer().Rank(need, cohort);

        Assert.All(r.Results, x =>
        {
            Assert.InRange(x.Reasons.Count, 2, 3);
            Assert.All(x.Reasons, reason => Assert.True(Enum.IsDefined(reason.Source)));
        });
        var a = r.Results.Single(x => x.Candidate.Slug == "a");
        var stat = a.Reasons.Single(x => x.Criterion == FitCriterion.Stat);
        Assert.Equal(StatSource.CoachConfirmed, stat.Source);   // stat reasons carry the stat line's source
        Assert.Equal(18.0, stat.Actual, 3);                      // the per-40 value Evidence computed, not a per-game value
        Assert.Equal(PositionGroup.Center, stat.CohortGroup);
        Assert.Equal(17, stat.TopPercent);                       // 100 - profile percentile (83)
        var height = a.Reasons.Single(x => x.Criterion == FitCriterion.Height);
        Assert.Equal(StatSource.SelfReported, height.Source);    // profile fields are self-declared
        Assert.Equal(ReasonState.Met, height.State);
    }

    [Fact]
    public void Missed_criteria_are_reported_as_missed()
    {
        var need = new FitNeed { MinHeight = 190, Region = "Crete" };
        var r = Scorer().Rank(need, [Cand("a", Position.PG, 160, region: "Attica")]).Results[0];

        Assert.All(r.Reasons, x => Assert.Equal(ReasonState.Missed, x.State));
    }

    // ---------- cohort rule (audit finding 2): the scorer only consumes the percentile Evidence computed ----------

    [Fact]
    public void Stat_without_a_cohort_percentile_is_missing_and_shows_no_top_reason()
    {
        var need = new FitNeed { MinHeight = 180, Priority = FitPriority.Rebounding };
        // 12 games, plenty of rebounds, but Evidence gave no percentile (fewer than 20 peers / minutes floor).
        var r = Scorer().Rank(need, [Cand("a", Position.C, 190, stat: Stat(reb: 9.0m))]).Results[0];

        Assert.Equal([FitCriterion.Stat], r.Missing);
        Assert.True(r.Partial);
        Assert.Equal(100, r.FitPercent);
        Assert.DoesNotContain(r.Reasons, x => x.Criterion == FitCriterion.Stat);
        Assert.All(r.Reasons, x => Assert.Null(x.TopPercent));
    }

    [Theory]
    [InlineData(1, 99)]
    [InlineData(50, 50)]
    [InlineData(83, 17)]
    [InlineData(99, 1)]
    public void Top_percent_is_100_minus_the_profile_percentile(int percentile, int top)
    {
        var need = new FitNeed { Priority = FitPriority.Rebounding };
        var r = Scorer().Rank(need, [Cand("a", Position.PF, 190, stat: Stat(reb: 5m), pct: percentile)]).Results[0];

        Assert.Equal(top, r.Reasons.Single().TopPercent);
        Assert.Equal(percentile, r.FitPercent);
        Assert.Equal(PositionGroup.Forward, r.Reasons.Single().CohortGroup);
    }

    // ---------- trust bar ----------

    [Fact]
    public void Trust_bar_shares_sum_to_100_and_match_counts()
    {
        var t = TrustBar.Compute([StatSource.SelfReported, StatSource.SelfReported, StatSource.SelfReported, StatSource.CoachConfirmed])!;
        Assert.Equal(new TrustShares(75, 25, 0), t);

        var thirds = TrustBar.Compute([StatSource.SelfReported, StatSource.CoachConfirmed, StatSource.LeagueVerifiedMock])!;
        Assert.Equal(100, thirds.SelfReported + thirds.CoachConfirmed + thirds.LeagueVerified);
        Assert.Equal(new TrustShares(34, 33, 33), thirds);
    }

    [Fact]
    public void Trust_bar_ignores_missing_sources_and_is_null_when_empty()
    {
        Assert.Null(TrustBar.Compute([]));
        Assert.Null(TrustBar.Compute([null, null]));
        Assert.Equal(new TrustShares(0, 100, 0), TrustBar.Compute([null, StatSource.CoachConfirmed]));
    }

    [Fact]
    public void Trust_bar_always_sums_to_100()
    {
        var rnd = new Random(7);
        for (var n = 1; n < 60; n++)
        {
            var sources = Enumerable.Range(0, n).Select(_ => (StatSource?)(StatSource)rnd.Next(3)).ToList();
            var t = TrustBar.Compute(sources)!;
            Assert.Equal(100, t.SelfReported + t.CoachConfirmed + t.LeagueVerified);
        }
    }
}
