using Ballpass.Web.Features.Evidence;

namespace Ballpass.Web.Features.Share;

/// <param name="Percentile">Whole percent 1-99, derived from the Evidence rank (ties counted as below).</param>
/// <param name="TopPercent">"Top x%" = 100 - Percentile, at least 1.</param>
/// <param name="N">Cohort size including the athlete (from Evidence).</param>
public record CohortHighlight(EvidenceMetric Metric, int Percentile, int TopPercent, int N);

/// <summary>
/// Season Wrapped "where she stands out" slide. Uses Evidence's <see cref="ICohortRank"/>, so the cohort (same gender and
/// position group, games and minutes floors, per-40 values) is the one the profile shows.
/// </summary>
public static class WrappedCohort
{
    /// <summary>A "stands out" slide only makes sense when she is at least above the middle of the cohort.</summary>
    public const double MinPercentile = 50;

    public static int PercentileOf(int rank, int n) =>
        n <= 1 ? 1 : (int)Math.Clamp(Math.Round(100.0 * (n - rank) / (n - 1), MidpointRounding.AwayFromZero), 1, 99);

    /// <summary>Best metric by percentile, or null when Evidence has no cohort or nothing reaches <see cref="MinPercentile"/>.</summary>
    public static async Task<CohortHighlight?> BestAsync(ICohortRank ranks, int athleteId)
    {
        CohortHighlight? best = null;
        foreach (var metric in Enum.GetValues<EvidenceMetric>())
        {
            if (await ranks.RankInCohort(athleteId, metric) is not { } r) continue;
            var pct = PercentileOf(r.Rank, r.N);
            if (best is null || pct > best.Percentile)
                best = new CohortHighlight(metric, pct, Math.Max(1, 100 - pct), r.N);
        }
        return best is { } b && b.Percentile >= MinPercentile ? b : null;
    }
}
