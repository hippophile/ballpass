using Ballpass.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ballpass.Web.Features.Evidence;

/// <summary>Config section Evidence. MinMinutesPerGame is our estimate, not a league rule.</summary>
public class EvidenceOptions
{
    public decimal MinMinutesPerGame { get; set; } = CohortCalculator.DefaultMinMinutes;
    /// <summary>Minimum qualifying peers for a percentile (config Evidence:MinPeers). Our estimate.</summary>
    public int MinPeers { get; set; } = CohortCalculator.DefaultMinPeers;
}

/// <summary>Public contract for the Share package: rank of an athlete inside the demo cohort (1 = best).</summary>
public interface ICohortRank
{
    /// <summary>Rank (1 = highest value) and n (peers + the athlete) or null when the cohort is too small / athlete unknown.</summary>
    Task<(int Rank, int N)?> RankInCohort(int athleteId, EvidenceMetric metric);
}

/// <summary>Percentile and the per-40 (or FG%) value it was computed from.</summary>
public record CohortPercentile(int Percentile, double Value);

public interface ICohortService : ICohortRank
{
    Task<CohortEvidence?> EvaluateAsync(Athlete athlete, bool confirmedOnly = false);

    /// <summary>
    /// The profile's whole-percent mid-rank percentile (1-99) of every demo athlete of the gender, in one pass. Athletes for whom the
    /// profile shows no percentile (games, minutes floor, fewer than 20 peers) are absent.
    /// </summary>
    Task<IReadOnlyDictionary<int, CohortPercentile>> PercentilesAsync(Gender gender, EvidenceMetric metric);
}

/// <summary>Pure cohort math (no database). Thresholds are our estimates and are labelled so in the UI.</summary>
public static class CohortCalculator
{
    /// <summary>Default minimum peers (config Evidence:MinPeers). Our estimate; 15 so the smaller demo groups (e.g. 19 women centers) still get a percentile, with n always shown.</summary>
    public const int DefaultMinPeers = 15;
    public const int MinGames = 10;
    /// <summary>Default minutes-per-game floor (config Evidence:MinMinutesPerGame). Our estimate.</summary>
    public const decimal DefaultMinMinutes = 15m;

    public static PositionGroup GroupOf(Position position) => position switch
    {
        Position.PG or Position.SG => PositionGroup.Guard,
        Position.SF or Position.PF => PositionGroup.Forward,
        _ => PositionGroup.Center,
    };

    public static IReadOnlyList<Position> PositionsOf(PositionGroup group) => group switch
    {
        PositionGroup.Guard => [Position.PG, Position.SG],
        PositionGroup.Forward => [Position.SF, Position.PF],
        _ => [Position.C],
    };

    /// <summary>Mid-rank: (below + 0.5 * equal) / peers, as a whole percent clamped to 1-99.</summary>
    public static int MidRankPercentile(IReadOnlyList<double> peers, double value)
    {
        if (peers.Count == 0) return 1;
        var below = peers.Count(p => p < value);
        var equal = peers.Count(p => p == value);
        var pct = (below + 0.5 * equal) / peers.Count * 100;
        return (int)Math.Clamp(Math.Round(pct, MidpointRounding.AwayFromZero), 1, 99);
    }

    /// <summary>1 + number of peers strictly above the value (ties share the better rank).</summary>
    public static int Rank(IReadOnlyList<double> peers, double value) => 1 + peers.Count(p => p > value);

    /// <summary>
    /// Peers = cohort members other than the athlete with at least <see cref="MinGames"/> games and
    /// <paramref name="minMinutes"/> minutes per game (per-40 percentiles inflate low-minute players) (and, with <paramref name="confirmedOnly"/>, a coach-confirmed latest line).
    /// </summary>
    public static IReadOnlyList<CohortMember> Peers(int athleteId, IReadOnlyList<CohortMember> cohort, bool confirmedOnly, decimal minMinutes = DefaultMinMinutes) =>
        cohort.Where(m => m.AthleteId != athleteId && m.Line.Games >= MinGames && m.Line.Minutes >= minMinutes
                          && (!confirmedOnly || m.Line.Source == StatSource.CoachConfirmed)).ToList();

