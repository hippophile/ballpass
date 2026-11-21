using Ballpass.Web.Data;

namespace Ballpass.Web.Features.Share;

public enum CohortMetric { Points, Rebounds, Assists, Steals, Blocks }

public record CohortRow(Position Position, decimal Points, decimal Rebounds, decimal Assists, decimal Steals, decimal Blocks)
{
    public decimal Get(CohortMetric m) => m switch
    {
        CohortMetric.Points => Points,
        CohortMetric.Rebounds => Rebounds,
        CohortMetric.Assists => Assists,
        CohortMetric.Steals => Steals,
        _ => Blocks,
    };
}

/// <param name="Percentile">Mid-rank percentile 0-100 (below + half of equal, over n).</param>
/// <param name="TopPercent">"Top x%" = 100 - Percentile, at least 1.</param>
/// <param name="N">Cohort size including the athlete.</param>
public record CohortHighlight(CohortMetric Metric, int Percentile, int TopPercent, int N);

/// <summary>
/// Local cohort ranking for the Season Wrapped "where she stands out" slide.
/// TODO(merge): the Evidence package's ICohortRank covers the same ground; replace this helper with it at merge.
/// </summary>
public static class SeasonCohort
{
    public const int MinCohort = 20;
    /// <summary>A "stands out" slide only makes sense when she is at least above the middle of the cohort.</summary>
    public const double MinPercentile = 50;

    private static readonly CohortMetric[] Metrics = Enum.GetValues<CohortMetric>();

    private static int Group(Position p) => p switch
    {
        Position.PG or Position.SG => 0,   // guards
        Position.SF or Position.PF => 1,   // forwards
        _ => 2,                            // centres
    };

    /// <summary>
    /// <paramref name="sameGenderSameSeason"/> = every athlete of the same gender with a line in the same season
    /// (including <paramref name="mine"/>). Only the same position group counts. Null when n &lt; 20 or nothing stands out.
    /// </summary>
    public static CohortHighlight? Best(CohortRow mine, IReadOnlyList<CohortRow> sameGenderSameSeason)
    {
        var peers = sameGenderSameSeason.Where(r => Group(r.Position) == Group(mine.Position)).ToList();
        if (peers.Count < MinCohort) return null;

        CohortMetric? best = null;
        var bestPct = -1.0;
        foreach (var metric in Metrics)
        {
            var value = mine.Get(metric);
            var below = peers.Count(p => p.Get(metric) < value);
            var equal = peers.Count(p => p.Get(metric) == value);
            var pct = 100.0 * (below + 0.5 * equal) / peers.Count;
            if (pct > bestPct) { bestPct = pct; best = metric; }
        }
        if (best is null || bestPct < MinPercentile) return null;

        var percentile = (int)Math.Round(bestPct, MidpointRounding.AwayFromZero);
        return new CohortHighlight(best.Value, percentile, Math.Max(1, 100 - percentile), peers.Count);
    }
}
