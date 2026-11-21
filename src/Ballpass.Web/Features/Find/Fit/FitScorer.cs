using Ballpass.Web.Data;

namespace Ballpass.Web.Features.Find.Fit;

/// <summary>
/// Pure, deterministic "fit" scoring: a weighted match between a coach's need and each athlete, on per-game stats.
/// Not an AI model and not a rating of the athlete. Weights are defaults (see <see cref="FitOptions"/>), never learned.
///
/// Rules: position and coach-confirmed-only are hard filters (hide + count). Every other requested criterion is soft, scored 0..1.
/// Weights renormalise over the requested criteria; a criterion with no usable data for an athlete (stat line missing or
/// under MinGames games, no club league) is "missing": the score renormalises over what is available and the athlete is tagged partial.
/// Ties break on coverage, best stat source, recency, slug.
/// </summary>
public class FitScorer(FitOptions options)
{
    public FitRanking Rank(FitNeed need, IReadOnlyList<FitCandidate> cohort)
    {
        var visible = cohort.Where(c => Passes(need, c)).ToList();
        var requested = Requested(need);
        var requestedWeight = requested.Sum(x => x.Weight);
        var statValues = StatCohort(need.Priority, cohort);

        var results = visible
            .Select(c => Score(need, c, requested, requestedWeight, statValues))
            .OrderByDescending(r => r.FitPercent ?? -1)
            .ThenByDescending(r => Math.Round(r.Coverage, 6))
            .ThenByDescending(r => SourceRank(r.Candidate.Stat))
            .ThenByDescending(r => r.Candidate.Stat?.Season ?? "", StringComparer.Ordinal)
            .ThenBy(r => r.Candidate.Slug, StringComparer.Ordinal)
            .ToList();

        return new FitRanking(results, cohort.Count - visible.Count);
    }

    private static bool Passes(FitNeed need, FitCandidate c)
    {
        if (need.Positions.Count > 0 && !need.Positions.Contains(c.Position)) return false;
        if (need.ConfirmedOnly && c.Stat is null or { Source: StatSource.SelfReported }) return false;
        return true;
    }

    private static int SourceRank(FitStat? stat) => stat is null ? 0 : (int)stat.Source + 1;

    private List<(FitCriterion Criterion, double Weight)> Requested(FitNeed need)
    {
        var w = options.Weights;
        var list = new List<(FitCriterion, double)>();
        if (need.WantsHeight) list.Add((FitCriterion.Height, w.Height));
        if (need.WantsAge) list.Add((FitCriterion.Age, w.Age));
        if (!string.IsNullOrEmpty(need.Region)) list.Add((FitCriterion.Region, w.Region));
        if (need.League is not null) list.Add((FitCriterion.League, w.League));
        if (need.Priority != FitPriority.None) list.Add((FitCriterion.Stat, w.Stat));
        return list;
    }

    private static double? PerGame(FitPriority priority, FitStat stat) => priority switch
    {
        FitPriority.Rebounding => (double)stat.Rebounds,
        FitPriority.Scoring => (double)stat.Points,
        FitPriority.Playmaking => (double)stat.Assists,
        _ => null,
    };

    /// <summary>Per-game values of the requested stat among same-position athletes with enough games (the "demo cohort").</summary>
    private Dictionary<Position, List<double>> StatCohort(FitPriority priority, IReadOnlyList<FitCandidate> cohort)
    {
        var map = new Dictionary<Position, List<double>>();
        if (priority == FitPriority.None) return map;
        foreach (var c in cohort)
        {
            if (c.Stat is null || c.Stat.Games < options.MinGames) continue;
            if (!map.TryGetValue(c.Position, out var list)) map[c.Position] = list = [];
            list.Add(PerGame(priority, c.Stat)!.Value);
        }
        return map;
    }