    public static CohortEvidence Evaluate(int athleteId, Gender gender, PositionGroup group, StatLine subject,
        IReadOnlyList<CohortMember> cohort, bool confirmedOnly, IStatAggregator stats, decimal minMinutes = DefaultMinMinutes, int minPeers = DefaultMinPeers)
    {
        var peers = Peers(athleteId, cohort, confirmedOnly, minMinutes);
        var metrics = Enum.GetValues<EvidenceMetric>().Select(metric =>
        {
            var value = stats.MetricValue(subject, metric);
            if (subject.Games < MinGames)
                return new MetricPercentile(metric, value, null, subject.Games, InsufficientReason.TooFewGames);
            if (subject.Minutes < minMinutes)
                return new MetricPercentile(metric, value, null, peers.Count, InsufficientReason.TooFewMinutes);
            if (peers.Count < minPeers)
                return new MetricPercentile(metric, value, null, peers.Count, InsufficientReason.TooFewPeers);
            var values = peers.Select(p => stats.MetricValue(p.Line, metric)).ToList();
            return new MetricPercentile(metric, value, MidRankPercentile(values, value), peers.Count, InsufficientReason.None);
        }).ToList();
        return new CohortEvidence(gender, group, peers.Count, confirmedOnly, subject.Source, subject.Games, metrics);
    }
}

// Uses its own DbContext (own scope): in static SSR a component renders while its parent's async init is still
// running, so sharing the request's scoped context with the host page could run two queries on it at once.
public class CohortService(IServiceScopeFactory scopes, IStatAggregator stats, IOptions<EvidenceOptions> options) : ICohortService
{
    private decimal MinMinutes => options.Value.MinMinutesPerGame;
    private int MinPeers => options.Value.MinPeers;

    // Scoped service: the compare page evaluates two athletes, often from the same cohort.
    private readonly Dictionary<(Gender, PositionGroup), IReadOnlyList<CohortMember>> cache = [];

    public async Task<CohortEvidence?> EvaluateAsync(Athlete athlete, bool confirmedOnly = false)
    {
        var subject = stats.LatestLine(athlete.Stats);
        if (subject is null) return null;
        var group = CohortCalculator.GroupOf(athlete.Position);
        var cohort = await LoadAsync(athlete.Gender, group);
        return CohortCalculator.Evaluate(athlete.Id, athlete.Gender, group, subject, cohort, confirmedOnly, stats, MinMinutes, MinPeers);
    }

    public async Task<(int Rank, int N)?> RankInCohort(int athleteId, EvidenceMetric metric)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
        var athlete = await db.Athletes.AsNoTracking().Include(a => a.Stats)
            .FirstOrDefaultAsync(a => a.Id == athleteId && a.IsDemo);
        var subject = athlete is null ? null : stats.LatestLine(athlete.Stats);
        if (athlete is null || subject is null || subject.Games < CohortCalculator.MinGames || subject.Minutes < MinMinutes) return null;

        var cohort = await LoadAsync(athlete.Gender, CohortCalculator.GroupOf(athlete.Position));
        var peers = CohortCalculator.Peers(athlete.Id, cohort, confirmedOnly: false, MinMinutes);
        if (peers.Count < MinPeers) return null;

        var rank = CohortCalculator.Rank(peers.Select(p => stats.MetricValue(p.Line, metric)).ToList(), stats.MetricValue(subject, metric));
        return (rank, peers.Count + 1);
    }

    public async Task<IReadOnlyDictionary<int, CohortPercentile>> PercentilesAsync(Gender gender, EvidenceMetric metric)
    {
        var result = new Dictionary<int, CohortPercentile>();
        foreach (var group in Enum.GetValues<PositionGroup>())
        {
            // Same rule as EvaluateAsync: the athlete needs the games and minutes floors, and at least MinPeers other athletes must qualify too.
            var eligible = CohortCalculator.Peers(athleteId: -1, await LoadAsync(gender, group), confirmedOnly: false, MinMinutes);
            if (eligible.Count - 1 < MinPeers) continue;
            var values = eligible.Select(m => stats.MetricValue(m.Line, metric)).ToList();
            for (var i = 0; i < eligible.Count; i++)
            {
                var peers = values.Where((_, j) => j != i).ToList();
                result[eligible[i].AthleteId] = new CohortPercentile(CohortCalculator.MidRankPercentile(peers, values[i]), values[i]);
            }
        }
        return result;
    }

    /// <summary>Every demo athlete of the gender and position group, each with the latest season line.</summary>
    private async Task<IReadOnlyList<CohortMember>> LoadAsync(Gender gender, PositionGroup group)
    {
        if (cache.TryGetValue((gender, group), out var hit)) return hit;
        var positions = CohortCalculator.PositionsOf(group);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BallpassDbContext>();
        var rows = await db.Athletes.AsNoTracking()
            .Where(a => a.IsDemo && a.Gender == gender && positions.Contains(a.Position))
            .SelectMany(a => a.Stats)
            .ToListAsync();
        var members = rows.GroupBy(l => l.AthleteId)
            .Select(g => new CohortMember(g.Key, stats.LatestLine(g)!))
            .ToList();
        return cache[(gender, group)] = members;
    }
}