    private FitResult Score(FitNeed need, FitCandidate c, List<(FitCriterion Criterion, double Weight)> requested, double requestedWeight,
        Dictionary<Position, List<double>> statValues)
    {
        double sum = 0, available = 0;
        var missing = new List<FitCriterion>();
        var scored = new List<(FitReason Reason, double Contribution, double Weight)>();

        foreach (var (criterion, weight) in requested)
        {
            var s = ScoreCriterion(need, c, criterion, statValues);
            if (s is null) { missing.Add(criterion); continue; }
            var (score, reason) = s.Value;
            sum += weight * score;
            available += weight;
            scored.Add((reason, weight * score, weight));
        }

        int? percent = available > 0 ? (int)Math.Round(sum / available * 100, MidpointRounding.AwayFromZero) : null;
        var coverage = requestedWeight > 0 ? available / requestedWeight : 1.0;
        var reasons = scored
            .OrderByDescending(x => x.Contribution).ThenByDescending(x => x.Weight).ThenBy(x => x.Reason.Criterion)
            .Take(3).Select(x => x.Reason).ToList();

        return new FitResult(c, percent, coverage, reasons, missing);
    }

    private (double Score, FitReason Reason)? ScoreCriterion(FitNeed need, FitCandidate c, FitCriterion criterion,
        Dictionary<Position, List<double>> statValues)
    {
        switch (criterion)
        {
            case FitCriterion.Height:
            {
                var s = RangeScore(c.HeightCm, need.MinHeight, need.MaxHeight, options.HeightFalloffCm);
                return (s, new FitReason(criterion, Profile(s), StatSource.SelfReported, c.HeightCm, null, null, null));
            }
            case FitCriterion.Age:
            {
                var s = RangeScore(c.Age, need.MinAge, need.MaxAge, options.AgeFalloffYears);
                return (s, new FitReason(criterion, Profile(s), StatSource.SelfReported, c.Age, null, null, null));
            }
            case FitCriterion.Region:
            {
                var s = string.Equals(c.Region, need.Region, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
                return (s, new FitReason(criterion, Profile(s), StatSource.SelfReported, 0, c.Region, null, null));
            }
            case FitCriterion.League:
            {
                if (c.League is null) return null;
                var distance = Math.Abs((int)c.League.Value - (int)need.League!.Value);
                var s = distance == 0 ? 1.0 : distance == 1 ? 0.5 : 0.0;
                return (s, new FitReason(criterion, Profile(s), StatSource.SelfReported, 0, c.League.Value.ToString(), null, null));
            }
            default:
            {
                if (c.Stat is null || c.Stat.Games < options.MinGames) return null;
                var value = PerGame(need.Priority, c.Stat)!.Value;
                var cohort = statValues[c.Position];
                var below = cohort.Count(v => v < value);
                var equal = cohort.Count(v => v == value);
                var pct = (below + 0.5 * equal) / cohort.Count;
                var state = pct >= 0.8 ? ReasonState.Met : pct < 0.2 ? ReasonState.Missed : ReasonState.Partial;
                var top = (int)Math.Max(1, Math.Round(100 * (1 - pct), MidpointRounding.AwayFromZero));
                return (pct, new FitReason(criterion, state, c.Stat.Source, value, null, top, c.Position));
            }
        }
    }

    private static ReasonState Profile(double score) => score >= 1 ? ReasonState.Met : score <= 0 ? ReasonState.Missed : ReasonState.Partial;

    /// <summary>1 inside [min,max]; falls linearly to 0 at <paramref name="falloff"/> units outside.</summary>
    private static double RangeScore(double value, int? min, int? max, double falloff)
    {
        var distance = 0.0;
        if (min is { } lo && value < lo) distance = lo - value;
        else if (max is { } hi && value > hi) distance = value - hi;
        return falloff <= 0 ? (distance == 0 ? 1 : 0) : Math.Max(0, 1 - distance / falloff);
    }
}

public static class TrustBar
{
    /// <summary>Share of each stat source among the given results (largest-remainder rounding, always sums to 100). Null when no source is known.</summary>
    public static TrustShares? Compute(IEnumerable<StatSource?> sources)
    {
        var counts = new int[3];
        foreach (var s in sources) if (s is { } v) counts[(int)v]++;
        var total = counts.Sum();
        if (total == 0) return null;

        var shares = counts.Select(c => c * 100 / total).ToArray();
        var remainders = counts.Select((c, i) => (Index: i, Remainder: c * 100 % total))
            .OrderByDescending(x => x.Remainder).ThenBy(x => x.Index).ToList();
        var leftover = 100 - shares.Sum();
        for (var k = 0; k < leftover; k++) shares[remainders[k].Index]++;
        return new TrustShares(shares[(int)StatSource.SelfReported], shares[(int)StatSource.CoachConfirmed], shares[(int)StatSource.LeagueVerifiedMock]);
    }
}
